using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Localization;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TVChairController : MonoBehaviour
{
    public static TVChairController activeChair;

    [Header("Настройки подсветки")]
    public Outline outline;

    [Header("Точки привязки")]
    [Tooltip("Куда телепортировать тело игрока перед посадкой")]
    public Transform seatAnchor;
    [Tooltip("Куда летит камера для просмотра телевизора")]
    public Transform tvCameraAnchor;

    [Header("Настройки питания")]
    [Tooltip("Конкретный генератор, от которого питается этот телевизор. Если не назначен, проверяется главный генератор (синглтон).")]
    public GeneratorController targetGenerator;

    [Header("Настройки видео")]
    public VideoPlayer videoPlayer;
    public VideoClip[] videoClips;

    [Header("Параметры")]
    public float cameraMoveSpeed = 3f;
    public float sanityRestoreRate = 5f;

    [Header("Элементы UI для скрытия")]
    [Tooltip("HUD элементы, которые нужно временно отключить во время просмотра")]
    public GameObject[] objectsToHide;

    [Header("Локализация и предупреждения")]
    [Tooltip("Сообщение об отсутствии электричества")]
    public LocalizedString noPowerMessage;
    [Tooltip("Текстовый компонент в центре экрана для отображения предупреждения")]
    public TextMeshProUGUI centerWarningText;
    [Tooltip("Время отображения предупреждения в секундах")]
    public float warningDisplayDuration = 2f;

    private bool isWatching = false;
    private PlayerInteract playerInteract;

    // Сохранение состояния камеры
    private Transform mainCamera;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;

    private Coroutine cameraCoroutine;
    private Coroutine warningCoroutine;
    private List<Renderer> hiddenRenderers = new List<Renderer>();
    private bool isCameraLocked = false;

    void Awake()
    {
        if (outline == null) outline = GetComponentInChildren<Outline>(true);
        if (outline != null) outline.enabled = false;

        if (centerWarningText != null)
        {
            centerWarningText.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
            
            // Настраиваем вывод звука через AudioSource, чтобы он улавливался AudioListener игрока и искажался при безумии
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            
            AudioSource tvAudioSource = videoPlayer.GetComponent<AudioSource>();
            if (tvAudioSource == null)
            {
                tvAudioSource = videoPlayer.gameObject.AddComponent<AudioSource>();
            }
            
            // Настраиваем 3D-звук для телевизора
            tvAudioSource.playOnAwake = false;
            tvAudioSource.spatialBlend = 1f; // Полностью 3D звук
            tvAudioSource.minDistance = 3f;
            tvAudioSource.maxDistance = 18f;
            
            // Связываем аудио-трек видеоплеера с AudioSource
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, tvAudioSource);
        }
        ClearTVToBlack();
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
    }

    public void SetHighlight(bool active)
    {
        if (outline != null) outline.enabled = active;
    }

    public void SitDown(PlayerInteract interactScript)
    {
        if (activeChair != null) return;

        // Проверяем наличие электричества
        if (!IsElectricityOn())
        {
            ShowNoPowerWarning();
            return;
        }

        activeChair = this;
        playerInteract = interactScript;

        mainCamera = playerInteract.playerCamera.transform;
        originalCameraParent = mainCamera.parent;
        originalCameraLocalPos = mainCamera.localPosition;
        originalCameraLocalRot = mainCamera.localRotation;

        // Отключаем управление движением
        PlayerMovement playerMovement = playerInteract.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
        {
            if (seatAnchor != null)
            {
                playerMovement.Teleport(seatAnchor);
            }
            playerMovement.enabled = false;
        }

        // Отключаем CharacterController, чтобы физика не выталкивала игрока вверх
        CharacterController charController = playerInteract.GetComponentInParent<CharacterController>();
        if (charController != null) charController.enabled = false;

        // Отключаем поворот головы мышь
        MouseLook mouseLook = playerInteract.GetComponentInParent<MouseLook>();
        if (mouseLook == null) mouseLook = playerInteract.GetComponentInChildren<MouseLook>();
        if (mouseLook != null) mouseLook.enabled = false;

        // Скрываем оружие / предметы в руках
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(false);
        }

        // Скрываем самого персонажа (все рендеры в иерархии игрока)
        hiddenRenderers.Clear();
        GameObject playerRoot = playerInteract.transform.root.gameObject;
        Renderer[] renderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // Не скрываем рендеры на самом объекте кресла/ТВ, только в иерархии игрока
            if (r.enabled && r.gameObject.activeInHierarchy)
            {
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }

        // Скрываем HUD (с защитой от отключения камеры)
        if (objectsToHide != null)
        {
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    if (obj.GetComponent<Camera>() != null || obj.GetComponentInChildren<Camera>() != null)
                    {
                        Debug.LogWarning($"[TVChair] Попытка скрыть объект {obj.name}, содержащий камеру. Пропущено для предотвращения отключения экрана.");
                        continue;
                    }
                    obj.SetActive(false);
                }
            }
        }

        // Отцепляем камеру, чтобы двигать в мировых координатах
        mainCamera.SetParent(null);

        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCamera(tvCameraAnchor.position, tvCameraAnchor.rotation, () =>
        {
            StartWatching();
        }));
    }

    private void StartWatching()
    {
        isWatching = true;

        if (videoPlayer != null && videoClips != null && videoClips.Length > 0)
        {
            int randIdx = Random.Range(0, videoClips.Length);
            videoPlayer.clip = videoClips[randIdx];
            videoPlayer.Play();
        }
    }

    public void StandUp()
    {
        if (!isWatching) return;
        isWatching = false;
        isCameraLocked = false;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            ClearTVToBlack();
        }

        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCameraBack(() =>
        {
            CompleteStandUp();
        }));
    }

    private void CompleteStandUp()
    {
        // Возвращаем камеру в иерархию
        if (mainCamera != null)
        {
            mainCamera.SetParent(originalCameraParent);
            mainCamera.localPosition = originalCameraLocalPos;
            mainCamera.localRotation = originalCameraLocalRot;
        }

        // Возвращаем управление движением
        if (playerInteract != null)
        {
            PlayerMovement playerMovement = playerInteract.GetComponentInParent<PlayerMovement>();
            if (playerMovement != null) playerMovement.enabled = true;

            // Возвращаем CharacterController
            CharacterController charController = playerInteract.GetComponentInParent<CharacterController>();
            if (charController != null) charController.enabled = true;

            // Возвращаем поворот мыши
            MouseLook mouseLook = playerInteract.GetComponentInParent<MouseLook>();
            if (mouseLook == null) mouseLook = playerInteract.GetComponentInChildren<MouseLook>();
            if (mouseLook != null) mouseLook.enabled = true;
        }

        // Показываем оружие
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(true);
        }

        // Показываем персонажа обратно
        foreach (var r in hiddenRenderers)
        {
            if (r != null) r.enabled = true;
        }
        hiddenRenderers.Clear();

        // Показываем HUD
        if (objectsToHide != null)
        {
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    if (obj.GetComponent<Camera>() != null || obj.GetComponentInChildren<Camera>() != null)
                    {
                        continue;
                    }
                    obj.SetActive(true);
                }
            }
        }

        activeChair = null;
        playerInteract = null;
    }

    void Update()
    {
        if (activeChair == this && isWatching)
        {
            // Если во время просмотра генератор отключился, принудительно встаем
            if (!IsElectricityOn())
            {
                StandUp();
                return;
            }

            // Восстановление психики
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.RestoreSanity(sanityRestoreRate * Time.deltaTime);
            }

            // Проверка клавиш выхода (Интеракция, TAB, ESC)
            KeyCode interactKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
            {
                StandUp();
            }
        }
    }

    void LateUpdate()
    {
        // Принудительно удерживаем камеру в точке якоря, перебивая любые другие скрипты
        if (isCameraLocked && mainCamera != null && tvCameraAnchor != null)
        {
            mainCamera.position = tvCameraAnchor.position;
            mainCamera.rotation = tvCameraAnchor.rotation;
        }
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        if (isWatching)
        {
            StandUp();
        }
    }

    private void ClearTVToBlack()
    {
        if (videoPlayer == null) return;

        if (videoPlayer.renderMode == VideoRenderMode.RenderTexture && videoPlayer.targetTexture != null)
        {
            RenderTexture rt = videoPlayer.targetTexture;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = active;
        }
        else if (videoPlayer.renderMode == VideoRenderMode.MaterialOverride && videoPlayer.targetMaterialRenderer != null)
        {
            string propName = videoPlayer.targetMaterialProperty;
            if (string.IsNullOrEmpty(propName))
            {
                if (videoPlayer.targetMaterialRenderer.material.HasProperty("_BaseMap"))
                    propName = "_BaseMap";
                else
                    propName = "_MainTex";
            }
            videoPlayer.targetMaterialRenderer.material.SetTexture(propName, Texture2D.blackTexture);
        }
    }

    private void ShowNoPowerWarning()
    {
        if (centerWarningText == null) return;

        if (warningCoroutine != null) StopCoroutine(warningCoroutine);
        warningCoroutine = StartCoroutine(ShowWarningRoutine());
    }

    private IEnumerator ShowWarningRoutine()
    {
        string msg = (noPowerMessage != null && !noPowerMessage.IsEmpty)
            ? noPowerMessage.GetLocalizedString()
            : "Нет электричества";

        centerWarningText.text = msg;
        centerWarningText.gameObject.SetActive(true);

        yield return new WaitForSeconds(warningDisplayDuration);

        centerWarningText.gameObject.SetActive(false);
    }

    private bool IsElectricityOn()
    {
        if (targetGenerator != null)
        {
            return targetGenerator.isWorking;
        }
        return GeneratorController.IsGeneratorWorking;
    }

    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot, System.Action onComplete)
    {
        float t = 0f;
        Vector3 startPos = mainCamera.position;
        Quaternion startRot = mainCamera.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            mainCamera.position = Vector3.Lerp(startPos, targetPos, smoothT);
            mainCamera.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
            yield return null;
        }

        mainCamera.position = targetPos;
        mainCamera.rotation = targetRot;
        isCameraLocked = true; // Блокируем камеру
        onComplete?.Invoke();
    }

    private IEnumerator MoveCameraBack(System.Action onComplete)
    {
        float t = 0f;
        Vector3 startPos = mainCamera.position;

        // Двигаемся к родительской позиции в мировых координатах (на случай если тело сместилось)
        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (originalCameraParent != null)
            {
                Vector3 targetWorldPos = originalCameraParent.TransformPoint(originalCameraLocalPos);
                Quaternion targetWorldRot = originalCameraParent.rotation * originalCameraLocalRot;

                mainCamera.position = Vector3.Lerp(startPos, targetWorldPos, smoothT);
                mainCamera.rotation = Quaternion.Slerp(mainCamera.rotation, targetWorldRot, smoothT);
            }
            yield return null;
        }

        onComplete?.Invoke();
    }
}
