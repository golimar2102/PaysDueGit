using UnityEngine;

public class GeneratorFuelPipe : MonoBehaviour
{
    [Tooltip("Компонент Outline для подсветки трубы при наведении/перетаскивании")]
    public Outline outline;

    private GeneratorController generator;

    void Awake()
    {
        generator = GetComponentInParent<GeneratorController>();
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

    /// <summary>
    /// Включает или выключает outline-подсветку трубы.
    /// </summary>
    public void SetHighlight(bool active)
    {
        if (outline != null)
        {
            outline.enabled = active;
        }
    }

    /// <summary>
    /// Проверяет, пуста ли труба (нет ли в ней канистры).
    /// </summary>
    public bool isEmpty
    {
        get { return generator != null && !generator.hasCanister; }
    }

    /// <summary>
    /// Вставляет канистру в трубу.
    /// </summary>
    public void InsertCanister(InventoryItemData canisterData)
    {
        if (generator != null)
        {
            generator.InsertCanister(canisterData);
        }
    }

    /// <summary>
    /// Извлекает канистру из трубы.
    /// </summary>
    public InventoryItemData ExtractCanister()
    {
        if (generator != null)
        {
            return generator.ExtractCanister();
        }
        return null;
    }
}