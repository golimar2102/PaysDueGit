using UnityEngine;

public class GeneratorFuseHolder : MonoBehaviour
{
    [Header("Настройки слота")]
    [Tooltip("Индекс этого предохранителя (0..4)")]
    public int slotIndex;
    
    [Tooltip("Пуст ли держатель в данный момент")]
    public bool isEmpty = true;

    [System.Serializable]
    public struct FuseVisualMapping
    {
        public int itemID;
        [Tooltip("Список 3D объектов (съемных частей), которые должны быть активны для этого предохранителя")]
        public GameObject[] visualObjects;
    }

    [Header("Визуальные элементы")]
    [Tooltip("Базовая 3D модель предохранителя (активна для любого типа)")]
    public GameObject fuseBaseModel;

    [Tooltip("Сопоставление ID предохранителей и их съемных частей")]
    public FuseVisualMapping[] fuseVisualParts;
    
    [Tooltip("Объект подсветки outline для этого держателя")]
    public Outline outline;

    [Header("Данные установленного предохранителя")]
    [Tooltip("Внутренние данные установленного предохранителя (содержит износ)")]
    public InventoryItemData installedFuseData;

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

    void Start()
    {
        UpdateVisuals();
    }

    /// <summary>
    /// Вставляет предохранитель в держатель.
    /// </summary>
    public void InsertFuse(InventoryItemData fuseData)
    {
        installedFuseData = fuseData;
        isEmpty = false;
        UpdateVisuals();

        if (generator != null)
        {
            generator.OnFuseInserted(slotIndex);
        }
    }

    /// <summary>
    /// Извлекает предохранитель из держателя и возвращает его данные.
    /// </summary>
    public InventoryItemData ExtractFuse()
    {
        if (isEmpty) return null;

        InventoryItemData extractedData = installedFuseData;
        installedFuseData = null;
        isEmpty = true;
        UpdateVisuals();

        if (generator != null)
        {
            generator.OnFuseExtracted(slotIndex);
        }

        return extractedData;
    }

    /// <summary>
    /// Обновляет отображение 3D модели предохранителя.
    /// </summary>
    public void UpdateVisuals()
    {
        if (fuseBaseModel != null)
        {
            fuseBaseModel.SetActive(!isEmpty);
        }

        if (fuseVisualParts != null)
        {
            foreach (var part in fuseVisualParts)
            {
                if (part.visualObjects != null)
                {
                    foreach (var obj in part.visualObjects)
                    {
                        if (obj != null) obj.SetActive(false);
                    }
                }
            }
            if (!isEmpty && installedFuseData != null)
            {
                foreach (var part in fuseVisualParts)
                {
                    if (part.itemID == installedFuseData.itemID && part.visualObjects != null)
                    {
                        foreach (var obj in part.visualObjects)
                        {
                            if (obj != null) obj.SetActive(true);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Включает или выключает outline-подсветку держателя.
    /// </summary>
    public void SetHighlight(bool active)
    {
        if (outline != null)
        {
            outline.enabled = active;
        }
    }
}
