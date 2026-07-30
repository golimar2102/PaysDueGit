using UnityEngine;
using System.Collections.Generic;

public class PlacementPoint : MonoBehaviour
{
    [System.Serializable]
    public struct PresetMapping
    {
        public int itemID;
        public GameObject presetObject;
    }

    [System.Serializable]
    public struct HighlightMapping
    {
        public int itemID;
        public GameObject highlightVisual;
    }

    [Header("Настройки точки")]
    [Tooltip("ID предметов, которые можно сюда поставить (например, ID мясорубки)")]
    public List<int> acceptedItemIDs = new List<int>();

    [Tooltip("Маппинг ID предметов к их пресетным объектам на сцене")]
    public List<PresetMapping> presetMappings = new List<PresetMapping>();

    [Tooltip("Маппинг ID предметов к их кастомным объектам подсказки (голограммам)")]
    public List<HighlightMapping> highlightMappings = new List<HighlightMapping>();

    [Tooltip("Объект (например, полупрозрачный кубик), который будет включаться при наведении/поиске места (дефолтная голограмма)")]
    public GameObject highlightVisual;

    [Tooltip("Если задано, то при установке предмета на эту точку этот объект включится (дефолтный/фоллбек пресет)")]
    public GameObject presetObjectToEnable;

    // Словарь для рантайм-хранения сгенерированных превьюшек по ID предмета
    private Dictionary<int, GameObject> generatedHighlights = new Dictionary<int, GameObject>();
    private GameObject defaultGeneratedHighlight;

    private bool isInitialized = false;

    void Start()
    {
        SetHighlight(false);
    }

    /// <summary>
    /// Инициализирует автоматические превью-голограммы для всех пресетов
    /// </summary>
    public void InitializeHighlights(Material previewMat)
    {
        if (isInitialized) return;
        isInitialized = true;

        if (previewMat == null)
        {
            ItemPlacementManager manager = FindFirstObjectByType<ItemPlacementManager>();
            if (manager != null)
            {
                previewMat = manager.distantSnapPreviewMaterial != null ? manager.distantSnapPreviewMaterial : manager.validPreviewMaterial;
            }
        }
        // 1. Очищаем дефолтный highlightVisual, если он совпадает с пресетом
        if (highlightVisual != null)
        {
            if (highlightVisual == presetObjectToEnable || IsPresetObject(highlightVisual))
            {
                highlightVisual = null;
            }
        }

        // 2. Очищаем элементы в highlightMappings, если они совпадают с пресетами
        for (int i = 0; i < highlightMappings.Count; i++)
        {
            var mapping = highlightMappings[i];
            if (mapping.highlightVisual != null)
            {
                if (mapping.highlightVisual == presetObjectToEnable || IsPresetObject(mapping.highlightVisual))
                {
                    mapping.highlightVisual = null;
                    highlightMappings[i] = mapping;
                }
            }
        }

        // 3. Создаем дефолтную голограмму из дефолтного пресета, если дефолтный highlightVisual пуст
        if (highlightVisual == null && presetObjectToEnable != null)
        {
            defaultGeneratedHighlight = CreateHighlightPreview(presetObjectToEnable, previewMat);
        }

        // 4. Генерируем голограммы для всех presetMappings
        foreach (var mapping in presetMappings)
        {
            if (mapping.presetObject != null && !generatedHighlights.ContainsKey(mapping.itemID))
            {
                // Проверяем, есть ли у нас кастомный highlightVisual для этого ID
                GameObject customHighlight = GetCustomHighlightFromInspector(mapping.itemID);
                
                GameObject hl;
                if (customHighlight != null)
                {
                    // Если пользователь задал кастомный визуал, используем его (но делаем клон с материалом)
                    hl = CreateHighlightPreview(customHighlight, previewMat);
                }
                else
                {
                    // Иначе генерируем голограмму прямо из пресетного объекта
                    hl = CreateHighlightPreview(mapping.presetObject, previewMat);
                }

                if (hl != null)
                {
                    generatedHighlights[mapping.itemID] = hl;
                }
            }
        }
        
        // 5. Также генерируем голограммы для тех highlightMappings, у которых нет пресета, но есть кастомный визуал
        foreach (var mapping in highlightMappings)
        {
            if (mapping.highlightVisual != null && !generatedHighlights.ContainsKey(mapping.itemID))
            {
                GameObject hl = CreateHighlightPreview(mapping.highlightVisual, previewMat);
                if (hl != null)
                {
                    generatedHighlights[mapping.itemID] = hl;
                }
            }
        }
    }

    private bool IsPresetObject(GameObject obj)
    {
        foreach (var mapping in presetMappings)
        {
            if (mapping.presetObject == obj) return true;
        }
        return false;
    }

    private GameObject GetCustomHighlightFromInspector(int itemID)
    {
        foreach (var mapping in highlightMappings)
        {
            if (mapping.itemID == itemID) return mapping.highlightVisual;
        }
        return null;
    }

    private GameObject CreateHighlightPreview(GameObject source, Material mat)
    {
        if (source == null) return null;

        GameObject hl = Instantiate(source, transform);
        hl.transform.localPosition = source.transform.localPosition;
        hl.transform.localRotation = source.transform.localRotation;
        hl.transform.localScale = source.transform.localScale;

        // Удаляем коллайдеры, скрипты и камеры, чтобы превьюшка не перехватывала управление
        foreach (var comp in hl.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp != null && comp != this) Destroy(comp);
        }
        foreach (var comp in hl.GetComponentsInChildren<Collider>(true)) if (comp != null) Destroy(comp);
        foreach (var comp in hl.GetComponentsInChildren<Rigidbody>(true)) if (comp != null) Destroy(comp);
        foreach (var comp in hl.GetComponentsInChildren<Camera>(true)) if (comp != null) Destroy(comp);
        foreach (var comp in hl.GetComponentsInChildren<AudioListener>(true)) if (comp != null) Destroy(comp);

        Renderer[] renderers = hl.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        hl.SetActive(false);
        return hl;
    }

    /// <summary>
    /// Включает или выключает визуальную подсказку
    /// </summary>
    public void SetHighlight(bool active, int itemID = -1)
    {
        if (!isInitialized && active)
        {
            InitializeHighlights(null);
        }

        // Проверяем, есть ли у нас кастомный статичный highlightVisual для этого ID
        GameObject customStaticHighlight = null;
        if (itemID != -1)
        {
            customStaticHighlight = GetCustomHighlightFromInspector(itemID);
        }
        
        // Если для конкретного ID кастомный визуал не задан, используем дефолтный статический highlightVisual
        if (customStaticHighlight == null)
        {
            customStaticHighlight = highlightVisual;
        }

        // Если найден статический кастомный визуал (который не был занулен в Start/Initialize), управляем им напрямую
        if (customStaticHighlight != null)
        {
            customStaticHighlight.SetActive(active);
            DisableAllGeneratedHighlights();
            return;
        }

        // Иначе работаем с авто-сгенерированными рантайм-голограммами
        if (active && itemID != -1)
        {
            DisableAllGeneratedHighlights();

            if (generatedHighlights.TryGetValue(itemID, out GameObject hl))
            {
                if (hl != null) hl.SetActive(true);
            }
            else if (defaultGeneratedHighlight != null)
            {
                defaultGeneratedHighlight.SetActive(true);
            }
        }
        else
        {
            DisableAllGeneratedHighlights();
        }
    }

    private void DisableAllGeneratedHighlights()
    {
        if (defaultGeneratedHighlight != null) defaultGeneratedHighlight.SetActive(false);
        foreach (var hl in generatedHighlights.Values)
        {
            if (hl != null) hl.SetActive(false);
        }
    }

    /// <summary>
    /// Проверяет, свободна ли точка (нет ли на ней уже предмета)
    /// </summary>
    public bool IsOccupied()
    {
        // Проверяем реальные пресеты на сцене (если у них есть PickUpItem и они активны)
        if (presetObjectToEnable != null && HasPickUpItem(presetObjectToEnable) && presetObjectToEnable.activeInHierarchy)
        {
            return true;
        }

        foreach (var mapping in presetMappings)
        {
            if (mapping.presetObject != null && HasPickUpItem(mapping.presetObject) && mapping.presetObject.activeInHierarchy)
            {
                return true;
            }
        }

        // Проверяем сферу вокруг самой PlacementPoint
        if (CheckSphereOccupied(transform.position)) return true;

        // Проверяем сферу вокруг положения пресетов (на случай если они смещены)
        if (presetObjectToEnable != null && CheckSphereOccupied(presetObjectToEnable.transform.position)) return true;

        foreach (var mapping in presetMappings)
        {
            if (mapping.presetObject != null && CheckSphereOccupied(mapping.presetObject.transform.position)) return true;
        }

        return false;
    }

    private bool HasPickUpItem(GameObject obj)
    {
        return obj.GetComponent<PickUpItem>() != null || obj.GetComponentInChildren<PickUpItem>() != null;
    }

    private bool CheckSphereOccupied(Vector3 position)
    {
        Collider[] cols = Physics.OverlapSphere(position, 0.25f, Physics.AllLayers, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            // Игнорируем коллайдеры на этом же GameObject (саму точку PlacementPoint) и ее дочерних элементах (голограммах)
            if (c.gameObject == gameObject || c.transform.IsChildOf(transform)) continue;

            // Нам подходят как обычные, так и триггерные коллайдеры предметов
            PickUpItem pickup = c.GetComponentInParent<PickUpItem>();
            MeatGrinderController grinder = c.GetComponentInParent<MeatGrinderController>();
            
            if (pickup != null || grinder != null)
            {
                // Убеждаемся, что найденный предмет активен на сцене
                if (pickup != null && !pickup.gameObject.activeInHierarchy) continue;
                if (grinder != null && !grinder.gameObject.activeInHierarchy) continue;
                
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Проверяет, подходит ли предмет для этой точки
    /// </summary>
    public bool AcceptsItem(int itemID)
    {
        if (acceptedItemIDs.Contains(itemID)) return true;

        foreach (var mapping in presetMappings)
        {
            if (mapping.itemID == itemID) return true;
        }

        return false;
    }

    /// <summary>
    /// Возвращает пресетный объект по ID предмета
    /// </summary>
    public GameObject GetPresetObject(int itemID)
    {
        foreach (var mapping in presetMappings)
        {
            if (mapping.itemID == itemID) return mapping.presetObject;
        }
        return presetObjectToEnable;
    }
}
