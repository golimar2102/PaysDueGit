using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Localization;
using TMPro;

public class LocationTransitionController : MonoBehaviour
{
    public static LocationTransitionController activeTransition = null;

    [Header("Настройки Двери")]
    [Tooltip("Объект дверцы/люка")]
    public Transform doorTransform;
    [Tooltip("Угол вращения дверцы при открытии (локальный Эйлер)")]
    public Vector3 doorOpenRotation = new Vector3(0f, 90f, 0f);
    [Tooltip("Скорость открытия двери")]
    public float doorOpenSpeed = 2f;

    [Header("Настройки Камеры и Полета")]
    [Tooltip("Путь полета камеры по точкам коридора (включая поворот)")]
    public Transform[] cameraFlightPath;
    [Tooltip("Скорость полета камеры")]
    public float cameraFlightSpeed = 2.5f;

    [Header("Точки телепортации")]
    [Tooltip("Точка (Anchor), куда перемещается камера после затемнения")]
    public Transform cameraAnchor;
    [Tooltip("Точка, куда перемещается игрок. Если пусто, используется cameraAnchor")]
    public Transform playerTeleportAnchor;

    [Header("Настройки Выхода")]
    [Tooltip("Точка отлета камеры назад перед затемнением при выходе")]
    public Transform exitFlightAnchor;
    [Tooltip("Скорость отлета камеры назад")]
    public float exitFlightSpeed = 2.5f;
    [Tooltip("Точка телепортации игрока при выходе. Если пусто, вернется на исходную точку")]
    public Transform playerExitAnchor;

    [Header("Скрытие интерфейса")]
    [Tooltip("Элементы UI HUD, которые будут скрыты во время перехода")]
    public GameObject[] objectsToHide;
    [Tooltip("Имя слоя оружия, который будет скрыт с камеры игрока")]
    public string weaponLayerName = "Weapon";

    [Header("Настройки смещения инвентаря")]
    [Tooltip("Смещение инвентаря на экране")]
    public Vector2 inventoryOffsetPosition = new Vector2(-300f, 0f);
    [Tooltip("Масштаб инвентаря")]
    public Vector3 inventoryScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    [Tooltip("Смещение хотбара на экране")]
    public Vector2 hotbarOffsetPosition = new Vector2(-300f, 25f);
    [Tooltip("Масштаб хотбара")]
    public Vector3 hotbarScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);

    [Header("Подсветка и Текст")]
    [Tooltip("Компонент Outline для подсветки двери при наведении")]
    public Outline outline;
    [Tooltip("Локализованный текст подсказки при наведении")]
    public LocalizedString interactPrompt;

    [Header("Пельмени")]
    [Tooltip("Ссылка на DoughScatterController для получения данных о заготовках")]
    public DoughScatterController doughScatterController;

    [Header("Партиклы")]
    [Tooltip("Партиклы, которые включаются при входе в локацию и выключаются при выходе")]
    public ParticleSystem[] transitionParticles;

    // Состояние перехода
    public bool isViewing { get; private set; } = false;
    public bool isTransitioning { get; private set; } = false;

    // Кэш для возврата
    private Transform mainCamera;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private Vector3 originalPlayerPos;
    private Quaternion originalPlayerRot;
    private int originalCullingMask;

    private PlayerMovement playerMovement;
    private MouseLook mouseLook;

    private RectTransform inventoryRect;
    private Vector2 originalInventoryPos;
    private Vector3 originalInventoryScale;

    private Vector3 doorClosedRotEuler;

    // Динамический черный экран
    private Canvas fadeCanvas;
    private UnityEngine.UI.Image fadeImage;

    // Список скрытых рендереров игрока
    private List<Renderer> disabledRenderers = new List<Renderer>();

    #region Свойства Пельменей (Интеграция с Dumpling и DoughScatterController)

    /// <summary>
    /// Возвращает количество собранных пельменей определенного типа (из статического счетчика).
    /// </summary>
    public int GetCollectedDumplingsCount(string meatType)
    {
        return DumplingCounter.GetCount(meatType);
    }

    /// <summary>
    /// Возвращает количество несобранных пельменей на сцене/столе.
    /// </summary>
    public int GetSceneDumplingsCount(string meatType)
    {
        int count = 0;
        Dumpling[] dumplings = FindObjectsByType<Dumpling>(FindObjectsSortMode.None);
        foreach (var d in dumplings)
        {
            if (d.meatType == meatType) count++;
        }
        return count;
    }

    /// <summary>
    /// Возвращает общее количество заготовок, разложенных на столе.
    /// </summary>
    public int GetBlanksOnBoardCount()
    {
        if (doughScatterController == null) return 0;
        int count = 0;
        if (doughScatterController.scatterAnchors != null)
        {
            foreach (var anchor in doughScatterController.scatterAnchors)
            {
                if (anchor != null && anchor.childCount > 0)
                {
                    foreach (Transform child in anchor)
                    {
                        if (child.name.Contains("CounterText") || child.GetComponent<TextMeshPro>() != null || child.GetComponent<TextMesh>() != null)
                            continue;
                        count++;
                    }
                }
            }
        }
        return count;
    }

    #endregion

    void Awake()
    {
        if (outline == null)
        {
            outline = GetComponent<Outline>();
            if (outline == null) outline = GetComponentInChildren<Outline>(true);
        }

        if (outline != null)
        {
            outline.enabled = false;
        }

        if (doorTransform != null)
        {
            doorClosedRotEuler = doorTransform.rotation.eulerAngles;
        }
    }

    void Start()
    {
        if (doughScatterController == null)
        {
            doughScatterController = FindFirstObjectByType<DoughScatterController>();
        }
        SetParticlesState(false);
    }

    void Update()
    {
        if (isViewing && !isTransitioning)
        {
            // Если инвентарь закрыли вручную (например, через Tab)
            if (InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
            {
                ExitTransition();
                return;
            }

            // Выход по нажатию Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitTransition();
                return;
            }

            // Выход по клавише взаимодействия (E)
            KeyCode interactKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
            if (Input.GetKeyDown(interactKey))
            {
                ExitTransition();
                return;
            }
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null)
        {
            outline.enabled = isHighlighted;
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
        activeTransition = this;

        // Находим компоненты игрока
        playerMovement = playerCam.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null) playerMovement = playerCam.GetComponentInChildren<PlayerMovement>();

        mouseLook = playerCam.GetComponent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInParent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInChildren<MouseLook>();

        // Сохраняем исходное состояние игрока (для возврата при выходе)
        if (playerMovement != null)
        {
            originalPlayerPos = playerMovement.transform.position;
            originalPlayerRot = playerMovement.transform.rotation;
        }

        // Выключаем управление движением
        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // Сохраняем исходное состояние камеры
        mainCamera = playerCam.transform;
        originalCameraParent = mainCamera.parent;
        originalCameraLocalPos = mainCamera.localPosition;
        originalCameraLocalRot = mainCamera.localRotation;

        // Отсоединяем камеру, чтобы лететь независимо
        mainCamera.SetParent(null);

        // Отключаем слой оружия, если задан
        if (!string.IsNullOrEmpty(weaponLayerName))
        {
            originalCullingMask = playerCam.cullingMask;
            int weaponLayer = LayerMask.NameToLayer(weaponLayerName);
            if (weaponLayer != -1)
            {
                playerCam.cullingMask &= ~(1 << weaponLayer);
            }
        }

        // 1. Анимация открытия дверцы (глобальное вращение)
        if (doorTransform != null)
        {
            Quaternion startRot = doorTransform.rotation;
            Quaternion targetRot = Quaternion.Euler(doorClosedRotEuler + doorOpenRotation);
            float elapsed = 0f;
            float doorDuration = 1f / Mathf.Max(doorOpenSpeed, 0.01f);
            if (doorDuration <= 0f) doorDuration = 0.5f;

            while (elapsed < doorDuration)
            {
                elapsed += Time.deltaTime;
                doorTransform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / doorDuration);
                yield return null;
            }
            doorTransform.rotation = targetRot;
        }

        // 2. Полет камеры по точкам коридора
        if (cameraFlightPath != null && cameraFlightPath.Length > 0)
        {
            foreach (Transform wp in cameraFlightPath)
            {
                if (wp == null) continue;
                Vector3 startPos = mainCamera.position;
                Quaternion startRot = mainCamera.rotation;
                float dist = Vector3.Distance(startPos, wp.position);
                float flightTime = dist / Mathf.Max(cameraFlightSpeed, 0.01f);
                if (flightTime <= 0f) flightTime = 0.5f;
                float elapsed = 0f;

                while (elapsed < flightTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / flightTime;
                    mainCamera.position = Vector3.Lerp(startPos, wp.position, t);
                    mainCamera.rotation = Quaternion.Slerp(startRot, wp.rotation, t);
                    yield return null;
                }
                mainCamera.position = wp.position;
                mainCamera.rotation = wp.rotation;
            }
        }

        // 3. Быстрое появление черного экрана
        yield return StartCoroutine(Fade(1f, 0.25f));

        // 4. Перемещение игрока и камеры
        Transform teleportDest = playerTeleportAnchor != null ? playerTeleportAnchor : cameraAnchor;
        if (playerMovement != null && teleportDest != null)
        {
            playerMovement.Teleport(teleportDest);
        }

        if (cameraAnchor != null)
        {
            mainCamera.position = cameraAnchor.position;
            mainCamera.rotation = cameraAnchor.rotation;
        }

        // Дверца закрывается обратно после того, как камера встала на финальный anchor (глобально)
        if (doorTransform != null)
        {
            doorTransform.rotation = Quaternion.Euler(doorClosedRotEuler);
        }

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

        // Открываем и настраиваем инвентарь
        if (InventoryManager.Instance != null)
        {
            if (!InventoryManager.Instance.isOpen)
            {
                InventoryManager.Instance.ToggleInventory();
            }

            InventoryManager.Instance.HaltHotbarAnimation();

            if (InventoryManager.Instance.inventoryUI != null)
            {
                inventoryRect = InventoryManager.Instance.inventoryUI.GetComponent<RectTransform>();
                if (inventoryRect != null)
                {
                    originalInventoryPos = inventoryRect.anchoredPosition;
                    originalInventoryScale = inventoryRect.localScale;

                    inventoryRect.anchoredPosition += inventoryOffsetPosition;
                    inventoryRect.localScale = new Vector3(
                        originalInventoryScale.x * inventoryScaleMultiplier.x,
                        originalInventoryScale.y * inventoryScaleMultiplier.y,
                        originalInventoryScale.z * inventoryScaleMultiplier.z
                    );
                }
            }

            if (InventoryManager.Instance.hotbarPanel != null)
            {
                RectTransform hotbarRect = InventoryManager.Instance.hotbarPanel;
                if (hotbarRect != null)
                {
                    hotbarRect.anchoredPosition += hotbarOffsetPosition;
                    hotbarRect.localScale = new Vector3(
                        hotbarRect.localScale.x * hotbarScaleMultiplier.x,
                        hotbarRect.localScale.y * hotbarScaleMultiplier.y,
                        hotbarRect.localScale.z * hotbarScaleMultiplier.z
                    );
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        isViewing = true;
        isTransitioning = false;

        SetParticlesState(true);

        // 5. Исчезновение черного экрана
        yield return StartCoroutine(Fade(0f, 0.25f));
    }

    public void ExitTransition()
    {
        if (!isViewing || isTransitioning) return;
        StartCoroutine(ExitTransitionCoroutine());
    }

    private IEnumerator ExitTransitionCoroutine()
    {
        isTransitioning = true;

        // 1. Полет камеры назад перед затемнением
        if (exitFlightAnchor != null && mainCamera != null)
        {
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            float dist = Vector3.Distance(startPos, exitFlightAnchor.position);
            float flightTime = dist / Mathf.Max(exitFlightSpeed, 0.01f);
            if (flightTime <= 0f) flightTime = 0.5f;
            float elapsed = 0f;

            while (elapsed < flightTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flightTime;
                mainCamera.position = Vector3.Lerp(startPos, exitFlightAnchor.position, t);
                mainCamera.rotation = Quaternion.Slerp(startRot, exitFlightAnchor.rotation, t);
                yield return null;
            }
            mainCamera.position = exitFlightAnchor.position;
            mainCamera.rotation = exitFlightAnchor.rotation;
        }

        // 2. Появление черного экрана
        yield return StartCoroutine(Fade(1f, 0.25f));

        SetParticlesState(false);

        // 3. Восстановление состояния инвентаря, камеры и телепорт игрока (сразу после ухода в черный экран)
        if (inventoryRect != null)
        {
            inventoryRect.anchoredPosition = originalInventoryPos;
            inventoryRect.localScale = originalInventoryScale;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            InventoryManager.Instance.ToggleInventory();
        }

        // Показываем UI обратно
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        // Возвращаем маску камеры
        if (mainCamera != null && !string.IsNullOrEmpty(weaponLayerName))
        {
            Camera camComp = mainCamera.GetComponent<Camera>();
            if (camComp != null)
            {
                camComp.cullingMask = originalCullingMask;
            }
        }

        // Возвращаем камеру игроку
        if (mainCamera != null)
        {
            mainCamera.SetParent(originalCameraParent);
            mainCamera.localPosition = originalCameraLocalPos;
            mainCamera.localRotation = originalCameraLocalRot;
        }

        // Возвращаем игрока к двери
        if (playerMovement != null)
        {
            if (playerExitAnchor != null)
            {
                playerMovement.Teleport(playerExitAnchor);
            }
            else
            {
                playerMovement.transform.position = originalPlayerPos;
                playerMovement.transform.rotation = originalPlayerRot;
                Physics.SyncTransforms();
            }
        }

        // Игрок снова появляется в мире
        SetPlayerVisibility(true);

        // Включаем движение и обзор мыши
        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;

        // Ждем пару секунд в темноте (уже находясь на исходной позиции)
        yield return new WaitForSeconds(2.0f);

        isViewing = false;
        activeTransition = null;
        isTransitioning = false;

        // 4. Исчезновение черного экрана
        yield return StartCoroutine(Fade(0f, 0.25f));
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

        // Prevent destroying canvas during scene changes if needed, but since it's dynamic, it's fine
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

    private void SetParticlesState(bool active)
    {
        if (transitionParticles == null) return;
        foreach (var ps in transitionParticles)
        {
            if (ps != null)
            {
                if (ps.gameObject.activeSelf != active)
                {
                    ps.gameObject.SetActive(active);
                }
                if (active)
                {
                    ps.Play(true);
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
