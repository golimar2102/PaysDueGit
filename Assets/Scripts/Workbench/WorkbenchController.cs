using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Localization;

public class WorkbenchController : MonoBehaviour
{
    public static WorkbenchController activeWorkbench = null;

    [Header("Настройки камеры")]
    [Tooltip("Точка камеры, куда прилетает взгляд игрока")]
    public Transform cameraTargetPos;

    [Tooltip("Скорость полета камеры")]
    public float cameraMoveSpeed = 4f;

    [Header("Скрытие интерфейса")]
    [Tooltip("Элементы UI HUD, которые будут скрыты во время просмотра")]
    public GameObject[] objectsToHide;

    [Tooltip("Имя слоя оружия, который будет скрыт с камеры игрока")]
    public string weaponLayerName = "Weapon";

    [Header("Настройки смещения инвентаря")]
    [Tooltip("Смещение инвентаря на экране")]
    public Vector2 inventoryOffsetPosition = new Vector2(-290f, 0f);
    [Tooltip("Масштаб инвентаря")]
    public Vector3 inventoryScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    [Tooltip("Смещение хотбара на экране")]
    public Vector2 hotbarOffsetPosition = new Vector2(-290f, 25f);
    [Tooltip("Масштаб хотбара")]
    public Vector3 hotbarScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);

    [Header("Интерфейс Верстака")]
    [Tooltip("Меню верстака, которое откроется после приближения камеры")]
    public GameObject workbenchMenuUI;

    [Header("Раскрытие стола")]
    [Tooltip("Левая створка стола")]
    public Transform leftFlap;
    [Tooltip("Правая створка стола")]
    public Transform rightFlap;
    [Tooltip("Целевой локальный поворот левой створки при раскрытии (в градусах)")]
    public Vector3 leftFlapOpenRotation = new Vector3(0, 90, 0);
    [Tooltip("Целевой локальный поворот правой створки при раскрытии (в градусах)")]
    public Vector3 rightFlapOpenRotation = new Vector3(0, -90, 0);
    [Tooltip("Скорость вращения створок")]
    public float flapRotationSpeed = 2f;
    [Tooltip("Задержка между открытием первой и второй створок")]
    public float flapRotationDelay = 0.5f;

    [Header("Звуки створок")]
    [Tooltip("Звук открытия левой створки")]
    public AudioSource leftFlapOpenSound;
    [Tooltip("Звук открытия правой створки")]
    public AudioSource rightFlapOpenSound;
    [Tooltip("Звук закрытия левой створки")]
    public AudioSource leftFlapCloseSound;
    [Tooltip("Звук закрытия правой створки")]
    public AudioSource rightFlapCloseSound;

    [Header("Текст взаимодействия")]
    [Tooltip("Локализованная подсказка для наведения курсора")]
    public LocalizedString interactPrompt;

    [Header("Подсветка")]
    [Tooltip("Компонент Outline для подсвечивания верстака при наведении")]
    public Outline outline;

    // Состояние процесса
    public bool isViewing { get; private set; } = false;
    private bool isTransitioning = false;

    // Сохранение состояния камеры и игрока
    private Transform mainCamera;
    private int originalCullingMask;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isCameraLocked = false;
    private Coroutine cameraCoroutine;
    private Coroutine flapsCoroutine;

    private PlayerMovement playerMovement;
    private MouseLook mouseLook;

    // Смещение UI
    private RectTransform inventoryRect;
    private Vector2 originalInventoryPos;
    private Vector3 originalInventoryScale;

    private RectTransform hotbarRect;
    private Vector2 originalHotbarPos;
    private Vector3 originalHotbarScale;

    // Исходные повороты створок
    private Quaternion originalLeftFlapRot;
    private Quaternion originalRightFlapRot;

    void Awake()
    {
        if (outline == null)
        {
            outline = GetComponentInChildren<Outline>(true);
        }
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    void Start()
    {
        if (leftFlap != null)
        {
            originalLeftFlapRot = leftFlap.localRotation;
        }
        if (rightFlap != null)
        {
            originalRightFlapRot = rightFlap.localRotation;
        }

        if (workbenchMenuUI != null)
        {
            workbenchMenuUI.SetActive(false);
        }
    }

    void OnDisable()
    {
        if (activeWorkbench == this)
        {
            activeWorkbench = null;
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null)
        {
            outline.enabled = isHighlighted;
        }
    }

    void Update()
    {
        if (isViewing && !isTransitioning)
        {
            // Если инвентарь был закрыт (например, игрок нажал TAB или иным образом закрыл), выходим
            if (InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
            {
                ExitWorkbenchMode();
                return;
            }

            // Выход на Escape или Tab (если не перехвачено инвентарем)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitWorkbenchMode();
                return;
            }
        }
    }

    void LateUpdate()
    {
        if (isCameraLocked && mainCamera != null && cameraTargetPos != null)
        {
            mainCamera.position = cameraTargetPos.position;
            mainCamera.rotation = cameraTargetPos.rotation;
        }
    }

    public void EnterWorkbenchMode(Camera playerCam)
    {
        if (isViewing || isTransitioning) return;

        isViewing = true;
        isTransitioning = true;
        activeWorkbench = this;

        mainCamera = playerCam.transform;

        originalCameraParent = mainCamera.parent;
        originalCameraLocalPos = mainCamera.localPosition;
        originalCameraLocalRot = mainCamera.localRotation;

        playerMovement = playerCam.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null) playerMovement = playerCam.GetComponentInChildren<PlayerMovement>();

        mouseLook = playerCam.GetComponent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInParent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInChildren<MouseLook>();

        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // Настройка маски culling для скрытия оружия
        if (!string.IsNullOrEmpty(weaponLayerName))
        {
            originalCullingMask = playerCam.cullingMask;
            int weaponLayer = LayerMask.NameToLayer(weaponLayerName);
            if (weaponLayer != -1)
            {
                playerCam.cullingMask &= ~(1 << weaponLayer);
            }
        }

        // Прячем оружие в руках через EquipmentManager
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(false);
        }

        // Скрытие элементов HUD
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        SetHighlight(false);

        // Запуск анимации приближения камеры
        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCamera(cameraTargetPos.position, cameraTargetPos.rotation));

        // Запуск раскрытия стола
        if (flapsCoroutine != null) StopCoroutine(flapsCoroutine);
        flapsCoroutine = StartCoroutine(OpenFlapsSequence());

        // Настройка и смещение инвентаря
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
                hotbarRect = InventoryManager.Instance.hotbarPanel;
                if (hotbarRect != null)
                {
                    originalHotbarPos = hotbarRect.anchoredPosition;
                    originalHotbarScale = hotbarRect.localScale;

                    hotbarRect.anchoredPosition += hotbarOffsetPosition;
                    hotbarRect.localScale = new Vector3(
                        originalHotbarScale.x * hotbarScaleMultiplier.x,
                        originalHotbarScale.y * hotbarScaleMultiplier.y,
                        originalHotbarScale.z * hotbarScaleMultiplier.z
                    );
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void ExitWorkbenchMode()
    {
        if (!isViewing || isTransitioning) return;

        isTransitioning = true;
        isCameraLocked = false;

        // Закрываем меню верстака
        if (workbenchMenuUI != null)
        {
            workbenchMenuUI.SetActive(false);
        }

        // Запуск закрытия створок
        if (flapsCoroutine != null) StopCoroutine(flapsCoroutine);
        flapsCoroutine = StartCoroutine(CloseFlapsSequence());

        // Возвращаем смещение инвентаря и закрываем его
        if (inventoryRect != null)
        {
            inventoryRect.anchoredPosition = originalInventoryPos;
            inventoryRect.localScale = originalInventoryScale;
        }



        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            InventoryManager.Instance.ToggleInventory();
        }

        // Возвращаем видимость оружия
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(true);
        }

        // Показываем обратно HUD
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        // Восстановление маски culling камеры
        if (mainCamera != null && !string.IsNullOrEmpty(weaponLayerName))
        {
            Camera camComp = mainCamera.GetComponent<Camera>();
            if (camComp != null)
            {
                camComp.cullingMask = originalCullingMask;
            }
        }

        // Камера летит обратно к игроку
        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCameraBack());
    }

    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot)
    {
        float t = 0;
        Vector3 startPos = mainCamera.position;
        Quaternion startRot = mainCamera.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            mainCamera.position = Vector3.Lerp(startPos, targetPos, smoothT);
            mainCamera.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
            yield return null;
        }

        mainCamera.position = targetPos;
        mainCamera.rotation = targetRot;
        isCameraLocked = true;
        isTransitioning = false;

        // Открываем меню верстака после завершения полета
        if (workbenchMenuUI != null)
        {
            workbenchMenuUI.SetActive(true);
            WorkbenchUI uiComp = workbenchMenuUI.GetComponent<WorkbenchUI>();
            if (uiComp != null)
            {
                uiComp.InitializeUI();
            }
        }
    }

    private IEnumerator MoveCameraBack()
    {
        float t = 0;
        Vector3 startPos = mainCamera.localPosition;
        Quaternion startRot = mainCamera.localRotation;

        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            mainCamera.localPosition = Vector3.Lerp(startPos, originalCameraLocalPos, smoothT);
            mainCamera.localRotation = Quaternion.Slerp(startRot, originalCameraLocalRot, smoothT);
            yield return null;
        }

        mainCamera.localPosition = originalCameraLocalPos;
        mainCamera.localRotation = originalCameraLocalRot;

        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;

        isViewing = false;
        activeWorkbench = null;
        isTransitioning = false;
    }

    private IEnumerator OpenFlapsSequence()
    {
        float duration = flapRotationSpeed > 0f ? (1f / flapRotationSpeed) : 0.5f;

        if (leftFlap != null)
        {
            if (leftFlapOpenSound != null) leftFlapOpenSound.Play();
            StartCoroutine(RotateFlapSmooth(leftFlap, Quaternion.Euler(leftFlapOpenRotation), duration));
        }

        yield return new WaitForSeconds(flapRotationDelay);

        if (rightFlap != null)
        {
            if (rightFlapOpenSound != null) rightFlapOpenSound.Play();
            StartCoroutine(RotateFlapSmooth(rightFlap, Quaternion.Euler(rightFlapOpenRotation), duration));
        }
    }

    private IEnumerator CloseFlapsSequence()
    {
        float duration = flapRotationSpeed > 0f ? (1f / flapRotationSpeed) : 0.5f;

        if (rightFlap != null)
        {
            if (rightFlapCloseSound != null) rightFlapCloseSound.Play();
            StartCoroutine(RotateFlapSmooth(rightFlap, originalRightFlapRot, duration));
        }

        yield return new WaitForSeconds(flapRotationDelay);

        if (leftFlap != null)
        {
            if (leftFlapCloseSound != null) leftFlapCloseSound.Play();
            StartCoroutine(RotateFlapSmooth(leftFlap, originalLeftFlapRot, duration));
        }
    }

    private IEnumerator RotateFlapSmooth(Transform flap, Quaternion targetRot, float duration)
    {
        Quaternion startRot = flap.localRotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normT = Mathf.Clamp01(elapsed / duration);
            flap.localRotation = Quaternion.Slerp(startRot, targetRot, Mathf.SmoothStep(0f, 1f, normT));
            yield return null;
        }
        flap.localRotation = targetRot;
    }
}
