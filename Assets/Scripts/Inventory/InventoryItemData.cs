using UnityEngine;

[System.Serializable]
public class InventoryItemData
{
    public int itemID;
    public string itemName;
    public Sprite itemIcon;
    public ItemCategory category = ItemCategory.None;

    // --- ДАННЫЕ СТАКОВ ---
    public bool isStackable;
    public int amount;
    public int maxStackSize;

    // --- ДАННЫЕ РАСХОДНИКОВ (Масло, патроны) ---
    public bool isConsumable;
    public ConsumableType consumableType;
    public int currentAmount;
    public int maxAmount;
    public int amountPerUse;
    public Sprite[] fillIcons;
    public LiquidType currentLiquidType = LiquidType.None;
    public string baseItemName;

    // --- ДАННЫЕ ФОНАРЯ (топливо хранится отдельно для каждой лампы в инвентаре) ---
    public float lanternFuel = -1f; // -1 = не инициализировано (возьмём из LanternController при первой экипировке)

    // Конструктор: "фотографирует" физический предмет
    public InventoryItemData(PickUpItem item)
    {
        itemID = item.itemID;
        itemName = item.itemName;
        category = item.category;
        baseItemName = item.localizedItemName.IsEmpty ? "Неизвестно" : item.localizedItemName.GetLocalizedString();
        itemIcon = item.itemIcon;

        // Забираем данные о стаках
        isStackable = item.isStackable;
        amount = item.amount;
        maxStackSize = item.maxStackSize;

        // Проверяем, есть ли на предмете топливо/патроны
        ConsumableItem cons = item.GetComponent<ConsumableItem>();
        if (cons != null)
        {
            isConsumable = true;
            consumableType = cons.type;
            currentAmount = cons.currentAmount;
            maxAmount = cons.maxAmount;
            amountPerUse = cons.amountPerUse;
            currentLiquidType = cons.currentLiquidType;

            // Если это масло для ламп и тип None, инициализируем как Oil
            if (consumableType == ConsumableType.LampOil && currentLiquidType == LiquidType.None)
            {
                currentLiquidType = LiquidType.Oil;
            }

            // Получаем иконки для конкретной жидкости
            Sprite[] customIcons = cons.GetFillIconsForLiquid(currentLiquidType);
            fillIcons = (customIcons != null && customIcons.Length > 0) ? customIcons : cons.fillIcons;

            // --- ОБНОВЛЕНИЕ ИМЕНИ С УЧЕТОМ ЖИДКОСТИ ---
            if (currentLiquidType != LiquidType.None)
            {
                bool isDefaultLampOil = consumableType == ConsumableType.LampOil && currentLiquidType == LiquidType.Oil;
                if (!isDefaultLampOil)
                {
                    string liquidSuffix = PlayerInteract.GetLocalizedLiquidName(currentLiquidType);
                    itemName = $"{baseItemName} ({liquidSuffix})";
                }
            }

            // --- ОБНОВЛЕНИЕ ИКОНКИ ЗАПОЛНЕНИЯ ---
            if (fillIcons != null && fillIcons.Length > 0)
            {
                float fillPercentage = Mathf.Clamp01((float)currentAmount / maxAmount);
                int iconIndex = Mathf.RoundToInt(fillPercentage * (fillIcons.Length - 1));
                itemIcon = fillIcons[iconIndex];
            }
        }
        else
        {
            isConsumable = false;
        }

        lanternFuel = -1f; // Будет инициализировано при первой экипировке
        // Если проп уже хранит топливо (подобрали брошенную лампу) — берём его
        if (item.lanternFuel >= 0f)
            lanternFuel = item.lanternFuel;
    }

    // Функция клонирования (нужна для разделения стаков при выбросе)
    public InventoryItemData Clone()
    {
        return (InventoryItemData)this.MemberwiseClone();
    }
}