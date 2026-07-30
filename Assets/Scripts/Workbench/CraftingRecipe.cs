using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Crafting Recipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Tooltip("Название рецепта в Editor (чисто для удобства разработчика)")]
    public string recipeDeveloperName;

    [Tooltip("ID получаемого в результате крафта предмета")]
    public int resultItemID;

    [Tooltip("Количество получаемых предметов")]
    public int resultAmount = 1;

    [Tooltip("Категория верстака, в которой будет отображаться этот рецепт")]
    public CraftingCategory category;

    [System.Serializable]
    public struct Ingredient
    {
        [Tooltip("ID необходимого предмета-ингредиента")]
        public int itemID;
        [Tooltip("Количество, необходимое для крафта")]
        public int amount;
    }

    [Tooltip("Список ингредиентов для крафта")]
    public List<Ingredient> ingredients = new List<Ingredient>();

    [Header("Требование к жидкости (Дополнительный ингредиент)")]
    [Tooltip("Требуется ли жидкость для этого рецепта?")]
    public bool requiresLiquid = false;

    [Tooltip("Тип требуемой жидкости (например, CleanWater)")]
    public LiquidType requiredLiquid = LiquidType.None;

    [Tooltip("Количество требуемой жидкости (например, 20 ед./мл)")]
    public int requiredLiquidAmount = 1;
}