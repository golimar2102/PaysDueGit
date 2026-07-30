using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.Localization;
using TMPro;

[System.Serializable]
public class OrganRewardConfig
{
    public NPCType npcType;
    public int rewardItemID;
    public int rewardItemAmount = 1;             // Кол-во предметов
}

[System.Serializable]
public class TableOrganConfig
{
    public string organName = "Guts";            // Название для инспектора
    public string childNameInCorpse = "guts";    // Имя дочернего объекта в модели трупа
    public int defaultRewardItemID;              // ID предмета-награды по умолчанию
    public int defaultRewardItemAmount = 1;      // Кол-во по умолчанию
    public System.Collections.Generic.List<OrganRewardConfig> typeSpecificRewards = new System.Collections.Generic.List<OrganRewardConfig>();

    [HideInInspector]
    public bool isExtracted = false;             // Флаг, вытащили ли уже его
    
    [HideInInspector]
    public Collider dynamicCollider;             // Ссылка на коллайдер органа на трупе
    [HideInInspector]
    public bool isColliderDynamicallyCreated = false;
}

public class ButcheringTableController : MonoBehaviour
{
    public static ButcheringTableController activeTable = null;

    public enum ButcheringState
    {
        None,           // Corpse is not placed, or placed but minigame not started
        ToolCheck,      // Minigame started, verifying tools
        WaitingForKnife,// Waiting for Knife (ID 49) drag & drop
        WaitingForCut,  // Slicing chest with the knife
        WaitingForSpread,// Spreading chest with hands
        WaitingForOrgans,// Extracting organs
        CutComplete     // Chest is open, butchering complete
    }

    [Header("Состояние")]
    public ButcheringState state = ButcheringState.None;
    public bool hasCorpse = false;
    public NPCCorpse placedCorpse = null;
    public bool isMinigameActive = false;

    [Header("Настройки предметов")]
    public int knifeItemID = 49;

    [Header("Звуки")]
    [Tooltip("Звук при успешном извлечении органа")]
    public AudioClip organExtractSound;
    [Tooltip("Аудиоресурс для воспроизведения звуков (если пустой, будет создан автоматически)")]
    public AudioSource audioSource;

    [Header("Настройки извлечения органов")]
    public System.Collections.Generic.List<TableOrganConfig> organs = new System.Collections.Generic.List<TableOrganConfig>();

    [Header("Точки крепления и коллайдеры")]
    [Tooltip("Точка, куда прилетает взгляд камеры при начале разделки")]
    public Transform cameraTargetPos;
    [Tooltip("Скорость полета камеры")]
    public float cameraMoveSpeed = 4f;
    [Tooltip("Точка позиционирования трупа на столе")]
    public Transform corpsePlacePoint;
    [Tooltip("Коллайдер груди NPC для перетаскивания ножа и проведения разреза")]
    public Collider chestCollider;

    private bool isChestColliderDynamicallyCreated = false;
    [Tooltip("3D модель ножа, воткнутого в грудь NPC (изначально выключена)")]
    public GameObject knifeVisual;

    [Header("Настройки движения ножа")]
    [Tooltip("Начальное локальное положение ножа при установке (progress = 0)")]
    public Vector3 knifeStartLocalPos;
    [Tooltip("Конечное локальное положение ножа в конце разреза (progress = 1)")]
    public Vector3 knifeEndLocalPos;

    [Header("Настройки жеста разрезания")]
    [Tooltip("Дистанция в пикселях, на которую нужно потянуть мышь вниз для разрезания/раздвигания")]
    public float requiredDragPixelDistance = 300f;
    [Tooltip("Имя BlendShape (Shape Key) и параметра Аниматора на трупе для разрезания")]
    public string blendShapeName = "TorsoOpen";
    [Tooltip("Использовать нормализованные значения (0..1) вместо (0..100) для параметра Аниматора")]
    public bool useNormalizedAnimatorParam = false;
    [Tooltip("Максимальный вес BlendShape, достигаемый при надрезе ножом (0..100)")]
    public float knifeMaxShapeKeyWeight = 30f;
    [Tooltip("Конечный вес BlendShape, достигаемый при раздвигании руками (0..100)")]
    public float spreadEndShapeKeyWeight = 100f;

    [Header("Скрытие интерфейса")]
    [Tooltip("Элементы UI HUD, скрываемые во время разделки")]
    public GameObject[] objectsToHide;
    [Tooltip("Слой оружия, скрываемый от камеры")]
    public string weaponLayerName = "Weapon";

    [Header("Смещение инвентаря")]
    public Vector2 inventoryOffsetPosition = new Vector2(-300f, 0f);
    public Vector3 inventoryScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    public Vector2 hotbarOffsetPosition = new Vector2(-300f, 25f);
    public Vector3 hotbarScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);

    [Header("Локализация (Всплывающие подсказки стола)")]
    public LocalizedString locPromptPlace;      // "Положить тело"
    public LocalizedString locPromptPickUp;     // "Забрать тело"
    public LocalizedString locPromptButcher;    // "Начать разделку"

    [Header("Локализация UI внутри миниигры")]
    public TextMeshProUGUI promptText;          // Текст подсказки/инструкции игроку
    public TextMeshProUGUI errorText;           // Текст ошибок (например, не хватает инструментов)
    
    public LocalizedString locNeedTools;        // "Требуется нож и тесак!"
    public LocalizedString locDragKnife;        // "Перетащите нож на грудь NPC"
    public LocalizedString locCutInstruction;   // "Зажмите ЛКМ и проведите вниз, чтобы разрезать грудь"
    public LocalizedString locSpreadInstruction;// "Зажмите ЛКМ и проведите вниз, чтобы раздвинуть рану руками"
    public LocalizedString locExitPrompt;       // "Выход: [Tab] / [Esc]"

    [Header("События")]
    [Tooltip("Событие при успешном завершении разреза")]
    public UnityEvent onCutComplete;

    [Header("Подсветка")]
    public Outline outline;

    // Смещение UI
    private RectTransform inventoryRect;
    private Vector2 originalInventoryPos;
    private Vector3 originalInventoryScale;

    // Внутреннее состояние камеры
    private Transform mainCamera;
    private int originalCullingMask;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isCameraLocked = false;
    private Coroutine cameraCoroutine;

    // Внутренние ссылки
    private PlayerMovement playerMovement;
    private MouseLook mouseLook;

    // Параметры разрезания
    private float currentCutProgress = 0f;
    private float currentSpreadProgress = 0f;
    private bool isCuttingMouseDrag = false;
    private float cutStartMouseY = 0f;
    private Animator corpseAnimator = null;

    void Awake()
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    void Start()
    {
        if (knifeVisual != null) knifeVisual.SetActive(false);
        if (promptText != null) promptText.gameObject.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null) outline.enabled = isHighlighted;
    }

    public string GetInteractPrompt(bool carryingCorpse, KeyCode interactKey, KeyCode toggleKey)
    {
        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        if (!hasCorpse)
        {
            if (carryingCorpse)
            {
                string placeStr = (locPromptPlace != null && !locPromptPlace.IsEmpty) ? locPromptPlace.GetLocalizedString() : (isEn ? "Place body" : "Положить тело");
                return $"<color=#FFD700>[{interactKey}]</color> {placeStr}";
            }
            return "";
        }
        else
        {
            if (state == ButcheringState.None)
            {
                string pickupStr = (locPromptPickUp != null && !locPromptPickUp.IsEmpty) ? locPromptPickUp.GetLocalizedString() : (isEn ? "Take body back" : "Забрать тело");
                string butcherStr = (locPromptButcher != null && !locPromptButcher.IsEmpty) ? locPromptButcher.GetLocalizedString() : (isEn ? "Butcher body" : "Начать разделку");
                return $"<color=#FFD700>[{interactKey}]</color> {pickupStr}\n<color=#FFD700>[{toggleKey}]</color> {butcherStr}";
            }
            return "";
        }
    }

    public bool CanPickUpCorpse()
    {
        if (!hasCorpse || isMinigameActive) return false;

        // Если извлечен хотя бы один орган, но не все, забирать труп нельзя
        bool hasAnyExtracted = false;
        bool allExtracted = true;
        if (organs != null && organs.Count > 0)
        {
            foreach (var organ in organs)
            {
                if (organ != null)
                {
                    if (organ.isExtracted) hasAnyExtracted = true;
                    else allExtracted = false;
                }
            }
        }

        if (hasAnyExtracted && !allExtracted)
        {
            return false;
        }

        return true;
    }

    public bool CanStartButchering()
    {
        return hasCorpse && state != ButcheringState.CutComplete;
    }

    public void PlaceCorpse(NPCCorpse corpse)
    {
        if (corpse == null || hasCorpse) return;

        placedCorpse = corpse;
        hasCorpse = true;
        
        // Устанавливаем ссылку на стол в самом трупе
        corpse.currentTable = this;

        // Восстанавливаем сохраненное состояние разделки трупа
        if (placedCorpse.isButchered)
        {
            state = ButcheringState.CutComplete;
            currentCutProgress = 1.0f;
            currentSpreadProgress = 1.0f;
        }
        else if (placedCorpse.isChestSpread)
        {
            state = ButcheringState.WaitingForOrgans;
            currentCutProgress = 1.0f;
            currentSpreadProgress = 1.0f;
        }
        else if (placedCorpse.isChestCut)
        {
            state = ButcheringState.WaitingForSpread;
            currentCutProgress = 1.0f;
            currentSpreadProgress = 0f;
        }
        else
        {
            state = ButcheringState.None;
            currentCutProgress = 0f;
            currentSpreadProgress = 0f;
        }

        // Сбрасываем глобальную ссылку carriedCorpse
        if (NPCCorpse.carriedCorpse == corpse)
        {
            NPCCorpse.carriedCorpse = null;
        }

        // Прикрепляем к столу
        corpse.transform.SetParent(corpsePlacePoint);
        corpse.transform.localPosition = Vector3.zero;
        corpse.transform.localRotation = Quaternion.identity;
        corpse.transform.localScale = Vector3.one;

        // Снимаем кинематику, отключаем гравитацию (чтобы аниматор работал правильно и физика не двигала)
        Rigidbody rb = corpse.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Переводим все коллайдеры трупа в режим триггеров
        Collider[] colliders = corpse.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col != null)
            {
                col.enabled = true;
                col.isTrigger = true;
            }
        }

        CapsuleCollider tableCapsule = corpse.GetComponent<CapsuleCollider>();
        if (tableCapsule == null) tableCapsule = corpse.GetComponentInChildren<CapsuleCollider>();
        if (tableCapsule != null)
        {
            tableCapsule.center = corpse.deadColliderCenter;
        }

        // Включаем дополнительные модели для разделки (оптимизация)
        corpse.SetExtraButcheringModelsActive(true);

        // Обновляем аниматор NPC
        corpseAnimator = corpse.GetComponent<Animator>();
        if (corpseAnimator == null) corpseAnimator = corpse.GetComponentInChildren<Animator>();
        if (corpseAnimator != null)
        {
            corpseAnimator.SetBool("OnTable", true);
            if (!string.IsNullOrEmpty(blendShapeName))
            {
                corpseAnimator.SetFloat(blendShapeName, 0f);
            }
        }

        if (knifeVisual != null) knifeVisual.SetActive(false);

        // Находим или создаем коллайдер груди на TorsoBody трупа
        if (chestCollider == null && placedCorpse != null)
        {
            Transform torsoBody = FindChildRecursive(placedCorpse.transform, "TorsoBody");
            if (torsoBody != null)
            {
                Collider col = torsoBody.GetComponent<Collider>();
                if (col == null)
                {
                    col = torsoBody.gameObject.AddComponent<BoxCollider>();
                    isChestColliderDynamicallyCreated = true;
                }
                else
                {
                    isChestColliderDynamicallyCreated = false;
                }
                
                col.isTrigger = true;
                col.enabled = true;
                chestCollider = col;
            }
        }

        ResetOrgansOnPlacedCorpse();
    }

    public void PickUpCorpse(GameObject player)
    {
        if (!CanPickUpCorpse()) return;

        NPCCorpse corpse = placedCorpse;

        // Снимаем ссылку на таблицу в трупе и отсоединяем от стола перед подбором в руки
        if (corpse != null)
        {
            corpse.transform.SetParent(null); // Сначала отсоединяем от стола
            corpse.currentTable = null;
            corpse.originalParent = null; // Сбрасываем, чтобы падал на пол, а не на стол
        }

        // Очищаем временные коллайдеры органов при подборе трупа
        CleanupDynamicColliders();

        // Уничтожаем временный коллайдер груди, если он был создан динамически
        if (chestCollider != null)
        {
            if (isChestColliderDynamicallyCreated)
            {
                Destroy(chestCollider);
                chestCollider = null;
            }
            else
            {
                chestCollider.enabled = true;
            }
        }

        placedCorpse = null;
        hasCorpse = false;
        state = ButcheringState.None;

        if (corpse != null)
        {
            // Отключаем OnTable в аниматоре
            Animator corpseAnim = corpse.GetComponent<Animator>();
            if (corpseAnim == null) corpseAnim = corpse.GetComponentInChildren<Animator>();
            if (corpseAnim != null)
            {
                corpseAnim.SetBool("OnTable", false);
            }

            // Возвращаем в руки
            corpse.PickUp(player);
        }
        corpseAnimator = null;
    }

    public void EnterButcheringMode(Camera playerCam)
    {
        if (isMinigameActive) return;

        isMinigameActive = true;
        activeTable = this;

        // Отключаем капсульный коллайдер трупа, чтобы он не перекрывал клики и перетаскивание во время миниигры
        if (placedCorpse != null)
        {
            CapsuleCollider capsule = placedCorpse.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = placedCorpse.GetComponentInChildren<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.enabled = false;
            }
        }

        mainCamera = playerCam.transform;
        originalCameraParent = mainCamera.parent;
        originalCameraLocalPos = mainCamera.localPosition;
        originalCameraLocalRot = mainCamera.localRotation;

        playerMovement = playerCam.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null) playerMovement = playerCam.GetComponentInChildren<PlayerMovement>();

        mouseLook = playerCam.GetComponent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInParent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInChildren<MouseLook>();

        // Выключаем движение и обзор игрока
        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // Скрываем оружие
        if (!string.IsNullOrEmpty(weaponLayerName))
        {
            originalCullingMask = playerCam.cullingMask;
            int weaponLayer = LayerMask.NameToLayer(weaponLayerName);
            if (weaponLayer != -1)
            {
                playerCam.cullingMask &= ~(1 << weaponLayer);
            }
        }

        // Скрываем HUD
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        // Летим камерой к empty child
        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCamera(cameraTargetPos.position, cameraTargetPos.rotation));

        // Открываем инвентарь для drag-and-drop
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
        }

        // Только если нож еще не был установлен, сбрасываем состояние и проверяем инструменты
        if (state == ButcheringState.None || state == ButcheringState.ToolCheck || state == ButcheringState.WaitingForKnife)
        {
            state = ButcheringState.ToolCheck;
            ValidateTools();
        }
        else if (state == ButcheringState.WaitingForCut)
        {
            if (errorText != null) errorText.gameObject.SetActive(false);
            
            bool isEn = false;
            try
            {
                if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
                {
                    string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                    isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
                }
            }
            catch {}

            if (promptText != null)
            {
                string prompt = (locCutInstruction != null && !locCutInstruction.IsEmpty) ? locCutInstruction.GetLocalizedString() : (isEn ? "Hold LMB and drag downwards to cut chest" : "Зажмите ЛКМ и проведите вниз, чтобы разрезать грудь");
                string exit = (locExitPrompt != null && !locExitPrompt.IsEmpty) ? locExitPrompt.GetLocalizedString() : (isEn ? "Exit: [Tab] / [Esc]" : "Выход: [Tab] / [Esc]");
                promptText.text = $"{prompt}\n<size=80%><color=#A0A0A0>{exit}</color></size>";
                promptText.gameObject.SetActive(true);
            }
        }
        else if (state == ButcheringState.WaitingForSpread)
        {
            if (errorText != null) errorText.gameObject.SetActive(false);
            ShowSpreadPrompt();
        }
        else if (state == ButcheringState.WaitingForOrgans)
        {
            if (errorText != null) errorText.gameObject.SetActive(false);
            ShowOrgansPrompt();
        }
    }

    public void ExitButcheringMode()
    {
        if (!isMinigameActive) return;

        isMinigameActive = false;
        activeTable = null;
        isCameraLocked = false;
        isCuttingMouseDrag = false;

        // Если нож еще не был вставлен, сбрасываем состояние до None, чтобы труп можно было забрать
        if (state == ButcheringState.ToolCheck || state == ButcheringState.WaitingForKnife)
        {
            state = ButcheringState.None;
        }

        // Очищаем временные коллайдеры при выходе из режима разделки
        CleanupDynamicColliders();

        // Включаем обратно коллайдер груди
        if (chestCollider != null) chestCollider.enabled = true;

        // Включаем обратно капсульный коллайдер трупа в качестве триггера, чтобы с ним можно было взаимодействовать
        if (placedCorpse != null)
        {
            CapsuleCollider capsule = placedCorpse.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = placedCorpse.GetComponentInChildren<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.enabled = true;
                capsule.isTrigger = true;
            }
        }

        if (promptText != null) promptText.gameObject.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);

        // Возвращаем UI инвентаря
        if (inventoryRect != null)
        {
            inventoryRect.anchoredPosition = originalInventoryPos;
            inventoryRect.localScale = originalInventoryScale;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            InventoryManager.Instance.ToggleInventory();
        }

        // Показываем скрытые элементы HUD
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        // Возвращаем culling mask
        if (mainCamera != null && !string.IsNullOrEmpty(weaponLayerName))
        {
            Camera cam = mainCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.cullingMask = originalCullingMask;
            }
        }

        // Летим камерой обратно
        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCameraBack());
    }

    private void ValidateTools()
    {
        bool hasKnife = InventoryManager.Instance != null && InventoryManager.Instance.HasItem(knifeItemID);

        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        if (hasKnife)
        {
            state = ButcheringState.WaitingForKnife;
            if (errorText != null) errorText.gameObject.SetActive(false);
            
            if (promptText != null)
            {
                string prompt = (locDragKnife != null && !locDragKnife.IsEmpty) ? locDragKnife.GetLocalizedString() : (isEn ? "Drag knife onto NPC's chest" : "Перетащите нож на грудь NPC");
                string exit = (locExitPrompt != null && !locExitPrompt.IsEmpty) ? locExitPrompt.GetLocalizedString() : (isEn ? "Exit: [Tab] / [Esc]" : "Выход: [Tab] / [Esc]");
                promptText.text = $"{prompt}\n<size=80%><color=#A0A0A0>{exit}</color></size>";
                promptText.gameObject.SetActive(true);
            }
        }
        else
        {
            state = ButcheringState.ToolCheck;
            if (promptText != null) promptText.gameObject.SetActive(false);

            if (errorText != null)
            {
                string missingMsg = isEn ? "Need a Knife!" : "Требуется нож!";
                string exit = (locExitPrompt != null && !locExitPrompt.IsEmpty) ? locExitPrompt.GetLocalizedString() : (isEn ? "Exit: [Tab] / [Esc]" : "Выход: [Tab] / [Esc]");
                errorText.text = $"{missingMsg}\n<size=80%><color=#A0A0A0>{exit}</color></size>";
                errorText.gameObject.SetActive(true);
            }
        }
    }

    public void OnKnifePlaced(InventoryItemData knifeItem)
    {
        if (state != ButcheringState.WaitingForKnife) return;

        state = ButcheringState.WaitingForCut;

        // Показываем воткнутый нож визуально и ставим его в начальное положение
        if (knifeVisual != null)
        {
            knifeVisual.transform.localPosition = knifeStartLocalPos;
            knifeVisual.SetActive(true);
        }

        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        // Меняем подсказку игроку
        if (promptText != null)
        {
            string prompt = (locCutInstruction != null && !locCutInstruction.IsEmpty) ? locCutInstruction.GetLocalizedString() : (isEn ? "Hold LMB and drag downwards to cut chest" : "Зажмите ЛКМ и проведите вниз, чтобы разрезать грудь");
            string exit = (locExitPrompt != null && !locExitPrompt.IsEmpty) ? locExitPrompt.GetLocalizedString() : (isEn ? "Exit: [Tab] / [Esc]" : "Выход: [Tab] / [Esc]");
            promptText.text = $"{prompt}\n<size=80%><color=#A0A0A0>{exit}</color></size>";
        }
    }

    void Update()
    {
        if (isMinigameActive)
        {
            // Выход по закрытию инвентаря
            if (InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
            {
                ExitButcheringMode();
                return;
            }

            // Выход по Esc
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitButcheringMode();
                return;
            }

            // Логика разрезания
            if (state == ButcheringState.WaitingForCut)
            {
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    // Проверяем клик по груди
                    if (IsMouseOverChest())
                    {
                        isCuttingMouseDrag = true;
                        cutStartMouseY = Input.mousePosition.y;
                    }
                }
                else if (Input.GetKeyUp(KeyCode.Mouse0))
                {
                    isCuttingMouseDrag = false;
                }

                if (isCuttingMouseDrag && Input.GetKey(KeyCode.Mouse0))
                {
                    float currentY = Input.mousePosition.y;
                    // Движение вниз по экрану
                    if (currentY < cutStartMouseY)
                    {
                        float deltaY = cutStartMouseY - currentY;
                        float progressDelta = deltaY / requiredDragPixelDistance;
                        
                        currentCutProgress = Mathf.Clamp01(currentCutProgress + progressDelta);

                        // Двигаем нож вдоль разреза
                        if (knifeVisual != null)
                        {
                            knifeVisual.transform.localPosition = Vector3.Lerp(knifeStartLocalPos, knifeEndLocalPos, currentCutProgress);
                        }

                        if (currentCutProgress >= 1.0f)
                        {
                            if (placedCorpse != null) placedCorpse.isChestCut = true;
                            TransitionToSpreadState();
                        }
                    }
                    cutStartMouseY = currentY;
                }
            }
            // Логика раздвигания руками
            else if (state == ButcheringState.WaitingForSpread)
            {
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    // Проверяем клик по груди
                    if (IsMouseOverChest())
                    {
                        isCuttingMouseDrag = true;
                        cutStartMouseY = Input.mousePosition.y;
                    }
                }
                else if (Input.GetKeyUp(KeyCode.Mouse0))
                {
                    isCuttingMouseDrag = false;
                }

                if (isCuttingMouseDrag && Input.GetKey(KeyCode.Mouse0))
                {
                    float currentY = Input.mousePosition.y;
                    if (currentY < cutStartMouseY)
                    {
                        float deltaY = cutStartMouseY - currentY;
                        float progressDelta = deltaY / requiredDragPixelDistance;
                        
                        currentSpreadProgress = Mathf.Clamp01(currentSpreadProgress + progressDelta);

                        if (currentSpreadProgress >= 1.0f)
                        {
                            if (placedCorpse != null) placedCorpse.isChestSpread = true;
                            TransitionToOrgansState();
                        }
                    }
                    cutStartMouseY = currentY;
                }
            }
            // Логика извлечения органов
            else if (state == ButcheringState.WaitingForOrgans)
            {
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    Camera cam = mainCamera.GetComponent<Camera>();
                    if (cam != null)
                    {
                        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                        RaycastHit hit;
                        if (Physics.Raycast(ray, out hit, 100f))
                        {
                            TableOrganConfig organ = GetOrganByDynamicCollider(hit.collider);
                            if (organ != null && !organ.isExtracted)
                            {
                                ExtractOrgan(organ);
                            }
                        }
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

        // Обновляем блендшейпы и параметры аниматора после работы анимаций персонажа
        if (hasCorpse && placedCorpse != null)
        {
            if (!string.IsNullOrEmpty(blendShapeName))
            {
                // Вычисляем текущий вес блендшейпа на основе состояния
                float currentWeight = 0f;
                if (state == ButcheringState.WaitingForCut)
                {
                    currentWeight = Mathf.Lerp(0f, knifeMaxShapeKeyWeight, currentCutProgress);
                }
                else if (state == ButcheringState.WaitingForSpread)
                {
                    currentWeight = Mathf.Lerp(knifeMaxShapeKeyWeight, spreadEndShapeKeyWeight, currentSpreadProgress);
                }
                else if (state == ButcheringState.WaitingForOrgans || state == ButcheringState.CutComplete)
                {
                    currentWeight = spreadEndShapeKeyWeight;
                }

                placedCorpse.SetBlendShapeWeight(blendShapeName, currentWeight);

                if (corpseAnimator != null)
                {
                    float animValue = useNormalizedAnimatorParam ? (currentWeight / 100f) : currentWeight;
                    corpseAnimator.SetFloat(blendShapeName, animValue);
                }
            }
        }
    }

    private void CompleteButchering()
    {
        state = ButcheringState.CutComplete;
        isCuttingMouseDrag = false;

        if (placedCorpse != null)
        {
            placedCorpse.isButchered = true;
        }

        // Событие завершения вскрытия груди
        onCutComplete?.Invoke();

        // Выключаем подсказки
        if (promptText != null) promptText.gameObject.SetActive(false);

        Debug.Log("[ButcheringTable] Corpse chest opened, spread, and organs extracted successfully!");
    }

    private void TransitionToSpreadState()
    {
        state = ButcheringState.WaitingForSpread;
        isCuttingMouseDrag = false;

        // Скрываем нож и возвращаем его в инвентарь игрока
        if (knifeVisual != null) knifeVisual.SetActive(false);
        ReturnKnifeToInventory();

        // Показываем новую подсказку на экране
        ShowSpreadPrompt();
    }

    private void ReturnKnifeToInventory()
    {
        if (InventoryManager.Instance != null)
        {
            GameObject knifePrefab = InventoryManager.Instance.GetPrefabByID(knifeItemID);
            if (knifePrefab != null)
            {
                PickUpItem pickup = knifePrefab.GetComponent<PickUpItem>();
                if (pickup == null) pickup = knifePrefab.GetComponentInChildren<PickUpItem>();
                if (pickup != null)
                {
                    InventoryItemData knifeData = new InventoryItemData(pickup);
                    knifeData.amount = 1;
                    InventoryManager.Instance.AddItem(knifeData);
                }
            }
        }
    }

    private void ShowSpreadPrompt()
    {
        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        if (promptText != null)
        {
            string prompt = (locSpreadInstruction != null && !locSpreadInstruction.IsEmpty) ? locSpreadInstruction.GetLocalizedString() : (isEn ? "Hold LMB and drag downwards to spread chest open" : "Зажмите ЛКМ и проведите вниз, чтобы раздвинуть рану руками");
            string exit = (locExitPrompt != null && !locExitPrompt.IsEmpty) ? locExitPrompt.GetLocalizedString() : (isEn ? "Exit: [Tab] / [Esc]" : "Выход: [Tab] / [Esc]");
            promptText.text = $"{prompt}\n<size=80%><color=#A0A0A0>{exit}</color></size>";
            promptText.gameObject.SetActive(true);
        }
    }

    private void TransitionToOrgansState()
    {
        state = ButcheringState.WaitingForOrgans;
        isCuttingMouseDrag = false;

        // Выключаем коллайдер груди, чтобы он не перекрывал клики
        if (chestCollider != null) chestCollider.enabled = false;

        // Автоматически добавляем/находим коллайдеры на органах трупа
        if (placedCorpse != null && organs != null)
        {
            foreach (var organ in organs)
            {
                // Проверяем, был ли орган уже извлечен в трупе
                if (organ != null && !placedCorpse.extractedOrganNames.Contains(organ.childNameInCorpse))
                {
                    Transform vt = FindChildRecursive(placedCorpse.transform, organ.childNameInCorpse);
                    if (vt != null)
                    {
                        Collider col = vt.GetComponent<Collider>();
                        if (col == null)
                        {
                            col = vt.gameObject.AddComponent<BoxCollider>();
                            organ.isColliderDynamicallyCreated = true;
                        }
                        else
                        {
                            organ.isColliderDynamicallyCreated = false;
                        }

                        col.isTrigger = true;
                        col.enabled = true;
                        organ.dynamicCollider = col;
                    }
                }
            }
        }

        ShowOrgansPrompt();
    }

    private void ShowOrgansPrompt()
    {
        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        if (promptText != null)
        {
            string prompt = isEn ? "Click on organs to extract them" : "Извлеките органы кликом мыши";
            string exit = (locExitPrompt != null && !locExitPrompt.IsEmpty) ? locExitPrompt.GetLocalizedString() : (isEn ? "Exit: [Tab] / [Esc]" : "Выход: [Tab] / [Esc]");
            promptText.text = $"{prompt}\n<size=80%><color=#A0A0A0>{exit}</color></size>";
            promptText.gameObject.SetActive(true);
        }
    }

    private void ExtractOrgan(TableOrganConfig organ)
    {
        organ.isExtracted = true;

        if (placedCorpse != null)
        {
            placedCorpse.extractedOrganNames.Add(organ.childNameInCorpse);
        }

        // Выключаем коллайдер
        if (organ.dynamicCollider != null)
        {
            if (organ.isColliderDynamicallyCreated)
            {
                Destroy(organ.dynamicCollider);
            }
            else
            {
                organ.dynamicCollider.enabled = false;
            }
            organ.dynamicCollider = null;
        }

        // Скрываем оригинальный меш на трупе сразу
        if (placedCorpse != null)
        {
            Transform visualTransform = FindChildRecursive(placedCorpse.transform, organ.childNameInCorpse);
            if (visualTransform != null)
            {
                visualTransform.gameObject.SetActive(false);
            }
        }

        // Воспроизводим звук
        if (organExtractSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(organExtractSound);
            }
            else
            {
                AudioSource.PlayClipAtPoint(organExtractSound, transform.position);
            }
        }

        // Выдаем предмет-награду в зависимости от типа NPC на трупе
        int rewardItemID = organ.defaultRewardItemID;
        int rewardItemAmount = organ.defaultRewardItemAmount;
        if (placedCorpse != null && organ.typeSpecificRewards != null)
        {
            foreach (var reward in organ.typeSpecificRewards)
            {
                if (reward != null && reward.npcType == placedCorpse.npcType)
                {
                    rewardItemID = reward.rewardItemID;
                    rewardItemAmount = reward.rewardItemAmount;
                    break;
                }
            }
        }

        if (rewardItemID > 0 && rewardItemAmount > 0 && InventoryManager.Instance != null)
        {
            GameObject itemPrefab = InventoryManager.Instance.GetPrefabByID(rewardItemID);
            if (itemPrefab != null)
            {
                PickUpItem pickup = itemPrefab.GetComponent<PickUpItem>();
                if (pickup == null) pickup = itemPrefab.GetComponentInChildren<PickUpItem>();
                if (pickup != null)
                {
                    InventoryItemData itemData = new InventoryItemData(pickup);
                    itemData.amount = rewardItemAmount;
                    InventoryManager.Instance.AddItem(itemData);
                }
            }
        }

        // Проверяем, все ли органы извлечены
        bool allExtracted = true;
        if (organs != null)
        {
            foreach (var o in organs)
            {
                if (o != null && !o.isExtracted)
                {
                    allExtracted = false;
                    break;
                }
            }
        }

        if (allExtracted)
        {
            CompleteButchering();
        }
    }

    private TableOrganConfig GetOrganByDynamicCollider(Collider col)
    {
        if (organs != null && col != null)
        {
            foreach (var organ in organs)
            {
                if (organ != null && organ.dynamicCollider == col)
                {
                    return organ;
                }
            }
        }
        return null;
    }

    private void CleanupDynamicColliders()
    {
        if (organs != null)
        {
            foreach (var organ in organs)
            {
                if (organ != null && organ.dynamicCollider != null)
                {
                    if (organ.isColliderDynamicallyCreated)
                    {
                        Destroy(organ.dynamicCollider);
                    }
                    else
                    {
                        organ.dynamicCollider.enabled = false;
                    }
                    organ.dynamicCollider = null;
                }
            }
        }
    }

    private void ResetOrgansOnPlacedCorpse()
    {
        if (placedCorpse == null) return;

        // Включаем обратно коллайдер груди при установке трупа
        if (chestCollider != null) chestCollider.enabled = true;

        if (organs != null)
        {
            foreach (var organ in organs)
            {
                if (organ != null)
                {
                    // Состояние органов берем из сохраненного списка на трупе
                    bool extracted = placedCorpse.extractedOrganNames.Contains(organ.childNameInCorpse);
                    organ.isExtracted = extracted;
                    organ.dynamicCollider = null;

                    Transform visual = FindChildRecursive(placedCorpse.transform, organ.childNameInCorpse);
                    if (visual != null)
                    {
                        visual.gameObject.SetActive(!extracted);
                    }
                }
            }
        }
    }

    private Transform FindChildRecursive(Transform parent, string nameToFind)
    {
        if (parent.name.Equals(nameToFind, System.StringComparison.OrdinalIgnoreCase) ||
            parent.name.Contains(nameToFind))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), nameToFind);
            if (result != null) return result;
        }
        return null;
    }

    private bool IsMouseOverChest()
    {
        if (chestCollider == null || mainCamera == null) return false;

        Camera cam = mainCamera.GetComponent<Camera>();
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
        
        foreach (var hit in hits)
        {
            if (hit.collider == chestCollider || hit.transform.IsChildOf(chestCollider.transform))
            {
                return true;
            }
        }
        return false;
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
    }
}
