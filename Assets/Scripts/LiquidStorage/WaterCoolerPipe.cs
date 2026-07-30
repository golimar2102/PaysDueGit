using UnityEngine;
using UnityEngine.Localization;

public class WaterCoolerPipe : MonoBehaviour
{
    [Header("Настройки")]
    public WaterCoolerController controller;
    public Outline outline;
    [Tooltip("Локализованный текст подсказки для вливания")]
    public LocalizedString pourPrompt;
    [Tooltip("Локализованный текст: Неподходящая жидкость")]
    public LocalizedString locIncompatibleLiquid;
    [Tooltip("Локализованный текст: Кулер содержит другую жидкость ({0})")]
    public LocalizedString locDifferentLiquid;
    [Tooltip("Локализованный текст: Кулер полон")]
    public LocalizedString locFull;
    [Tooltip("Локализованный текст: Нужен сосуд с подходящей жидкостью")]
    public LocalizedString locNeedContainer;

    private float canisterFloatAccumulator = 0f;

    void Awake()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<WaterCoolerController>();
        }
        if (outline == null)
        {
            outline = GetComponent<Outline>();
        }
        if (outline == null)
        {
            outline = GetComponentInChildren<Outline>(true);
        }
        SetHighlight(false);
    }

    public void SetHighlight(bool active)
    {
        if (outline != null)
        {
            outline.enabled = active;
        }
    }

    public bool IsFull()
    {
        return controller != null && controller.currentWater >= controller.maxWater;
    }

    /// <summary>
    /// Логика вливания воды из контейнера в руках игрока в кулер.
    /// </summary>
    public void PourWater(InventorySlot slot)
    {
        if (controller == null || slot == null || slot.itemData == null) return;

        if (IsFull())
        {
            StopPouring();
            return;
        }

        InventoryItemData data = slot.itemData;
        
        // Проверяем, что вливаем разрешенную жидкость
        if (!controller.allowedLiquids.Contains(data.currentLiquidType) || data.currentAmount <= 0)
        {
            StopPouring();
            return;
        }

        // Если кулер не пуст, жидкость должна совпадать по типу
        if (controller.currentWater > 0f && controller.currentLiquidType != LiquidType.None && data.currentLiquidType != controller.currentLiquidType)
        {
            StopPouring();
            return;
        }

        // Запуск зацикленного звука перелива
        if (controller.pourSound != null && !controller.pourSound.isPlaying)
        {
            controller.pourSound.loop = true;
            controller.pourSound.Play();
        }

        // Устанавливаем тип жидкости в кулере, если он был пуст
        if (controller.currentWater <= 0f)
        {
            controller.currentLiquidType = data.currentLiquidType;
        }

        // Рассчитываем объем переливания за кадр
        float transfer = controller.pourRate * Time.deltaTime;
        float actualTransfer = Mathf.Min(transfer, data.currentAmount);
        actualTransfer = Mathf.Min(actualTransfer, controller.maxWater - controller.currentWater);

        controller.currentWater += actualTransfer;

        // Вычитаем целые единицы из контейнера игрока
        canisterFloatAccumulator += actualTransfer;
        if (canisterFloatAccumulator >= 1f)
        {
            int intTransfer = Mathf.FloorToInt(canisterFloatAccumulator);
            canisterFloatAccumulator -= intTransfer;
            data.currentAmount -= intTransfer;

            if (data.currentAmount <= 0)
            {
                data.currentAmount = 0;
                data.currentLiquidType = LiquidType.None;
                data.itemName = data.baseItemName;

                // Сбрасываем иконки заполненности до пустой бутылки/канистры
                if (InventoryManager.Instance != null)
                {
                    GameObject prefab = InventoryManager.Instance.GetPrefabByID(data.itemID);
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
                StopPouring();
            }
            else
            {
                // Обновляем иконку уровня заполненности
                if (data.fillIcons != null && data.fillIcons.Length > 0)
                {
                    float fillPct = Mathf.Clamp01((float)data.currentAmount / data.maxAmount);
                    int idx = Mathf.RoundToInt(fillPct * (data.fillIcons.Length - 1));
                    data.itemIcon = data.fillIcons[idx];
                }
            }

            slot.UpdateSlotUI();
        }
    }

    public void StopPouring()
    {
        canisterFloatAccumulator = 0f;
        if (controller != null && controller.pourSound != null && controller.pourSound.isPlaying)
        {
            controller.pourSound.Stop();
        }
    }
}
