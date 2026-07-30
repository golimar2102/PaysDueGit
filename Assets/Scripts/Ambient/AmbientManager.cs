using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Менеджер фонового эмбиента. Управляет плавными переходами (crossfade)
/// фоновой музыки при смене игровых зон (Outside, House, Barn и т.д.).
/// </summary>
public class AmbientManager : MonoBehaviour
{
    public static AmbientManager Instance { get; private set; }

    [System.Serializable]
    public struct ZoneAmbientMapping
    {
        [Tooltip("Игровая зона из DayNightCycle")]
        public GameZone zone;
        [Tooltip("Аудиоклип эмбиента для этой зоны (оставьте пустым для тишины)")]
        public AudioClip ambientClip;
        [Range(0f, 1f)]
        [Tooltip("Громкость эмбиента для этой зоны")]
        public float volume;
    }

    [Header("Настройки аудиоисточников")]
    [Tooltip("Первый источник звука (создается автоматически, если не указан)")]
    public AudioSource audioSourceA;
    [Tooltip("Второй источник звука (создается автоматически, если не указан)")]
    public AudioSource audioSourceB;
    [Tooltip("Аудио-микшер группа для музыки")]
    public AudioMixerGroup musicMixerGroup;

    [Header("Настройки переходов")]
    [Tooltip("Длительность плавного перехода между эмбиентами (в секундах)")]
    public float fadeDuration = 2.0f;
    [Range(0f, 1f)]
    [Tooltip("Глобальный множитель громкости эмбиента")]
    public float masterAmbientVolume = 1.0f;

    [Header("Дефолтный эмбиент (Outside)")]
    [Tooltip("Клип по умолчанию (играет, если для текущей зоны нет настроек)")]
    public AudioClip defaultAmbientClip;
    [Range(0f, 1f)]
    [Tooltip("Громкость дефолтного клипа")]
    public float defaultAmbientVolume = 0.5f;

    [Header("Карта эмбиентов для зон")]
    public List<ZoneAmbientMapping> zoneAmbients = new List<ZoneAmbientMapping>();

    // Список активных триггерных зон, в которых находится игрок
    private List<AmbientZone> activeTriggerZones = new List<AmbientZone>();

    private bool isUsingSourceA = true;
    private Coroutine fadeCoroutine;
    private AudioClip currentClip;
    private float currentTargetVolume;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Делаем менеджер сквозным между сценами, если это необходимо
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Автоматическое создание источников звука, если они не заданы
        if (audioSourceA == null) audioSourceA = gameObject.AddComponent<AudioSource>();
        if (audioSourceB == null) audioSourceB = gameObject.AddComponent<AudioSource>();

        ConfigureAudioSource(audioSourceA);
        ConfigureAudioSource(audioSourceB);
    }

    private void Start()
    {
        // Первоначальное определение и запуск эмбиента
        EvaluateActiveAmbient();
    }

    private void OnEnable()
    {
        // Подписываемся на глобальное событие смены зоны
        DayNightCycle.OnZoneChanged += HandleZoneChanged;
    }

    private void OnDisable()
    {
        // Отписываемся во избежание утечек памяти
        DayNightCycle.OnZoneChanged -= HandleZoneChanged;
    }

    private void Update()
    {
        // Если переход не выполняется, плавно подстраиваем громкость под текущие настройки masterAmbientVolume
        if (fadeCoroutine == null)
        {
            AudioSource active = isUsingSourceA ? audioSourceA : audioSourceB;
            if (active.isPlaying && active.clip != null)
            {
                float targetVol = currentTargetVolume * masterAmbientVolume;
                if (!Mathf.Approximately(active.volume, targetVol))
                {
                    active.volume = Mathf.MoveTowards(active.volume, targetVol, Time.deltaTime * 2f);
                }
            }
        }
    }

    private void ConfigureAudioSource(AudioSource source)
    {
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // Полностью 2D фоновый звук
        if (musicMixerGroup != null)
        {
            source.outputAudioMixerGroup = musicMixerGroup;
        }
    }

    private void HandleZoneChanged(GameZone newZone)
    {
        Debug.Log($"[AmbientManager] Получено уведомление о смене зоны DayNightCycle на: {newZone}");
        EvaluateActiveAmbient();
    }

    /// <summary>
    /// Регистрирует триггерную зону при входе игрока.
    /// </summary>
    public void RegisterTriggerZone(AmbientZone zone)
    {
        if (!activeTriggerZones.Contains(zone))
        {
            activeTriggerZones.Add(zone);
            EvaluateActiveAmbient();
        }
    }

    /// <summary>
    /// Разрегистрирует триггерную зону при выходе игрока.
    /// </summary>
    public void UnregisterTriggerZone(AmbientZone zone)
    {
        if (activeTriggerZones.Contains(zone))
        {
            activeTriggerZones.Remove(zone);
            EvaluateActiveAmbient();
        }
    }

    /// <summary>
    /// Оценивает текущую ситуацию и выбирает, какой эмбиент должен проигрываться.
    /// </summary>
    private void EvaluateActiveAmbient()
    {
        AudioClip targetClip = null;
        float targetVolume = 0f;

        // 1. Проверяем, находится ли игрок в триггерных зонах (они имеют наивысший приоритет)
        if (activeTriggerZones.Count > 0)
        {
            AmbientZone bestZone = activeTriggerZones[0];
            for (int i = 1; i < activeTriggerZones.Count; i++)
            {
                if (activeTriggerZones[i].priority > bestZone.priority)
                {
                    bestZone = activeTriggerZones[i];
                }
            }
            targetClip = bestZone.ambientClip;
            targetVolume = bestZone.volumeMultiplier;
            Debug.Log($"[AmbientManager] Выбран эмбиент из триггерной зоны '{bestZone.gameObject.name}' с приоритетом {bestZone.priority}");
        }
        // 2. Если триггерных зон нет, берем эмбиент из DayNightCycle
        else if (DayNightCycle.Instance != null)
        {
            GameZone currentZone = DayNightCycle.Instance.currentZone;
            bool mappingFound = false;

            foreach (var mapping in zoneAmbients)
            {
                if (mapping.zone == currentZone)
                {
                    targetClip = mapping.ambientClip;
                    targetVolume = mapping.volume;
                    mappingFound = true;
                    break;
                }
            }

            if (mappingFound)
            {
                Debug.Log($"[AmbientManager] Выбран эмбиент для зоны {currentZone} из DayNightCycle");
            }
            else
            {
                // Если настроек для этой зоны нет — используем дефолтные
                targetClip = defaultAmbientClip;
                targetVolume = defaultAmbientVolume;
                Debug.Log($"[AmbientManager] Для зоны {currentZone} настройки отсутствуют. Используется дефолтный эмбиент");
            }
        }
        else
        {
            // Фаллбек, если DayNightCycle отсутствует на сцене
            targetClip = defaultAmbientClip;
            targetVolume = defaultAmbientVolume;
        }

        StartCrossfade(targetClip, targetVolume);
    }

    /// <summary>
    /// Запускает процесс перекрестного затухания к новому аудиоклипу.
    /// </summary>
    private void StartCrossfade(AudioClip newClip, float targetVolume)
    {
        if (currentClip == newClip)
        {
            // Если клип тот же, обновляем его целевую громкость без перезапуска
            currentTargetVolume = targetVolume;
            if (fadeCoroutine == null)
            {
                AudioSource active = isUsingSourceA ? audioSourceA : audioSourceB;
                active.volume = targetVolume * masterAmbientVolume;
            }
            return;
        }

        currentClip = newClip;
        currentTargetVolume = targetVolume;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(CrossfadeCoroutine(newClip, targetVolume, fadeDuration));
    }

    private IEnumerator CrossfadeCoroutine(AudioClip newClip, float targetVolume, float duration)
    {
        AudioSource active = isUsingSourceA ? audioSourceA : audioSourceB;
        AudioSource inactive = isUsingSourceA ? audioSourceB : audioSourceA;

        // Подготавливаем неактивный источник
        inactive.clip = newClip;
        if (newClip != null)
        {
            inactive.volume = 0f;
            inactive.Play();
        }

        float elapsed = 0f;
        float startActiveVolume = active.volume;
        float startInactiveVolume = inactive.volume;

        if (duration <= 0.05f)
        {
            active.volume = 0f;
            active.Stop();
            if (newClip != null)
            {
                inactive.volume = targetVolume * masterAmbientVolume;
            }
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                active.volume = Mathf.Lerp(startActiveVolume, 0f, smoothT);
                if (newClip != null)
                {
                    inactive.volume = Mathf.Lerp(startInactiveVolume, targetVolume * masterAmbientVolume, smoothT);
                }

                yield return null;
            }
        }

        active.volume = 0f;
        active.Stop();
        active.clip = null;

        if (newClip != null)
        {
            inactive.volume = targetVolume * masterAmbientVolume;
        }

        // Меняем источники местами
        isUsingSourceA = !isUsingSourceA;
        fadeCoroutine = null;
        
        Debug.Log($"[AmbientManager] Переход завершен. Текущий клип: {(newClip != null ? newClip.name : "Тишина")}");
    }
}
