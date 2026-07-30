using UnityEngine;
using System.Collections.Generic;

public class PlantedPlant : MonoBehaviour
{
    [System.Serializable]
    public class PlantStage
    {
        public string stageName = "Фаза";
        [Tooltip("3D Модель для этой стадии (БЕЗ СКРИПТОВ!)")]
        public GameObject visualPrefab;

        [Header("Исправление кривых осей")]
        [Tooltip("Если эта конкретная модель ложится на бок, впиши сюда исправление угла (например, X: -90, 90 или 270)")]
        public Vector3 rotationFix = Vector3.zero;

        [Space(10)]
        [Tooltip("Через СКОЛЬКО ИГРОВЫХ ЧАСОВ после посадки наступит эта фаза? (Например: 0, 24, 48)")]
        public float hoursToReach; 
        
        [Header("Взаимодействие")]
        [Tooltip("Можно ли собрать просто кликом мышки (голыми руками)?")]
        public bool canGatherByHand;
        
        [Tooltip("Список названий инструментов, которыми можно собрать (Например: Axe, Топор, Knife)")]
        public List<string> allowedTools = new List<string>();

        [Tooltip("Список ID инструментов, которыми можно собрать")]
        public List<int> allowedToolIDs = new List<int>();

        [Tooltip("Что выпадет при сборе/срубании на этой стадии")]
        public GameObject[] dropItems;
    }

    [Header("Настройки Роста")]
    [Tooltip("Список стадий (от Семени до Взрослого дерева)")]
    public List<PlantStage> stages = new List<PlantStage>();

    private int plantedDay;
    private float plantedHour;
    
    private int currentStageIndex = -1;
    private GameObject currentVisualObject;

    [HideInInspector] public GameObject emptyHolePrefab;

    void Start()
    {
        if (DayNightCycle.Instance != null)
        {
            plantedDay = DayNightCycle.Instance.currentDay;
            plantedHour = DayNightCycle.Instance.timeOfDay;
        }

        // Принудительно запускаем самую первую стадию (Семя)
        if (stages.Count > 0)
        {
            SetStage(0);
        }
    }

    private float nextGrowthCheckTime = 0f;
    private const float GrowthCheckInterval = 2.0f;

    void Update()
    {
        if (Time.time >= nextGrowthCheckTime)
        {
            nextGrowthCheckTime = Time.time + GrowthCheckInterval;
            CheckGrowth();
        }
    }

    private void CheckGrowth()
    {
        if (DayNightCycle.Instance == null || stages.Count == 0) return;
        if (currentStageIndex >= stages.Count - 1) return; 

        int daysPassed = DayNightCycle.Instance.currentDay - plantedDay;
        float currentTotalHours = (daysPassed * 24f) + DayNightCycle.Instance.timeOfDay;
        float plantedTotalHours = plantedHour;
        float hoursPassed = currentTotalHours - plantedTotalHours;

        PlantStage nextStage = stages[currentStageIndex + 1];
        
        if (hoursPassed >= nextStage.hoursToReach)
        {
            SetStage(currentStageIndex + 1);
        }
    }

    private void SetStage(int newStageIndex)
    {
        currentStageIndex = newStageIndex;
        PlantStage currentStage = stages[currentStageIndex];

        if (currentVisualObject != null)
        {
            Destroy(currentVisualObject);
        }

        if (currentStage.visualPrefab != null)
        {
            // --- ЗАЩИТА ОТ БЕСКОНЕЧНОГО ЦИКЛА ---
            if (currentStage.visualPrefab.GetComponent<PlantedPlant>() != null)
            {
                Debug.LogError($"[КРИТИЧЕСКАЯ ОШИБКА] В фазе '{currentStage.stageName}' в поле Visual Prefab вставлен сам префаб растения! Unity выключил его из-за бесконечного цикла. Вставь туда ОБЫЧНУЮ 3D-МОДЕЛЬ без скриптов!");
                return; 
            }

            // Создаем объект
            currentVisualObject = Instantiate(currentStage.visualPrefab, transform, false);
            
            // Сбрасываем позицию в ноль (ровно по центру грядки)
            currentVisualObject.transform.localPosition = Vector3.zero;
            
            // МАГИЯ ПОВОРОТА: Берем родной поворот префаба и прибавляем к нему наш ручной фикс из Инспектора!
            currentVisualObject.transform.localRotation = currentStage.visualPrefab.transform.localRotation * Quaternion.Euler(currentStage.rotationFix);
            
            currentVisualObject.SetActive(true);
        }
    }

    public string GetStageName()
    {
        if (currentStageIndex >= 0 && currentStageIndex < stages.Count)
        {
            return stages[currentStageIndex].stageName;
        }
        return "Растение";
    }

    public int GetAgeInHours()
    {
        if (DayNightCycle.Instance == null) return 0;
        int daysPassed = DayNightCycle.Instance.currentDay - plantedDay;
        float currentTotalHours = (daysPassed * 24f) + DayNightCycle.Instance.timeOfDay;
        float hoursPassed = currentTotalHours - plantedHour;
        return Mathf.Max(0, Mathf.FloorToInt(hoursPassed));
    }

    public bool IsHarvestable()
    {
        if (currentStageIndex < 0 || currentStageIndex >= stages.Count) return false;
        PlantStage stage = stages[currentStageIndex];
        return stage.canGatherByHand || 
               (stage.allowedTools != null && stage.allowedTools.Count > 0) || 
               (stage.allowedToolIDs != null && stage.allowedToolIDs.Count > 0);
    }

    // Эта функция вызывается из PlayerInteract, когда мы кликаем по растению
    public void HarvestByHand()
    {
        if (currentStageIndex < 0 || currentStageIndex >= stages.Count) return;
        PlantStage stage = stages[currentStageIndex];

        // 1. Узнаем, что сейчас в руках у игрока (ID и имя)
        int itemIDInHand = -1;
        string itemInHand = "";
        if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
        {
            int slotIndex = InventoryManager.Instance.selectedSlotIndex;
            if (slotIndex >= 0 && slotIndex < InventoryManager.Instance.hotbarSlots.Length)
            {
                var slot = InventoryManager.Instance.hotbarSlots[slotIndex];
                if (slot != null && !slot.IsEmpty())
                {
                    itemIDInHand = slot.currentItemID;
                    itemInHand = slot.currentItemName;
                }
            }
        }

        bool canHarvest = false;

        // 2. Проверяем, можно ли собрать руками
        if (stage.canGatherByHand)
        {
            canHarvest = true;
        }
        // 3. Если руками нельзя, проверяем, подходит ли инструмент по ID или имени
        else if (itemIDInHand != -1 || !string.IsNullOrEmpty(itemInHand))
        {
            if (stage.allowedToolIDs != null && stage.allowedToolIDs.Contains(itemIDInHand))
            {
                canHarvest = true;
            }
            else if (stage.allowedTools != null && stage.allowedTools.Count > 0 && !string.IsNullOrEmpty(itemInHand))
            {
                string itemInHandLower = itemInHand.ToLower();
                foreach (string toolName in stage.allowedTools)
                {
                    if (!string.IsNullOrEmpty(toolName) && itemInHandLower.Contains(toolName.ToLower()))
                    {
                        canHarvest = true;
                        break;
                    }
                }
            }
        }

        // 4. Результат
        if (canHarvest)
        {
            DropLootAndDestroy(stage);
        }
        else
        {
            if ((stage.allowedTools != null && stage.allowedTools.Count > 0) || (stage.allowedToolIDs != null && stage.allowedToolIDs.Count > 0))
            {
                List<string> requirements = new List<string>();
                if (stage.allowedToolIDs != null && stage.allowedToolIDs.Count > 0)
                {
                    requirements.Add($"IDs: {string.Join(", ", stage.allowedToolIDs)}");
                }
                if (stage.allowedTools != null && stage.allowedTools.Count > 0)
                {
                    requirements.Add($"Имена: {string.Join(", ", stage.allowedTools)}");
                }
                Debug.Log($"Нужен инструмент! Подходит: {string.Join(" или ", requirements)}");
            }
            else
            {
                Debug.Log("Это растение пока нельзя собрать.");
            }
        }
    }

    public void ChopDown()
    {
        // Оставили для совместимости с будущими скриптами ударов
        if (currentStageIndex < 0 || currentStageIndex >= stages.Count) return;
        PlantStage stage = stages[currentStageIndex];
        DropLootAndDestroy(stage);
    }

    private void DropLootAndDestroy(PlantStage stage)
    {
        // 1. Выкидываем предметы
        if (stage.dropItems != null)
        {
            foreach (GameObject loot in stage.dropItems)
            {
                if (loot != null)
                {
                    Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0.8f, Random.Range(-0.5f, 0.5f));
                    Vector3 dropPos = transform.position + randomOffset;
                    Instantiate(loot, dropPos, Quaternion.identity);
                }
            }
        }

        // 2. Восстанавливаем пустую лунку и удаляем посаженную
        Transform parentHole = transform.parent;
        if (parentHole != null && parentHole.CompareTag("PlantedHole"))
        {
            if (emptyHolePrefab != null)
            {
                Instantiate(emptyHolePrefab, parentHole.position, parentHole.rotation);
            }
            Destroy(parentHole.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}