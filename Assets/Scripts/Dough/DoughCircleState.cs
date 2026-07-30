using UnityEngine;

public class DoughCircleState : MonoBehaviour
{
    [Tooltip("Заполнена ли заготовка мясом")]
    public bool isFilled = false;

    [Tooltip("ID предмета фарша, которым заполнена заготовка")]
    public int filledMeatItemID = -1;

    [Tooltip("Ссылка на ячейку контейнера, из которой был взят фарш")]
    public MeatContainerCell originCell = null;

    [Tooltip("Ссылка на созданный визуальный объект фарша")]
    public GameObject meatVisual = null;
}