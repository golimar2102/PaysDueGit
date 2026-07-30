using UnityEngine;

[System.Serializable]
public struct DumplingLogoMapping
{
    [Tooltip("Тип мяса пельменя (например, Beef, Pork, Canine, Feline, Avian)")]
    public string meatType;
    [Tooltip("ID предмета пачки пельменей в инвентаре")]
    public int dumplingPackItemID;
    [Tooltip("Материал логотипа для этого типа пельменей")]
    public Material logoMaterial;
}

[System.Serializable]
public struct CookedDumplingsMapping
{
    [Tooltip("Тип мяса пельменя")]
    public string meatType;
    [Tooltip("ID предмета готовых пельменей (на тарелке)")]
    public int cookedItemID;
}

public class BoilingPot : MonoBehaviour
{
    [Header("Компоненты кипения")]
    [Tooltip("Эффект пара/пузырей при кипении")]
    public ParticleSystem steamParticles;
    [Tooltip("Звук бурления воды (Loop)")]
    public AudioSource boilingLoopSound;

    [Header("Настройки времени кипения")]
    [Tooltip("Время закипания до 100 градусов (в игровых минутах)")]
    public float boilDurationInGameMinutes = 15f;
    [Tooltip("Через какое время начнет идти пар (в игровых минутах)")]
    public float steamStartInGameMinutes = 5f;

    [Header("Настройки остывания")]
    [Tooltip("Время полного остывания от 100 до 0 градусов (в игровых минутах)")]
    public float coolingDurationInGameMinutes = 15f;

    [Header("Настройки пельменей и логотипов")]
    [Tooltip("Объект пельменей внутри кастрюли (Mesh)")]
    public GameObject dumplingsMesh;
    [Tooltip("Крышка кастрюли (Mesh)")]
    public GameObject lidMesh;
    [Tooltip("Рендерер первого логотипа на кастрюле")]
    public Renderer logoRenderer1;
    [Tooltip("Рендерер второго логотипа на кастрюле")]
    public Renderer logoRenderer2;
    [Tooltip("Маппинг типов пельменей (ID пачек и материалы логотипов)")]
    public DumplingLogoMapping[] logoMappings;
    [Tooltip("Время варки пельменей в кипящей воде (в игровых минутах)")]
    public float dumplingsCookDurationInGameMinutes = 10f;

    [Header("Настройки тарелки и готовых блюд")]
    [Tooltip("ID предмета тарелки в инвентаре")]
    public int plateItemID = 50;
    [Tooltip("Маппинг типов сырых пельменей на ID готовых пельменей на тарелке")]
    public CookedDumplingsMapping[] cookedDumplingsMappings;

    [Header("Состояние (Информативно)")]
    [Tooltip("Текущая температура воды в градусах (максимум 100)")]
    [SerializeField] private float currentTemperature = 0f;
    [SerializeField] private bool isBoiling = false;
    [Tooltip("Добавлены ли в кастрюлю пельмени?")]
    [SerializeField] private bool hasDumplings = false;
    [Tooltip("Текущий тип пельменей в кастрюле")]
    [SerializeField] private string currentDumplingType = "";
    [Tooltip("Текущий прогресс варки пельменей (в игровых минутах)")]
    [SerializeField] private float currentDumplingsCookProgressMinutes = 0f;
    [Tooltip("Сварились ли пельмени?")]
    [SerializeField] private bool areDumplingsCooked = false;

    private ConsumableItem consumableItem;
    private PickUpItem pickUpItem;
    private float lastInGameTimeHours = -1f;

    // Переменные оптимизации
    private StoveKnob[] cachedKnobs;
    private StoveKnob activeBurner;
    private float burnerCheckTimer = 0f;
    private const float BURNER_CHECK_INTERVAL = 0.2f; // Проверка 5 раз в секунду
    private readonly Collider[] overlapResults = new Collider[8];

    void Awake()
    {
        consumableItem = GetComponent<ConsumableItem>();
        pickUpItem = GetComponent<PickUpItem>();
    }

    void Start()
    {
        // Кэшируем ручки плиты один раз при старте
        cachedKnobs = FindObjectsByType<StoveKnob>(FindObjectsSortMode.None);

        // При старте выключаем пар и звук, если они случайно были включены
        if (steamParticles != null) steamParticles.Stop();
        if (boilingLoopSound != null) boilingLoopSound.Stop();

        // Скрываем элементы пельменей и логотипов по умолчанию (или выставляем текущие)
        UpdateDumplingVisuals();
    }

    void Update()
    {
        // 1. Проверяем, есть ли вода в кастрюле
        bool hasWater = consumableItem != null && 
                         consumableItem.currentAmount > 0 && 
                         (consumableItem.currentLiquidType == LiquidType.DirtyWater || consumableItem.currentLiquidType == LiquidType.CleanWater);

        // Если вода закончилась (например, вылили), то сбрасываем пельмени и прогресс варки
        if (hasDumplings && (consumableItem == null || consumableItem.currentAmount <= 0 || consumableItem.currentLiquidType == LiquidType.None))
        {
            hasDumplings = false;
            currentDumplingType = "";
            areDumplingsCooked = false;
            currentDumplingsCookProgressMinutes = 0f;
            UpdateDumplingVisuals();
        }

        // 2. Ищем активную конфорку с интервалом
        burnerCheckTimer -= Time.deltaTime;
        if (burnerCheckTimer <= 0f)
        {
            burnerCheckTimer = BURNER_CHECK_INTERVAL;
            activeBurner = GetActiveBurnerItIsPlacedOn();
        }

        bool isHeating = hasWater && activeBurner != null;

        // Вычисляем прошедшее игровое время
        float currentInGameTimeHours = DayNightCycle.Instance != null 
            ? (DayNightCycle.Instance.currentDay * 24f + DayNightCycle.Instance.timeOfDay) 
            : 0f;

        float elapsedGameMinutes = 0f;
        if (lastInGameTimeHours >= 0f)
        {
            float elapsedGameHours = currentInGameTimeHours - lastInGameTimeHours;
            if (elapsedGameHours < 0f) elapsedGameHours = 0f;
            elapsedGameMinutes = elapsedGameHours * 60f;
        }
        else
        {
            // Инициализация времени
            lastInGameTimeHours = currentInGameTimeHours;
        }

        // Обновляем время для следующего кадра
        lastInGameTimeHours = currentInGameTimeHours;

        if (isHeating)
        {
            // Нагрев: прибавляем температуру до максимума в 100 градусов
            float heatingSpeed = 100f / boilDurationInGameMinutes;
            currentTemperature = Mathf.Min(currentTemperature + heatingSpeed * elapsedGameMinutes, 100f);

            // Завершение кипячения грязной воды в чистую при достижении 100 градусов
            if (currentTemperature >= 100f)
            {
                if (consumableItem.currentLiquidType == LiquidType.DirtyWater)
                {
                    consumableItem.currentLiquidType = LiquidType.CleanWater;
                    consumableItem.UpdateVisuals();
                }

                // Варка пельменей: идет только при кипящей воде (100 градусов)
                if (hasDumplings && !areDumplingsCooked)
                {
                    currentDumplingsCookProgressMinutes += elapsedGameMinutes;
                    if (currentDumplingsCookProgressMinutes >= dumplingsCookDurationInGameMinutes)
                    {
                        areDumplingsCooked = true;
                        UpdateDumplingVisuals(); // Обновляем крышку
                    }
                }
            }
        }
        else
        {
            // Остывание кастрюли до 0 градусов
            float coolingSpeed = 100f / coolingDurationInGameMinutes;
            currentTemperature = Mathf.Max(currentTemperature - coolingSpeed * elapsedGameMinutes, 0f);
        }

        // Обновляем эффекты пара и звука на основе температуры и состояния пельменей
        UpdateBoilingEffects();
    }

    private void UpdateBoilingEffects()
    {
        // Порог температуры, когда начинает идти пар
        float steamStartTemperature = (steamStartInGameMinutes / boilDurationInGameMinutes) * 100f;

        // Если пельмени сварились, пар и бурление принудительно отключаются по ТЗ
        bool shouldSteam = currentTemperature >= steamStartTemperature && !areDumplingsCooked;

        if (shouldSteam)
        {
            if (steamParticles != null && !steamParticles.isPlaying)
            {
                steamParticles.Play();
            }
            if (boilingLoopSound != null && !boilingLoopSound.isPlaying)
            {
                boilingLoopSound.loop = true;
                boilingLoopSound.Play();
            }
            isBoiling = true;
        }
        else
        {
            if (steamParticles != null && steamParticles.isPlaying)
            {
                steamParticles.Stop();
            }
            if (boilingLoopSound != null && boilingLoopSound.isPlaying)
            {
                boilingLoopSound.Stop();
            }
            isBoiling = false;
        }
    }

    // Проверяем, можно ли положить пельмени из рук
    public bool CanAcceptDumplings(int itemID, out string meatType)
    {
        meatType = "";

        // 1. Проверяем, есть ли чистая вода в кастрюле
        if (consumableItem == null || 
            consumableItem.currentLiquidType != LiquidType.CleanWater || 
            consumableItem.currentAmount <= 0)
        {
            return false;
        }

        // 2. Если пельмени уже положены, новые не принимаем
        if (hasDumplings)
        {
            return false;
        }

        // 3. Ищем предмет в маппинге пачек пельменей
        if (logoMappings != null)
        {
            foreach (var mapping in logoMappings)
            {
                if (mapping.dumplingPackItemID == itemID)
                {
                    meatType = mapping.meatType;
                    return true;
                }
            }
        }

        return false;
    }

    // Добавление пельменей в кастрюлю
    public void AddDumplings(string meatType)
    {
        currentDumplingType = meatType;
        hasDumplings = true;
        UpdateDumplingVisuals();
    }

    // Дополнительные API для игрока
    public bool HasAnyDumplings()
    {
        return hasDumplings;
    }

    public bool AreDumplingsCooked()
    {
        return hasDumplings && areDumplingsCooked;
    }

    // Перемещение готовых пельменей в тарелку (вызывается из PlayerInteract)
    public void PlateDumplings()
    {
        if (!AreDumplingsCooked()) return;

        // 1. Ищем готовый ID предмета по типу мяса пельменей
        int cookedItemID = -1;
        if (cookedDumplingsMappings != null)
        {
            foreach (var mapping in cookedDumplingsMappings)
            {
                if (mapping.meatType == currentDumplingType)
                {
                    cookedItemID = mapping.cookedItemID;
                    break;
                }
            }
        }

        if (cookedItemID != -1)
        {
            // 2. Спавним готовые пельмени в инвентаре/руках игрока
            SpawnCookedDumplingsInInventory(cookedItemID);
        }
        else
        {
            Debug.LogError($"[BoilingPot] Не найден маппинг готовых пельменей для типа мяса {currentDumplingType}!");
        }

        // 3. Сбрасываем состояние пельменей в кастрюле
        hasDumplings = false;
        areDumplingsCooked = false;
        currentDumplingsCookProgressMinutes = 0f;
        currentDumplingType = "";

        // Очищаем воду из кастрюли
        if (consumableItem != null)
        {
            consumableItem.currentAmount = 0;
            consumableItem.currentLiquidType = LiquidType.None;
            consumableItem.UpdateVisuals();
        }

        // 4. Обновляем визуал (скрываем пельмени, логотипы, крышку)
        UpdateDumplingVisuals();
    }

    private void SpawnCookedDumplingsInInventory(int cookedItemID)
    {
        if (InventoryManager.Instance == null) return;

        GameObject prefab = InventoryManager.Instance.GetPrefabByID(cookedItemID);
        if (prefab == null)
        {
            Debug.LogError($"[BoilingPot] Префаб готовых пельменей с ID {cookedItemID} не найден в базе предметов!");
            return;
        }

        // Временно спавним неактивный объект для считывания InventoryItemData
        GameObject tempObj = Instantiate(prefab);
        tempObj.SetActive(false);

        PickUpItem pickup = tempObj.GetComponent<PickUpItem>();
        if (pickup == null) pickup = tempObj.GetComponentInChildren<PickUpItem>();

        if (pickup != null)
        {
            InventoryItemData data = new InventoryItemData(pickup);
            bool success = InventoryManager.Instance.AddItem(data);
            if (success)
            {
                Debug.Log($"[BoilingPot] Сваренные пельмени {pickup.itemName} успешно добавлены в инвентарь!");
            }
            else
            {
                // Если инвентарь полон, выбрасываем готовые пельмени на пол перед игроком
                Debug.LogWarning("[BoilingPot] Инвентарь полон, выбрасываем готовые пельмени на пол!");
                InventoryManager.Instance.SpawnDroppedItem(data);
            }
        }

        Destroy(tempObj);
    }

    private void UpdateDumplingVisuals()
    {
        if (dumplingsMesh != null) dumplingsMesh.SetActive(hasDumplings);
        if (logoRenderer1 != null) logoRenderer1.gameObject.SetActive(hasDumplings);
        if (logoRenderer2 != null) logoRenderer2.gameObject.SetActive(hasDumplings);

        // Крышка включается принудительно, когда пельмени полностью сварились
        if (lidMesh != null)
        {
            lidMesh.SetActive(areDumplingsCooked);
        }

        if (hasDumplings)
        {
            Material targetMaterial = null;
            if (logoMappings != null)
            {
                foreach (var mapping in logoMappings)
                {
                    if (mapping.meatType == currentDumplingType)
                    {
                        targetMaterial = mapping.logoMaterial;
                        break;
                    }
                }
            }

            if (targetMaterial != null)
            {
                if (logoRenderer1 != null) logoRenderer1.material = targetMaterial;
                if (logoRenderer2 != null) logoRenderer2.material = targetMaterial;
            }
        }
    }

    private StoveKnob GetActiveBurnerItIsPlacedOn()
    {
        if (cachedKnobs == null || cachedKnobs.Length == 0) return null;

        foreach (var knob in cachedKnobs)
        {
            if (knob == null || knob.burnerPlacement == null) continue;

            // Проверяем, включен ли газ на конфорке
            if (!knob.isOn) continue;

            // Проверяем электричество/питание, если оно требуется
            if (knob.requiresPower)
            {
                bool isPowerOk = knob.targetGenerator != null ? knob.targetGenerator.isWorking : GeneratorController.IsGeneratorWorking;
                if (!isPowerOk) continue;
            }

            // Проверяем физическое нахождение кастрюли на точке размещения этой конфорки
            if (IsPotOnPlacementPoint(knob.burnerPlacement))
            {
                return knob;
            }
        }
        return null;
    }

    private bool IsPotOnPlacementPoint(PlacementPoint pp)
    {
        int count = Physics.OverlapSphereNonAlloc(pp.transform.position, 0.4f, overlapResults, Physics.AllLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider c = overlapResults[i];
            if (c != null && (c.transform == transform || c.transform.IsChildOf(transform)))
            {
                return true;
            }
        }
        return false;
    }
}
