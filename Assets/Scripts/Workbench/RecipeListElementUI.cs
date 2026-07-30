using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecipeListElementUI : MonoBehaviour
{
    [Tooltip("Иконка скрафченного предмета")]
    public Image itemIcon;

    [Tooltip("Название скрафченного предмета")]
    public TextMeshProUGUI itemNameText;

    [Tooltip("Контейнер для маленьких иконок ингредиентов (превью)")]
    public Transform ingredientsSummaryParent;

    [Tooltip("Кнопка для выбора рецепта")]
    public Button selectButton;
}