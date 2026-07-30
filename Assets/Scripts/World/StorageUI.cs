using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class StorageUI : MonoBehaviour
{
    public static StorageUI Instance;

    [Header("UI Компоненты")]
    [Tooltip("Главная панель интерфейса хранилища (которая включается/выключается)")]
    public GameObject containerPanel;
    [Tooltip("Grid Layout Parent для спавна слотов")]
    public Transform slotsParent;
    [Tooltip("Префаб ячейки инвентаря (InventorySlot)")]
    public GameObject slotPrefab;
    [Tooltip("Текст названия открытого хранилища")]
    public TextMeshProUGUI titleText;

    [HideInInspector]
    public StorageContainer activeContainer;

    private List<InventorySlot> spawnedSlots = new List<InventorySlot>();
    private bool isOpen = false;

    private RectTransform inventoryRect;
    private Vector2 originalInventoryPos;
    private Vector3 originalInventoryScale;

    private RectTransform hotbarRect;
    private Vector2 originalHotbarPos;
    private Vector3 originalHotbarScale;

    private bool adjustedLayout = false;

    [System.Serializable]
    public struct HiddenObjectState
    {
        public GameObject obj;
        public bool originalActiveState;
    }
    private List<HiddenObjectState> currentlyHiddenObjects = new List<HiddenObjectState>();

    private PlayerMovement cachedPlayerMovement;
    private MouseLook cachedMouseLook;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (containerPanel != null) containerPanel.SetActive(false);
    }

    void Update()
    {
        if (isOpen)
        {
            // 1. Быстрое закрытие на клавиши Esc, Tab или клавишу взаимодействия
            KeyCode interactKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(interactKey))
            {
                Close();
                return;
            }

            // 2. Резервный чек закрытия инвентаря
            if (InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
            {
                Close();
                return;
            }

            // 3. Отслеживание изменений предметов для мгновенной синхронизации 3D моделей
            if (activeContainer != null)
            {
                bool needsSync = false;
                for (int i = 0; i < spawnedSlots.Count; i++)
                {
                    if (i >= activeContainer.storedItems.Count) break;

                    InventoryItemData uiItem = spawnedSlots[i].itemData;
                    InventoryItemData storedItem = activeContainer.storedItems[i];

                    if (spawnedSlots[i].IsEmpty() != (storedItem == null))
                    {
                        needsSync = true;
                        break;
                    }

                    if (!spawnedSlots[i].IsEmpty() && storedItem != null)
                    {
                        if (uiItem.itemID != storedItem.itemID || uiItem.amount != storedItem.amount)
                        {
                            needsSync = true;
                            break;
                        }
                    }
                }

                if (needsSync)
                {
                    SaveActiveContainerData();
                }
            }
        }
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public void Open(StorageContainer container)
    {
        if (container == null) return;

        activeContainer = container;
        isOpen = true;

        if (containerPanel != null) containerPanel.SetActive(true);
        if (titleText != null)
        {
            titleText.text = (container.localizedContainerName != null && !container.localizedContainerName.IsEmpty)
                ? container.localizedContainerName.GetLocalizedString()
                : "Хранилище";
        }

        // Чистим старые слоты
        ClearSpawnedSlots();

        // Спавним новые слоты под размер хранилища
        if (slotPrefab != null && slotsParent != null)
        {
            for (int i = 0; i < container.slotCount; i++)
            {
                GameObject slotObj = Instantiate(slotPrefab, slotsParent);
                InventorySlot slot = slotObj.GetComponent<InventorySlot>();
                if (slot == null) slot = slotObj.GetComponentInChildren<InventorySlot>();

                if (slot != null)
                {
                    slot.isLocked = false;
                    slot.isSpecialSlot = false;

                    // Заполняем данными
                    if (i < container.storedItems.Count && container.storedItems[i] != null && container.storedItems[i].amount > 0)
                    {
                        slot.AddItem(container.storedItems[i]);
                    }
                    else
                    {
                        slot.ClearSlot();
                    }

                    spawnedSlots.Add(slot);
                }
            }
        }

        // Отключаем передвижение и обзор игрока
        cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
        if (cachedPlayerMovement != null) cachedPlayerMovement.enabled = false;

        cachedMouseLook = FindFirstObjectByType<MouseLook>();
        if (cachedMouseLook != null) cachedMouseLook.enabled = false;

        // Открываем инвентарь игрока, чтобы он мог перетаскивать вещи
        if (InventoryManager.Instance != null)
        {
            if (!InventoryManager.Instance.isOpen)
            {
                InventoryManager.Instance.ToggleInventory();
            }

            InventoryManager.Instance.HaltHotbarAnimation();

            // Если у контейнера включена кастомизация, применяем её
            if (container.customizeInventoryLayout)
            {
                if (InventoryManager.Instance.inventoryUI != null)
                {
                    inventoryRect = InventoryManager.Instance.inventoryUI.GetComponent<RectTransform>();
                    if (inventoryRect != null)
                    {
                        originalInventoryPos = inventoryRect.anchoredPosition;
                        originalInventoryScale = inventoryRect.localScale;

                        inventoryRect.anchoredPosition += container.inventoryOffsetPosition;
                        inventoryRect.localScale = new Vector3(
                            originalInventoryScale.x * container.inventoryScaleMultiplier.x,
                            originalInventoryScale.y * container.inventoryScaleMultiplier.y,
                            originalInventoryScale.z * container.inventoryScaleMultiplier.z
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

                        hotbarRect.anchoredPosition += container.hotbarOffsetPosition;
                        hotbarRect.localScale = new Vector3(
                            originalHotbarScale.x * container.hotbarScaleMultiplier.x,
                            originalHotbarScale.y * container.hotbarScaleMultiplier.y,
                            originalHotbarScale.z * container.hotbarScaleMultiplier.z
                        );
                    }
                }
                adjustedLayout = true;
            }

            // Скрываем указанные элементы HUD (независимо от customizeInventoryLayout!)
            if (container.objectsToHide != null && container.objectsToHide.Length > 0)
            {
                currentlyHiddenObjects.Clear();
                foreach (var obj in container.objectsToHide)
                {
                    if (obj != null)
                    {
                        GameObject targetObj = FindSceneInstance(obj);
                        if (targetObj != null)
                        {
                            currentlyHiddenObjects.Add(new HiddenObjectState
                            {
                                obj = targetObj,
                                originalActiveState = targetObj.activeSelf
                            });
                            targetObj.SetActive(false);
                        }
                    }
                }
            }
        }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        SaveActiveContainerData();

        // Закрываем физически контейнер
        if (activeContainer != null)
        {
            activeContainer.Close();
            activeContainer = null;
        }

        if (containerPanel != null) containerPanel.SetActive(false);

        ClearSpawnedSlots();

        // Восстанавливаем разметку инвентаря
        if (adjustedLayout)
        {
            if (inventoryRect != null)
            {
                inventoryRect.anchoredPosition = originalInventoryPos;
                inventoryRect.localScale = originalInventoryScale;
            }

            if (hotbarRect != null)
            {
                if (InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
                {
                    // Если инвентарь уже закрыт, возвращаем хотбар на HUD позицию
                    hotbarRect.anchoredPosition = new Vector2(InventoryManager.Instance.baseHotbarX, InventoryManager.Instance.hudPosY);
                    hotbarRect.localScale = new Vector3(InventoryManager.Instance.hudScale, InventoryManager.Instance.hudScale, 1f);
                }
                else
                {
                    // Иначе возвращаем к исходной открытой позиции
                    hotbarRect.anchoredPosition = originalHotbarPos;
                    hotbarRect.localScale = originalHotbarScale;
                }
            }
            adjustedLayout = false;
        }

        // Восстанавливаем скрытые объекты HUD (независимо от adjustedLayout!)
        if (currentlyHiddenObjects != null && currentlyHiddenObjects.Count > 0)
        {
            foreach (var state in currentlyHiddenObjects)
            {
                if (state.obj != null)
                {
                    state.obj.SetActive(state.originalActiveState);
                }
            }
            currentlyHiddenObjects.Clear();
        }

        // Включаем обратно управление и обзор игрока
        PlayerMovement pm = cachedPlayerMovement != null ? cachedPlayerMovement : FindFirstObjectByType<PlayerMovement>();
        if (pm != null) pm.enabled = true;
        cachedPlayerMovement = null;

        MouseLook ml = cachedMouseLook != null ? cachedMouseLook : FindFirstObjectByType<MouseLook>();
        if (ml != null) ml.enabled = true;
        cachedMouseLook = null;

        // Закрываем инвентарь игрока, если он все еще открыт
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            InventoryManager.Instance.ToggleInventory();
        }
    }

    public void SaveActiveContainerData()
    {
        if (activeContainer == null) return;

        List<InventoryItemData> currentItems = new List<InventoryItemData>();
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null && !spawnedSlots[i].IsEmpty() && spawnedSlots[i].itemData != null)
            {
                currentItems.Add(spawnedSlots[i].itemData);
            }
            else
            {
                currentItems.Add(null);
            }
        }

        // Передаем данные обратно в хранилище и запускаем синхронизацию 3D моделей
        activeContainer.UpdateStoredItems(currentItems);
    }

    private void ClearSpawnedSlots()
    {
        foreach (var slot in spawnedSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        spawnedSlots.Clear();
    }

    // Метод быстрого перемещения предметов по Shift-клику
    public void QuickTransfer(InventorySlot sourceSlot)
    {
        if (sourceSlot == null || sourceSlot.IsEmpty() || sourceSlot.itemData == null) return;

        bool isFromContainer = spawnedSlots.Contains(sourceSlot);

        if (isFromContainer)
        {
            // Перемещаем в инвентарь игрока
            int leftover = InventoryManager.Instance.AddItemWithLeftover(sourceSlot.itemData);
            if (leftover <= 0)
            {
                sourceSlot.ClearSlot();
            }
            else
            {
                sourceSlot.itemData.amount = leftover;
                sourceSlot.UpdateSlotUI();
            }
        }
        else
        {
            // Перемещаем в хранилище
            int leftover = AddItemToContainerSlots(sourceSlot.itemData);
            if (leftover <= 0)
            {
                sourceSlot.ClearSlot();
            }
            else
            {
                sourceSlot.itemData.amount = leftover;
                sourceSlot.UpdateSlotUI();
            }
        }

        // После быстрой передачи сохраняем данные и обновляем 3D модели на сцене
        SaveActiveContainerData();
    }

    private int AddItemToContainerSlots(InventoryItemData data)
    {
        int amountToAdd = data.amount;

        // 1. Пытаемся сложить в существующие стаки
        if (data.isStackable)
        {
            for (int i = 0; i < spawnedSlots.Count; i++)
            {
                InventorySlot slot = spawnedSlots[i];
                if (slot != null && !slot.IsEmpty() && slot.itemData != null &&
                    slot.itemData.itemID == data.itemID &&
                    slot.itemData.amount < slot.itemData.maxStackSize &&
                    !InventoryManager.IsUsedLightSource(slot.itemData) &&
                    !InventoryManager.IsUsedLightSource(data))
                {
                    int spaceLeft = slot.itemData.maxStackSize - slot.itemData.amount;
                    if (amountToAdd <= spaceLeft)
                    {
                        slot.itemData.amount += amountToAdd;
                        slot.UpdateSlotUI();
                        return 0;
                    }
                    else
                    {
                        slot.itemData.amount += spaceLeft;
                        slot.UpdateSlotUI();
                        amountToAdd -= spaceLeft;
                    }
                }
            }
        }

        // 2. Пытаемся положить в первую пустую ячейку хранилища
        if (amountToAdd > 0)
        {
            for (int i = 0; i < spawnedSlots.Count; i++)
            {
                InventorySlot slot = spawnedSlots[i];
                if (slot != null && slot.IsEmpty())
                {
                    InventoryItemData clone = data.Clone();
                    clone.amount = amountToAdd;
                    slot.AddItem(clone);
                    return 0;
                }
            }
        }

        return amountToAdd;
    }

    private GameObject FindSceneInstance(GameObject sourceObj)
    {
        if (sourceObj == null) return null;
        
        // Если объект принадлежит сцене, это уже инстанс сцены
        if (sourceObj.scene.IsValid())
        {
            return sourceObj;
        }

        // Если это префаб ассета (нет валидной сцены), ищем его по имени на сцене
        string nameToFind = sourceObj.name;
        
        // Сначала пробуем найти по точному имени среди активных
        GameObject activeObj = GameObject.Find(nameToFind);
        if (activeObj != null) return activeObj;

        // Если не нашли среди активных, ищем среди всех (включая неактивные)
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj.name == nameToFind && obj.scene.IsValid())
            {
                return obj;
            }
        }

        return null;
    }
}
