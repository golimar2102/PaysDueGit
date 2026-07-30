using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events; 
using System.Collections;
using TMPro;

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Элементы")]
    public Image iconDisplay;
    public GameObject highlightFrame;
    
    [Header("Текст стака")] // <-- НОВОЕ
    public TextMeshProUGUI amountText;

    [Header("Полоска прочности (Масло, Предохранители)")]
    public Image durabilityBar;

    [Header("Блокировка слота")]
    public bool isLocked = false;
    public GameObject lockedVisuals;

    [Header("Эффекты при ошибке (Тряска)")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 8f;

    [Header("Специальный слот (Мешки, Фонари)")]
    public bool isSpecialSlot = false;
    [Tooltip("ID предметов, которые можно положить в этот слот. Оставьте пустым — принимает всё.")]
    public int[] acceptedItemIDs = new int[0];

    [Header("Связанный слот (разблокируется, если тут есть предмет)")]
    [Tooltip("Укажите слот (например, для лампы), который должен разблокироваться при наличии предмета в этом слоте.")]
    public InventorySlot linkedUnlockSlot;

    [HideInInspector]
    public bool isDraggingSingleItem = false;

    [Header("События слота")]
    public UnityEvent onSlotFilled;
    public UnityEvent onSlotEmptied;

    [Header("Эффекты при наведении")]
    public float hoverScale = 1.1f;
    public float zoomSpeed = 15f;

    [HideInInspector] public InventoryItemData itemData; 
    
    public string currentItemName { get { return itemData != null ? itemData.itemName : ""; } }
    public int currentItemID { get { return itemData != null ? itemData.itemID : -1; } } 

    [HideInInspector] public bool isSelected = false; 
    public bool isEmpty { get; private set; } = true;

    // --- НОВОЕ: Глобальная переменная для отслеживания слота под курсором ---
    public static InventorySlot hoveredSlot;

    private static GameObject dragGhost; 
    private static Image ghostImage;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool isShaking = false;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        if (highlightFrame != null && !isSelected) highlightFrame.SetActive(false);
        if (lockedVisuals != null) lockedVisuals.SetActive(isLocked);

        UpdateSlotUI(); // <-- ИЗМЕНЕНО: Сразу обновляем UI при старте
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * zoomSpeed);
    }

    // Очищаем ссылку при выключении слота (закрытии инвентаря)
    void OnDisable()
    {
        if (hoveredSlot == this) hoveredSlot = null;
        targetScale = originalScale;
        if (highlightFrame != null && !isSelected) highlightFrame.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isLocked) return;

        // Если мы навели на дочерний слот (например, крючок внутри CB_Waist), 
        // то родительский слот должен проигнорировать это событие
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            InventorySlot actualSlot = eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlot>();
            if (actualSlot != null && actualSlot != this) return;
        }

        hoveredSlot = this; // ЗАПОМИНАЕМ, ЧТО МЫ НАВЕЛИСЬ НА ЭТОТ СЛОТ

        targetScale = originalScale * hoverScale;
        if (highlightFrame != null) highlightFrame.SetActive(true); 

        RefreshTooltip();
    }

    public void RefreshTooltip()
    {
        if (InventoryManager.Instance != null)
        {
            if (!isEmpty && !isLocked)
                InventoryManager.Instance.ShowTooltip(currentItemName);
            else
                InventoryManager.Instance.HideTooltip();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Игнорируем выход, если мы всё ещё находимся над этим же слотом 
        // (например, перешли на его внутренний текст или картинку)
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            InventorySlot actualSlot = eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlot>();
            if (actualSlot == this) return;
        }

        if (hoveredSlot == this) hoveredSlot = null; // ОЧИЩАЕМ, КОГДА МЫШКА УШЛА

        targetScale = originalScale;
        
        if (highlightFrame != null && !isSelected) 
        {
            highlightFrame.SetActive(false);
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.HideTooltip();

            // Если мы ушли с дочернего слота обратно на родительский, 
            // заставляем родительский обновить свой тултип
            if (eventData.pointerCurrentRaycast.gameObject != null)
            {
                InventorySlot newSlot = eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlot>();
                if (newSlot != null && newSlot != this)
                {
                    InventorySlot.hoveredSlot = newSlot;
                    newSlot.RefreshTooltip();
                }
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked)
        {
            StartCoroutine(ShakeEffect());
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log($"[InventorySlot] Right click on slot. ItemData: {(itemData != null ? itemData.itemName : "null")}");
            if (itemData != null)
            {
                Debug.Log($"[InventorySlot] isConsumable: {itemData.isConsumable}, type: {itemData.consumableType}, amount: {itemData.currentAmount}, liquidType: {itemData.currentLiquidType}");
                
                if (itemData.isConsumable && 
                    (itemData.consumableType == ConsumableType.LiquidContainer || itemData.consumableType == ConsumableType.LampOil) && 
                    itemData.currentAmount > 0 && itemData.currentLiquidType != LiquidType.None)
                {
                    if (InventoryContextMenu.Instance != null)
                    {
                        Debug.Log($"[InventorySlot] Show context menu via InventoryContextMenu.Instance at {eventData.position}");
                        InventoryContextMenu.Instance.Show(this, eventData.position);
                    }
                    else if (InventoryManager.Instance != null && InventoryManager.Instance.contextMenu != null)
                    {
                        Debug.Log($"[InventorySlot] Show context menu via InventoryManager fallback at {eventData.position}");
                        InventoryManager.Instance.contextMenu.Show(this, eventData.position);
                    }
                    else
                    {
                        Debug.LogWarning($"[InventorySlot] Context menu instance not found!");
                    }
                }
                else
                {
                    Debug.Log($"[InventorySlot] Conditions for context menu not met!");
                }
            }
        }
    }

    private IEnumerator ShakeEffect()
    {
        if (isShaking) yield break;
        isShaking = true;

        Quaternion originalRot = transform.localRotation;
        float elapsed = 0.0f;

        while (elapsed < shakeDuration)
        {
            float zRotation = Random.Range(-shakeMagnitude, shakeMagnitude);
            transform.localRotation = originalRot * Quaternion.Euler(0, 0, zRotation);
            elapsed += Time.deltaTime;
            yield return null; 
        }

        transform.localRotation = originalRot; 
        isShaking = false;
    }

    public bool IsEmpty() 
    { 
        return isEmpty && !isLocked && !isSpecialSlot; 
    }

    /// <summary>
    /// Возвращает true, если данный слот принимает предмет с таким itemData.
    /// Для не-специального слота — всегда true.
    /// Для специального — проверяет список acceptedItemIDs (если список пуст, принимает всё).
    /// </summary>
    public bool AcceptsItem(InventoryItemData data)
    {
        if (!isSpecialSlot) return true;
        if (data == null) return false;
        if (acceptedItemIDs == null || acceptedItemIDs.Length == 0) return true;
        foreach (int id in acceptedItemIDs)
            if (id == data.itemID) return true;
        return false;
    }

    // === НОВАЯ ФУНКЦИЯ ОБНОВЛЕНИЯ ВИЗУАЛА СЛОТА ===
    public void UpdateSlotUI()
    {
        if (itemData != null && !isEmpty)
        {
            if (iconDisplay != null) 
            {
                iconDisplay.sprite = itemData.itemIcon;
                iconDisplay.enabled = true;
                iconDisplay.gameObject.SetActive(true);
            }
            
            // Если предмет стакается и его больше 1 - показываем цифру
            if (amountText != null) 
            {
                if (itemData.isStackable && itemData.amount > 1) 
                {
                    amountText.text = itemData.amount.ToString();
                    amountText.gameObject.SetActive(true);
                } 
                else 
                {
                    amountText.gameObject.SetActive(false);
                }
            }

            // Полоска прочности для расходников (предохранители, масло) или предметов с топливом/прочностью (фонарь, свеча)
            if (durabilityBar != null)
            {
                if (itemData.isConsumable && itemData.maxAmount > 0)
                {
                    float fillPct = Mathf.Clamp01((float)itemData.currentAmount / itemData.maxAmount);
                    durabilityBar.fillAmount = fillPct;
                    durabilityBar.color = Color.Lerp(Color.red, Color.green, fillPct);
                    durabilityBar.gameObject.SetActive(true);
                }
                else if (itemData.lanternFuel >= 0f || (InventoryManager.Instance != null && InventoryManager.Instance.IsLanternItem(itemData.itemID)))
                {
                    float maxFuel = 100f;
                    LanternController lanternRef = FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
                    if (lanternRef != null) maxFuel = lanternRef.maxFuel;

                    float currentFuel = itemData.lanternFuel < 0f ? maxFuel : itemData.lanternFuel;
                    float fillPct = Mathf.Clamp01(currentFuel / maxFuel);
                    durabilityBar.fillAmount = fillPct;
                    durabilityBar.color = Color.Lerp(Color.red, Color.green, fillPct);
                    durabilityBar.gameObject.SetActive(true);
                }
                else
                {
                    durabilityBar.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            if (iconDisplay != null) iconDisplay.enabled = false;
            if (amountText != null) amountText.gameObject.SetActive(false);
            if (durabilityBar != null) durabilityBar.gameObject.SetActive(false);
        }
    }

    public void AddItem(InventoryItemData data)
    {
        itemData = data;
        isEmpty = false;
        
        UpdateSlotUI();
        
        if (isSpecialSlot)
        {
            onSlotFilled?.Invoke();
            // Уведомляем менеджер инвентаря об изменении рюкзачного слота
            if (InventoryManager.Instance != null && InventoryManager.Instance.backpackSlot == this)
                InventoryManager.Instance.RefreshInventorySlots();
            // Уведомляем об изменении слота ремня
            if (InventoryManager.Instance != null && InventoryManager.Instance.waistSlot == this)
                InventoryManager.Instance.RefreshWaistSlots();
        }

        if (linkedUnlockSlot != null && InventoryManager.Instance != null)
            InventoryManager.Instance.RefreshLinkedSlot(this);
    }

    public void ClearSlot()
    {
        itemData = null;
        isEmpty = true;

        UpdateSlotUI();

        if (isSpecialSlot)
        {
            onSlotEmptied?.Invoke();
            // Уведомляем менеджер инвентаря об изменении рюкзачного слота
            if (InventoryManager.Instance != null && InventoryManager.Instance.backpackSlot == this)
                InventoryManager.Instance.RefreshInventorySlots();
            // Уведомляем об изменении слота ремня
            if (InventoryManager.Instance != null && InventoryManager.Instance.waistSlot == this)
                InventoryManager.Instance.RefreshWaistSlots();
        }

        if (linkedUnlockSlot != null && InventoryManager.Instance != null)
            InventoryManager.Instance.RefreshLinkedSlot(this);
    }

    public void UnlockSlot()
    {
        isLocked = false;
        if (lockedVisuals != null) lockedVisuals.SetActive(false);
    }

    public void LockSlot()
    {
        isLocked = true;
        if (lockedVisuals != null) lockedVisuals.SetActive(true);
        if (!isEmpty) ClearSlot(); 
    }

    /// <summary>
    /// Блокирует или разблокирует слот БЕЗ уничтожения предметов.
    /// Используется системой рюкзаков: предмет остаётся «хранится» в недоступном слоте.
    /// </summary>
    public void SetLockedKeepItems(bool locked)
    {
        isLocked = locked;
        if (lockedVisuals != null) lockedVisuals.SetActive(locked);
        // Если слот был разблокирован — обновляем UI, чтобы предмет снова отобразился
        if (!locked) UpdateSlotUI();

        // Если статус блокировки изменился, обязательно обновляем связанный слот (если он есть)
        if (linkedUnlockSlot != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RefreshLinkedSlot(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isEmpty || iconDisplay == null || isLocked) return;

        // Если нажата ПКМ (Right) и предметов больше 1, берем только один
        if (eventData.button == PointerEventData.InputButton.Right && itemData.isStackable && itemData.amount > 1)
        {
            isDraggingSingleItem = true;
            // Иконку исходного слота не скрываем, так как в нем останутся предметы
        }
        else
        {
            isDraggingSingleItem = false;
            iconDisplay.enabled = false;
            if (amountText != null) amountText.gameObject.SetActive(false);
            if (durabilityBar != null) durabilityBar.gameObject.SetActive(false);
        }

        if (dragGhost == null)
        {
            dragGhost = new GameObject("DragGhost");
            ghostImage = dragGhost.AddComponent<Image>();
            ghostImage.raycastTarget = false; 

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) dragGhost.transform.SetParent(canvas.rootCanvas.transform, false);
        }

        dragGhost.transform.SetAsLastSibling();
        ghostImage.sprite = iconDisplay.sprite;
        ghostImage.rectTransform.sizeDelta = iconDisplay.rectTransform.rect.size;
        
        dragGhost.SetActive(true);
        OnDrag(eventData);

        if (itemData != null && GeneratorController.IsFuseItem(itemData.itemID))
        {
            GeneratorController.OnFuseDragStartedAll();
        }

        if (itemData != null && GeneratorController.IsCanisterItem(itemData))
        {
            GeneratorController.OnCanisterDragStartedAll();
        }

        if (itemData != null)
        {
            bool isMeat = MeatContainerController.IsMeatItem(itemData.itemID);
            Debug.Log($"[MeatDrag] OnBeginDrag: itemID={itemData.itemID}, isMeat={isMeat}, ControllerInstance={(MeatContainerController.Instance != null ? "not null" : "null")}");
            if (isMeat)
            {
                MeatContainerController.OnMeatDragStartedAll(itemData.itemID);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isEmpty || dragGhost == null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvas.rootCanvas.transform as RectTransform,
            eventData.position,
            canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera,
            out Vector3 worldPoint))
        {
            dragGhost.transform.position = worldPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        GeneratorController.OnFuseDragEndedAll();
        GeneratorController.OnCanisterDragEndedAll();
        MeatContainerController.OnMeatDragEndedAll();

        if (!isEmpty && itemData != null && GeneratorController.IsFuseItem(itemData.itemID))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 20f);
            GeneratorFuseHolder holder = null;
            foreach (var h in hits)
            {
                holder = h.collider.GetComponent<GeneratorFuseHolder>();
                if (holder == null) holder = h.collider.GetComponentInChildren<GeneratorFuseHolder>();
                if (holder == null) holder = h.collider.GetComponentInParent<GeneratorFuseHolder>();
                if (holder != null) break;
            }

            if (holder != null && holder.isEmpty)
            {
                InventoryItemData fuseData = itemData.Clone();
                fuseData.amount = 1;

                holder.InsertFuse(fuseData);

                if (itemData.isStackable && itemData.amount > 1)
                {
                    itemData.amount--;
                    UpdateSlotUI();
                }
                else
                {
                    ClearSlot();
                }

                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
                }
            }
        }

        if (!isEmpty && itemData != null && GeneratorController.IsCanisterItem(itemData))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 20f);
            GeneratorFuelPipe pipe = null;
            foreach (var h in hits)
            {
                pipe = h.collider.GetComponent<GeneratorFuelPipe>();
                if (pipe == null) pipe = h.collider.GetComponentInChildren<GeneratorFuelPipe>();
                if (pipe == null) pipe = h.collider.GetComponentInParent<GeneratorFuelPipe>();
                if (pipe != null) break;
            }

            if (pipe != null && pipe.isEmpty)
            {
                InventoryItemData canisterData = itemData.Clone();
                canisterData.amount = 1;

                pipe.InsertCanister(canisterData);

                if (itemData.isStackable && itemData.amount > 1)
                {
                    itemData.amount--;
                    UpdateSlotUI();
                }
                else
                {
                    ClearSlot();
                }

            }
        }

        // --- ДЛЯ РАСКАТКИ ТЕСТА (DOUGH ROLLING) ---
        if (!isEmpty && itemData != null)
        {
            DoughRollingController activeBoard = DoughRollingController.activeRollingBoard;
            if (activeBoard != null && activeBoard.isViewing)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
                bool hitBoard = false;
                foreach (var h in hits)
                {
                    if (h.collider.transform == activeBoard.transform || 
                        h.collider.transform.IsChildOf(activeBoard.transform) ||
                        h.collider.GetComponentInParent<DoughRollingController>() == activeBoard ||
                        (activeBoard.doughVisual != null && (h.collider.transform == activeBoard.doughVisual.transform || h.collider.transform.IsChildOf(activeBoard.doughVisual.transform) || h.collider.GetComponentInParent<SkinnedMeshRenderer>() == activeBoard.doughSkinnedMesh)) ||
                        (activeBoard.surfaceCollider != null && (h.collider == activeBoard.surfaceCollider || h.collider.transform.IsChildOf(activeBoard.surfaceCollider.transform) || activeBoard.surfaceCollider.transform.IsChildOf(h.collider.transform))))
                    {
                        hitBoard = true;
                        break;
                    }
                }

                if (hitBoard)
                {
                    if (itemData.itemID == activeBoard.inputDoughItemID && activeBoard.CanPlaceDough())
                    {
                        activeBoard.PlaceDoughFromDrag(itemData);

                        if (itemData.isStackable && itemData.amount > 1)
                        {
                            itemData.amount--;
                            UpdateSlotUI();
                        }
                        else
                        {
                            ClearSlot();
                        }

                        if (InventoryManager.Instance != null)
                        {
                            InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
                        }
                    }
                    else if (itemData.itemID == activeBoard.rollingPinItemID && !activeBoard.hasRollingPin && activeBoard.hasDough && !activeBoard.isRolled)
                    {
                        bool canUsePin = true;
                        if (itemData.isConsumable && itemData.currentAmount <= 0)
                        {
                            canUsePin = false;
                        }

                        if (canUsePin)
                        {
                            activeBoard.PlaceRollingPinFromDrag(itemData);

                            if (itemData.isStackable && itemData.amount > 1)
                            {
                                itemData.amount--;
                                UpdateSlotUI();
                            }
                            else
                            {
                                ClearSlot();
                            }

                            if (InventoryManager.Instance != null)
                            {
                                InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
                            }
                        }
                    }
                }
            }
        }

        // --- ДЛЯ РАЗДЕЛКИ ТУШ (NPC CORPSE BUTCHERING) ---
        if (!isEmpty && itemData != null)
        {
            ButcheringTableController activeTable = ButcheringTableController.activeTable;
            if (activeTable != null && activeTable.isMinigameActive && activeTable.state == ButcheringTableController.ButcheringState.WaitingForKnife)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
                bool hitChest = false;
                foreach (var h in hits)
                {
                    if (h.collider == activeTable.chestCollider || h.collider.transform.IsChildOf(activeTable.chestCollider.transform))
                    {
                        hitChest = true;
                        break;
                    }
                }

                if (hitChest && itemData.itemID == activeTable.knifeItemID)
                {
                    activeTable.OnKnifePlaced(itemData);

                    if (itemData.isStackable && itemData.amount > 1)
                    {
                        itemData.amount--;
                        UpdateSlotUI();
                    }
                    else
                    {
                        ClearSlot();
                    }

                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
                    }
                }
            }
        }

        // --- ДЛЯ КОНТЕЙНЕРА МЯСА (MEAT CONTAINER FILLING) ---
        if (!isEmpty && itemData != null)
        {
            bool isMeat = MeatContainerController.IsMeatItem(itemData.itemID);
            Debug.Log($"[MeatDrag] OnEndDrag: itemID={itemData.itemID}, isMeat={isMeat}");

            if (isMeat)
            {
                DoughRollingController activeBoard = DoughRollingController.activeRollingBoard;
                Debug.Log($"[MeatDrag] activeBoard={(activeBoard != null ? "not null" : "null")}, isViewing={(activeBoard != null ? activeBoard.isViewing.ToString() : "false")}");

                if (activeBoard != null && activeBoard.isViewing)
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
                    Debug.Log($"[MeatDrag] RaycastAll hits count: {hits.Length}");

                    MeatContainerCell targetCell = null;
                    foreach (var h in hits)
                    {
                        Debug.Log($"[MeatDrag] Hit object: {h.collider.gameObject.name}, path: {GetGameObjectPath(h.collider.gameObject)}");
                        targetCell = h.collider.GetComponent<MeatContainerCell>();
                        if (targetCell == null) targetCell = h.collider.GetComponentInParent<MeatContainerCell>();
                        if (targetCell != null)
                        {
                            Debug.Log($"[MeatDrag] Found MeatContainerCell component on hit object! Cell requiredItemID={targetCell.requiredItemID}, isFilled={targetCell.isFilled}");
                            break;
                        }
                    }

                    if (targetCell != null && targetCell.requiredItemID == itemData.itemID && targetCell.currentPortions < targetCell.maxPortions)
                    {
                        Debug.Log($"[MeatDrag] Success! Filling cell with itemID={itemData.itemID}");
                        targetCell.FillCell();

                        if (itemData.isStackable && itemData.amount > 1)
                        {
                            itemData.amount--;
                            UpdateSlotUI();
                        }
                        else
                        {
                            ClearSlot();
                        }

                        if (InventoryManager.Instance != null)
                        {
                            InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
                        }
                    }
                    else if (targetCell != null)
                    {
                        Debug.LogWarning($"[MeatDrag] Cell matches failed: requiredItemID={targetCell.requiredItemID} (expected {itemData.itemID}), isFilled={targetCell.isFilled}");
                    }
                }
            }
        }

        if (dragGhost != null) dragGhost.SetActive(false);
        if (!isEmpty && iconDisplay != null) 
        {
            iconDisplay.enabled = true;
            if (amountText != null && itemData.isStackable && itemData.amount > 1)
                amountText.gameObject.SetActive(true);
            if (durabilityBar != null && itemData.isConsumable && itemData.maxAmount > 0)
            {
                float fillPct = Mathf.Clamp01((float)itemData.currentAmount / itemData.maxAmount);
                durabilityBar.fillAmount = fillPct;
                durabilityBar.color = Color.Lerp(Color.red, Color.green, fillPct);
                durabilityBar.gameObject.SetActive(true);
            }
        }
        isDraggingSingleItem = false;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (isLocked) 
        {
            StartCoroutine(ShakeEffect()); 
            return; 
        }

        if (eventData.pointerDrag != null)
        {
            InventorySlot sourceSlot = eventData.pointerDrag.GetComponent<InventorySlot>();
            
            if (sourceSlot != null && sourceSlot != this && !sourceSlot.IsEmpty())
            {
                bool canAcceptIncoming = true;
                bool sourceCanAcceptMine = true;

                if (this.isSpecialSlot && !this.AcceptsItem(sourceSlot.itemData))
                    canAcceptIncoming = false;

                if (sourceSlot.isSpecialSlot && !this.isEmpty && !sourceSlot.AcceptsItem(this.itemData))
                    sourceCanAcceptMine = false;

                if (canAcceptIncoming && sourceCanAcceptMine)
                {
                    // --- ЗАПРАВКА ЛАМПЫ: если тащим LampOil на слот с фонарём ---
                    if (sourceSlot.itemData != null && sourceSlot.itemData.isConsumable &&
                        sourceSlot.itemData.consumableType == ConsumableType.LampOil &&
                        sourceSlot.itemData.currentLiquidType == LiquidType.Oil &&
                        !this.isEmpty && this.itemData != null &&
                        InventoryManager.Instance != null &&
                        InventoryManager.Instance.IsLanternItem(this.itemData.itemID))
                    {
                        // Если бутылка пустая — не заправляем, делаем обычный swap
                        bool bottleHasFuel = sourceSlot.itemData.isStackable
                            ? sourceSlot.itemData.amount > 1
                            : sourceSlot.itemData.currentAmount > 0;

                        if (bottleHasFuel)
                        {
                            float fuelToAdd = sourceSlot.itemData.amountPerUse;

                            // Инициализируем топливо если ещё не инициализировано (новая лампа = полный бак)
                            if (this.itemData.lanternFuel < 0f)
                            {
                                LanternController lanternInit = FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
                                this.itemData.lanternFuel = lanternInit != null ? lanternInit.maxFuel : 100f;
                            }

                            // Находим максимальный запас топлива
                            float maxFuel = 100f;
                            LanternController lanternRef = FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
                            if (lanternRef != null) maxFuel = lanternRef.maxFuel;

                            // Заправляем лампу в инвентаре
                            this.itemData.lanternFuel = Mathf.Min(this.itemData.lanternFuel + fuelToAdd, maxFuel);

                            // Если эта лампа сейчас в руках — обновляем LanternController сразу
                            if (InventoryManager.Instance.selectedSlotIndex >= 0 &&
                                InventoryManager.Instance.hotbarSlots != null)
                            {
                                int si = InventoryManager.Instance.selectedSlotIndex;
                                if (si < InventoryManager.Instance.hotbarSlots.Length &&
                                    InventoryManager.Instance.hotbarSlots[si] == this &&
                                    lanternRef != null)
                                {
                                    lanternRef.currentFuel = this.itemData.lanternFuel;
                                }
                            }

                            // Уменьшаем количество заправки
                            if (sourceSlot.itemData.isStackable && sourceSlot.itemData.amount > 1)
                            {
                                sourceSlot.itemData.amount--;
                                sourceSlot.UpdateSlotUI();
                            }
                            else
                            {
                                // Не-стаковый расходник: уменьшаем currentAmount, но НЕ удаляем слот
                                sourceSlot.itemData.currentAmount -= sourceSlot.itemData.amountPerUse;
                                if (sourceSlot.itemData.currentAmount < 0)
                                    sourceSlot.itemData.currentAmount = 0;
                                if (sourceSlot.itemData.fillIcons != null && sourceSlot.itemData.fillIcons.Length > 0)
                                {
                                    float pct = Mathf.Clamp01((float)sourceSlot.itemData.currentAmount / sourceSlot.itemData.maxAmount);
                                    int idx = Mathf.RoundToInt(pct * (sourceSlot.itemData.fillIcons.Length - 1));
                                    sourceSlot.itemData.itemIcon = sourceSlot.itemData.fillIcons[idx];
                                }
                                sourceSlot.UpdateSlotUI();
                                // Пустая бутылка остаётся в слоте — не вызываем ClearSlot()
                            }

                            InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
                            return; // Не делаем swap
                        }
                        // Если бутылка пустая — падаем к обычному swap ниже
                    }

                    // --- СЛИЯНИЕ СТАКОВ ПРИ ПЕРЕТАСКИВАНИИ ---
                    if (!this.isEmpty && 
                        this.itemData.isStackable && 
                        sourceSlot.itemData.isStackable && 
                        this.itemData.itemID == sourceSlot.itemData.itemID &&
                        !InventoryManager.IsUsedLightSource(this.itemData) &&
                        !InventoryManager.IsUsedLightSource(sourceSlot.itemData))
                    {
                        if (sourceSlot.isDraggingSingleItem)
                        {
                            // Перемещение ровно 1 предмета в существующий стак
                            if (this.itemData.amount < this.itemData.maxStackSize)
                            {
                                this.itemData.amount++;
                                sourceSlot.itemData.amount--;
                                this.UpdateSlotUI();
                                sourceSlot.UpdateSlotUI();
                            }
                        }
                        else
                        {
                            MergeStacks(sourceSlot);
                        }
                    }
                    else if (this.isEmpty && sourceSlot.isDraggingSingleItem)
                    {
                        // Перемещение ровно 1 предмета в пустой слот
                        InventoryItemData singleItem = sourceSlot.itemData.Clone();
                        singleItem.amount = 1;
                        if (singleItem.lanternFuel >= 0f)
                        {
                            sourceSlot.itemData.lanternFuel = -1f;
                        }
                        this.AddItem(singleItem);

                        sourceSlot.itemData.amount--;
                        sourceSlot.UpdateSlotUI();
                    }
                    else if (!sourceSlot.isDraggingSingleItem)
                    {
                        // Обычный Swap (только если тащим весь стак)
                        SwapItems(sourceSlot);
                    }
                }
            }
        }
    }

    // === НОВОЕ: Функция слияния ===
    private void MergeStacks(InventorySlot sourceSlot)
    {
        int spaceLeft = itemData.maxStackSize - itemData.amount;
        if (spaceLeft <= 0) return; // Этот слот уже забит до краев

        if (sourceSlot.itemData.amount <= spaceLeft)
        {
            // Если влазит полностью - переносим всё
            itemData.amount += sourceSlot.itemData.amount;
            sourceSlot.ClearSlot();
        }
        else
        {
            // Если влазит только часть - откусываем кусок
            itemData.amount += spaceLeft;
            sourceSlot.itemData.amount -= spaceLeft;
            sourceSlot.UpdateSlotUI();
        }
        
        UpdateSlotUI();
        
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
        }
    }

    private void SwapItems(InventorySlot sourceSlot)
    {
        InventoryItemData thisData = itemData;
        bool thisEmpty = isEmpty;

        if (sourceSlot.IsEmpty()) ClearSlot();
        else AddItem(sourceSlot.itemData);

        if (thisEmpty) sourceSlot.ClearSlot();
        else sourceSlot.AddItem(thisData);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
        }
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}