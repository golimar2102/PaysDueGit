using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Localization;

public class DoughRollingController : MonoBehaviour
{
    public static DoughRollingController activeRollingBoard = null;

    [Header("Настройки предметов")]
    [Tooltip("ID теста в инвентаре")]
    public int inputDoughItemID = 20;
    [Tooltip("ID скалки в инвентаре")]
    public int rollingPinItemID = 22;

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
    public Vector2 inventoryOffsetPosition = new Vector2(-300f, 0f);
    [Tooltip("Масштаб инвентаря")]
    public Vector3 inventoryScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    [Tooltip("Смещение хотбара на экране")]
    public Vector2 hotbarOffsetPosition = new Vector2(-300f, 25f);
    [Tooltip("Масштаб хотбара")]
    public Vector3 hotbarScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);

    [Header("Визуальные объекты теста")]
    [Tooltip("3D модель сырого теста на доске (должна содержать SkinnedMeshRenderer с Blend Shape)")]
    public GameObject doughVisual;

    [Tooltip("SkinnedMeshRenderer теста (если пустой, найдется автоматически на doughVisual)")]
    public SkinnedMeshRenderer doughSkinnedMesh;

    [Tooltip("Имя Blend Shape для раскатанного теста")]
    public string rolledBlendShapeName = "Rolled";

    [Tooltip("Чувствительность движения мыши при раскатывании")]
    public float rollSensitivity = 0.1f;

    [Header("Визуальные объекты скалки")]
    [Tooltip("3D модель скалки на доске")]
    public GameObject rollingPinVisual;

    [Tooltip("Ускорение раскатки при наличии скалки")]
    public float rollingPinSpeedMultiplier = 2f;

    [Tooltip("Локальная ось вращения скалки")]
    public Vector3 rollingPinRotationAxis = Vector3.right;

    [Tooltip("Множитель скорости вращения скалки")]
    public float rollingPinRotationSpeed = 100f;

    [Tooltip("Амплитуда хождения скалки взад-вперед")]
    public float rollingPinSlideAmount = 0.15f;

    [Header("Звуки")]
    [Tooltip("Звук укладки предметов на доску")]
    public AudioSource placeSound;
    [Tooltip("Звук раскатки теста (зацикленный)")]
    public AudioSource rollSound;
    [Tooltip("Звук подбора предметов")]
    public AudioSource pickupSound;

    [Header("Текст взаимодействия (PlayerInteract)")]
    [Tooltip("Локализованная подсказка для наведения курсора")]
    public LocalizedString interactPrompt;

    [Header("Подсветка")]
    [Tooltip("Компонент Outline для подсвечивания стола при наведении")]
    public Outline outline;

    [Header("Коллайдер поверхности стола")]
    [Tooltip("Коллайдер поверхности стола. Если пустой, ищется автоматически на этом объекте")]
    public Collider surfaceCollider;

    [Header("Настройки вырезания теста")]
    [Tooltip("Компонент контроллера вырезания теста (если пустой, найдется автоматически)")]
    public DoughCuttingController cuttingController;

    [Header("Масштабирование коллайдера теста")]
    [Tooltip("Во сколько раз увеличится горизонтальный размер коллайдера при раскатке")]
    public float horizontalColliderScaleMultiplier = 2.2f;

    [Tooltip("Во сколько раз уменьшится толщина коллайдера при раскатке")]
    public float verticalColliderScaleMultiplier = 0.3f;

    // Состояние процесса
    public bool isViewing { get; private set; } = false;
    public bool hasDough { get; private set; } = false;
    public bool hasRollingPin { get; private set; } = false;
    public bool isRolling { get; private set; } = false;
    public bool isRolled { get; private set; } = false;
    public float rollProgress { get; private set; } = 0f;

    private bool isTransitioning = false;
    private int rolledBlendShapeIndex = -1;

    // Сохранение состояния камеры и игрока
    private Transform mainCamera;
    private int originalCullingMask;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isCameraLocked = false;
    private Coroutine cameraCoroutine;

    private PlayerMovement playerMovement;
    private MouseLook mouseLook;
    private KeyCode interactKey = KeyCode.E;

    // Смещение UI
    private RectTransform inventoryRect;
    private Vector2 originalInventoryPos;
    private Vector3 originalInventoryScale;
    private Vector3 originalPinLocalPos;
    private Quaternion originalPinLocalRot;
    private float rollingPinAngle = 0f;
    private int rollingPinCurrentDurability = 100;
    private int rollingPinMaxDurability = 100;
    private int rollingPinAmountPerUse = 10;
    private Vector3 originalColliderSize;
    private Vector3 originalColliderCenter;
    private bool hasCachedOriginalColliderSize = false;

    void Awake()
    {
        if (outline == null)
        {
            Outline[] allOutlines = GetComponentsInChildren<Outline>(true);
            foreach (var o in allOutlines)
            {
                string nameLower = o.gameObject.name.ToLower();
                if (nameLower.Contains("cutter") || nameLower.Contains("mold") || nameLower.Contains("pin") || nameLower.Contains("скалк") || nameLower.Contains("формоч"))
                {
                    continue;
                }
                outline = o;
                break;
            }
        }
        if (outline != null)
        {
            outline.enabled = false;
        }

        if (surfaceCollider == null)
        {
            surfaceCollider = GetComponent<Collider>();
        }
    }

    void Start()
    {
        if (doughVisual != null) doughVisual.SetActive(false);

        if (rollingPinVisual != null)
        {
            originalPinLocalPos = rollingPinVisual.transform.localPosition;
            originalPinLocalRot = rollingPinVisual.transform.localRotation;
            rollingPinVisual.SetActive(false);
        }

        if (cuttingController == null)
        {
            cuttingController = GetComponent<DoughCuttingController>();
        }

        FindSkinnedMeshAndIndex();
        RefreshKeyBindings();
    }

    void OnEnable()
    {
        SettingsManager.OnSettingsSaved += RefreshKeyBindings;
    }

    void OnDisable()
    {
        SettingsManager.OnSettingsSaved -= RefreshKeyBindings;
        if (activeRollingBoard == this)
        {
            activeRollingBoard = null;
        }
    }

    private void RefreshKeyBindings()
    {
        interactKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null)
        {
            outline.enabled = isHighlighted;
        }
    }

    private void FindSkinnedMeshAndIndex()
    {
        if (doughSkinnedMesh == null && doughVisual != null)
        {
            doughSkinnedMesh = doughVisual.GetComponent<SkinnedMeshRenderer>();
            if (doughSkinnedMesh == null)
            {
                doughSkinnedMesh = doughVisual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
        }

        if (doughSkinnedMesh != null)
        {
            rolledBlendShapeIndex = doughSkinnedMesh.sharedMesh.GetBlendShapeIndex(rolledBlendShapeName);
            
            if (rolledBlendShapeIndex == -1)
            {
                for (int i = 0; i < doughSkinnedMesh.sharedMesh.blendShapeCount; i++)
                {
                    if (string.Equals(doughSkinnedMesh.sharedMesh.GetBlendShapeName(i), rolledBlendShapeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        rolledBlendShapeIndex = i;
                        break;
                    }
                }
            }
        }
    }

    void Update()
    {
        if (isViewing && !isTransitioning)
        {
            if (InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
            {
                ExitDoughRollingMode();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitDoughRollingMode();
                return;
            }

            if (Input.GetKeyDown(interactKey))
            {
                if (!hasDough || isRolled)
                {
                    ExitDoughRollingMode();
                    return;
                }
            }

            if (hasDough && !isRolled)
            {
                bool isHoldingLMB = Input.GetKey(KeyCode.Mouse0);
                bool isMouseOverDough = IsMouseOverDough();

                if (isHoldingLMB && isMouseOverDough)
                {
                    float mouseX = Input.GetAxis("Mouse X");
                    float mouseY = Input.GetAxis("Mouse Y");
                    float mouseSpeed = Mathf.Abs(mouseX) + Mathf.Abs(mouseY);

                    if (mouseSpeed > 0.001f)
                    {
                        isRolling = true;
                        
                        float currentSensitivity = rollSensitivity;
                        if (hasRollingPin)
                        {
                            currentSensitivity *= rollingPinSpeedMultiplier;
                        }

                        float increment = mouseSpeed * currentSensitivity;
                        rollProgress += increment;
                        rollProgress = Mathf.Clamp(rollProgress, 0f, 100f);

                        if (doughSkinnedMesh != null && rolledBlendShapeIndex != -1)
                        {
                            doughSkinnedMesh.SetBlendShapeWeight(rolledBlendShapeIndex, rollProgress);
                        }

                        UpdateDoughColliderSize();

                        // Физическая анимация скалки при движении
                        if (hasRollingPin && rollingPinVisual != null)
                        {
                            // Наращиваем угол вращения вокруг оси качения
                            rollingPinAngle += mouseSpeed * rollingPinRotationSpeed * currentSensitivity;
                            Quaternion rollRot = Quaternion.AngleAxis(rollingPinAngle, rollingPinRotationAxis);

                            // Рандомное виляние боком (yaw) вокруг вертикальной оси Y
                            float randomYaw = Mathf.Sin(rollProgress * 0.15f) * 12f + (Mathf.PingPong(rollProgress * 0.25f, 10f) - 5f);
                            Quaternion yawRot = Quaternion.AngleAxis(randomYaw, Vector3.up);

                            rollingPinVisual.transform.localRotation = originalPinLocalRot * yawRot * rollRot;

                            // Движение из стороны в сторону (основное по X)
                            float slideOffset = Mathf.Sin(rollProgress * 0.2f) * rollingPinSlideAmount;
                            // Движение вперед-назад (меньшее по Z)
                            float forwardOffset = Mathf.Cos(rollProgress * 0.25f) * (rollingPinSlideAmount * 0.4f);
                            // Рандомные отклонения (боковые сдвиги) для живости движения
                            float randomXOffset = (Mathf.PingPong(rollProgress * 0.1f, 1f) - 0.5f) * (rollingPinSlideAmount * 0.3f);
                            float randomZOffset = (Mathf.PingPong(rollProgress * 0.15f, 1f) - 0.5f) * (rollingPinSlideAmount * 0.3f);

                            rollingPinVisual.transform.localPosition = originalPinLocalPos + new Vector3(slideOffset + randomXOffset, 0f, forwardOffset + randomZOffset);
                        }

                        if (rollSound != null)
                        {
                            if (!rollSound.isPlaying)
                            {
                                rollSound.loop = true;
                                rollSound.Play();
                            }
                        }
                    }
                    else
                    {
                        isRolling = false;
                        if (rollSound != null && rollSound.isPlaying)
                        {
                            rollSound.Pause();
                        }
                    }
                }
                else
                {
                    isRolling = false;
                    if (rollSound != null && rollSound.isPlaying)
                    {
                        rollSound.Pause();
                    }
                }

                if (rollProgress >= 100f)
                {
                    isRolled = true;
                    isRolling = false;
                    
                    if (doughSkinnedMesh != null && rolledBlendShapeIndex != -1)
                    {
                        doughSkinnedMesh.SetBlendShapeWeight(rolledBlendShapeIndex, 100f);
                    }

                    if (rollSound != null && rollSound.isPlaying)
                    {
                        rollSound.Stop();
                    }

                    // Автоматически убираем скалку в инвентарь при завершении
                    if (hasRollingPin)
                    {
                        // Убавляем прочность скалки при успешной раскатке
                        rollingPinCurrentDurability -= rollingPinAmountPerUse;
                        if (rollingPinCurrentDurability < 0) rollingPinCurrentDurability = 0;

                        TryPickUpRollingPin();
                    }

                    // Запускаем режим вырезания теста
                    if (cuttingController != null)
                    {
                        cuttingController.StartFlashing();
                    }
                }
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

    private bool IsMouseOverBoard()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            Collider col = surfaceCollider != null ? surfaceCollider : GetComponent<Collider>();
            if (col != null && (hit.collider == col || hit.transform.IsChildOf(col.transform)))
            {
                return true;
            }
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                return true;
            }
            if (doughVisual != null && (hit.transform == doughVisual.transform || hit.transform.IsChildOf(doughVisual.transform)))
            {
                return true;
            }
        }
        return false;
    }

    public bool IsMouseOverDough()
    {
        if (!hasDough || doughVisual == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == doughVisual || hit.collider.transform.IsChildOf(doughVisual.transform))
            {
                return true;
            }
        }
        return false;
    }

    public void EnterDoughRollingMode(Camera playerCam)
    {
        if (isViewing || isTransitioning) return;

        isViewing = true;
        isTransitioning = true;
        activeRollingBoard = this;

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

        if (!string.IsNullOrEmpty(weaponLayerName))
        {
            originalCullingMask = playerCam.cullingMask;
            int weaponLayer = LayerMask.NameToLayer(weaponLayerName);
            if (weaponLayer != -1)
            {
                playerCam.cullingMask &= ~(1 << weaponLayer);
            }
        }

        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        SetHighlight(false);

        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCamera(cameraTargetPos.position, cameraTargetPos.rotation));

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

        if (isRolled && cuttingController != null)
        {
            cuttingController.StartFlashing();
        }
    }

    public void ExitDoughRollingMode()
    {
        if (!isViewing || isTransitioning) return;

        isTransitioning = true;
        isCameraLocked = false;

        if (rollSound != null && rollSound.isPlaying)
        {
            rollSound.Pause();
        }

        // Если тесто положили, но так и не начали резать (или сделали 0 надрезов),
        // возвращаем тесто обратно в инвентарь при выходе из-за стола
        if (hasDough)
        {
            bool wasCut = false;
            if (cuttingController != null && cuttingController.currentCutCount > 0)
            {
                wasCut = true;
            }

            if (!wasCut)
            {
                TryPickUpDough();
            }
        }

        if (cuttingController != null)
        {
            cuttingController.StopCuttingMode();
        }

        // Собираем все разбросанные по столу кружки обратно в стопку при выходе
        DoughScatterController scatter = GetComponent<DoughScatterController>();
        if (scatter == null) scatter = GetComponentInChildren<DoughScatterController>(true);
        if (scatter != null)
        {
            scatter.GatherCirclesToStack();
        }

        // Автоматически возвращаем скалку в инвентарь игроку при выходе
        if (hasRollingPin)
        {
            TryPickUpRollingPin();
        }

        if (inventoryRect != null)
        {
            inventoryRect.anchoredPosition = originalInventoryPos;
            inventoryRect.localScale = originalInventoryScale;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            InventoryManager.Instance.ToggleInventory();
        }

        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        if (mainCamera != null && !string.IsNullOrEmpty(weaponLayerName))
        {
            Camera camComp = mainCamera.GetComponent<Camera>();
            if (camComp != null)
            {
                camComp.cullingMask = originalCullingMask;
            }
        }

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
        activeRollingBoard = null;
        isTransitioning = false;
    }

    public bool CanPlaceDough()
    {
        if (hasDough) return false;

        // Проверяем, есть ли заготовки на доске
        DoughScatterController scatter = GetComponent<DoughScatterController>();
        if (scatter == null) scatter = GetComponentInChildren<DoughScatterController>(true);
        if (scatter != null && scatter.HasBlanksOnBoard()) return false;

        // Проверяем, есть ли готовые пельмени на доске
        if (FindAnyObjectByType<Dumpling>() != null) return false;

        return true;
    }

    private void TryPlaceDoughFromInventory()
    {
        if (!CanPlaceDough()) return;

        InventorySlot slot = FindDoughSlot();
        if (slot != null && slot.itemData != null)
        {
            PlaceDough(slot.itemData);

            slot.itemData.amount--;
            if (slot.itemData.amount <= 0)
            {
                slot.ClearSlot();
            }
            else
            {
                slot.UpdateSlotUI();
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
            }
        }
    }

    private InventorySlot FindDoughSlot()
    {
        if (InventoryManager.Instance == null) return null;

        foreach (var slot in InventoryManager.Instance.hotbarSlots)
        {
            if (slot != null && !slot.IsEmpty() && slot.currentItemID == inputDoughItemID)
            {
                return slot;
            }
        }

        foreach (var slot in InventoryManager.Instance.inventorySlots)
        {
            if (slot != null && !slot.IsEmpty() && slot.currentItemID == inputDoughItemID)
            {
                return slot;
            }
        }

        return null;
    }

    public void PlaceDoughFromDrag(InventoryItemData dragItemData)
    {
        PlaceDough(dragItemData);
    }

    private void PlaceDough(InventoryItemData data)
    {
        hasDough = true;
        isRolled = false;
        isRolling = false;
        rollProgress = 0f;

        if (cuttingController != null)
        {
            cuttingController.ResetCuttingProgress();
        }

        FindSkinnedMeshAndIndex();

        if (doughSkinnedMesh != null && rolledBlendShapeIndex != -1)
        {
            doughSkinnedMesh.SetBlendShapeWeight(rolledBlendShapeIndex, 0f);
        }

        if (doughVisual != null)
        {
            doughVisual.SetActive(true);

            PickUpItem pickup = doughVisual.GetComponent<PickUpItem>();
            if (pickup == null) pickup = doughVisual.GetComponentInChildren<PickUpItem>(true);
            if (pickup != null)
            {
                pickup.enabled = false;
            }

            Collider[] colliders = doughVisual.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                c.enabled = true;
            }

            Renderer[] renderers = doughVisual.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = true;
            }
        }

        UpdateDoughColliderSize();

        if (placeSound != null) placeSound.Play();
    }

    private void TryPlaceRollingPinFromInventory()
    {
        InventorySlot slot = FindRollingPinSlot();
        if (slot != null && slot.itemData != null)
        {
            if (slot.itemData.isConsumable && slot.itemData.currentAmount <= 0)
            {
                return;
            }

            if (slot.itemData.isConsumable)
            {
                rollingPinCurrentDurability = slot.itemData.currentAmount;
                rollingPinMaxDurability = slot.itemData.maxAmount;
                rollingPinAmountPerUse = slot.itemData.amountPerUse;
            }
            else
            {
                rollingPinCurrentDurability = 100;
                rollingPinMaxDurability = 100;
                rollingPinAmountPerUse = 10;
            }

            PlaceRollingPin();

            slot.itemData.amount--;
            if (slot.itemData.amount <= 0)
            {
                slot.ClearSlot();
            }
            else
            {
                slot.UpdateSlotUI();
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
            }
        }
    }

    private InventorySlot FindRollingPinSlot()
    {
        if (InventoryManager.Instance == null) return null;

        foreach (var slot in InventoryManager.Instance.hotbarSlots)
        {
            if (slot != null && !slot.IsEmpty() && slot.currentItemID == rollingPinItemID)
            {
                return slot;
            }
        }

        foreach (var slot in InventoryManager.Instance.inventorySlots)
        {
            if (slot != null && !slot.IsEmpty() && slot.currentItemID == rollingPinItemID)
            {
                return slot;
            }
        }

        return null;
    }

    public void PlaceRollingPinFromDrag(InventoryItemData dragItemData)
    {
        if (dragItemData.isConsumable)
        {
            rollingPinCurrentDurability = dragItemData.currentAmount;
            rollingPinMaxDurability = dragItemData.maxAmount;
            rollingPinAmountPerUse = dragItemData.amountPerUse;
        }
        else
        {
            rollingPinCurrentDurability = 100;
            rollingPinMaxDurability = 100;
            rollingPinAmountPerUse = 10;
        }
        PlaceRollingPin();
    }

    private void PlaceRollingPin()
    {
        hasRollingPin = true;
        if (rollingPinVisual != null)
        {
            rollingPinVisual.transform.localPosition = originalPinLocalPos;
            rollingPinVisual.transform.localRotation = originalPinLocalRot;
            rollingPinVisual.SetActive(true);

            PickUpItem pickup = rollingPinVisual.GetComponent<PickUpItem>();
            if (pickup == null) pickup = rollingPinVisual.GetComponentInChildren<PickUpItem>(true);
            if (pickup != null)
            {
                pickup.enabled = false;
            }

            Collider[] colliders = rollingPinVisual.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }

            Renderer[] renderers = rollingPinVisual.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = true;
            }
        }

        if (placeSound != null) placeSound.Play();
    }

    private void TryPickUpRollingPin()
    {
        if (InventoryManager.Instance != null)
        {
            // Если прочность кончилась, скалка ломается и не возвращается
            if (rollingPinCurrentDurability <= 0)
            {
                hasRollingPin = false;
                if (rollingPinVisual != null) rollingPinVisual.SetActive(false);
                if (pickupSound != null) pickupSound.Play();
                return;
            }

            GameObject pinPrefab = InventoryManager.Instance.GetPrefabByID(rollingPinItemID);
            if (pinPrefab == null) return;

            PickUpItem pickupComponent = pinPrefab.GetComponent<PickUpItem>();
            if (pickupComponent == null) pickupComponent = pinPrefab.GetComponentInChildren<PickUpItem>(true);

            if (pickupComponent != null)
            {
                InventoryItemData pinData = new InventoryItemData(pickupComponent);
                pinData.amount = 1;

                // Записываем измененную прочность
                pinData.isConsumable = true;
                pinData.consumableType = ConsumableType.Item;
                pinData.currentAmount = rollingPinCurrentDurability;
                pinData.maxAmount = rollingPinMaxDurability;
                pinData.amountPerUse = rollingPinAmountPerUse;

                // Обновляем иконку заполнения в соответствии с оставшимся количеством
                if (pinData.fillIcons != null && pinData.fillIcons.Length > 0)
                {
                    float fillPercentage = Mathf.Clamp01((float)rollingPinCurrentDurability / rollingPinMaxDurability);
                    int iconIndex = Mathf.RoundToInt(fillPercentage * (pinData.fillIcons.Length - 1));
                    pinData.itemIcon = pinData.fillIcons[iconIndex];
                }

                bool added = InventoryManager.Instance.AddItem(pinData);
                if (!added)
                {
                    InventoryManager.Instance.SpawnDroppedItem(pinData);
                }

                hasRollingPin = false;
                if (rollingPinVisual != null) rollingPinVisual.SetActive(false);
                if (pickupSound != null) pickupSound.Play();
            }
        }
    }

    private void UpdateDoughColliderSize()
    {
        if (doughVisual == null) return;

        BoxCollider boxCol = doughVisual.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = doughVisual.GetComponentInChildren<BoxCollider>(true);

        if (boxCol != null)
        {
            if (!hasCachedOriginalColliderSize)
            {
                originalColliderSize = boxCol.size;
                originalColliderCenter = boxCol.center;
                hasCachedOriginalColliderSize = true;
            }

            float t = rollProgress / 100f;

            // Determine which local axis is vertical in world space
            Vector3 localUp = boxCol.transform.InverseTransformDirection(Vector3.up);
            float absX = Mathf.Abs(localUp.x);
            float absY = Mathf.Abs(localUp.y);
            float absZ = Mathf.Abs(localUp.z);

            Vector3 targetSize = originalColliderSize;
            Vector3 targetCenter = originalColliderCenter;

            if (absX > absY && absX > absZ)
            {
                // Local X is vertical
                targetSize.x = originalColliderSize.x * verticalColliderScaleMultiplier;
                targetSize.y = originalColliderSize.y * horizontalColliderScaleMultiplier;
                targetSize.z = originalColliderSize.z * horizontalColliderScaleMultiplier;

                targetCenter.x = originalColliderCenter.x * verticalColliderScaleMultiplier;
            }
            else if (absZ > absY && absZ > absX)
            {
                // Local Z is vertical
                targetSize.z = originalColliderSize.z * verticalColliderScaleMultiplier;
                targetSize.x = originalColliderSize.x * horizontalColliderScaleMultiplier;
                targetSize.y = originalColliderSize.y * horizontalColliderScaleMultiplier;

                targetCenter.z = originalColliderCenter.z * verticalColliderScaleMultiplier;
            }
            else
            {
                // Local Y is vertical (default)
                targetSize.y = originalColliderSize.y * verticalColliderScaleMultiplier;
                targetSize.x = originalColliderSize.x * horizontalColliderScaleMultiplier;
                targetSize.z = originalColliderSize.z * horizontalColliderScaleMultiplier;

                targetCenter.y = originalColliderCenter.y * verticalColliderScaleMultiplier;
            }

            boxCol.size = Vector3.Lerp(originalColliderSize, targetSize, t);
            boxCol.center = Vector3.Lerp(originalColliderCenter, targetCenter, t);
        }
    }

    public void ClearDoughState()
    {
        hasDough = false;
        isRolled = false;
        rollProgress = 0f;

        if (doughVisual != null)
        {
            doughVisual.SetActive(false);

            // Сбрасываем коллайдер теста в исходный размер для следующего раунда раскатки
            if (hasCachedOriginalColliderSize)
            {
                BoxCollider boxCol = doughVisual.GetComponent<BoxCollider>();
                if (boxCol == null) boxCol = doughVisual.GetComponentInChildren<BoxCollider>(true);
                if (boxCol != null)
                {
                    boxCol.size = originalColliderSize;
                    boxCol.center = originalColliderCenter;
                }
            }
        }
    }

    private void TryPickUpDough()
    {
        if (!hasDough) return;

        if (InventoryManager.Instance != null)
        {
            GameObject doughPrefab = InventoryManager.Instance.GetPrefabByID(inputDoughItemID);
            if (doughPrefab != null)
            {
                PickUpItem pickupComponent = doughPrefab.GetComponent<PickUpItem>();
                if (pickupComponent == null) pickupComponent = doughPrefab.GetComponentInChildren<PickUpItem>(true);

                if (pickupComponent != null)
                {
                    InventoryItemData doughData = new InventoryItemData(pickupComponent);
                    doughData.amount = 1;

                    bool added = InventoryManager.Instance.AddItem(doughData);
                    if (!added)
                    {
                        InventoryManager.Instance.SpawnDroppedItem(doughData);
                    }
                }
            }
        }

        ClearDoughState();
        if (pickupSound != null) pickupSound.Play();
    }
}
