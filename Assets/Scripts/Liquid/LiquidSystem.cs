using UnityEngine;

public enum LiquidType
{
    None,
    DirtyWater,
    CleanWater,
    Blood,
    Oil,
    Biomass,
}

[System.Serializable]
public struct LiquidVisualMapping
{
    [Tooltip("Тип жидкости")]
    public LiquidType liquidType;
    
    [Tooltip("Материал для отображения жидкости в 3D")]
    public Material material;
    
    [Tooltip("Иконки заполнения для инвентаря (от пустой к полной)")]
    public Sprite[] fillIcons;
}

public static class LiquidHelper
{
    public static string GetLiquidNameRu(LiquidType type)
    {
        switch (type)
        {
            case LiquidType.DirtyWater: return "Грязная вода";
            case LiquidType.CleanWater: return "Чистая вода";
            case LiquidType.Blood: return "Кровь";
            case LiquidType.Oil: return "Масло";
            case LiquidType.Biomass: return "Биомасса";
            default: return "Пусто";
        }
    }

    public static string GetLiquidNameEn(LiquidType type)
    {
        switch (type)
        {
            case LiquidType.DirtyWater: return "Dirty Water";
            case LiquidType.CleanWater: return "Clean Water";
            case LiquidType.Blood: return "Blood";
            case LiquidType.Oil: return "Oil";
            case LiquidType.Biomass: return "Biomass";
            default: return "Empty";
        }
    }
}