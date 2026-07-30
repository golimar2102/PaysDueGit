using UnityEngine;

public class WorldClock : MonoBehaviour
{
    [Header("Стрелки часов")]
    [Tooltip("Перетащи сюда дочерний объект часовой стрелки")]
    public Transform hourHand;
    [Tooltip("Перетащи сюда дочерний объект минутной стрелки")]
    public Transform minuteHand;

    [Header("Настройки вращения стрелок")]
    [Tooltip("Вокруг какой оси крутится часовая стрелка?")]
    public Vector3 hourRotationAxis = new Vector3(0, 0, 1);
    [Tooltip("Вокруг какой оси крутится минутная стрелка?")]
    public Vector3 minuteRotationAxis = new Vector3(0, 0, 1);
    [Tooltip("Поставь галочку, если время идет в обратную сторону")]
    public bool reverseRotation = false;

    [Header("Маятник")]
    [Tooltip("Перетащи сюда дочерний объект маятника")]
    public Transform pendulum;
    [Tooltip("Вокруг какой оси качается маятник?")]
    public Vector3 pendulumRotationAxis = new Vector3(0, 0, 1);
    [Tooltip("Максимальный угол отклонения маятника (в градусах)")]
    public float swingAngle = 15f;
    [Tooltip("Период полного колебания маятника туда-обратно (в секундах)")]
    public float swingPeriod = 2f;

    [Header("Звуки маятника")]
    [Tooltip("Аудио-источник для воспроизведения тиканья")]
    public AudioSource audioSource;
    [Tooltip("Звук при движении в одну сторону (Tick)")]
    public AudioClip tickClip;
    [Tooltip("Звук при движении в другую сторону (Tock). Если не назначен, будет играть Tick")]
    public AudioClip tockClip;

    [Header("Циферблат: Смена материалов")]
    [Tooltip("Рендерер циферблата для смены материалов день/ночь")]
    public Renderer dialRenderer;
    [Tooltip("Индекс материала циферблата в рендерере")]
    public int dialMaterialIndex = 0;
    [Tooltip("Материал циферблата днем")]
    public Material dayMaterial;
    [Tooltip("Материал циферблата ночью")]
    public Material nightMaterial;
    [Tooltip("Час начала дня (например, 6)")]
    public float dayTimeStart = 6f;
    [Tooltip("Час начала ночи (например, 18)")]
    public float nightTimeStart = 18f;

    [Header("Циферблат: Вращающийся диск день/ночь")]
    [Tooltip("Вращающийся диск (например, солнце/луна над циферблатом)")]
    public Transform celestialDisk;
    [Tooltip("Ось вращения диска")]
    public Vector3 celestialDiskRotationAxis = new Vector3(0, 0, 1);

    [Header("Циферблат: Градиент свечения/цвета")]
    [Tooltip("Использовать ли плавное изменение цвета/свечения циферблата?")]
    public bool useEmissionGradient = false;
    [Tooltip("Название свойства цвета/свечения в шейдере")]
    public string emissionColorPropertyName = "_EmissionColor";
    [Tooltip("Градиент цвета в течение суток (0 = полночь, 0.5 = полдень, 1 = полночь)")]
    public Gradient dialColorGradient;
    [Tooltip("Кривая интенсивности свечения в течение суток")]
    public AnimationCurve dialEmissionCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // Кэшированные начальные углы
    private Vector3 hourStartEuler;
    private Vector3 minuteStartEuler;
    private Vector3 pendulumStartEuler;
    private Vector3 celestialDiskStartEuler;

    // Вспомогательные переменные для маятника
    private float timeAccumulator;
    private float lastCos;
    private Material dialMaterialInstance;

    void Start()
    {
        // Кэшируем начальные вращения
        if (hourHand != null) hourStartEuler = hourHand.localEulerAngles;
        if (minuteHand != null) minuteStartEuler = minuteHand.localEulerAngles;
        if (pendulum != null) pendulumStartEuler = pendulum.localEulerAngles;
        if (celestialDisk != null) celestialDiskStartEuler = celestialDisk.localEulerAngles;

        // Инициализируем предыдущий косинус для точного воспроизведения звука в крайних точках
        lastCos = Mathf.Cos(0f);
        timeAccumulator = 0f;

        // Если используем градиент и назначен рендерер, создаем инстанс материала
        if (useEmissionGradient && dialRenderer != null)
        {
            if (dialMaterialIndex >= 0 && dialMaterialIndex < dialRenderer.materials.Length)
            {
                dialMaterialInstance = dialRenderer.materials[dialMaterialIndex];
            }
        }
    }

    void Update()
    {
        UpdatePendulum();
    }

    void LateUpdate()
    {
        UpdateClockTime();
        UpdateDialVisuals();
    }

    private void UpdateClockTime()
    {
        if (DayNightCycle.Instance == null) return;

        float time = DayNightCycle.Instance.timeOfDay;
        float dir = reverseRotation ? -1f : 1f;

        // Часовая стрелка: делает один оборот (360 градусов) за 12 часов
        float hourAngle = (time % 12f) * 30f * dir;
        if (hourHand != null)
        {
            hourHand.localEulerAngles = hourStartEuler + (hourRotationAxis * hourAngle);
        }

        // Минутная стрелка: делает один оборот (360 градусов) за 1 час
        float minuteAngle = (time % 1f) * 360f * dir;
        if (minuteHand != null)
        {
            minuteHand.localEulerAngles = minuteStartEuler + (minuteRotationAxis * minuteAngle);
        }

        // Вращающийся celestial диск: делает один оборот (360 градусов) за 24 часа
        if (celestialDisk != null)
        {
            float celestialAngle = (time / 24f) * 360f * dir;
            celestialDisk.localEulerAngles = celestialDiskStartEuler + (celestialDiskRotationAxis * celestialAngle);
        }
    }

    private void UpdatePendulum()
    {
        if (pendulum == null) return;

        timeAccumulator += Time.deltaTime;
        float angleRad = 2f * Mathf.PI * timeAccumulator / swingPeriod;
        float currentSin = Mathf.Sin(angleRad);
        float currentCos = Mathf.Cos(angleRad);

        // Качание маятника
        float angle = currentSin * swingAngle;
        pendulum.localEulerAngles = pendulumStartEuler + (pendulumRotationAxis * angle);

        // Воспроизведение звука в пиковых точках (когда скорость равна 0, т.е. косинус равен 0 и меняет знак)
        if (Mathf.Sign(currentCos) != Mathf.Sign(lastCos))
        {
            if (audioSource != null && audioSource.enabled && audioSource.gameObject.activeInHierarchy)
            {
                if (currentSin > 0f)
                {
                    if (tickClip != null)
                    {
                        audioSource.PlayOneShot(tickClip);
                    }
                }
                else
                {
                    AudioClip clipToPlay = tockClip != null ? tockClip : tickClip;
                    if (clipToPlay != null)
                    {
                        audioSource.PlayOneShot(clipToPlay);
                    }
                }
            }
        }

        lastCos = currentCos;
    }

    private void UpdateDialVisuals()
    {
        if (DayNightCycle.Instance == null) return;

        float time = DayNightCycle.Instance.timeOfDay;

        // 1. Метод смены материалов
        if (dialRenderer != null && !useEmissionGradient)
        {
            bool isDay = time >= dayTimeStart && time < nightTimeStart;
            Material targetMaterial = isDay ? dayMaterial : nightMaterial;

            if (targetMaterial != null)
            {
                Material[] sharedMaterials = dialRenderer.sharedMaterials;
                if (dialMaterialIndex >= 0 && dialMaterialIndex < sharedMaterials.Length)
                {
                    if (sharedMaterials[dialMaterialIndex] != targetMaterial)
                    {
                        sharedMaterials[dialMaterialIndex] = targetMaterial;
                        dialRenderer.sharedMaterials = sharedMaterials;
                    }
                }
            }
        }

        // 2. Метод плавного градиента свечения
        if (useEmissionGradient && dialMaterialInstance != null)
        {
            float timePercent = time / 24f;
            Color baseColor = dialColorGradient.Evaluate(timePercent);
            float intensity = dialEmissionCurve.keys.Length > 0 ? dialEmissionCurve.Evaluate(timePercent) : 1f;

            dialMaterialInstance.SetColor(emissionColorPropertyName, baseColor * intensity);
        }
    }

    void OnDestroy()
    {
        // Чистим созданный экземпляр материала во избежание утечки памяти
        if (dialMaterialInstance != null)
        {
            Destroy(dialMaterialInstance);
        }
    }
}