using UnityEngine;
using UnityEngine.Localization;
using System.Collections.Generic;

public class WaterCoolerTap : MonoBehaviour
{
    [Header("Настройки")]
    public WaterCoolerController controller;
    public Outline outline;
    [Tooltip("Локализованный текст подсказки для набора воды")]
    public LocalizedString fillPrompt;
    [Tooltip("Локализованный текст подсказки для питья")]
    public LocalizedString drinkPrompt;
    [Tooltip("Локализованный текст: Сосуд полон")]
    public LocalizedString locContainerFull;
    [Tooltip("Локализованный текст: Сосуд содержит другую жидкость ({0})")]
    public LocalizedString locContainerDifferent;
    [Tooltip("Локализованный текст: Кулер пуст")]
    public LocalizedString locEmpty;
    [Tooltip("Локализованный текст: Нужен пустой сосуд для набора воды")]
    public LocalizedString locNeedEmptyContainer;
    [Tooltip("Локализованный текст: Вы не хотите пить")]
    public LocalizedString locNotThirsty;
    [Tooltip("Список жидкостей, которые игрок может пить напрямую")]
    public List<LiquidType> drinkableLiquids = new List<LiquidType> { LiquidType.CleanWater, LiquidType.DirtyWater };

    private float fillFloatAccumulator = 0f;

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

    public bool IsEmpty()
    {
        return controller == null || controller.currentWater <= 0f || controller.currentLiquidType == LiquidType.None;
    }

    /// <summary>
    /// Набор жидкости из краника кулера в контейнер в руках игрока.
    /// </summary>
    public void FillContainer(InventorySlot slot)
    {
        if (controller == null || slot == null || slot.itemData == null) return;

        if (IsEmpty())
        {
            StopFilling();
            return;
        }

        InventoryItemData data = slot.itemData;
        if (data.currentAmount >= data.maxAmount)
        {
            StopFilling();
            return;
        }

        // Проверяем, не налита ли в контейнер другая несовместимая жидкость
        if (data.currentLiquidType != LiquidType.None && data.currentLiquidType != controller.currentLiquidType)
        {
            StopFilling();
            return;
        }

        // Запуск зацикленного звука наливания
        if (controller.fillSound != null && !controller.fillSound.isPlaying)
        {
            controller.fillSound.loop = true;
            controller.fillSound.Play();
        }

        // Рассчитываем объем налива за кадр
        float transfer = controller.pourRate * Time.deltaTime;
        float actualTransfer = Mathf.Min(transfer, data.maxAmount - data.currentAmount);
        actualTransfer = Mathf.Min(actualTransfer, controller.currentWater);

        controller.currentWater -= actualTransfer;
        LiquidType liquidFilled = controller.currentLiquidType;

        // Если в кулере закончилась жидкость, сбрасываем её тип
        if (controller.currentWater <= 0f)
        {
            controller.currentWater = 0f;
            controller.currentLiquidType = LiquidType.None;
        }

        // Добавляем целые единицы в контейнер
        fillFloatAccumulator += actualTransfer;
        if (fillFloatAccumulator >= 1f)
        {
            int intTransfer = Mathf.FloorToInt(fillFloatAccumulator);
            fillFloatAccumulator -= intTransfer;
            data.currentAmount += intTransfer;
            data.currentLiquidType = liquidFilled;

            if (string.IsNullOrEmpty(data.baseItemName))
            {
                data.baseItemName = data.itemName;
            }

            string liquidSuffix = PlayerInteract.GetLocalizedLiquidName(liquidFilled);
            data.itemName = $"{data.baseItemName} ({liquidSuffix})";

            // Считываем иконки из префаба
            if (InventoryManager.Instance != null)
            {
                GameObject prefab = InventoryManager.Instance.GetPrefabByID(data.itemID);
                if (prefab != null)
                {
                    ConsumableItem cons = prefab.GetComponent<ConsumableItem>();
                    if (cons != null)
                    {
                        Sprite[] customIcons = cons.GetFillIconsForLiquid(liquidFilled);
                        data.fillIcons = (customIcons != null && customIcons.Length > 0) ? customIcons : cons.fillIcons;
                    }
                }
            }

            // Обновляем иконку уровня заполненности
            if (data.fillIcons != null && data.fillIcons.Length > 0)
            {
                float fillPct = Mathf.Clamp01((float)data.currentAmount / data.maxAmount);
                int idx = Mathf.RoundToInt(fillPct * (data.fillIcons.Length - 1));
                data.itemIcon = data.fillIcons[idx];
            }

            slot.UpdateSlotUI();
        }
    }

    public void StopFilling()
    {
        fillFloatAccumulator = 0f;
        if (controller != null && controller.fillSound != null && controller.fillSound.isPlaying)
        {
            controller.fillSound.Stop();
        }
    }

    /// <summary>
    /// Действие непосредственного питья жидкости игроком (восстановление жажды).
    /// </summary>
    public void DrinkWater()
    {
        if (controller == null || PlayerStats.Instance == null) return;

        if (IsEmpty())
        {
            StopDrinking();
            return;
        }

        // Проверяем, пригодна ли жидкость для питья
        if (!drinkableLiquids.Contains(controller.currentLiquidType))
        {
            StopDrinking();
            return;
        }

        // Если жажда полностью утолена, пить не нужно
        if (PlayerStats.Instance.currentThirst >= PlayerStats.Instance.maxThirst)
        {
            StopDrinking();
            return;
        }

        // Запуск зацикленного звука питья/набора
        if (controller.fillSound != null && !controller.fillSound.isPlaying)
        {
            controller.fillSound.loop = true;
            controller.fillSound.Play();
        }

        // Восстановление жажды за кадр
        float transfer = controller.pourRate * Time.deltaTime;
        float actualTransfer = Mathf.Min(transfer, controller.currentWater);
        
        float neededThirst = PlayerStats.Instance.maxThirst - PlayerStats.Instance.currentThirst;
        actualTransfer = Mathf.Min(actualTransfer, neededThirst);

        if (actualTransfer > 0f)
        {
            controller.currentWater -= actualTransfer;
            PlayerStats.Instance.QuenchThirst(actualTransfer);

            // Если кулер опустошен, сбрасываем тип жидкости
            if (controller.currentWater <= 0f)
            {
                controller.currentWater = 0f;
                controller.currentLiquidType = LiquidType.None;
            }
        }
        else
        {
            StopDrinking();
        }
    }

    public void StopDrinking()
    {
        if (controller != null && controller.fillSound != null && controller.fillSound.isPlaying)
        {
            controller.fillSound.Stop();
        }
    }
}
