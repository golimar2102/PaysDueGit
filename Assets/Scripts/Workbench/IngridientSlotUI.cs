using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IngredientSlotUI : MonoBehaviour
{
    [Tooltip("Иконка ингредиента")]
    public Image ingredientIcon;

    [Tooltip("Название ингредиента")]
    public TextMeshProUGUI ingredientNameText;

    [Tooltip("Соотношение количества (например: 3 / 4)")]
    public TextMeshProUGUI stockText;
}