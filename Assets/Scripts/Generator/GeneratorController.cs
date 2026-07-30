using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GeneratorController : MonoBehaviour
{
    public static GeneratorController Instance;
    public static List<GeneratorController> AllGenerators = new List<GeneratorController>();

    [Header("Состояние генератора")]
    [Tooltip("Флаг активности генератора (может читаться другими скриптами)")]
    public bool isWorking = false;
    
    [Tooltip("Текущий уровень топлива в генераторе")]
    public float currentFuel = 0f;
    [Tooltip("Максимальный объем топлива в генераторе")]
    public float maxFuel = 100f;

    [Header("Топливный отсек")]
    [Tooltip("Ссылка на скрипт трубы заправки")]
    public GeneratorFuelPipe fuelPipe;

    [Tooltip("3D модель канистры у трубы, появляющаяся при заправке")]
    public GameObject jerrycan3DModel;
    [Tooltip("Объект-жидкость в сосуде, который будет масштабироваться")]
    public Transform fuelVisualObject;
    [Tooltip("Максимальный масштаб жидкости при полном заполнении")]
    public float maxFuelVisualScale = 1f;
    public enum ScaleAxis { X, Y, Z }
    [Tooltip("Ось масштабирования жидкости")]
    public ScaleAxis fuelScaleAxis = ScaleAxis.Y;
    
    [Tooltip("Скорость перелива топлива (единиц в секунду)")]
    public float fuelPourRate = 10f;
    [Tooltip("Скорость расхода топлива при работе генератора (единиц в секунду)")]
    public float fuelConsumptionRate = 1f;

    [Header("Данные установленной канистры")]
    [Tooltip("Внутренние данные установленной в трубу канистры")]
    public InventoryItemData installedCanisterData;

    [Header("Отсек свитчей")]
    [Tooltip("Массив свитчей (все должны быть включены для запуска)")]
    public GeneratorSwitch[] switches;

    [Header("Отсек рубильника и предохранителей")]
    [Tooltip("5 3D-держателей предохранителей")]
    public GeneratorFuseHolder[] fuseHolders;
    [Tooltip("Трансформ рычага рубильника")]
    public Transform leverTransform;
    [Tooltip("Локальный поворот рычага в режиме ON")]
    public Vector3 leverOnAngle = new Vector3(-45f, 0f, 0f);
    [Tooltip("Локальный поворот рычага в режиме OFF")]
    public Vector3 leverOffAngle = new Vector3(45f, 0f, 0f);
    [Tooltip("Время анимации переключения рычага")]
    public float leverToggleDuration = 0.2f;

    [Header("Настройки предохранителей")]
    [Tooltip("ID обычного предохранителя")]
    public int regularFuseItemID = 100;
    [Tooltip("ID улучшенного предохранителя")]
    public int advancedFuseItemID = 101;
    [Tooltip("ID третьего (особого) предохранителя")]
    public int thirdFuseItemID = 102;
    [Tooltip("Скорость износа прочности обычного предохранителя в секунду")]
    public float regularFuseWearRate = 2f;
    [Tooltip("Скорость износа прочности улучшенного предохранителя в секунду")]
    public float advancedFuseWearRate = 0.5f;
    [Tooltip("Скорость износа прочности третьего предохранителя в секунду")]
    public float thirdFuseWearRate = 0.1f;

    [Header("Эффекты и Звуки")]
    [Tooltip("Эффект взрыва при сгорании предохранителя")]
    public ParticleSystem fuseExplosionEffect;
    [Tooltip("Звук взрыва")]
    public AudioSource explosionSound;
    [Tooltip("Звук переключения рычага")]
    public AudioSource leverSound;
    [Tooltip("Звук неудачи при запуске (рычаг отскакивает назад)")]
    public AudioSource failSound;
    [Tooltip("Звук работы генератора (должен быть зациклен)")]
    public AudioSource generatorLoopSound;
    [Tooltip("Звук перелива топлива (должен быть зациклен)")]
    public AudioSource pourSound;

    // Внутренние переменные
    private Coroutine leverCoroutine;
    private bool isPouring = false;
    private float fuseDurabilityAccumulator = 0f;
    private Vector3 initialFuelScale;
    private ConsumableItem canisterConsumable;

    // Ссылка на активный перетаскиваемый 3D предохранитель
    public static GeneratorFuseHolder activeDraggedHolder = null;

    public static bool IsGeneratorWorking
    {
        get { return Instance != null && Instance.isWorking; }
    }

    /// <summary>
    /// Проверяет, установлена ли канистра в генератор (не null и имеет валидный ID).
    /// </summary>
    public bool hasCanister
    {
        get { return installedCanisterData != null && installedCanisterData.itemID != 0; }
    }

    void Awake()
    {
        if (!AllGenerators.Contains(this))
        {
            AllGenerators.Add(this);
        }

        if (Instance == null)
        {
            Instance = this;
        }

        if (fuelVisualObject != null)
        {
            initialFuelScale = fuelVisualObject.localScale;
        }
    }

    void OnDestroy()
    {
        if (AllGenerators.Contains(this))
        {
            AllGenerators.Remove(this);
        }
        if (Instance == this)
        {
            Instance = AllGenerators.Count > 0 ? AllGenerators[0] : null;
        }
    }

    void Start()
    {
        // Автоматический поиск AudioSource, если они не заданы в инспекторе
        AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
        if (pourSound == null)
        {
            foreach (var src in sources)
            {
                string nameLower = src.gameObject.name.ToLower();
                if (nameLower.Contains("pour") || nameLower.Contains("fill") || nameLower.Contains("liquid") || nameLower.Contains("fuel") || nameLower.Contains("canister"))
                {
                    pourSound = src;
                    Debug.Log($"[GeneratorController] Auto-assigned pourSound to child AudioSource: {src.gameObject.name}");
                    break;
                }
            }
            if (pourSound == null)
            {
                foreach (var src in sources)
                {
                    if (src != generatorLoopSound && src != explosionSound && src != leverSound && src != failSound)
                    {
                        pourSound = src;
                        Debug.Log($"[GeneratorController] Fallback auto-assigned pourSound to: {src.gameObject.name}");
                        break;
                    }
                }
            }
        }

        if (generatorLoopSound == null)
        {
            foreach (var src in sources)
            {
                string nameLower = src.gameObject.name.ToLower();
                if (nameLower.Contains("loop") || nameLower.Contains("work") || nameLower.Contains("engine") || nameLower.Contains("generator") || nameLower.Contains("run") || nameLower.Contains("idle"))
                {
                    generatorLoopSound = src;
                    Debug.Log($"[GeneratorController] Auto-assigned generatorLoopSound to child AudioSource: {src.gameObject.name}");
                    break;
                }
            }
            if (generatorLoopSound == null)
            {
                foreach (var src in sources)
                {
                    if (src != pourSound && src != explosionSound && src != leverSound && src != failSound)
                    {
                        generatorLoopSound = src;
                        Debug.Log($"[GeneratorController] Fallback auto-assigned generatorLoopSound to: {src.gameObject.name}");
                        break;
                    }
                }
            }
        }

        // Принудительно включаем зацикливание и активируем AudioSource
        if (pourSound != null)
        {
            pourSound.gameObject.SetActive(true);
            pourSound.enabled = true;
            pourSound.loop = true;
        }

        if (generatorLoopSound != null)
        {
            generatorLoopSound.gameObject.SetActive(true);
            generatorLoopSound.enabled = true;
            generatorLoopSound.loop = true;
        }

        // Запускаем звук работы, если генератор запущен со старта
        if (isWorking && generatorLoopSound != null && !generatorLoopSound.isPlaying)
        {
            generatorLoopSound.Play();
        }

        // Инициализируем положение рычага
        if (leverTransform != null)
        {
            leverTransform.localRotation = Quaternion.Euler(isWorking ? leverOnAngle : leverOffAngle);
        }

        if (jerrycan3DModel != null)
        {
            jerrycan3DModel.SetActive(hasCanister);
            if (hasCanister)
            {
                canisterConsumable = jerrycan3DModel.GetComponent<ConsumableItem>();
                if (canisterConsumable == null)
                {
                    canisterConsumable = jerrycan3DModel.GetComponentInChildren<ConsumableItem>(true);
                }
                if (canisterConsumable != null)
                {
                    canisterConsumable.currentLiquidType = installedCanisterData.currentLiquidType;
                    canisterConsumable.currentAmount = installedCanisterData.currentAmount;
                    canisterConsumable.maxAmount = installedCanisterData.maxAmount;
                    canisterConsumable.UpdateVisuals();
                }
            }
        }

        UpdateFuelVisual();
    }

    void Update()
    {
        HandleFuelPouring();

        if (isWorking)
        {
            TickGenerator();
        }

        UpdateFuelVisual();
    }

    /// <summary>
    /// Проверяет, является ли указанный ID предмета предохранителем.
    /// </summary>
    public static bool IsFuseItem(int itemID)
    {
        return Instance != null && (
            itemID == Instance.regularFuseItemID || 
            itemID == Instance.advancedFuseItemID || 
            itemID == Instance.thirdFuseItemID
        );
    }

    /// <summary>
    /// Проверяет, является ли предмет канистрой с топливом (в данном случае Blood).
    /// </summary>
    public static bool IsCanisterItem(InventoryItemData data)
    {
        if (data == null) return false;
        // Канистра с жидкостью Blood и количеством больше 0
        return data.isConsumable && data.currentLiquidType == LiquidType.Blood && data.currentAmount > 0;
    }

    /// <summary>
    /// Включает подсветку трубы заправки (вызывается при начале драга канистры).
    /// </summary>
    public void OnCanisterDragStarted()
    {
        if (fuelPipe != null && fuelPipe.isEmpty)
        {
            fuelPipe.SetHighlight(true);
        }
    }

    /// <summary>
    /// Выключает подсветку трубы заправки.
    /// </summary>
    public void OnCanisterDragEnded()
    {
        if (fuelPipe != null)
        {
            fuelPipe.SetHighlight(false);
        }
    }

    public static void OnCanisterDragStartedAll()
    {
        foreach (var gen in AllGenerators)
        {
            if (gen != null) gen.OnCanisterDragStarted();
        }
    }

    public static void OnCanisterDragEndedAll()
    {
        foreach (var gen in AllGenerators)
        {
            if (gen != null) gen.OnCanisterDragEnded();
        }
    }

    /// <summary>
    /// Устанавливает канистру в трубу.
    /// </summary>
    public void InsertCanister(InventoryItemData canisterData)
    {
        installedCanisterData = canisterData;
        if (jerrycan3DModel != null)
        {
            jerrycan3DModel.SetActive(true);
            
            // Получаем компонент ConsumableItem с 3D модели канистры
            canisterConsumable = jerrycan3DModel.GetComponent<ConsumableItem>();
            if (canisterConsumable == null)
            {
                canisterConsumable = jerrycan3DModel.GetComponentInChildren<ConsumableItem>(true);
            }
            
            if (canisterConsumable != null)
            {
                canisterConsumable.currentLiquidType = canisterData.currentLiquidType;
                canisterConsumable.currentAmount = canisterData.currentAmount;
                canisterConsumable.maxAmount = canisterData.maxAmount;
                canisterConsumable.UpdateVisuals();
            }
        }
        Debug.Log("Канистра установлена в трубу генератора.");
    }

    /// <summary>
    /// Извлекает канистру из трубы.
    /// </summary>
    public InventoryItemData ExtractCanister()
    {
        if (!hasCanister) return null;

        InventoryItemData extractedData = installedCanisterData;
        installedCanisterData = null;

        if (jerrycan3DModel != null)
        {
            jerrycan3DModel.SetActive(false);
        }

        canisterConsumable = null;

        Debug.Log("Канистра извлечена из трубы генератора.");
        return extractedData;
    }

    /// <summary>
    /// Автоматически возвращает канистру в инвентарь игрока (или выбрасывает на землю, если инвентарь полон).
    /// </summary>
    public void ReturnCanisterToPlayer()
    {
        if (!hasCanister) return;

        InventoryItemData canisterData = ExtractCanister();
        if (canisterData != null)
        {
            if (InventoryManager.Instance != null)
            {
                bool added = InventoryManager.Instance.AddItem(canisterData);
                if (!added)
                {
                    InventoryManager.Instance.SpawnDroppedItem(canisterData);
                }
            }
            else
            {
                Debug.LogWarning("[GeneratorController] InventoryManager.Instance is null. Canister lost!");
            }
        }
    }

    /// <summary>
    /// Включает подсветку всех пустых держателей (вызывается при начале драга предохранителя).
    /// </summary>
    public void OnFuseDragStarted()
    {
        if (fuseHolders == null) return;
        foreach (var holder in fuseHolders)
        {
            if (holder != null && holder.isEmpty)
            {
                holder.SetHighlight(true);
            }
        }
    }

    /// <summary>
    /// Выключает подсветку держателей.
    /// </summary>
    public void OnFuseDragEnded()
    {
        if (fuseHolders == null) return;
        foreach (var holder in fuseHolders)
        {
            if (holder != null)
            {
                holder.SetHighlight(false);
            }
        }
    }

    public static void OnFuseDragStartedAll()
    {
        foreach (var gen in AllGenerators)
        {
            if (gen != null) gen.OnFuseDragStarted();
        }
    }

    public static void OnFuseDragEndedAll()
    {
        foreach (var gen in AllGenerators)
        {
            if (gen != null) gen.OnFuseDragEnded();
        }
    }

    public void OnFuseInserted(int index)
    {
        Debug.Log($"Предохранитель установлен в слот {index}");
    }

    public void OnFuseExtracted(int index)
    {
        Debug.Log($"Предохранитель извлечен из слота {index}");
        // Если вытащили предохранитель во время работы и не осталось активных - выключаемся
        if (isWorking && GetActiveFuseHolder() == null)
        {
            ShutdownGenerator();
        }
    }

    /// <summary>
    /// Находит первый заполненный держатель предохранителя.
    /// </summary>
    public GeneratorFuseHolder GetActiveFuseHolder()
    {
        if (fuseHolders == null) return null;
        for (int i = 0; i < fuseHolders.Length; i++)
        {
            if (fuseHolders[i] != null && !fuseHolders[i].isEmpty && fuseHolders[i].installedFuseData != null)
            {
                return fuseHolders[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Проверяет, переведены ли все тумблеры во 2-м отсеке в состояние ON.
    /// </summary>
    public bool AreAllSwitchesOn()
    {
        if (switches == null || switches.Length == 0) return true;
        foreach (var sw in switches)
        {
            if (sw != null && !sw.isOn) return false;
        }
        return true;
    }

    /// <summary>
    /// Случайным образом переключает тумблеры (вызывается при отключении генератора).
    /// </summary>
    public void RandomizeSwitches()
    {
        if (switches == null) return;
        foreach (var sw in switches)
        {
            if (sw != null)
            {
                sw.SetState(Random.value > 0.5f);
            }
        }
    }

    /// <summary>
    /// Логика перелива топлива из канистры, установленной в 3D трубу.
    /// </summary>
    private void HandleFuelPouring()
    {
        bool shouldPour = false;

        if (hasCanister)
        {
            InventoryItemData data = installedCanisterData;
            
            // Проверяем тип жидкости: должен быть Blood (кровь)
            bool hasCorrectLiquid = data.currentLiquidType == LiquidType.Blood;

            // Проверяем, что в канистре есть топливо, и генератор не заполнен полностью
            if (hasCorrectLiquid && data.currentAmount > 0 && currentFuel < maxFuel)
            {
                shouldPour = true;
            }
        }

        if (shouldPour)
        {
            if (!isPouring)
            {
                isPouring = true;
                if (pourSound != null && !pourSound.isPlaying) pourSound.Play();
            }

            // Переливаем топливо
            float transfer = fuelPourRate * Time.deltaTime;
            
            InventoryItemData canisterData = installedCanisterData;
            
            // Не переливаем больше, чем есть в канистре или нужно генератору
            float actualTransfer = Mathf.Min(transfer, canisterData.currentAmount);
            actualTransfer = Mathf.Min(actualTransfer, maxFuel - currentFuel);

            currentFuel += actualTransfer;
            
            // Вычитаем из канистры (currentAmount в канистре целочисленный, накапливаем изменения)
            // Чтобы избежать потери долей при deltaTime, используем промежуточное вычитание
            canisterFloatAccumulator += actualTransfer;
            if (canisterFloatAccumulator >= 1f)
            {
                int intTransfer = Mathf.FloorToInt(canisterFloatAccumulator);
                canisterFloatAccumulator -= intTransfer;
                canisterData.currentAmount -= intTransfer;
                
                if (canisterData.currentAmount <= 0)
                {
                    canisterData.currentAmount = 0;
                    
                    // Полностью опустошаем канистру
                    canisterData.currentLiquidType = LiquidType.None;
                    canisterData.itemName = canisterData.baseItemName;
                    
                    GameObject prefab = InventoryManager.Instance != null ? InventoryManager.Instance.GetPrefabByID(canisterData.itemID) : null;
                    if (prefab != null)
                    {
                        ConsumableItem cons = prefab.GetComponent<ConsumableItem>();
                        if (cons != null)
                        {
                            Sprite[] emptyIcons = cons.GetFillIconsForLiquid(LiquidType.None);
                            if (emptyIcons != null && emptyIcons.Length > 0)
                            {
                                canisterData.itemIcon = emptyIcons[0];
                                canisterData.fillIcons = emptyIcons;
                            }
                            else
                            {
                                canisterData.itemIcon = cons.fillIcons != null && cons.fillIcons.Length > 0 ? cons.fillIcons[0] : prefab.GetComponent<PickUpItem>().itemIcon;
                                canisterData.fillIcons = cons.fillIcons;
                            }
                        }
                    }
                }
                else
                {
                    // Обновляем визуальное отображение канистры (иконки заполненности)
                    if (canisterData.fillIcons != null && canisterData.fillIcons.Length > 0)
                    {
                        float fillPct = Mathf.Clamp01((float)canisterData.currentAmount / canisterData.maxAmount);
                        int idx = Mathf.RoundToInt(fillPct * (canisterData.fillIcons.Length - 1));
                        canisterData.itemIcon = canisterData.fillIcons[idx];
                    }
                }

                // Обновляем 3D модель канистры
                if (canisterConsumable != null)
                {
                    canisterConsumable.currentAmount = canisterData.currentAmount;
                    canisterConsumable.currentLiquidType = canisterData.currentLiquidType;
                    canisterConsumable.UpdateVisuals();
                }
            }

            UpdateFuelVisual();
        }
        else
        {
            if (isPouring)
            {
                isPouring = false;
                canisterFloatAccumulator = 0f;
                if (pourSound != null && pourSound.isPlaying) pourSound.Stop();
            }
        }

        // Автоматический возврат канистры игроку, если она опустела или бак заполнен
        if (hasCanister)
        {
            bool canisterEmpty = installedCanisterData.currentAmount <= 0;
            bool tankFull = currentFuel >= maxFuel;

            if (canisterEmpty || tankFull)
            {
                if (isPouring)
                {
                    isPouring = false;
                    canisterFloatAccumulator = 0f;
                    if (pourSound != null && pourSound.isPlaying) pourSound.Stop();
                }
                ReturnCanisterToPlayer();
            }
        }
    }

    private float canisterFloatAccumulator = 0f;

    private void UpdateFuelVisual()
    {
        if (fuelVisualObject == null) return;

        float fillPercentage = Mathf.Clamp01(currentFuel / maxFuel);
        Vector3 newScale = initialFuelScale;

        switch (fuelScaleAxis)
        {
            case ScaleAxis.X:
                newScale.x = initialFuelScale.x * fillPercentage * maxFuelVisualScale;
                break;
            case ScaleAxis.Y:
                newScale.y = initialFuelScale.y * fillPercentage * maxFuelVisualScale;
                break;
            case ScaleAxis.Z:
                newScale.z = initialFuelScale.z * fillPercentage * maxFuelVisualScale;
                break;
        }

        if (fillPercentage <= 0.005f)
        {
            newScale = Vector3.zero;
        }

        fuelVisualObject.localScale = newScale;
    }

    /// <summary>
    /// Логика расхода топлива и износа предохранителей при работе генератора.
    /// </summary>
    private void TickGenerator()
    {
        // Если хотя бы один тумблер выключен, глушим генератор
        if (!AreAllSwitchesOn())
        {
            ShutdownGenerator();
            return;
        }

        // Расход топлива
        currentFuel -= fuelConsumptionRate * Time.deltaTime;
        if (currentFuel <= 0f)
        {
            currentFuel = 0f;
            UpdateFuelVisual();
            ShutdownGenerator();
            return;
        }
        UpdateFuelVisual();

        // Износ активного предохранителя
        GeneratorFuseHolder activeHolder = GetActiveFuseHolder();
        if (activeHolder == null)
        {
            ShutdownGenerator();
            return;
        }

        float wearRate = regularFuseWearRate;
        if (activeHolder.installedFuseData.itemID == advancedFuseItemID)
        {
            wearRate = advancedFuseWearRate;
        }
        else if (activeHolder.installedFuseData.itemID == thirdFuseItemID)
        {
            wearRate = thirdFuseWearRate;
        }
        
        fuseDurabilityAccumulator += wearRate * Time.deltaTime;
        if (fuseDurabilityAccumulator >= 1f)
        {
            int wearAmount = Mathf.FloorToInt(fuseDurabilityAccumulator);
            fuseDurabilityAccumulator -= wearAmount;

            activeHolder.installedFuseData.currentAmount -= wearAmount;
            
            // Если прочность упала до нуля - взрываем
            if (activeHolder.installedFuseData.currentAmount <= 0)
            {
                ExplodeFuse(activeHolder);
            }
        }
    }

    /// <summary>
    /// Взрывает предохранитель при полном износе.
    /// </summary>
    private void ExplodeFuse(GeneratorFuseHolder holder)
    {
        Vector3 explosionPos = holder.transform.position;
        
        if (fuseExplosionEffect != null)
        {
            ParticleSystem effect = Instantiate(fuseExplosionEffect, explosionPos, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 2f);
        }

        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound.clip, explosionPos);
        }

        // Очищаем слот без возврата игроку
        holder.ExtractFuse();
        fuseDurabilityAccumulator = 0f;

        // Пытаемся найти следующий предохранитель
        if (GetActiveFuseHolder() == null)
        {
            ShutdownGenerator(true);
        }
    }

    /// <summary>
    /// Пытается переключить рычаг рубильника (ON/OFF).
    /// </summary>
    public void InteractLever()
    {
        if (isWorking)
        {
            // Просто выключаем
            ShutdownGenerator();
        }
        else
        {
            // Пробуем запустить
            bool canStart = true;

            // 1. Проверяем топливо
            if (currentFuel <= 0f) canStart = false;

            // 2. Проверяем предохранители
            if (GetActiveFuseHolder() == null) canStart = false;

            // 3. Проверяем свитчи
            if (!AreAllSwitchesOn()) canStart = false;

            if (canStart)
            {
                StartGenerator();
            }
            else
            {
                // Проигрываем звук неудачи и дергаем рычаг туда-сюда
                if (failSound != null) failSound.Play();
                if (leverCoroutine != null) StopCoroutine(leverCoroutine);
                leverCoroutine = StartCoroutine(AnimateLeverFail());
            }
        }
    }

    private void StartGenerator()
    {
        isWorking = true;
        if (leverSound != null) leverSound.Play();
        if (generatorLoopSound != null && !generatorLoopSound.isPlaying) generatorLoopSound.Play();

        if (leverCoroutine != null) StopCoroutine(leverCoroutine);
        leverCoroutine = StartCoroutine(AnimateLeverRotation(leverOnAngle));
    }

    private void ShutdownGenerator(bool randomizeSwitches = false)
    {
        isWorking = false;
        if (leverSound != null) leverSound.Play();
        if (generatorLoopSound != null && generatorLoopSound.isPlaying) generatorLoopSound.Stop();

        if (leverCoroutine != null) StopCoroutine(leverCoroutine);
        leverCoroutine = StartCoroutine(AnimateLeverRotation(leverOffAngle));

        // Рандомизируем свитчи при аварийном или ручном выключении
        if (randomizeSwitches)
        {
            RandomizeSwitches();
        }
    }

    private IEnumerator AnimateLeverRotation(Vector3 targetEuler)
    {
        float elapsed = 0f;
        Quaternion startRot = leverTransform.localRotation;
        Quaternion targetRot = Quaternion.Euler(targetEuler);

        while (elapsed < leverToggleDuration)
        {
            elapsed += Time.deltaTime;
            leverTransform.localRotation = Quaternion.Slerp(startRot, targetRot, elapsed / leverToggleDuration);
            yield return null;
        }

        leverTransform.localRotation = targetRot;
    }

    private IEnumerator AnimateLeverFail()
    {
        // Поворачиваем рычаг до середины и возвращаем обратно
        float elapsed = 0f;
        float duration = leverToggleDuration * 0.5f;
        Quaternion startRot = leverTransform.localRotation;
        Quaternion midRot = Quaternion.Euler(Vector3.Lerp(leverOffAngle, leverOnAngle, 0.4f));

        // Вперед
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            leverTransform.localRotation = Quaternion.Slerp(startRot, midRot, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        // Назад
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            leverTransform.localRotation = Quaternion.Slerp(midRot, startRot, elapsed / duration);
            yield return null;
        }

        leverTransform.localRotation = startRot;
    }
}
