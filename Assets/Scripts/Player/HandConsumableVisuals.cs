using UnityEngine;
using System.Collections.Generic;

public class HandConsumableVisuals : MonoBehaviour
{
    public Transform liquidVisualPivot;
    public ConsumableItem.Axis scaleAxis = ConsumableItem.Axis.Y;

    [Header("Настройки визуала в руках")]
    public Renderer liquidRenderer;
    public List<LiquidVisualMapping> liquidMaterials;

    private Vector3 initialScale;
    // Кэшируем предыдущие значения — пропускаем обновление если ничего не изменилось
    private float lastFillPercentage = -1f;
    private LiquidType lastLiquidType = (LiquidType)(-1);

    void Awake()
    {
        if (liquidVisualPivot != null)
            initialScale = liquidVisualPivot.localScale;
    }

    void Update()
    {
        if (InventoryManager.Instance == null || liquidVisualPivot == null) return;

        int index = InventoryManager.Instance.selectedSlotIndex;
        InventorySlot slot = InventoryManager.Instance.hotbarSlots[index];

        if (slot != null && !slot.IsEmpty() && slot.itemData != null && slot.itemData.isConsumable)
        {
            float fillPercentage = (float)slot.itemData.currentAmount / slot.itemData.maxAmount;
            LiquidType currentLiquidType = slot.itemData.currentLiquidType;

            // Обновляем материал при смене типа жидкости
            if (currentLiquidType != lastLiquidType)
            {
                lastLiquidType = currentLiquidType;
                UpdateLiquidMaterial(currentLiquidType);
            }

            // Пропускаем если значение не изменилось
            if (Mathf.Approximately(fillPercentage, lastFillPercentage)) return;
            lastFillPercentage = fillPercentage;

            Vector3 newScale = initialScale;

            switch (scaleAxis)
            {
                case ConsumableItem.Axis.X: newScale.x = initialScale.x * fillPercentage; break;
                case ConsumableItem.Axis.Y: newScale.y = initialScale.y * fillPercentage; break;
                case ConsumableItem.Axis.Z: newScale.z = initialScale.z * fillPercentage; break;
            }

            if (fillPercentage <= 0.01f) newScale = Vector3.zero;

            liquidVisualPivot.localScale = newScale;
        }
        else
        {
            // Сбрасываем кэш при смене предмета
            if (lastFillPercentage >= 0f || lastLiquidType != (LiquidType)(-1))
            {
                lastFillPercentage = -1f;
                lastLiquidType = (LiquidType)(-1);
                liquidVisualPivot.localScale = initialScale;
            }
        }
    }

    private void UpdateLiquidMaterial(LiquidType type)
    {
        if (liquidRenderer == null || liquidMaterials == null) return;

        foreach (var mapping in liquidMaterials)
        {
            if (mapping.liquidType == type)
            {
                liquidRenderer.sharedMaterial = mapping.material;
                return;
            }
        }
    }
}