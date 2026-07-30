using UnityEngine;
using UnityEngine.Localization;

[RequireComponent(typeof(Collider))]
public class LiquidSource : MonoBehaviour
{
    [Header("Настройки источника")]
    [Tooltip("Тип жидкости, которой заполнена бочка/источник")]
    public LiquidType liquidType = LiquidType.CleanWater;

    [Tooltip("Название источника (например, 'Бочка с маслом')")]
    public LocalizedString sourceName;

    [Tooltip("Бесконечный ли источник?")]
    public bool isInfinite = true;

    [Tooltip("Оставшееся количество жидкости в источнике (если не бесконечный)")]
    public int remainingAmount = 100;

    [Header("Звуки")]
    [Tooltip("Звук перелива жидкости")]
    public AudioSource pourSound;

    private Outline outline;

    void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    public void SetHighlight(bool highlight)
    {
        if (outline != null) outline.enabled = highlight;
    }
}