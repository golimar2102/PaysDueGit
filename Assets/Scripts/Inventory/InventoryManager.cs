using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro; 
using UnityEngine.Localization; 

[System.Serializable]
public class BackpackTier
{
    [Tooltip("Название тира для удобства в Inspector")]
    public string tierName;
    [Tooltip("ID предметов-рюкзаков этого тира")]
    public int[] itemIDs;
    [Tooltip("Сколько дополнительных слотов открывает этот рюкзак (помимо базовых)")]
    public int additionalSlots;
}

[System.Serializable]
public class WaistTier
{
    [Tooltip("Название тира ремня для удобства в Inspector")]
    public string tierName;
    [Tooltip("ID предметов-ремней этого тира")]
    public int[] itemIDs;
    [Tooltip("Сколько крючков (слотов) открывает этот ремень")]
    public int hookSlots;
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("UI Элементы")]
    public GameObject inventoryUI; 
    public TextMeshProUGUI equippedItemNameText; 
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipText;
    public TextMeshProUGUI coinsText;

    [Header("Слоты")]
    public InventorySlot[] hotbarSlots;
    public InventorySlot[] inventorySlots;

    [Header("Хотбар (Быстрый доступ)")]
    public int selectedSlotIndex = 0;
    public RectTransform hotbarPanel;
    public KeyCode toggleHotbarKey = KeyCode.N;
    public float slideSpeed = 8f;
    [Tooltip("Дополнительные элементы UI/HUD, скрываемые по клавише N вместе с хотбаром")]
    public GameObject[] extraHUDObjectsToHide;

    [Header("Настройки UI")]
    public float invPosY = 20f;
    public float invScale = 1f;
    public float hudPosY = 20f;
    public float hudScale = 0.7f;
    public float hiddenPosY = -150f;

    private bool isHotbarVisibleOnHUD = true;
    private Coroutine slideCoroutine;
    [HideInInspector] public float baseHotbarX;

    [Header("Настройки выброса предметов")]
    public GameObject mainCam;
    public float dropForce = 8f; 

    [Header("База предметов (ОБЯЗАТЕЛЬНО!)")]
    public List<GameObject> allItemsDatabase;

    [Header("Фонари")]
    [Tooltip("ID предметов-ламп в инвентаре (должно совпадать с ID в PickUpItem).")]
    public int[] lanternItemIDs;

    [Header("Рюкзак")]
    [Tooltip("Специальный слот инвентаря, предназначенный для рюкзака")]
    public InventorySlot backpackSlot;
    [Tooltip("Количество слотов без рюкзака")]
    public int baseInventorySlots = 6;
    [Tooltip("Тиры рюкзаков: каждый тир задаёт ID рюкзаков и количество доп. слотов")]
    public BackpackTier[] backpackTiers;

    [Header("Ремень (Пояс)")]
    [Tooltip("Специальный слот инвентаря, предназначенный для ремня")]
    public InventorySlot waistSlot;
    [Tooltip("Слоты-крючки на ремне (массив из WaistSlots)")]
    public InventorySlot[] waistSlots;
    [Tooltip("RectTransform контейнера WaistSlots для анимации выезда")]
    public RectTransform waistSlotsContainer;
    [Tooltip("Тиры ремней: каждый тир задаёт ID ремней и количество крючков")]
    public WaistTier[] waistTiers;
    [Tooltip("Скорость анимации выезда WaistSlots")]
    public float waistSlideSpeed = 10f;

    [Header("Контекстное меню")]
    [Tooltip("Ссылка на объект контекстного меню в инвентаре")]
    public InventoryContextMenu contextMenu;

    private Vector2 waistSlotsRestPos;

    [Header("Локализация")] 
    public LocalizedString locTimePrefix;

    [HideInInspector]
    public bool isOpen = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (inventoryUI != null) inventoryUI.SetActive(false);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);

        var sceneContextMenu = FindFirstObjectByType<InventoryContextMenu>(FindObjectsInactive.Include);
        if (sceneContextMenu != null)
        {
            contextMenu = sceneContextMenu;
        }
    }

    void Start()
    {
        SelectSlot(selectedSlotIndex);
        if (hotbarPanel != null)
        {
            baseHotbarX = hotbarPanel.anchoredPosition.x;
            hotbarPanel.localScale = new Vector3(hudScale, hudScale, 1f);
            hotbarPanel.anchoredPosition = new Vector2(baseHotbarX, hudPosY);
        }

        if (waistSlotsContainer != null)
        {
            waistSlotsRestPos = waistSlotsContainer.anchoredPosition;
            waistSlotsContainer.gameObject.SetActive(false);
        }

        RefreshInventorySlots();
        RefreshWaistSlots();
    }

    void Update()
    {
        if (TVChairController.activeChair != null) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive) return;
        if (PeepholeController.activePeephole != null) return;

        if (NPCCorpse.carriedCorpse != null) return;

        if (Input.GetKeyDown(KeyCode.Tab)) ToggleInventory();

        if (!isOpen)
        {
            HandleHotbarInput();

            if (Input.GetKeyDown(KeyCode.G)) DropEquippedItem();
            if (Input.GetKeyDown(toggleHotbarKey)) ToggleHotbarHUD();

            KeyCode useKey = (KeyCode)PlayerPrefs.GetInt("Key_Aim", (int)KeyCode.Mouse1);
            if (Input.GetKeyDown(useKey)) TryUseEquippedItem();
        }
        else
        {
            if (tooltipPanel != null && tooltipPanel.activeSelf)
            {
                Canvas parentCanvas = tooltipPanel.GetComponentInParent<Canvas>();
                RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
                Vector2 movePos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera, out movePos);
                tooltipPanel.GetComponent<RectTransform>().localPosition = movePos + new Vector2(20f, -20f);
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                if (InventorySlot.hoveredSlot != null && !InventorySlot.hoveredSlot.IsEmpty())
                {
                    DropItemFromSlot(InventorySlot.hoveredSlot);
                }
            }

            KeyCode fireKey = (KeyCode)PlayerPrefs.GetInt("Key_Fire", (int)KeyCode.Mouse0);
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(fireKey))
            {
                if (InventorySlot.hoveredSlot != null && !InventorySlot.hoveredSlot.IsEmpty())
                {
                    QuickTransferItem(InventorySlot.hoveredSlot);
                }
            }
        }
    }

    private void TryUseEquippedItem()
    {
        if (hotbarSlots == null || selectedSlotIndex < 0 || selectedSlotIndex >= hotbarSlots.Length) return;

        InventorySlot activeSlot = hotbarSlots[selectedSlotIndex];

        if (!activeSlot.IsEmpty() && activeSlot.itemData != null && activeSlot.itemData.isConsumable)
        {
            InventoryItemData data = activeSlot.itemData; 

            if (data.consumableType == ConsumableType.LiquidContainer) return;

            if (data.currentAmount <= 0)
            {
                return;
            }

            int amountToTake = Mathf.Min(data.amountPerUse, data.currentAmount);

            if (data.consumableType == ConsumableType.ShotgunAmmo)
            {
                WeaponController weapon = FindFirstObjectByType<WeaponController>(FindObjectsInactive.Include);
                if (weapon != null) weapon.AddAmmo(amountToTake);
                else return;

                data.currentAmount -= amountToTake;
            }
            else if (data.consumableType == ConsumableType.LampOil)
            {
                LanternController lantern = FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
                if (lantern != null) lantern.AddFuel(amountToTake);
                else return;

                data.currentAmount -= amountToTake;
                if (data.currentAmount <= 0 && InventoryContextMenu.Instance != null)
                {
                    InventoryContextMenu.Instance.PourOutLiquid(data);
                }
            }
            else if (data.consumableType == ConsumableType.LiquidContainer)
            {
                if (InventoryContextMenu.Instance != null)
                {
                    InventoryContextMenu.Instance.DrinkLiquid(data);
                }
                else
                {
                    data.currentAmount -= amountToTake;
                    if (data.currentAmount <= 0)
                    {
                        data.currentLiquidType = LiquidType.None;
                        data.itemName = data.baseItemName;
                    }
                }
            }

            if (data.fillIcons != null && data.fillIcons.Length > 0)
            {
                float fillPercentage = Mathf.Clamp01((float)data.currentAmount / data.maxAmount);
                int index = Mathf.RoundToInt(fillPercentage * (data.fillIcons.Length - 1));
                activeSlot.iconDisplay.sprite = data.fillIcons[index];
                data.itemIcon = data.fillIcons[index]; 
            }

            if (equippedItemNameText != null)
            {
                string itemName = data.itemName;
                if (data.isStackable && data.amount > 1)
                    itemName += $" (x{data.amount})";
                equippedItemNameText.text = itemName;
            }

            activeSlot.UpdateSlotUI();
        }
    }

    public void ShowTooltip(string itemName)
    {
        if (tooltipText == null || tooltipPanel == null) return;

        string textToShow = itemName;
        if (itemName.ToLower().Contains("часы") || itemName.ToLower().Contains("watch"))
        {
            if (DayNightCycle.Instance != null)
            {
                string timeStr = (locTimePrefix != null && !locTimePrefix.IsEmpty) ? locTimePrefix.GetLocalizedString() : "Время:";
                textToShow += $"\n<size=80%><color=#A0A0A0>{timeStr} {DayNightCycle.Instance.GetFormattedTime()}</color></size>";
            }
        }

        tooltipText.text = textToShow;
        tooltipPanel.SetActive(true);
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void UpdateCoinsUI()
    {
        if (coinsText != null && PlayerStats.Instance != null)
        {
            coinsText.text = PlayerStats.Instance.coins.ToString();
        }
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;
        if (inventoryUI != null) inventoryUI.SetActive(isOpen);

        if (contextMenu != null)
        {
            contextMenu.Hide();
        }

        if (isOpen)
        {
            UpdateCoinsUI();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (EquipmentManager.Instance != null) EquipmentManager.Instance.SetWeaponVisibility(false);
            if (hotbarPanel != null)
            {
                hotbarPanel.anchoredPosition = new Vector2(baseHotbarX, invPosY);
                hotbarPanel.localScale = new Vector3(invScale, invScale, 1f);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            InventorySlot.hoveredSlot = null;

            HideTooltip(); 
            if (EquipmentManager.Instance != null) EquipmentManager.Instance.SetWeaponVisibility(true);
            if (hotbarPanel != null)
            {
                float targetY = isHotbarVisibleOnHUD ? hudPosY : hiddenPosY;
                hotbarPanel.anchoredPosition = new Vector2(baseHotbarX, targetY);
                hotbarPanel.localScale = new Vector3(hudScale, hudScale, 1f);
            }

            if (extraHUDObjectsToHide != null)
            {
                foreach (var obj in extraHUDObjectsToHide)
                {
                    if (obj != null) obj.SetActive(isHotbarVisibleOnHUD);
                }
            }
        }
    }

    void HandleHotbarInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) 
        {
            selectedSlotIndex--;
            if (selectedSlotIndex < 0) selectedSlotIndex = hotbarSlots.Length - 1;
            SelectSlot(selectedSlotIndex);
        }
        else if (scroll < 0f) 
        {
            selectedSlotIndex++;
            if (selectedSlotIndex >= hotbarSlots.Length) selectedSlotIndex = 0;
            SelectSlot(selectedSlotIndex);
        }
    }

    private void ToggleHotbarHUD()
    {
        isHotbarVisibleOnHUD = !isHotbarVisibleOnHUD;
        float targetY = isHotbarVisibleOnHUD ? hudPosY : hiddenPosY;

        if (hotbarPanel != null)
        {
            hotbarPanel.anchoredPosition = new Vector2(baseHotbarX, targetY);
            hotbarPanel.localScale = new Vector3(hudScale, hudScale, 1f);
        }

        if (extraHUDObjectsToHide != null)
        {
            foreach (var obj in extraHUDObjectsToHide)
            {
                if (obj != null) obj.SetActive(isHotbarVisibleOnHUD);
            }
        }
    }

    public void HaltHotbarAnimation()
    {
        // Анимации больше нет
    }

    public void SelectSlot(int index)
    {
        if (hotbarSlots == null || hotbarSlots.Length == 0) return;
        if (index < 0 || index >= hotbarSlots.Length) return;

        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] != null)
            {
                hotbarSlots[i].isSelected = false; 
                if (hotbarSlots[i].highlightFrame != null) hotbarSlots[i].highlightFrame.SetActive(false);
            }
        }

        selectedSlotIndex = index;
        if (hotbarSlots[selectedSlotIndex] != null)
        {
            hotbarSlots[selectedSlotIndex].isSelected = true; 
            if (hotbarSlots[selectedSlotIndex].highlightFrame != null) hotbarSlots[selectedSlotIndex].highlightFrame.SetActive(true);
        }

        EquipItem(hotbarSlots[selectedSlotIndex]);
    }

    private void EquipItem(InventorySlot selectedSlot)
    {
        if (selectedSlot.IsEmpty())
        {
            if (EquipmentManager.Instance != null) EquipmentManager.Instance.UnequipAll();
            if (equippedItemNameText != null) equippedItemNameText.text = "";
            return;
        }

        int id = selectedSlot.currentItemID;
        string itemName = selectedSlot.currentItemName;

        if (selectedSlot.itemData != null && selectedSlot.itemData.isStackable && selectedSlot.itemData.amount > 1)
            itemName += $" (x{selectedSlot.itemData.amount})";

        if (EquipmentManager.Instance != null) EquipmentManager.Instance.EquipItem(id);
        if (equippedItemNameText != null) equippedItemNameText.text = itemName;

        if (IsLanternItem(id) && selectedSlot.itemData != null)
        {
            LanternController lantern = null;
            if (EquipmentManager.Instance != null)
            {
                foreach (var weapon in EquipmentManager.Instance.weapons)
                {
                    if (weapon.weaponObject != null && weapon.weaponObject.activeSelf)
                    {
                        lantern = weapon.weaponObject.GetComponentInChildren<LanternController>(true);
                        if (lantern != null) break;
                    }
                }
            }
            if (lantern == null)
            {
                lantern = FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
            }
            lantern?.SetActiveItemData(selectedSlot.itemData);
        }
    }

    public bool CanDropItems()
    {
        if (DoughRollingController.activeRollingBoard != null && DoughRollingController.activeRollingBoard.isViewing)
            return false;

        if (LocationTransitionController.activeTransition != null && LocationTransitionController.activeTransition.isViewing)
            return false;

        if (PeepholeController.activePeephole != null && PeepholeController.activePeephole.isViewing)
            return false;

        if (WorkbenchController.activeWorkbench != null && WorkbenchController.activeWorkbench.isViewing)
            return false;

        return true;
    }

    public void DropEquippedItem()
    {
        if (!CanDropItems()) return;
        if (hotbarSlots == null || selectedSlotIndex < 0 || selectedSlotIndex >= hotbarSlots.Length) return;
        DropItemFromSlot(hotbarSlots[selectedSlotIndex]);
    }

    private void DropItemFromSlot(InventorySlot slotToDrop)
    {
        if (!CanDropItems()) return;
        if (slotToDrop == null || slotToDrop.IsEmpty() || slotToDrop.itemData == null) return;

        int dropAmount = 1; 
        if (!slotToDrop.itemData.isStackable || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            dropAmount = slotToDrop.itemData.amount;
        }

        InventoryItemData dataToDrop = slotToDrop.itemData.Clone();
        dataToDrop.amount = dropAmount;
        if (dropAmount < slotToDrop.itemData.amount)
        {
            if (dataToDrop.lanternFuel >= 0f)
            {
                slotToDrop.itemData.lanternFuel = -1f;
            }
        }

        SpawnDroppedItem(dataToDrop);

        slotToDrop.itemData.amount -= dropAmount;

        if (slotToDrop.itemData.amount <= 0)
        {
            slotToDrop.ClearSlot();
            HideTooltip();

            if (hotbarSlots != null && selectedSlotIndex >= 0 && selectedSlotIndex < hotbarSlots.Length)
            {
                if (slotToDrop == hotbarSlots[selectedSlotIndex])
                {
                    SelectSlot(selectedSlotIndex);
                }
            }
        }
        else
        {
            slotToDrop.UpdateSlotUI(); 
            if (hotbarSlots != null && selectedSlotIndex >= 0 && selectedSlotIndex < hotbarSlots.Length)
            {
                if (slotToDrop == hotbarSlots[selectedSlotIndex]) EquipItem(slotToDrop);
            }
        }
    }

    public void SpawnDroppedItem(InventoryItemData data)
    {
        GameObject prefabToDrop = GetPrefabByID(data.itemID); 

        if (prefabToDrop != null && mainCam != null)
        {
            Vector3 spawnPos = mainCam.transform.position + mainCam.transform.forward * 1.2f;
            Vector3 dropEulerAngles = mainCam.transform.eulerAngles;
            dropEulerAngles.x = 0f; 
            dropEulerAngles.z = 0f; 
            dropEulerAngles.y += 90f; 

            GameObject spawnedItem = Instantiate(prefabToDrop, spawnPos, Quaternion.Euler(dropEulerAngles));

            // --- ИСПРАВЛЕНИЕ: Имя класса с большой буквы U ---
            PickUpItem pickupComponent = spawnedItem.GetComponent<PickUpItem>();
            if (pickupComponent == null) pickupComponent = spawnedItem.GetComponentInChildren<PickUpItem>();

            if (pickupComponent != null)
            {
                pickupComponent.RestoreData(data);
                pickupComponent.Toss(mainCam.transform.forward, dropForce);
            }
        }
        
    }

    private void QuickTransferItem(InventorySlot sourceSlot)
    {
        if (sourceSlot == null || sourceSlot.IsEmpty() || sourceSlot.itemData == null) return;

        if (StorageUI.Instance != null && StorageUI.Instance.IsOpen())
        {
            StorageUI.Instance.QuickTransfer(sourceSlot);
            return;
        }

        InventoryItemData dataToMove = sourceSlot.itemData;
        int amountLeft = dataToMove.amount;

        bool isFromHotbar = System.Array.IndexOf(hotbarSlots, sourceSlot) != -1;
        bool isFromInventory = System.Array.IndexOf(inventorySlots, sourceSlot) != -1;

        InventorySlot[] targetArray = isFromHotbar ? inventorySlots : hotbarSlots;

        if (dataToMove.isStackable)
        {
            amountLeft = AddToExistingStacks(dataToMove, amountLeft, targetArray);

            if (amountLeft > 0 && !isFromHotbar && !isFromInventory) 
            {
                amountLeft = AddToExistingStacks(dataToMove, amountLeft, inventorySlots);
            }
        }

        if (amountLeft > 0)
        {
            dataToMove.amount = amountLeft;
            amountLeft = MoveDataToFirstEmptySlot(dataToMove, targetArray);

            if (amountLeft > 0 && !isFromHotbar && !isFromInventory)
            {
                amountLeft = MoveDataToFirstEmptySlot(dataToMove, inventorySlots);
            }
        }

        if (amountLeft <= 0)
        {
            sourceSlot.ClearSlot();
            HideTooltip();
            if (isFromHotbar && sourceSlot == hotbarSlots[selectedSlotIndex]) SelectSlot(selectedSlotIndex);
        }
        else
        {
            sourceSlot.itemData.amount = amountLeft;
            sourceSlot.UpdateSlotUI();
        }
    }

    private int MoveDataToFirstEmptySlot(InventoryItemData data, InventorySlot[] targetArray)
    {
        if (targetArray == null) return data.amount;

        for (int i = 0; i < targetArray.Length; i++)
        {
            if (targetArray[i] == null) continue;

            if (targetArray[i].IsEmpty())
            {
                InventoryItemData clonedData = data.Clone(); 
                targetArray[i].AddItem(clonedData);

                if (targetArray == hotbarSlots && i == selectedSlotIndex)
                {
                    EquipItem(targetArray[i]);
                }
                return 0; 
            }
        }
        return data.amount; 
    }

    public bool AddItem(InventoryItemData data)
    {
        int leftover = AddItemWithLeftover(data);
        return leftover < data.amount; 
    }

    public int AddItemWithLeftover(InventoryItemData data)
    {
        if (data != null && data.itemID == 87)
        {
            int coinsToAdd = data.amount > 0 ? data.amount : 1;
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddCoins(coinsToAdd);
            }
            return 0;
        }

        int amountToAdd = data.amount;

        if (data.isStackable)
        {
            if (hotbarSlots != null) amountToAdd = AddToExistingStacks(data, amountToAdd, hotbarSlots);
            if (amountToAdd <= 0) return 0;

            if (inventorySlots != null) amountToAdd = AddToExistingStacks(data, amountToAdd, inventorySlots);
            if (amountToAdd <= 0) return 0;
        }

        if (amountToAdd > 0)
        {
            data.amount = amountToAdd; 

            if (hotbarSlots != null)
            {
                for (int i = 0; i < hotbarSlots.Length; i++)
                {
                    if (hotbarSlots[i] == null) continue;
                    if (hotbarSlots[i].IsEmpty())
                    {
                        hotbarSlots[i].AddItem(data.Clone());
                        if (i == selectedSlotIndex) EquipItem(hotbarSlots[i]);
                        return 0; 
                    }
                }
            }

            if (inventorySlots != null)
            {
                for (int i = 0; i < inventorySlots.Length; i++)
                {
                    if (inventorySlots[i] == null) continue;
                    if (inventorySlots[i].IsEmpty())
                    {
                        inventorySlots[i].AddItem(data.Clone());
                        return 0; 
                    }
                }
            }
        }

        return amountToAdd; 
    }

    private int AddToExistingStacks(InventoryItemData data, int amountToAdd, InventorySlot[] slots)
    {
        if (IsUsedLightSource(data)) return amountToAdd;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty() && slots[i].itemData != null &&
                slots[i].itemData.itemID == data.itemID && 
                slots[i].itemData.amount < slots[i].itemData.maxStackSize &&
                !IsUsedLightSource(slots[i].itemData))
            {
                int spaceLeft = slots[i].itemData.maxStackSize - slots[i].itemData.amount;
                if (amountToAdd <= spaceLeft)
                {
                    slots[i].itemData.amount += amountToAdd;
                    slots[i].UpdateSlotUI();

                    if (slots == hotbarSlots && i == selectedSlotIndex) EquipItem(slots[i]);
                    return 0; 
                }
                else
                {
                    slots[i].itemData.amount += spaceLeft;
                    slots[i].UpdateSlotUI();
                    amountToAdd -= spaceLeft; 
                }
            }
        }
        return amountToAdd;
    }

    public void ConsumeItemInActiveSlot()
    {
        if (hotbarSlots == null || selectedSlotIndex < 0 || selectedSlotIndex >= hotbarSlots.Length) return;
        InventorySlot activeSlot = hotbarSlots[selectedSlotIndex];

        if (activeSlot.IsEmpty()) return;

        if (activeSlot.itemData.isStackable && activeSlot.itemData.amount > 1)
        {
            activeSlot.itemData.amount--;
            activeSlot.UpdateSlotUI();
            EquipItem(activeSlot); 
        }
        else
        {
            activeSlot.ClearSlot();
            SelectSlot(selectedSlotIndex); 
        }
    }

    // =========================================================
    // =========================================================

    /// <summary>
    /// Вызывается при изменении предмета в waistSlot
    /// </summary>
    public void RefreshWaistSlots()
    {
        if (waistSlots == null) return;

        int totalHooks = 0;

        if (waistSlot != null && waistSlot.itemData != null)
        {
            int itemID = waistSlot.itemData.itemID;
            if (waistTiers != null)
            {
                foreach (WaistTier tier in waistTiers)
                {
                    if (tier.itemIDs == null) continue;
                    foreach (int id in tier.itemIDs)
                    {
                        if (id == itemID)
                        {
                            totalHooks = tier.hookSlots;
                            goto applyWaist;
                        }
                    }
                }
            }
        }

        applyWaist:
        bool beltEquipped = totalHooks > 0;

        // Анимируем контейнер WaistSlots
        if (waistSlotsContainer != null)
        {
            if (beltEquipped)
            {
                waistSlotsContainer.gameObject.SetActive(true);
                if (waistSlideCoroutine != null) StopCoroutine(waistSlideCoroutine);
                waistSlideCoroutine = StartCoroutine(AnimateWaistSlots(true));
            }
            else
            {
                if (waistSlideCoroutine != null) StopCoroutine(waistSlideCoroutine);
                waistSlideCoroutine = StartCoroutine(AnimateWaistSlots(false));
            }
        }

        for (int i = 0; i < waistSlots.Length; i++)
        {
            InventorySlot slot = waistSlots[i];
            if (slot == null || slot == waistSlot) continue;

            bool shouldBeLocked = i >= totalHooks;

            if (shouldBeLocked)
            {
                LockAndEvictSlot(slot);
            }
            else
            {
                slot.SetLockedKeepItems(false);
            }
        }
    }

    private Coroutine waistSlideCoroutine;

    /// <summary>
    /// Анимация выезда/скрытия контейнера WaistSlots и...
    /// </summary>
    private IEnumerator AnimateWaistSlots(bool slideIn)
    {
        if (waistSlotsContainer == null) yield break;

        waistSlotsContainer.anchoredPosition = waistSlotsRestPos;

        Vector3 startScale = slideIn ? new Vector3(0f, 1f, 1f) : waistSlotsContainer.localScale;
        Vector3 targetScale = slideIn ? new Vector3(1f, 1f, 1f) : new Vector3(0f, 1f, 1f);
        float t = 0f;

        if (slideIn)
            waistSlotsContainer.localScale = startScale;

        while (t < 1f)
        {
            t += Time.deltaTime * waistSlideSpeed;
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            waistSlotsContainer.localScale = Vector3.Lerp(startScale, targetScale, smooth);
            yield return null;
        }

        waistSlotsContainer.localScale = targetScale;

        if (!slideIn)
            waistSlotsContainer.gameObject.SetActive(false);
    }

    /// <summary>
    /// Вспомогательный метод: кладёт предмет в первый ...
    /// </summary>
    private int MoveDataToFirstFreeUnlockedSlot(InventoryItemData data, InventorySlot[] slots)
    {
        if (slots == null) return data.amount;
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.isSpecialSlot || slot.isLocked) continue;
            if (!slot.isEmpty) continue;
            slot.AddItem(data.Clone());
            if (slots == hotbarSlots && i == selectedSlotIndex) EquipItem(slot);
            return 0;
        }
        return data.amount;
    }

    // =========================================================
    // СИСТЕМА РЮКЗАКА (BACKPACK)
    // =========================================================

    /// <summary>
    /// Обновляет доступность слотов инвентаря в зависи...
    /// </summary>
    public void RefreshInventorySlots()
    {
        if (inventorySlots == null) return;

        int totalAvailable = baseInventorySlots;

        if (backpackSlot != null && backpackSlot.itemData != null)
        {
            int itemID = backpackSlot.itemData.itemID;
            if (backpackTiers != null)
            {
                foreach (BackpackTier tier in backpackTiers)
                {
                    if (tier.itemIDs == null) continue;
                    foreach (int id in tier.itemIDs)
                    {
                        if (id == itemID)
                        {
                            totalAvailable = baseInventorySlots + tier.additionalSlots;
                            goto applySlots;
                        }
                    }
                }
            }
        }

        applySlots:
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot == null || slot.isSpecialSlot) continue;

            bool shouldBeLocked = i >= totalAvailable;

            if (shouldBeLocked)
            {
                LockAndEvictSlot(slot);
            }
            else
            {
                slot.SetLockedKeepItems(false);
            }
        }
    }

    /// <summary>
    /// Пытается добавить предмет в существующие стаки ...
    /// </summary>
    private int TryMergeIntoAccessibleSlots(InventoryItemData data, int amount, int maxIndex)
    {
        if (IsUsedLightSource(data)) return amount;

        for (int i = 0; i < inventorySlots.Length && i < maxIndex; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot == null || slot.isSpecialSlot || slot.isEmpty || slot.itemData == null) continue;
            if (slot.itemData.itemID != data.itemID) continue;
            if (IsUsedLightSource(slot.itemData)) continue;

            int space = slot.itemData.maxStackSize - slot.itemData.amount;
            if (space <= 0) continue;

            int transfer = Mathf.Min(amount, space);
            slot.itemData.amount += transfer;
            slot.UpdateSlotUI();
            amount -= transfer;

            if (amount <= 0) return 0;
        }
        return amount;
    }

    /// <summary>
    /// Пытается положить предмет в первый свободный сл...
    /// </summary>
    private int TryMoveToFirstFreeAccessibleSlot(InventoryItemData data, int maxIndex)
    {
        for (int i = 0; i < inventorySlots.Length && i < maxIndex; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot == null || slot.isSpecialSlot) continue;
            if (!slot.isEmpty || slot.isLocked) continue;

            slot.AddItem(data.Clone());
            return 0;
        }
        return data.amount;
    }

    /// <summary>
    /// Блокирует слот и безопасно выталкивает из него ...
    /// </summary>
    public void LockAndEvictSlot(InventorySlot slotToLock)
    {
        if (slotToLock == null) return;

        bool wasLocked = slotToLock.isLocked;
        bool hasItemToEvict = !slotToLock.isEmpty && slotToLock.itemData != null;

        slotToLock.SetLockedKeepItems(true);

        if (!wasLocked && hasItemToEvict)
        {
            InventoryItemData dataToMove = slotToLock.itemData.Clone();
            slotToLock.ClearSlot();

            int leftover = dataToMove.amount;

            // Пробуем долить в инвентарь
            if (dataToMove.isStackable)
            {
                leftover = AddToExistingStacks(dataToMove, leftover, hotbarSlots);
                if (leftover > 0) leftover = AddToExistingStacks(dataToMove, leftover, inventorySlots);
            }

            // Ищем свободный слот
            if (leftover > 0)
            {
                dataToMove.amount = leftover;
                leftover = MoveDataToFirstFreeUnlockedSlot(dataToMove, hotbarSlots);
            }
            if (leftover > 0)
            {
                dataToMove.amount = leftover;
                leftover = MoveDataToFirstFreeUnlockedSlot(dataToMove, inventorySlots);
            }

            // Дропаем, если мест нет
            if (leftover > 0)
            {
                dataToMove.amount = leftover;
                SpawnDroppedItem(dataToMove);
            }
        }
    }

    /// <summary>
    /// Обновляет связанный слот (разблокирует, если ес...
    /// </summary>
    public void RefreshLinkedSlot(InventorySlot sourceSlot)
    {
        if (sourceSlot == null || sourceSlot.linkedUnlockSlot == null) return;

        bool shouldUnlock = !sourceSlot.isEmpty && !sourceSlot.isLocked;

        if (shouldUnlock)
            sourceSlot.linkedUnlockSlot.SetLockedKeepItems(false);
        else
            LockAndEvictSlot(sourceSlot.linkedUnlockSlot);
    }

    /// <summary>
    /// Проверяет, является ли предмет с данным ID фонарём.
    /// </summary>
    public bool IsLanternItem(int id)
    {
        if (lanternItemIDs == null || lanternItemIDs.Length == 0) return false;
        foreach (int lanternId in lanternItemIDs)
        {
            if (id == lanternId) return true;
        }
        return false;
    }

    /// <summary>
    /// Проверяет, является ли источник света (фонарь, ...
    /// </summary>
    public static bool IsUsedLightSource(InventoryItemData item)
    {
        if (item == null) return false;
        if (item.lanternFuel < 0f) return false;

        float maxFuel = 100f;
        LanternController lanternRef = FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
        if (lanternRef != null) maxFuel = lanternRef.maxFuel;

        return item.lanternFuel < (maxFuel - 0.5f);
    }

    /// <summary>
    /// Проверяет, могут ли указанные предметы поместит...
    /// </summary>
    public bool CanFitItems(List<DispenseItemConfig> items)
    {
        Dictionary<int, List<int>> slotCapacities = new Dictionary<int, List<int>>();
        int emptySlots = 0;

        void AnalyzeSlots(InventorySlot[] slots)
        {
            if (slots == null) return;
            foreach (var slot in slots)
            {
                if (slot == null || slot.isLocked) continue;
                if (slot.IsEmpty())
                {
                    emptySlots++;
                }
                else if (slot.itemData != null && !IsUsedLightSource(slot.itemData))
                {
                    int id = slot.itemData.itemID;
                    int space = slot.itemData.maxStackSize - slot.itemData.amount;
                    if (space > 0)
                    {
                        if (!slotCapacities.ContainsKey(id))
                        {
                            slotCapacities[id] = new List<int>();
                        }
                        slotCapacities[id].Add(space);
                    }
                }
            }
        }

        AnalyzeSlots(hotbarSlots);
        AnalyzeSlots(inventorySlots);

        foreach (var item in items)
        {
            GameObject prefab = GetPrefabByID(item.itemID);
            if (prefab == null) continue;

            PickUpItem pickup = prefab.GetComponent<PickUpItem>();
            if (pickup == null)
            {
                pickup = prefab.GetComponentInChildren<PickUpItem>();
            }
            if (pickup == null) continue;

            int remaining = item.amount;
            if (pickup.isStackable)
            {
                if (slotCapacities.TryGetValue(item.itemID, out var spaces))
                {
                    for (int i = 0; i < spaces.Count; i++)
                    {
                        int space = spaces[i];
                        int taken = Mathf.Min(remaining, space);
                        remaining -= taken;
                        spaces[i] -= taken;
                        if (remaining <= 0) break;
                    }
                }

                if (remaining > 0)
                {
                    int slotsNeeded = Mathf.CeilToInt((float)remaining / pickup.maxStackSize);
                    if (emptySlots >= slotsNeeded)
                    {
                        emptySlots -= slotsNeeded;
                        int leftoverInNewSlot = (slotsNeeded * pickup.maxStackSize) - remaining;
                        if (leftoverInNewSlot > 0)
                        {
                            if (!slotCapacities.ContainsKey(item.itemID))
                            {
                                slotCapacities[item.itemID] = new List<int>();
                            }
                            slotCapacities[item.itemID].Add(leftoverInNewSlot);
                        }
                        remaining = 0;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (emptySlots >= remaining)
                {
                    emptySlots -= remaining;
                    remaining = 0;
                }
                else
                {
                    return false;
                }
            }
        }

        return true;
    }

    public GameObject GetPrefabByID(int id)
    {
        if (allItemsDatabase == null) return null;

        foreach (GameObject obj in allItemsDatabase)
        {
            if (obj != null)
            {
                PickUpItem p = obj.GetComponent<PickUpItem>();
                if (p != null && p.itemID == id) return obj;
            }
        }
        return null;
    }

    public bool HasItem(int itemID)
    {
        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                if (slot != null && !slot.isEmpty && slot.itemData != null && slot.itemData.itemID == itemID)
                    return true;
            }
        }
        if (inventorySlots != null)
        {
            foreach (var slot in inventorySlots)
            {
                if (slot != null && !slot.isEmpty && slot.itemData != null && slot.itemData.itemID == itemID)
                    return true;
            }
        }
        if (waistSlots != null)
        {
            foreach (var slot in waistSlots)
            {
                if (slot != null && !slot.isEmpty && slot.itemData != null && slot.itemData.itemID == itemID)
                    return true;
            }
        }
        if (backpackSlot != null && !backpackSlot.isEmpty && backpackSlot.itemData != null && backpackSlot.itemData.itemID == itemID)
            return true;
        if (waistSlot != null && !waistSlot.isEmpty && waistSlot.itemData != null && waistSlot.itemData.itemID == itemID)
            return true;

        return false;
    }

    public int GetItemCount(int itemID)
    {
        int count = 0;
        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                if (slot != null && !slot.isEmpty && slot.itemData != null && slot.itemData.itemID == itemID)
                    count += slot.itemData.amount;
            }
        }
        if (inventorySlots != null)
        {
            foreach (var slot in inventorySlots)
            {
                if (slot != null && !slot.isEmpty && slot.itemData != null && slot.itemData.itemID == itemID)
                    count += slot.itemData.amount;
            }
        }
        if (waistSlots != null)
        {
            foreach (var slot in waistSlots)
            {
                if (slot != null && !slot.isEmpty && slot.itemData != null && slot.itemData.itemID == itemID)
                    count += slot.itemData.amount;
            }
        }
        return count;
    }

    public bool RemoveItems(int itemID, int amountToRemove)
    {
        if (GetItemCount(itemID) < amountToRemove)
            return false;

        int remaining = amountToRemove;

        void DeductFromSlots(InventorySlot[] slots)
        {
            if (slots == null || remaining <= 0) return;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && !slot.isEmpty && slot.itemData != null && slot.itemData.itemID == itemID)
                {
                    int slotAmount = slot.itemData.amount;
                    if (slotAmount <= remaining)
                    {
                        remaining -= slotAmount;
                        slot.ClearSlot();
                    }
                    else
                    {
                        slot.itemData.amount -= remaining;
                        remaining = 0;
                        slot.UpdateSlotUI();
                        break;
                    }
                }
            }
        }

        DeductFromSlots(hotbarSlots);
        DeductFromSlots(inventorySlots);
        DeductFromSlots(waistSlots);

        SelectSlot(selectedSlotIndex);

        return remaining == 0;
    }

    public int GetItemCountWithLiquid(int itemID, bool requiresLiquid, LiquidType liquidType)
    {
        int count = 0;

        System.Action<InventorySlot> checkSlot = (slot) =>
        {
            if (slot != null && !slot.IsEmpty() && slot.itemData != null)
            {
                if (requiresLiquid)
                {
                    if (slot.itemData.currentLiquidType == liquidType && (slot.itemData.currentAmount > 0 || slot.itemData.consumableType == ConsumableType.LiquidContainer || slot.itemData.consumableType == ConsumableType.LampOil))
                    {
                        count += slot.itemData.amount;
                    }
                }
                else if (slot.itemData.itemID == itemID)
                {
                    count += slot.itemData.amount;
                }
            }
        };

        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots) checkSlot(slot);
        }
        if (inventorySlots != null)
        {
            foreach (var slot in inventorySlots) checkSlot(slot);
        }
        if (waistSlots != null)
        {
            foreach (var slot in waistSlots) checkSlot(slot);
        }

        return count;
    }

    public int GetTotalLiquidAmount(LiquidType liquidType)
    {
        if (liquidType == LiquidType.None) return 0;
        int total = 0;

        System.Action<InventorySlot> checkSlot = (slot) =>
        {
            if (slot != null && !slot.IsEmpty() && slot.itemData != null)
            {
                if (slot.itemData.currentLiquidType == liquidType && slot.itemData.currentAmount > 0)
                {
                    total += slot.itemData.currentAmount * Mathf.Max(1, slot.itemData.amount);
                }
            }
        };

        if (hotbarSlots != null) foreach (var slot in hotbarSlots) checkSlot(slot);
        if (inventorySlots != null) foreach (var slot in inventorySlots) checkSlot(slot);
        if (waistSlots != null) foreach (var slot in waistSlots) checkSlot(slot);

        return total;
    }

    public bool DeductLiquid(LiquidType liquidType, int amountToDeduct)
    {
        if (liquidType == LiquidType.None || amountToDeduct <= 0) return true;
        if (GetTotalLiquidAmount(liquidType) < amountToDeduct) return false;

        int remainingToDeduct = amountToDeduct;

        System.Func<InventorySlot, bool> processSlot = (slot) =>
        {
            if (slot != null && !slot.IsEmpty() && slot.itemData != null)
            {
                if (slot.itemData.currentLiquidType == liquidType && slot.itemData.currentAmount > 0)
                {
                    int availableInThisContainer = slot.itemData.currentAmount;
                    int take = Mathf.Min(remainingToDeduct, availableInThisContainer);

                    slot.itemData.currentAmount -= take;
                    remainingToDeduct -= take;

                    if (slot.itemData.currentAmount <= 0)
                    {
                        EmptyLiquidFromItem(slot.itemData);
                    }
                    else
                    {
                        UpdateLiquidItemDataVisuals(slot.itemData);
                    }

                    slot.UpdateSlotUI();

                    if (remainingToDeduct <= 0) return true;
                }
            }
            return false;
        };

        if (inventorySlots != null) foreach (var slot in inventorySlots) { if (processSlot(slot)) break; }
        if (remainingToDeduct > 0 && hotbarSlots != null) foreach (var slot in hotbarSlots) { if (processSlot(slot)) break; }
        if (remainingToDeduct > 0 && waistSlots != null) foreach (var slot in waistSlots) { if (processSlot(slot)) break; }

        SelectSlot(selectedSlotIndex);
        return remainingToDeduct <= 0;
    }

    public void UpdateLiquidItemDataVisuals(InventoryItemData data)
    {
        if (data == null || !data.isConsumable) return;
        if (data.fillIcons != null && data.fillIcons.Length > 0 && data.maxAmount > 0)
        {
            float fillPercentage = Mathf.Clamp01((float)data.currentAmount / data.maxAmount);
            int iconIndex = Mathf.RoundToInt(fillPercentage * (data.fillIcons.Length - 1));
            data.itemIcon = data.fillIcons[iconIndex];
        }
    }

    public void EmptyLiquidFromItem(InventoryItemData data)
    {
        if (InventoryContextMenu.Instance != null)
        {
            InventoryContextMenu.Instance.PourOutLiquid(data);
        }
        else
        {
            data.currentLiquidType = LiquidType.None;
            data.currentAmount = 0;
            data.itemName = data.baseItemName;
            GameObject prefab = GetPrefabByID(data.itemID);
            if (prefab != null)
            {
                ConsumableItem cons = prefab.GetComponent<ConsumableItem>();
                if (cons != null)
                {
                    Sprite[] emptyIcons = cons.GetFillIconsForLiquid(LiquidType.None);
                    if (emptyIcons != null && emptyIcons.Length > 0)
                    {
                        data.itemIcon = emptyIcons[0];
                        data.fillIcons = emptyIcons;
                    }
                    else
                    {
                        data.itemIcon = cons.fillIcons != null && cons.fillIcons.Length > 0 ? cons.fillIcons[0] : prefab.GetComponent<PickUpItem>().itemIcon;
                        data.fillIcons = cons.fillIcons;
                    }
                }
            }
        }
    }

    public void RemoveItemsWithLiquid(int itemID, int amountToRemove, bool requiresLiquid, LiquidType liquidType)
    {
        int remainingToRemove = amountToRemove;

        System.Func<InventorySlot, bool> processSlot = (slot) =>
        {
            if (slot != null && !slot.IsEmpty() && slot.itemData != null)
            {
                bool matches = requiresLiquid ? (slot.itemData.currentLiquidType == liquidType && (slot.itemData.currentAmount > 0 || slot.itemData.consumableType == ConsumableType.LiquidContainer || slot.itemData.consumableType == ConsumableType.LampOil)) : (slot.itemData.itemID == itemID);
                if (matches)
                {
                    if (requiresLiquid)
                    {
                        int containersToEmpty = Mathf.Min(remainingToRemove, slot.itemData.amount);

                        if (slot.itemData.amount == 1)
                        {
                            EmptyLiquidFromItem(slot.itemData);
                            slot.UpdateSlotUI();
                            remainingToRemove--;
                        }
                        else
                        {
                            slot.itemData.amount -= containersToEmpty;
                            slot.UpdateSlotUI();

                            for (int i = 0; i < containersToEmpty; i++)
                            {
                                GameObject emptyPrefab = GetPrefabByID(slot.itemData.itemID);
                                if (emptyPrefab != null)
                                {
                                    PickUpItem p = emptyPrefab.GetComponent<PickUpItem>();
                                    if (p == null) p = emptyPrefab.GetComponentInChildren<PickUpItem>();
                                    if (p != null)
                                    {
                                        InventoryItemData emptyData = new InventoryItemData(p);
                                        emptyData.amount = 1;
                                        EmptyLiquidFromItem(emptyData);

                                        int leftover = AddItemWithLeftover(emptyData);
                                        if (leftover > 0)
                                        {
                                            InventoryItemData leftoverData = emptyData.Clone();
                                            leftoverData.amount = leftover;
                                            SpawnDroppedItem(leftoverData);
                                        }
                                    }
                                }
                            }
                            remainingToRemove -= containersToEmpty;
                        }
                    }
                    else
                    {
                        if (slot.itemData.amount >= remainingToRemove)
                        {
                            slot.itemData.amount -= remainingToRemove;
                            remainingToRemove = 0;
                        }
                        else
                        {
                            remainingToRemove -= slot.itemData.amount;
                            slot.itemData.amount = 0;
                        }

                        if (slot.itemData.amount <= 0)
                        {
                            slot.ClearSlot();
                        }
                        else
                        {
                            slot.UpdateSlotUI();
                        }
                    }

                    if (remainingToRemove <= 0) return true;
                }
            }
            return false;
        };

        if (inventorySlots != null)
        {
            foreach (var slot in inventorySlots)
            {
                if (processSlot(slot)) break;
            }
        }
        if (remainingToRemove > 0 && hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                if (processSlot(slot)) break;
            }
        }
        if (remainingToRemove > 0 && waistSlots != null)
        {
            foreach (var slot in waistSlots)
            {
                if (processSlot(slot)) break;
            }
        }

        SelectSlot(selectedSlotIndex);
    }
}