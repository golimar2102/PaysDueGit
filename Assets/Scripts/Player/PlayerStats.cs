using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Панель UI статов")]
    [Tooltip("Родительский объект панели статов (будет принудительно включен)")]
    public GameObject statsUIPanel;

    [Header("Здоровье")]
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("Иконка заполнения ХП (Canvas -> HPFill)")]
    public Image hpFill;

    [Header("Голод")]
    public float maxHunger = 100f;
    public float currentHunger;
    [Tooltip("Сколько единиц сытости отнимается за ОДИН ИГРОВОЙ ЧАС")]
    public float hungerPerGameHour = 10f;
    [Tooltip("Иконка заполнения Голода (Canvas -> HungerFill)")]
    public Image hungerFill;

    [Header("Жажда")]
    public float maxThirst = 100f;
    public float currentThirst;
    [Tooltip("Сколько единиц жажды отнимается за ОДИН ИГРОВОЙ ЧАС")]
    public float thirstPerGameHour = 15f;
    [Tooltip("Иконка заполнения Жажды (Canvas -> ThirstFill)")]
    public Image thirstFill;

    [Header("Стамина")]
    public float maxStamina = 100f;
    public float currentStamina;
    [Tooltip("Сколько стамины восстанавливается в секунду")]
    public float staminaRegenRate = 15f;
    [Tooltip("Задержка в секундах перед началом восстановления стамины")]
    public float staminaRegenDelay = 1.0f;
    [Tooltip("Иконка заполнения Стамины (Canvas -> StaminaFill)")]
    public Image staminaFill;
    private float lastStaminaUseTime = 0f;

    [Header("Психика")]
    public float maxSanity = 100f;
    public float currentSanity;
    [Tooltip("Иконка заполнения Психики (Canvas -> SanityFill)")]
    public Image sanityFill;

    [Header("Светочувствительность рассудка")]
    [Tooltip("Скорость уменьшения рассудка на свету (в секунду)")]
    public float sanityDecayRateLight = 0.05f;
    [Tooltip("Скорость уменьшения рассудка в темноте (в секунду)")]
    public float sanityDecayRateDark = 1.0f;
    [Tooltip("Маска слоев препятствий для проверки видимости источников света")]
    public LayerMask lightObstacleMask;
    [Tooltip("Как часто делать лучевые проверки источников света (в секундах)")]
    public float lightCheckInterval = 0.15f;

    [Header("Настройки эффектов безумия (Глюков)")]
    [Tooltip("Порог рассудка, ниже которого начинаются глюки")]
    [Range(0f, 100f)] public float sanityThreshold = 60f;
    [Tooltip("Скорость перехода эффектов (появление/исчезновение)")]
    public float effectTransitionSpeed = 2f;
    [Tooltip("Максимальная сила хроматической аберрации")]
    public float maxChromaticAberration = 0.65f;
    [Tooltip("Максимальное искажение линзы (Lens Distortion)")]
    public float maxLensDistortion = -0.4f;
    [Tooltip("Максимальная интенсивность виньетки")]
    public float maxVignetteIntensity = 0.45f;
    [Tooltip("Цвет виньетки безумия")]
    public Color vignetteColor = new Color(0.45f, 0.02f, 0.02f);
    [Tooltip("Максимальное обесцвечивание (десатурация, от 0 до -100)")]
    public float maxSaturationDecrease = -65f;
    
    [Header("Настройки тряски камеры")]
    [Tooltip("Частота покачивания камеры")]
    public float wobbleFrequency = 5f;
    [Tooltip("Амплитуда покачивания камеры")]
    public float wobbleAmplitude = 0.5f;

    private float nextLightCheckTime = 0f;
    private bool playerIsInLight = true;
    private Light[] cachedLights;
    private float nextLightCacheTime = 0f;

    [Header("Истощение (Голод = 0)")]
    public float starvationDamage = 2f;
    public float starvationTickRate = 2f;
    private float starvationTimer = 0f;

    [Header("Обезвоживание (Жажда = 0)")]
    public float dehydrationDamage = 3f;
    public float dehydrationTickRate = 2f;
    private float dehydrationTimer = 0f;

    [Header("Карма Игрока")]
    [Tooltip("Числовое значение кармы игрока")]
    public float karma = 0f;

    [Header("Имя Игрока")]
    [Tooltip("Имя игрока. Если оставить пустым, подставится имя пользователя Windows при старте.")]
    public string playerName;

    [Header("Монеты")]
    [Tooltip("Количество монет игрока")]
    public int coins = 0;
    [Tooltip("Ссылка на TextMeshProUGUI для отображения количества монет в инвентаре/интерфейсе")]
    public TextMeshProUGUI coinsText;

    // Dirty flags — UpdateUI вызывается только при реальных изменениях
    private bool uiDirty = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = System.Environment.UserName;
        }

        currentHealth = maxHealth;
        currentHunger = maxHunger;
        currentThirst = maxThirst;
        currentStamina = maxStamina;
        currentSanity = maxSanity;

        // Инициализируем маску препятствий для света, если она не задана
        if (lightObstacleMask == 0)
        {
            lightObstacleMask = LayerMask.GetMask("Default", "Ground", "Obstacle", "Wall");
            if (lightObstacleMask == 0)
            {
                lightObstacleMask = ~0; // fallback на все слои
            }
        }

        // Автоматически добавляем эффекты рассудка
        if (GetComponent<PlayerSanityEffects>() == null)
        {
            gameObject.AddComponent<PlayerSanityEffects>();
        }

        if (statsUIPanel != null)
        {
            statsUIPanel.SetActive(true);
        }

        UpdateUI();
    }

    void Update()
    {
        HandleHunger();
        HandleThirst();
        HandleStaminaRegen();
        HandleSanity();

        if (statsUIPanel != null && !statsUIPanel.activeSelf)
        {
            statsUIPanel.SetActive(true);
        }

        if (uiDirty)
        {
            UpdateUI();
            uiDirty = false;
        }
    }

    private void HandleStaminaRegen()
    {
        if (currentStamina < maxStamina && Time.time > lastStaminaUseTime + staminaRegenDelay)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            if (currentStamina > maxStamina) currentStamina = maxStamina;
            uiDirty = true;
        }
    }

    public bool HasStamina(float amount)
    {
        return currentStamina >= amount;
    }

    public void UseStamina(float amount)
    {
        currentStamina -= amount;
        if (currentStamina < 0) currentStamina = 0;
        lastStaminaUseTime = Time.time;
        uiDirty = true;
    }

    private void HandleHunger()
    {
        if (currentHunger > 0)
        {
            if (DayNightCycle.Instance != null)
            {
                float hoursPassedThisFrame = Time.deltaTime * DayNightCycle.Instance.timeMultiplier;
                currentHunger -= hungerPerGameHour * hoursPassedThisFrame;
            }
            else
            {
                currentHunger -= (hungerPerGameHour * 0.1f) * Time.deltaTime;
            }

            if (currentHunger < 0) currentHunger = 0;
            uiDirty = true;
        }
        else
        {
            starvationTimer += Time.deltaTime;
            if (starvationTimer >= starvationTickRate)
            {
                TakeDamage(starvationDamage);
                starvationTimer = 0f;
            }
        }
    }

    public void Feed(float amount)
    {
        currentHunger += amount;
        if (currentHunger > maxHunger) currentHunger = maxHunger;
        uiDirty = true;
    }

    private void HandleThirst()
    {
        if (currentThirst > 0)
        {
            if (DayNightCycle.Instance != null)
            {
                float hoursPassedThisFrame = Time.deltaTime * DayNightCycle.Instance.timeMultiplier;
                currentThirst -= thirstPerGameHour * hoursPassedThisFrame;
            }
            else
            {
                currentThirst -= (thirstPerGameHour * 0.1f) * Time.deltaTime;
            }

            if (currentThirst < 0) currentThirst = 0;
            uiDirty = true;
        }
        else
        {
            dehydrationTimer += Time.deltaTime;
            if (dehydrationTimer >= dehydrationTickRate)
            {
                TakeDamage(dehydrationDamage);
                dehydrationTimer = 0f;
            }
        }
    }

    public void QuenchThirst(float amount)
    {
        currentThirst += amount;
        if (currentThirst > maxThirst) currentThirst = maxThirst;
        uiDirty = true;
    }

    public bool IsDead { get; private set; }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        currentHealth -= damage;
        uiDirty = true;
        if (currentHealth <= 0) Die();
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        uiDirty = true;
    }

    private void UpdateUI()
    {
        if (hpFill != null && maxHealth > 0f)
            hpFill.fillAmount = Mathf.Clamp01(currentHealth / maxHealth);

        if (hungerFill != null && maxHunger > 0f)
            hungerFill.fillAmount = Mathf.Clamp01(currentHunger / maxHunger);

        if (thirstFill != null && maxThirst > 0f)
            thirstFill.fillAmount = Mathf.Clamp01(currentThirst / maxThirst);

        if (staminaFill != null && maxStamina > 0f)
            staminaFill.fillAmount = Mathf.Clamp01(currentStamina / maxStamina);

        if (sanityFill != null && maxSanity > 0f)
            sanityFill.fillAmount = Mathf.Clamp01(currentSanity / maxSanity);

        UpdateCoinsUI();
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        if (coins < 0) coins = 0;
        uiDirty = true;
        UpdateCoinsUI();
    }

    public void RemoveCoins(int amount)
    {
        coins -= amount;
        if (coins < 0) coins = 0;
        uiDirty = true;
        UpdateCoinsUI();
    }

    public bool HasCoins(int amount)
    {
        return coins >= amount;
    }

    public void UpdateCoinsUI()
    {
        if (coinsText != null)
        {
            coinsText.text = coins.ToString();
        }
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.UpdateCoinsUI();
        }
        if (VendingMachineController.activeVendingMachine != null)
        {
            VendingMachineController.activeVendingMachine.UpdatePriceDisplay();
        }
    }

    public void RestoreSanity(float amount)
    {
        currentSanity += amount;
        if (currentSanity > maxSanity) currentSanity = maxSanity;
        uiDirty = true;
    }

    public void DecreaseSanity(float amount)
    {
        currentSanity -= amount;
        if (currentSanity < 0) currentSanity = 0;
        uiDirty = true;
    }

    public void ModifyKarma(float amount)
    {
        karma += amount;
        Debug.Log($"[PlayerStats] Карма изменена на {amount}. Текущая карма: {karma}");
    }

    private void Die()
    {
        IsDead = true;
        Debug.Log("ПОТРАЧЕНО! Игрок умер.");
        this.enabled = false;

        PlayerDeath deathScript = GetComponent<PlayerDeath>();
        if (deathScript != null)
        {
            deathScript.OnDeath();
        }
    }

    private void HandleSanity()
    {
        // Если игрок сидит на стуле и смотрит ТВ, рассудок восстанавливается стулом, обычный распад не идет
        if (TVChairController.activeChair != null)
        {
            return;
        }

        // Каждые несколько кадров (lightCheckInterval) обновляем статус освещенности
        if (Time.time >= nextLightCheckTime)
        {
            playerIsInLight = CheckIfPlayerInLight();
            nextLightCheckTime = Time.time + lightCheckInterval;
        }

        // Выбираем скорость распада
        float decayRate = playerIsInLight ? sanityDecayRateLight : sanityDecayRateDark;
        
        // Уменьшаем рассудок
        DecreaseSanity(decayRate * Time.deltaTime);
    }

    private bool CheckIfPlayerInLight()
    {
        // 1. Проверяем, держит ли игрок включенный фонарь
        LanternController activeLantern = FindFirstObjectByType<LanternController>();
        if (activeLantern != null && activeLantern.gameObject.activeInHierarchy && activeLantern.IsLit)
        {
            return true;
        }

        // 2. Проверяем нахождение на улице днем (если есть DayNightCycle)
        if (DayNightCycle.Instance != null && !DayNightCycle.Instance.isPlayerIndoors)
        {
            if (DayNightCycle.Instance.CurrentLightIntensityFactor > 0.05f)
            {
                return true; // Днем на улице всегда светло (за счет неба/солнца)
            }
        }

        // 3. Сканируем локальные источники света в сцене
        if (Time.time >= nextLightCacheTime || cachedLights == null)
        {
            cachedLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            nextLightCacheTime = Time.time + 0.5f; // Кэшируем на 0.5 сек для оптимизации
        }

        Vector3 playerHead = transform.position + Vector3.up * 1.5f;
        
        // Вычисляем маску, исключая слой самого игрока, чтобы избежать самостолкновений
        int mask = lightObstacleMask.value;
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer != -1)
        {
            mask &= ~(1 << playerLayer);
        }
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer != -1)
        {
            mask &= ~(1 << ignoreRaycastLayer);
        }

        foreach (Light light in cachedLights)
        {
            if (light == null || !light.enabled || !light.gameObject.activeInHierarchy || light.intensity <= 0.01f)
                continue;

            // Направленный свет (Солнце)
            if (light.type == LightType.Directional)
            {
                // Если мы в здании, солнце не светит на нас напрямую
                if (DayNightCycle.Instance != null && DayNightCycle.Instance.isPlayerIndoors)
                    continue;

                Vector3 sunDir = -light.transform.forward;
                bool hitSomething = Physics.Raycast(playerHead, sunDir, out RaycastHit hit, 150f, mask, QueryTriggerInteraction.Ignore);
                if (!hitSomething || hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    return true; // Стоим под прямыми лучами солнца
                }
            }
            // Точечный свет (Point)
            else if (light.type == LightType.Point)
            {
                Vector3 toLight = light.transform.position - playerHead;
                float dist = toLight.magnitude;
                
                if (dist <= light.range)
                {
                    // Если мы ближе 30см, считаем, что мы точно под лампой
                    if (dist <= 0.3f)
                    {
                        return true;
                    }
                    
                    // Бросаем луч от головы к лампе, но укорачиваем его на 30см, чтобы обойти коллайдер плафона/светильника
                    bool hitSomething = Physics.Raycast(playerHead, toLight.normalized, out RaycastHit hit, dist - 0.3f, mask, QueryTriggerInteraction.Ignore);
                    if (!hitSomething || hit.transform == transform || hit.transform.IsChildOf(transform))
                    {
                        return true; // Препятствий на пути к лампе нет
                    }
                }
            }
            // Прожектор (Spot)
            else if (light.type == LightType.Spot)
            {
                Vector3 toLight = light.transform.position - playerHead;
                float dist = toLight.magnitude;
                
                if (dist <= light.range)
                {
                    // Если мы ближе 30см, мы точно на свету
                    if (dist <= 0.3f)
                    {
                        return true;
                    }
                    
                    Vector3 dirFromLight = -toLight.normalized;
                    if (Vector3.Angle(light.transform.forward, dirFromLight) <= light.spotAngle * 0.5f)
                    {
                        // Бросаем луч от головы к источнику прожектора, укороченный на 30см
                        bool hitSomething = Physics.Raycast(playerHead, toLight.normalized, out RaycastHit hit, dist - 0.3f, mask, QueryTriggerInteraction.Ignore);
                        if (!hitSomething || hit.transform == transform || hit.transform.IsChildOf(transform))
                        {
                            return true; // Препятствий на пути к прожектору нет
                        }
                    }
                }
            }
        }

        return false;
    }
}