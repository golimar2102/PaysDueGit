using UnityEngine;

public enum ConsumableType
{
    LampOil,      
    ShotgunAmmo,
    Fuse,
    LiquidContainer,
    Item
}

[RequireComponent(typeof(PickUpItem))]
public class ConsumableItem : MonoBehaviour
{
    [Header("Настройки расходника")]
    public ConsumableType type = ConsumableType.LampOil;
    public int amountPerUse = 10;
    public int currentAmount = 100;
    public int maxAmount = 100;

    [Header("Динамические иконки (2D)")]
    public Sprite[] fillIcons;

    [Header("Визуал жидкости (3D)")]
    public Transform liquidVisualPivot;
    public enum Axis { X, Y, Z }
    public Axis scaleAxis = Axis.Y;

    [Header("Настройки жидкостей")]
    public LiquidType currentLiquidType = LiquidType.None;
    public System.Collections.Generic.List<LiquidVisualMapping> liquidMaterials;
    public Renderer liquidRenderer;

    private PickUpItem pickUpItem;
    private Vector3 initialLiquidScale;

    void Awake()
    {
        pickUpItem = GetComponent<PickUpItem>();
        if (liquidVisualPivot != null) initialLiquidScale = liquidVisualPivot.localScale;
        
        // По умолчанию для масла ставим тип Oil
        if (type == ConsumableType.LampOil && currentLiquidType == LiquidType.None)
        {
            currentLiquidType = LiquidType.Oil;
        }

        UpdateVisuals(); 
    }

    public Sprite[] GetFillIconsForLiquid(LiquidType type)
    {
        if (liquidMaterials != null)
        {
            foreach (var mapping in liquidMaterials)
            {
                if (mapping.liquidType == type) return mapping.fillIcons;
            }
        }
        return null;
    }

    public Material GetMaterialForLiquid(LiquidType type)
    {
        if (liquidMaterials != null)
        {
            foreach (var mapping in liquidMaterials)
            {
                if (mapping.liquidType == type) return mapping.material;
            }
        }
        return null;
    }

    // Эта функция теперь просто обновляет вид банки, валяющейся НА ПОЛУ!
    public void UpdateVisuals()
    {
        float fillPercentage = Mathf.Clamp01((float)currentAmount / maxAmount);

        // Получаем иконки для конкретной жидкости
        Sprite[] activeFillIcons = GetFillIconsForLiquid(currentLiquidType);
        if (activeFillIcons == null || activeFillIcons.Length == 0)
        {
            activeFillIcons = fillIcons;
        }

        if (activeFillIcons != null && activeFillIcons.Length > 0)
        {
            int index = Mathf.RoundToInt(fillPercentage * (activeFillIcons.Length - 1));
            if (pickUpItem != null) pickUpItem.itemIcon = activeFillIcons[index];
        }

        // Обновляем материал жидкости
        if (liquidRenderer != null)
        {
            Material mat = GetMaterialForLiquid(currentLiquidType);
            if (mat != null)
            {
                liquidRenderer.sharedMaterial = mat;
            }
        }

        if (liquidVisualPivot != null)
        {
            Vector3 newScale = initialLiquidScale;
            switch (scaleAxis)
            {
                case Axis.X: newScale.x = initialLiquidScale.x * fillPercentage; break;
                case Axis.Y: newScale.y = initialLiquidScale.y * fillPercentage; break;
                case Axis.Z: newScale.z = initialLiquidScale.z * fillPercentage; break;
            }
            if (fillPercentage <= 0.01f) newScale = Vector3.zero;
            liquidVisualPivot.localScale = newScale;
        }
    }
}