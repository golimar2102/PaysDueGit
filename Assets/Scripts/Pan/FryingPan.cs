using UnityEngine;

[System.Serializable]
public struct DumplingPanMapping
{
    [Tooltip("Тип мяса пельменя (например, Beef, Pork, Canine, Feline, Avian)")]
    public string meatType;
    [Tooltip("ID предмета пачки пельменей в инвентаре")]
    public int dumplingPackItemID;
}

[System.Serializable]
public struct CookedPanDumplingsMapping
{
    [Tooltip("Тип мяса пельменя")]
    public string meatType;
    [Tooltip("ID предмета готовых пельменей (в миске)")]
    public int cookedItemID;
}

public class FryingPan : MonoBehaviour
{
    [Header("Настройки времени жарки")]
    [Tooltip("Время нагрева сковороды до рабочей температуры (в игровых минутах)")]
    public float heatDurationInGameMinutes = 5f;
    [Tooltip("Время жарки пельменей (в игровых минутах)")]
    public float cookDurationInGameMinutes = 10f;
    [Tooltip("Время остывания сковороды (в игровых минутах)")]
    public float coolingDurationInGameMinutes = 10f;

    [Header("Настройки пельменей и визуалов")]
    [Tooltip("Объект пельменей внутри сковороды (Mesh)")]
    public GameObject dumplingsMesh;
    [Tooltip("Рендерер пельменей для смены текстуры")]
    public Renderer dumplingsRenderer;
    [Tooltip("Материал для сырых пельменей")]
    public Material rawMaterial;
    [Tooltip("Материал для готовых пельменей")]
    public Material cookedMaterial;

    [Header("Настройки миски и готовых блюд")]
    [Tooltip("ID предмета миски в инвентаре")]
    public int bowlItemID = 60;
    [Tooltip("Маппинг типов пельменей (ID пачек)")]
    public DumplingPanMapping[] dumplingMappings;
    [Tooltip("Маппинг типов сырых пельменей на ID готовых в миске")]
    public CookedPanDumplingsMapping[] cookedDumplingsMappings;

    [Header("Эффекты жарки")]
    [Tooltip("Эффект дыма/пара при жарке")]
    public ParticleSystem fryParticles;
    [Tooltip("Звук шипения масла (Loop)")]
    public AudioSource fryingLoopSound;

    [Header("Состояние (Информативно)")]
    [SerializeField] private float currentTemperature = 0f;
    [SerializeField] private bool isFrying = false;
    [SerializeField] private bool hasDumplings = false;
    [SerializeField] private string currentDumplingType = "";
    [SerializeField] private float currentCookProgressMinutes = 0f;
    [SerializeField] private bool areDumplingsCooked = false;

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
        pickUpItem = GetComponent<PickUpItem>();
    }

    void Start()
    {
        // Кэшируем ручки плиты один раз при старте
        cachedKnobs = FindObjectsByType<StoveKnob>(FindObjectsSortMode.None);

        // При старте выключаем пар и звук
        if (fryParticles != null) fryParticles.Stop();
        if (fryingLoopSound != null) fryingLoopSound.Stop();

        UpdateDumplingVisuals();
    }

    void Update()
    {
        // Ищем активную конфорку с интервалом
        burnerCheckTimer -= Time.deltaTime;
        if (burnerCheckTimer <= 0f)
        {
            burnerCheckTimer = BURNER_CHECK_INTERVAL;
            activeBurner = GetActiveBurnerItIsPlacedOn();
        }

        bool isHeating = activeBurner != null;

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
            // Нагрев сковороды
            float heatingSpeed = 100f / heatDurationInGameMinutes;
            currentTemperature = Mathf.Min(currentTemperature + heatingSpeed * elapsedGameMinutes, 100f);

            // Жарка пельменей: идет только при горячей сковороде (100 градусов)
            if (currentTemperature >= 100f)
            {
                if (hasDumplings && !areDumplingsCooked)
                {
                    currentCookProgressMinutes += elapsedGameMinutes;
                    if (currentCookProgressMinutes >= cookDurationInGameMinutes)
                    {
                        areDumplingsCooked = true;
                        UpdateDumplingVisuals();
                    }
                }
            }
        }
        else
        {
            // Остывание сковороды
            float coolingSpeed = 100f / coolingDurationInGameMinutes;
            currentTemperature = Mathf.Max(currentTemperature - coolingSpeed * elapsedGameMinutes, 0f);
        }

        UpdateFryingEffects();
    }

    private void UpdateFryingEffects()
    {
        // Эффекты шипения активны, когда сковорода горячая (>50 градусов), в ней лежат пельмени и они еще не сгорели/не готовы (или готовы, но пока не убраны).
        // По ТЗ BoilingPot: пар и кипение принудительно отключаются, когда пельмени сварились.
        // Сделаем так же для жарки:
        bool shouldFryEffects = currentTemperature >= 50f && hasDumplings && !areDumplingsCooked;

        if (shouldFryEffects)
        {
            if (fryParticles != null && !fryParticles.isPlaying)
            {
                fryParticles.Play();
            }
            if (fryingLoopSound != null && !fryingLoopSound.isPlaying)
            {
                fryingLoopSound.loop = true;
                fryingLoopSound.Play();
            }
            isFrying = true;
        }
        else
        {
            if (fryParticles != null && fryParticles.isPlaying)
            {
                fryParticles.Stop();
            }
            if (fryingLoopSound != null && fryingLoopSound.isPlaying)
            {
                fryingLoopSound.Stop();
            }
            isFrying = false;
        }
    }

    // Проверяем, можно ли положить пельмени из рук
    public bool CanAcceptDumplings(int itemID, out string meatType)
    {
        meatType = "";

        // Если пельмени уже положены, новые не принимаем
        if (hasDumplings)
        {
            return false;
        }

        // Ищем предмет в маппинге пачек пельменей
        if (dumplingMappings != null)
        {
            foreach (var mapping in dumplingMappings)
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

    // Добавление пельменей в сковороду
    public void AddDumplings(string meatType)
    {
        currentDumplingType = meatType;
        hasDumplings = true;
        areDumplingsCooked = false;
        currentCookProgressMinutes = 0f;
        UpdateDumplingVisuals();
    }

    public bool HasAnyDumplings()
    {
        return hasDumplings;
    }

    public bool AreDumplingsCooked()
    {
        return hasDumplings && areDumplingsCooked;
    }

    // Перемещение готовых пельменей в миску
    public void PlateDumplings()
    {
        if (!AreDumplingsCooked()) return;

        // 1. Ищем готовый ID предмета по типу мяса
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
            // 2. Спавним готовые пельмени в инвентаре/руках
            SpawnCookedDumplingsInInventory(cookedItemID);
        }
        else
        {
            Debug.LogError($"[FryingPan] Не найден маппинг готовых пельменей для типа мяса {currentDumplingType}!");
        }

        // 3. Сбрасываем состояние пельменей в сковороде
        hasDumplings = false;
        areDumplingsCooked = false;
        currentCookProgressMinutes = 0f;
        currentDumplingType = "";

        // 4. Обновляем визуал (скрываем/меняем)
        UpdateDumplingVisuals();
    }

    private void SpawnCookedDumplingsInInventory(int cookedItemID)
    {
        if (InventoryManager.Instance == null) return;

        GameObject prefab = InventoryManager.Instance.GetPrefabByID(cookedItemID);
        if (prefab == null)
        {
            Debug.LogError($"[FryingPan] Префаб готовых пельменей с ID {cookedItemID} не найден в базе предметов!");
            return;
        }

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
                Debug.Log($"[FryingPan] Пожаренные пельмени {pickup.itemName} успешно добавлены в инвентарь!");
            }
            else
            {
                // Если инвентарь полон, выбрасываем готовые пельмени на пол перед игроком
                Debug.LogWarning("[FryingPan] Инвентарь полон, выбрасываем готовые пельмени на пол!");
                InventoryManager.Instance.SpawnDroppedItem(data);
            }
        }

        Destroy(tempObj);
    }

    private void UpdateDumplingVisuals()
    {
        if (dumplingsMesh != null) dumplingsMesh.SetActive(hasDumplings);

        if (hasDumplings && dumplingsRenderer != null)
        {
            dumplingsRenderer.material = areDumplingsCooked ? cookedMaterial : rawMaterial;
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

            // Проверяем электричество/питание
            if (knob.requiresPower)
            {
                bool isPowerOk = knob.targetGenerator != null ? knob.targetGenerator.isWorking : GeneratorController.IsGeneratorWorking;
                if (!isPowerOk) continue;
            }

            // Проверяем физическое нахождение сковороды на точке размещения конфорки
            if (IsPanOnPlacementPoint(knob.burnerPlacement))
            {
                return knob;
            }
        }
        return null;
    }

    private bool IsPanOnPlacementPoint(PlacementPoint pp)
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
