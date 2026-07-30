using UnityEngine;
using UnityEngine.Rendering;

public enum GameZone
{
    Farm,
    Barn,
    Apartment,
    Windmill,
    HUB,
    IndustrialZone
}

[System.Serializable]
public class ZoneEnvironmentSettings
{
    [Header("Настройки Тумана")]
    [Tooltip("Переопределить ли настройки тумана для этой зоны")]
    public bool overrideFog = false;
    [Tooltip("Включен ли туман в этой зоне")]
    public bool fogEnabled = false;
    [Tooltip("Цвет тумана в этой зоне")]
    public Color fogColor = Color.gray;
    [Tooltip("Плотность тумана (для Exponential/ExponentialSquared)")]
    public float fogDensity = 0.002f;
    [Tooltip("Начальная дистанция тумана (для Linear)")]
    public float fogStartDistance = 0f;
    [Tooltip("Конечная дистанция тумана (для Linear)")]
    public float fogEndDistance = 1000f;

    [Header("Настройки Скайбокса")]
    [Tooltip("Переопределить ли скайбокс для этой зоны")]
    public bool overrideSkybox = false;
    [Tooltip("Материал скайбокса для этой зоны")]
    public Material skyboxMaterial;

    [Header("Настройки Отражений")]
    [Tooltip("Переопределить ли интенсивность отражений окружения")]
    public bool overrideReflections = false;
    [Tooltip("Интенсивность отражений (0 - 1)")]
    [Range(0f, 1f)]
    public float reflectionIntensity = 0.2f;
}

[System.Serializable]
public class ZoneConfig
{
    public GameZone zone;
    [Tooltip("Является ли зона помещением (будет применен темный цвет эмбиента и затушено солнце)")]
    public bool isIndoors = true;
    [Tooltip("Цвет эмбиента для этой зоны (применяется, если это помещение)")]
    public Color darkIndoorColor = new Color(0.05f, 0.05f, 0.05f);
    [Tooltip("Объекты (например, источники света), которые должны быть активны только в этой зоне")]
    public GameObject[] zoneObjects;
    [Tooltip("Global Volume для этой зоны (пост-обработка). Может быть пустым.")]
    public Volume zoneVolume;
    [Tooltip("Настройки окружения (Environment) для этой зоны")]
    public ZoneEnvironmentSettings environmentSettings = new ZoneEnvironmentSettings();
}

[System.Serializable]
public struct HourLighting
{
    [Range(0f, 1f)]
    [Tooltip("Коэффициент интенсивности уличного света (0 - темно, 1 - максимальная яркость)")]
    public float lightIntensityFactor;
    [Tooltip("Цвет окружающего освещения (ambient) в этот час")]
    public Color ambientColor;
    [Tooltip("Цвет тумана в этот час")]
    public Color fogColor;
}

// [ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance;
    public static event System.Action<GameZone> OnZoneChanged;

    [Header("Настройки переходов (Global Volume и Освещение)")]
    [Tooltip("Скорость плавного перехода освещения и пост-эффектов (Volume) между зонами")]
    public float transitionSpeed = 2f;

    private float indoorTransitionT = 0f;

    // Базовые настройки окружения сцены (сохраняются при старте)
    private bool baseFogEnabled;
    private float baseFogDensity;
    private float baseFogStartDistance;
    private float baseFogEndDistance;
    private Material baseSkyboxMaterial;
    private float baseReflectionIntensity;

    [Header("Настройки времени")]
    [Tooltip("Текущий день (начинается с 1)")]
    public int currentDay = 1; 

    [Tooltip("Текущее время в часах (от 0 до 24)")]
    [Range(0, 24)]
    public float timeOfDay = 12f; 
    
    [Tooltip("Скорость течения времени. 1 = 1 игровой час проходит за 1 реальную секунду")]
    public float timeMultiplier = 0.5f; 

    [Header("Освещение")]
    [Tooltip("Направление солнца (для вращения процедурного скайбокса, если используется)")]
    public Transform sunDirectionObject;
    
    [Tooltip("Источники света на улице, яркость которых меняется от времени суток")]
    public Light[] outdoorLights;

    [Header("Настройки освещения по часам (0 - 23)")]
    public HourLighting[] hourlyLighting = new HourLighting[24];

    public float CurrentLightIntensityFactor
    {
        get
        {
            EvaluateHourlyLighting(timeOfDay, out float factor, out _, out _);
            return factor;
        }
    }

    private float[] baseLightIntensities;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (hourlyLighting == null || hourlyLighting.Length != 24)
        {
            InitializeDefaultHourlyLighting();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hourlyLighting == null || hourlyLighting.Length != 24)
        {
            InitializeDefaultHourlyLighting();
        }
    }
#endif

    private void InitializeDefaultHourlyLighting()
    {
        hourlyLighting = new HourLighting[24];
        for (int h = 0; h < 24; h++)
        {
            hourlyLighting[h] = GetDefaultLightingForHour(h);
        }
    }

    private HourLighting GetDefaultLightingForHour(int hour)
    {
        HourLighting lighting = new HourLighting();
        if (hour >= 6 && hour <= 18)
        {
            float factor = 1f - (Mathf.Abs(hour - 12f) / 6f);
            factor = Mathf.Max(0f, factor);

            lighting.lightIntensityFactor = factor;

            Color dawnDuskColor = new Color(0.85f, 0.45f, 0.25f);
            Color middayColor = new Color(0.65f, 0.7f, 0.8f);
            lighting.ambientColor = Color.Lerp(dawnDuskColor, middayColor, factor);
            lighting.fogColor = lighting.ambientColor;
        }
        else
        {
            lighting.lightIntensityFactor = 0f;
            lighting.ambientColor = new Color(0.08f, 0.09f, 0.15f);
            lighting.fogColor = lighting.ambientColor;
        }
        return lighting;
    }

    public void EvaluateHourlyLighting(float time, out float lightIntensityFactor, out Color ambientColor, out Color fogColor)
    {
        if (hourlyLighting == null || hourlyLighting.Length == 0)
        {
            lightIntensityFactor = 1f;
            ambientColor = Color.gray;
            fogColor = Color.gray;
            return;
        }

        float normalizedTime = time % 24f;
        if (normalizedTime < 0) normalizedTime += 24f;

        int currentHour = Mathf.FloorToInt(normalizedTime) % hourlyLighting.Length;
        int nextHour = (currentHour + 1) % hourlyLighting.Length;
        float t = normalizedTime - Mathf.FloorToInt(normalizedTime);

        HourLighting current = hourlyLighting[currentHour];
        HourLighting next = hourlyLighting[nextHour];

        lightIntensityFactor = Mathf.Lerp(current.lightIntensityFactor, next.lightIntensityFactor, t);
        ambientColor = Color.Lerp(current.ambientColor, next.ambientColor, t);
        fogColor = Color.Lerp(current.fogColor, next.fogColor, t);
    }

    void Start()
    {
        InitializeBaseIntensities();

        // Сохраняем исходные настройки окружения сцены в качестве базовых (для улицы)
        baseFogEnabled = RenderSettings.fog;
        baseFogDensity = RenderSettings.fogDensity;
        baseFogStartDistance = RenderSettings.fogStartDistance;
        baseFogEndDistance = RenderSettings.fogEndDistance;
        baseSkyboxMaterial = RenderSettings.skybox;
        baseReflectionIntensity = RenderSettings.reflectionIntensity;

        // Force the first state check on the next frame/Update run
        lastZone = currentZone;
        wasStateInitialized = false;

        // Устанавливаем мгновенное значение при старте, чтобы избежать медленного затухания в начале игры
        indoorTransitionT = isPlayerIndoors ? 1f : 0f;

        ApplyIndoorOutdoorState();
    }

    private void InitializeBaseIntensities()
    {
        if (outdoorLights != null)
        {
            baseLightIntensities = new float[outdoorLights.Length];
            for (int i = 0; i < outdoorLights.Length; i++)
            {
                if (outdoorLights[i] != null)
                {
                    baseLightIntensities[i] = outdoorLights[i].intensity;
                }
            }
        }
    }

    private void UpdateOutdoorLights(float intensityFactor, Color lightColor)
    {
        if (outdoorLights == null || baseLightIntensities == null) return;

        for (int i = 0; i < outdoorLights.Length; i++)
        {
            if (outdoorLights[i] == null) continue;

            if (isPlayerIndoors)
            {
                outdoorLights[i].intensity = 0f;
            }
            else
            {
                float baseIntensity = i < baseLightIntensities.Length ? baseLightIntensities[i] : 1f;
                outdoorLights[i].intensity = baseIntensity * intensityFactor;
                outdoorLights[i].color = lightColor;
            }
        }
    }

    void Update()
    {
        timeOfDay += Time.deltaTime * timeMultiplier;
        
        if (timeOfDay >= 24f)
        {
            timeOfDay %= 24f;
            currentDay++;
            Debug.Log($"Наступил день {currentDay}!");
        }

        // Плавно интерполируем переходной коэффициент для помещений
        float targetT = isPlayerIndoors ? 1f : 0f;
        indoorTransitionT = Mathf.MoveTowards(indoorTransitionT, targetT, Time.deltaTime * transitionSpeed);
        
        bool stateChanged = !wasStateInitialized || (currentZone != lastZone);
        if (stateChanged)
        {
            lastZone = currentZone;
            wasStateInitialized = true;
            ApplyIndoorOutdoorState();
            OnZoneChanged?.Invoke(currentZone);
        }

        UpdateSunAndLighting(stateChanged);
        UpdateZoneVolumes();
    }
    
    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(timeOfDay);
        int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);
        return $"{hours:00}:{minutes:00}";
    }

    [Header("Зоны")]
    [Tooltip("Текущая зона игрока")]
    public GameZone currentZone = GameZone.Farm;

    [Tooltip("Список настроек для всех зон в игре")]
    public System.Collections.Generic.List<ZoneConfig> zoneConfigs = new System.Collections.Generic.List<ZoneConfig>()
    {
        new ZoneConfig { zone = GameZone.Farm, isIndoors = false },
        new ZoneConfig { zone = GameZone.Barn, isIndoors = true, darkIndoorColor = new Color(0.05f, 0.05f, 0.05f) }
    };

    public bool isPlayerIndoors
    {
        get
        {
            if (zoneConfigs == null) return false;
            foreach (var config in zoneConfigs)
            {
                if (config.zone == currentZone)
                {
                    return config.isIndoors;
                }
            }
            return false;
        }
    }

    public Color darkIndoorColor
    {
        get
        {
            if (zoneConfigs != null)
            {
                foreach (var config in zoneConfigs)
                {
                    if (config.zone == currentZone)
                    {
                        return config.darkIndoorColor;
                    }
                }
            }
            return new Color(0.05f, 0.05f, 0.05f);
        }
    }

    [Header("Настройки Освещения на Улице")]
    [Tooltip("Режим освещения окружения на улице. Flat = цвет из градиента (рекомендуется для PSX), Skybox = из скайбокса.")]
    public UnityEngine.Rendering.AmbientMode outdoorAmbientMode = UnityEngine.Rendering.AmbientMode.Flat;

    [Header("Оптимизация Global Illumination (GI)")]
    [Tooltip("Как часто (в реальных секундах) обновлять освещение от Skybox при нахождении на улице (актуально только для режима Skybox).")]
    public float giUpdateInterval = 1f;

    private GameZone lastZone;
    private bool wasStateInitialized = false;
    private float nextGIUpdateTime;

    public void SetZone(GameZone newZone)
    {
        if (currentZone == newZone) return;
        currentZone = newZone;
        ApplyIndoorOutdoorState();
    }

    private void ApplyIndoorOutdoorState()
    {
        // 1. Переключаем активность объектов зон
        if (zoneConfigs != null)
        {
            foreach (var config in zoneConfigs)
            {
                if (config.zoneObjects == null) continue;
                
                bool isActiveZone = (config.zone == currentZone);
                foreach (var obj in config.zoneObjects)
                {
                    if (obj != null)
                    {
                        // Проверяем, не является ли объект частью Volume, чтобы не отключать его мгновенно
                        if (IsVolumeObject(obj)) continue;

                        obj.SetActive(isActiveZone);
                    }
                }
            }
        }

        // Освещение теперь плавно обновляется в UpdateSunAndLighting на основе indoorTransitionT,
        // поэтому мгновенное принудительное переключение здесь убрано во избежание мерцаний.
    }

    private void UpdateSunAndLighting(bool stateChanged)
    {
        float timePercent = timeOfDay / 24f;

        EvaluateHourlyLighting(timeOfDay, out float lightIntensityFactor, out Color ambientColor, out Color fogColor);

        // Обновляем позицию солнца для процедурного скайбокса (если задан)
        if (sunDirectionObject != null)
        {
            float sunAngle = (timePercent * 360f) - 90f;
            sunDirectionObject.localRotation = Quaternion.Euler(sunAngle, -30f, 0f);
        }

        // Вычисляем целевой цвет эмбиента внутри помещения
        Color indoorAmbColor = darkIndoorColor;

        // Плавно интерполируем цвет окружающего освещения и тумана
        Color finalAmbient = Color.Lerp(ambientColor, indoorAmbColor, indoorTransitionT);
        Color finalFog = Color.Lerp(fogColor, indoorAmbColor, indoorTransitionT);

        // Интенсивность уличных источников света плавно гасится при входе в помещение
        float finalLightIntensityFactor = Mathf.Lerp(lightIntensityFactor, 0f, indoorTransitionT);
        UpdateOutdoorLights(finalLightIntensityFactor, finalAmbient);

        // Обновляем параметры рендеринга каждый кадр для плавности
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = finalAmbient;

        // --- ОБРАБОТКА OVERRIDE-НАСТРОЕК ОКРУЖЕНИЯ ДЛЯ ТЕКУЩЕЙ ЗОНЫ ---
        bool targetFogEnabled = baseFogEnabled;
        float targetFogDensity = baseFogDensity;
        float targetFogStart = baseFogStartDistance;
        float targetFogEnd = baseFogEndDistance;
        float targetReflection = baseReflectionIntensity;
        Material targetSkybox = baseSkyboxMaterial;
        Color targetFogColorValue = finalFog;

        ZoneConfig currentZoneConfig = null;
        if (zoneConfigs != null)
        {
            foreach (var config in zoneConfigs)
            {
                if (config.zone == currentZone)
                {
                    currentZoneConfig = config;
                    break;
                }
            }
        }

        if (currentZoneConfig != null && currentZoneConfig.environmentSettings != null)
        {
            var env = currentZoneConfig.environmentSettings;
            if (env.overrideFog)
            {
                targetFogEnabled = env.fogEnabled;
                targetFogDensity = env.fogDensity;
                targetFogStart = env.fogStartDistance;
                targetFogEnd = env.fogEndDistance;
                targetFogColorValue = env.fogColor;
            }
            if (env.overrideSkybox)
            {
                targetSkybox = env.skyboxMaterial;
            }
            if (env.overrideReflections)
            {
                targetReflection = env.reflectionIntensity;
            }
        }

        // Плавно интерполируем параметры тумана и отражений
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColorValue, Time.deltaTime * transitionSpeed);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * transitionSpeed);
        RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, targetFogStart, Time.deltaTime * transitionSpeed);
        RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, targetFogEnd, Time.deltaTime * transitionSpeed);
        RenderSettings.reflectionIntensity = Mathf.Lerp(RenderSettings.reflectionIntensity, targetReflection, Time.deltaTime * transitionSpeed);

        // Плавное включение/выключение тумана (без скачков):
        // Если туман должен работать, включаем его сразу, чтобы плотность росла плавно.
        // Если туман выключается, держим его включенным, пока плотность не упадет почти до нуля, чтобы избежать рывка.
        if (targetFogEnabled)
        {
            RenderSettings.fog = true;
        }
        else
        {
            if (RenderSettings.fogDensity < 0.001f || RenderSettings.fogEndDistance > 9900f)
            {
                RenderSettings.fog = false;
            }
        }

        // Переключаем скайбокс при необходимости
        if (RenderSettings.skybox != targetSkybox)
        {
            RenderSettings.skybox = targetSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void UpdateZoneVolumes()
    {
        if (zoneConfigs == null) return;

        foreach (var config in zoneConfigs)
        {
            if (config.zoneVolume != null)
            {
                bool isCurrent = (config.zone == currentZone);
                float targetWeight = isCurrent ? 1f : 0f;

                // Плавно смешиваем вес Global Volume
                config.zoneVolume.weight = Mathf.MoveTowards(config.zoneVolume.weight, targetWeight, Time.deltaTime * transitionSpeed);

                // Оптимизация производительности: выключаем GameObject объема, если его влияние равно 0
                if (config.zoneVolume.weight > 0.001f)
                {
                    if (!config.zoneVolume.gameObject.activeSelf)
                        config.zoneVolume.gameObject.SetActive(true);
                }
                else
                {
                    if (config.zoneVolume.gameObject.activeSelf)
                        config.zoneVolume.gameObject.SetActive(false);
                }
            }
        }
    }

    private bool IsVolumeObject(GameObject obj)
    {
        if (zoneConfigs == null) return false;
        foreach (var config in zoneConfigs)
        {
            if (config.zoneVolume != null && (config.zoneVolume.gameObject == obj || config.zoneVolume.transform.IsChildOf(obj.transform)))
            {
                return true;
            }
        }
        return false;
    }
}