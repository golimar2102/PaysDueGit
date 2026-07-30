using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PeepholeController : MonoBehaviour
{
    public static PeepholeController activePeephole = null;

    [Header("Настройки подсветки и текста")]
    [Tooltip("Outline компонент для подсветки глазка/двери")]
    public Outline peepholeOutline;
    [Tooltip("Локализованный текст подсказки при наведении")]
    public LocalizedString interactPrompt;

    [Header("Настройки камеры и полета")]
    [Tooltip("Точка (empty child), куда влетает камера в глазок")]
    public Transform cameraFlightTarget;
    [Tooltip("Скорость полета камеры в глазок")]
    public float cameraFlightSpeed = 2.5f;
    [Tooltip("Длительность полета камеры, если lerp по времени")]
    public float cameraFlightDuration = 0.5f;

    [Header("Точки позиционирования")]
    [Tooltip("Финальная точка (empty child) обзора после затемнения")]
    public Transform cameraFinalAnchor;

    [Header("Эффект Рыбьего Глаза (Fisheye)")]
    [Tooltip("Включить ли эффект изменения FOV для рыбьего глаза")]
    public bool useFisheyeEffect = true;
    [Tooltip("Угол обзора (FOV) во время просмотра в глазок")]
    public float peepholeFOV = 100f;
    [Tooltip("Включить ли искажение линзы (Lens Distortion)")]
    public bool useLensDistortion = true;
    [Tooltip("Интенсивность искажения (-0.5 создает классический эффект выпуклости)")]
    [Range(-1f, 1f)] public float distortionIntensity = -0.5f;
    [Tooltip("Включить ли виньетку для круглого контура глазка")]
    public bool useVignette = true;
    [Tooltip("Интенсивность виньетки")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.5f;
    [Tooltip("Сглаженность границ виньетки")]
    [Range(0f, 1f)] public float vignetteSmoothness = 0.2f;

    [Header("Настройки освещения")]
    [Tooltip("Источники света или лампы, которые автоматически включаются при просмотре и выключаются при выходе")]
    public GameObject[] lightsToToggle;

    [Header("Скрытие UI")]
    [Tooltip("Элементы UI HUD, которые будут скрыты во время просмотра")]
    public GameObject[] objectsToHide;

    [Header("Параметры затемнения")]
    [Tooltip("Длительность затемнения экрана")]
    public float fadeDuration = 0.25f;

    // Состояния
    public bool isViewing { get; private set; } = false;
    public bool isTransitioning { get; private set; } = false;

    // Кэш для возврата
    private Transform mainCamera;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private float originalCameraFOV;

    private PlayerMovement playerMovement;
    private MouseLook mouseLook;

    // Динамический черный экран (Canvas + Image)
    private Canvas fadeCanvas;
    private UnityEngine.UI.Image fadeImage;

    // Динамический Volume для эффектов URP
    private GameObject postProcessVolumeObj;

    // Список скрытых рендереров игрока
    private List<Renderer> disabledRenderers = new List<Renderer>();

    void Awake()
    {
        if (peepholeOutline == null)
        {
            peepholeOutline = GetComponent<Outline>();
            if (peepholeOutline == null) peepholeOutline = GetComponentInChildren<Outline>(true);
        }

        if (peepholeOutline != null)
        {
            peepholeOutline.enabled = false;
        }
    }

    void Update()
    {
        if (isViewing && !isTransitioning)
        {
            // Выход по клавише TAB или взаимодействия (E)
            KeyCode interactKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(interactKey))
            {
                ExitTransition();
            }
        }
    }

    void OnDestroy()
    {
        EnableFisheyePostProcess(false);
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (peepholeOutline != null)
        {
            peepholeOutline.enabled = isHighlighted;
        }
    }

    public void StartTransition(Camera playerCam)
    {
        if (isViewing || isTransitioning) return;
        StartCoroutine(TransitionCoroutine(playerCam));
    }

    private IEnumerator TransitionCoroutine(Camera playerCam)
    {
        isTransitioning = true;
        activePeephole = this;

        // Находим компоненты игрока
        playerMovement = playerCam.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null) playerMovement = playerCam.GetComponentInChildren<PlayerMovement>();

        mouseLook = playerCam.GetComponent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInParent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInChildren<MouseLook>();

        // Выключаем управление движением
        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // Сохраняем исходное состояние камеры
        mainCamera = playerCam.transform;
        originalCameraParent = mainCamera.parent;
        originalCameraLocalPos = mainCamera.localPosition;
        originalCameraLocalRot = mainCamera.localRotation;
        originalCameraFOV = playerCam.fieldOfView;

        // Отсоединяем камеру, чтобы лететь независимо
        mainCamera.SetParent(null);

        // 1. Полет камеры к глазку (cameraFlightTarget) с плавным увеличением FOV
        if (cameraFlightTarget != null)
        {
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            float elapsedFlight = 0f;

            while (elapsedFlight < cameraFlightDuration)
            {
                elapsedFlight += Time.deltaTime;
                float t = elapsedFlight / cameraFlightDuration;
                mainCamera.position = Vector3.Lerp(startPos, cameraFlightTarget.position, t);
                mainCamera.rotation = Quaternion.Slerp(startRot, cameraFlightTarget.rotation, t);
                
                if (useFisheyeEffect)
                {
                    playerCam.fieldOfView = Mathf.Lerp(originalCameraFOV, peepholeFOV, t);
                }
                yield return null;
            }
            mainCamera.position = cameraFlightTarget.position;
            mainCamera.rotation = cameraFlightTarget.rotation;
        }

        // 2. Быстрое появление черного экрана
        yield return StartCoroutine(Fade(1f, fadeDuration));

        // Игрок полностью пропадает из мира (скрываем все рендереры)
        SetPlayerVisibility(false);

        // Скрываем UI элементы
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        // Перемещаем камеру в финальную точку обзора (cameraFinalAnchor)
        if (cameraFinalAnchor != null)
        {
            mainCamera.position = cameraFinalAnchor.position;
            mainCamera.rotation = cameraFinalAnchor.rotation;
        }

        // Применяем финальный FOV и активируем URP Volume
        if (useFisheyeEffect)
        {
            playerCam.fieldOfView = peepholeFOV;
            EnableFisheyePostProcess(true);
        }

        // Включаем свет автоматически при начале просмотра
        SetLightsState(true);

        // Курсор остается залоченным (нельзя двигать)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isViewing = true;
        isTransitioning = false;

        // 3. Исчезновение черного экрана
        yield return StartCoroutine(Fade(0f, fadeDuration));
    }

    public void ExitTransition()
    {
        if (!isViewing || isTransitioning) return;
        StartCoroutine(ExitTransitionCoroutine());
    }

    private IEnumerator ExitTransitionCoroutine()
    {
        isTransitioning = true;

        // 1. Появление черного экрана
        yield return StartCoroutine(Fade(1f, fadeDuration));

        // Отключаем пост-эффекты рыбьего глаза
        EnableFisheyePostProcess(false);

        // Выключаем свет при выходе
        SetLightsState(false);

        // 2. Восстановление состояния камеры, UI и игрока
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        // Возвращаем камеру игроку
        if (mainCamera != null)
        {
            Camera cam = mainCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = originalCameraFOV;
            }

            mainCamera.SetParent(originalCameraParent);
            mainCamera.localPosition = originalCameraLocalPos;
            mainCamera.localRotation = originalCameraLocalRot;
        }

        // Игрок снова появляется в мире
        SetPlayerVisibility(true);

        // Включаем движение и обзор мыши
        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;

        isViewing = false;
        activePeephole = null;
        isTransitioning = false;

        // 3. Исчезновение черного экрана
        yield return StartCoroutine(Fade(0f, fadeDuration));
    }

    private void SetPlayerVisibility(bool visible)
    {
        if (playerMovement == null) return;

        if (!visible)
        {
            disabledRenderers.Clear();
            Renderer[] renderers = playerMovement.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r != null && r.enabled && r.transform != mainCamera && !r.transform.IsChildOf(mainCamera))
                {
                    r.enabled = false;
                    disabledRenderers.Add(r);
                }
            }
        }
        else
        {
            foreach (Renderer r in disabledRenderers)
            {
                if (r != null)
                {
                    r.enabled = true;
                }
            }
            disabledRenderers.Clear();
        }
    }

    private void EnableFisheyePostProcess(bool enable)
    {
        if (enable)
        {
            if (postProcessVolumeObj != null) return;
            postProcessVolumeObj = new GameObject("PeepholePostProcessVolume");
            Volume volume = postProcessVolumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 99;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            
            if (useLensDistortion)
            {
                LensDistortion lensDistortion = profile.Add<LensDistortion>();
                lensDistortion.active = true;
                lensDistortion.intensity.Override(distortionIntensity);
                lensDistortion.xMultiplier.Override(1f);
                lensDistortion.yMultiplier.Override(1f);
                lensDistortion.scale.Override(1f);
            }

            if (useVignette)
            {
                Vignette vignette = profile.Add<Vignette>();
                vignette.active = true;
                vignette.intensity.Override(vignetteIntensity);
                vignette.smoothness.Override(vignetteSmoothness);
                vignette.rounded.Override(true);
                vignette.color.Override(Color.black);
            }

            volume.profile = profile;
        }
        else
        {
            if (postProcessVolumeObj != null)
            {
                Volume volume = postProcessVolumeObj.GetComponent<Volume>();
                if (volume != null && volume.profile != null)
                {
                    Destroy(volume.profile);
                }
                Destroy(postProcessVolumeObj);
                postProcessVolumeObj = null;
            }
        }
    }

    private void CreateFadeScreen()
    {
        GameObject existing = GameObject.Find("DynamicFadeCanvas");
        if (existing != null)
        {
            fadeCanvas = existing.GetComponent<Canvas>();
            fadeImage = existing.GetComponentInChildren<UnityEngine.UI.Image>();
            return;
        }

        GameObject canvasObj = new GameObject("DynamicFadeCanvas");
        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 1000;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rect = fadeImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (fadeImage == null) CreateFadeScreen();
        if (fadeImage == null) yield break;

        fadeCanvas.gameObject.SetActive(true);
        Color color = fadeImage.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        fadeImage.color = color;

        if (targetAlpha <= 0f)
        {
            fadeCanvas.gameObject.SetActive(false);
        }
    }

    private void SetLightsState(bool active)
    {
        if (lightsToToggle == null) return;
        foreach (GameObject lightObj in lightsToToggle)
        {
            if (lightObj != null)
            {
                lightObj.SetActive(active);
            }
        }
    }
}
