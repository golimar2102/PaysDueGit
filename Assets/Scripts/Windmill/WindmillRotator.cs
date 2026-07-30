using UnityEngine;

public class WindmillRotator : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z,
        Custom
    }

    [System.Serializable]
    public class RotatingPart
    {
        [Tooltip("Трансформ вращающейся части")]
        public Transform partTransform;

        [Tooltip("Ось вращения для этой части")]
        public RotationAxis rotationAxis = RotationAxis.Y;

        [Tooltip("Собственная ось вращения (если выбрано Custom)")]
        public Vector3 customRotationAxis = Vector3.right;

        [Tooltip("Скорость вращения в градусах в секунду")]
        public float rotationSpeed = 30f;

        [Tooltip("Использовать локальные координаты для вращения")]
        public bool useLocalSpace = true;

        [System.NonSerialized]
        public Quaternion initialLocalRotation;
        [System.NonSerialized]
        public Quaternion initialWorldRotation;
        [System.NonSerialized]
        public float currentAngle = 0f;
        [System.NonSerialized]
        public bool isInitialized = false;
    }

    [Header("Настройки вращающихся частей")]
    [Tooltip("Список отдельно вращающихся частей мельницы. Если пуст, вращается сам объект мельницы.")]
    public RotatingPart[] rotatingParts;

    [Header("Основные настройки (Одиночный объект / Fallback)")]
    [Tooltip("Ось вращения")]
    public RotationAxis rotationAxis = RotationAxis.Y;

    [Tooltip("Собственная ось вращения (если выбрано Custom)")]
    public Vector3 customRotationAxis = Vector3.right;

    [Tooltip("Скорость вращения в градусах в секунду")]
    public float rotationSpeed = 30f;

    [Tooltip("Использовать локальные координаты для вращения")]
    public bool useLocalSpace = true;

    [Tooltip("Отключать ли вращение и звук, когда игрок внутри здания (для оптимизации)")]
    public bool disableIndoors = true;

    [Header("Управление рычагом")]
    [Tooltip("Рычаг (WorldToggleDevice), управляющий работой мельницы")]
    public WorldToggleDevice controlLever;

    [Tooltip("Останавливать ли мельницу, когда рычаг включен (или выключен)")]
    public bool stopWhenLeverIsOn = true;

    [Tooltip("Внешний флаг остановки мельницы (можно задавать из других скриптов)")]
    public bool isStopped = false;

    [Header("Эффект ветра (Колебания скорости)")]
    [Tooltip("Включить органическое изменение скорости ветра с помощью шума Перлина")]
    public bool enableWindFluctuation = true;

    [Tooltip("Амплитуда колебаний (0 - нет колебаний, 1 - скорость меняется от 0 до двойной скорости)")]
    [Range(0f, 1f)]
    public float fluctuationMagnitude = 0.2f;

    [Tooltip("Частота колебаний ветра (скорость изменения силы ветра)")]
    public float fluctuationFrequency = 0.5f;

    [Header("Звуковые эффекты")]
    [Tooltip("Аудиоисточник для озвучивания вращения мельницы")]
    public AudioSource audioSource;

    [Tooltip("Базовая громкость аудио")]
    [Range(0f, 1f)]
    public float baseVolume = 0.5f;

    [Tooltip("Изменять громкость и высоту звука (Pitch) в зависимости от текущей скорости ветра")]
    public bool modulateAudioWithSpeed = true;

    private float noiseOffset;
    private float currentSpeed;

    private Quaternion initialLocalRotation;
    private Quaternion initialWorldRotation;
    private float currentAngle = 0f;

    void Start()
    {
        // Уникальный сдвиг шума для каждого объекта, чтобы несколько мельниц не вращались абсолютно синхронно
        noiseOffset = Random.Range(0f, 1000f);

        // Инициализируем начальные вращения для частей
        if (rotatingParts != null)
        {
            foreach (RotatingPart part in rotatingParts)
            {
                if (part == null || part.partTransform == null) continue;
                part.initialLocalRotation = part.partTransform.localRotation;
                part.initialWorldRotation = part.partTransform.rotation;
                part.currentAngle = 0f;
                part.isInitialized = true;
            }
        }

        // Инициализируем начальные вращения для самого объекта
        initialLocalRotation = transform.localRotation;
        initialWorldRotation = transform.rotation;
        currentAngle = 0f;

        if (audioSource != null)
        {
            audioSource.loop = true;
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }

    void Update()
    {
        // Оптимизация: отключаем вращение и звук, если игрок внутри строения
        if (disableIndoors && DayNightCycle.Instance != null && DayNightCycle.Instance.isPlayerIndoors)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
            return;
        }

        // Проверяем, остановлена ли мельница рычагом или внешним флагом
        bool stopped = isStopped || (controlLever != null && (controlLever.isOn == stopWhenLeverIsOn));

        if (stopped)
        {
            currentSpeed = 0f;
        }
        else
        {
            // Рассчитываем текущую скорость с учетом шума Перлина
            float windMultiplier = 1f;
            if (enableWindFluctuation)
            {
                float noise = Mathf.PerlinNoise(Time.time * fluctuationFrequency + noiseOffset, 0f); // Возвращает 0..1
                // Приводим шум от 0..1 к диапазону -1..1 для плавных отклонений вокруг 1.0f
                windMultiplier = 1f + (noise * 2f - 1f) * fluctuationMagnitude;
            }
            currentSpeed = rotationSpeed * windMultiplier;

            // Выполняем вращение частей
            if (rotatingParts != null && rotatingParts.Length > 0)
            {
                foreach (RotatingPart part in rotatingParts)
                {
                    if (part == null || part.partTransform == null) continue;

                    // Если часть была добавлена динамически или не инициализировалась в Start
                    if (!part.isInitialized)
                    {
                        part.initialLocalRotation = part.partTransform.localRotation;
                        part.initialWorldRotation = part.partTransform.rotation;
                        part.currentAngle = 0f;
                        part.isInitialized = true;
                    }

                    float partSpeed = part.rotationSpeed * windMultiplier;
                    part.currentAngle += partSpeed * Time.deltaTime;
                    Vector3 partRotationVector = GetRotationVector(part.rotationAxis, part.customRotationAxis);

                    if (part.useLocalSpace)
                    {
                        part.partTransform.localRotation = part.initialLocalRotation * Quaternion.AngleAxis(part.currentAngle, partRotationVector);
                    }
                    else
                    {
                        part.partTransform.rotation = Quaternion.AngleAxis(part.currentAngle, partRotationVector) * part.initialWorldRotation;
                    }
                }
            }
            else
            {
                // Выполняем вращение самого объекта
                currentAngle += currentSpeed * Time.deltaTime;
                Vector3 rotationVector = GetRotationVector(rotationAxis, customRotationAxis);

                if (useLocalSpace)
                {
                    transform.localRotation = initialLocalRotation * Quaternion.AngleAxis(currentAngle, rotationVector);
                }
                else
                {
                    transform.rotation = Quaternion.AngleAxis(currentAngle, rotationVector) * initialWorldRotation;
                }
            }
        }

        // Управляем аудио
        UpdateAudio();
    }

    private Vector3 GetRotationVector()
    {
        return GetRotationVector(rotationAxis, customRotationAxis);
    }

    private Vector3 GetRotationVector(RotationAxis axis, Vector3 customAxis)
    {
        switch (axis)
        {
            case RotationAxis.X:
                return Vector3.right;
            case RotationAxis.Y:
                return Vector3.up;
            case RotationAxis.Z:
                return Vector3.forward;
            case RotationAxis.Custom:
                return customAxis.normalized;
            default:
                return Vector3.up;
        }
    }

    private void UpdateAudio()
    {
        if (audioSource == null) return;

        if (modulateAudioWithSpeed && rotationSpeed != 0f)
        {
            float ratio = Mathf.Abs(currentSpeed / rotationSpeed);
            audioSource.volume = baseVolume * ratio;
            // Плавное изменение высоты звука (от 0.8 до 1.2) в зависимости от ветра
            audioSource.pitch = Mathf.Lerp(0.8f, 1.2f, ratio);
        }
        else
        {
            audioSource.volume = baseVolume;
            audioSource.pitch = 1f;
        }

        // Если мельница не крутится вообще, останавливаем звук
        if (Mathf.Approximately(currentSpeed, 0f))
        {
            if (audioSource.isPlaying) audioSource.Pause();
        }
        else
        {
            if (!audioSource.isPlaying) audioSource.Play();
        }
    }
}
