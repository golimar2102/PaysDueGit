using System.Collections;
using UnityEngine;

/// <summary>
/// Скрипт плавного включения и выключения 3D звука при входе и выходе игрока из триггера.
/// Вы можете вручную перетащить любой коллайдер (в том числе с другого объекта) в поле Trigger Collider.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProximityAudioController : MonoBehaviour
{
    [Header("Основные настройки звука")]
    [Tooltip("Ссылка на AudioSource. Если не указана, возьмется с этого же объекта.")]
    public AudioSource audioSource;

    [Range(0f, 1f)]
    [Tooltip("Максимальная громкость, до которой плавно поднимется звук.")]
    public float maxVolume = 1f;

    [Tooltip("Тег объекта, который активирует триггер (обычно 'Player').")]
    public string playerTag = "Player";

    [Header("Настройки триггера")]
    [Tooltip("Коллайдер-триггер. Сюда можно перетащить любой коллайдер (с этого или любого другого объекта в сцене).")]
    public Collider triggerCollider;

    [Header("Настройки плавности")]
    [Tooltip("Время плавного нарастания звука (в секундах) при входе игрока в триггер.")]
    public float fadeInDuration = 1.5f;

    [Tooltip("Время плавного затухания звука (в секундах) при выходе игрока из триггера.")]
    public float fadeOutDuration = 1.5f;

    [Tooltip("Приостанавливать (Pause) воспроизведение звука на нулевой громкости для экономии ресурсов.")]
    public bool pauseWhenSilent = true;

    [Header("Электричество")]
    [Tooltip("Зависит ли звук от работы генератора?")]
    public bool requiresPower = false;
    [Tooltip("Конкретный генератор, от которого зависит это устройство. Если пустой, используется глобальный Instance.")]
    public GeneratorController targetGenerator;

    private Coroutine fadeCoroutine;
    private bool isPlayerInside = false;
    private bool lastPowerState = false;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (audioSource == null)
        {
            Debug.LogError($"[ProximityAudioController] На объекте {gameObject.name} отсутствует AudioSource!", this);
            enabled = false;
            return;
        }

        // Если коллайдер не назначен вручную, пробуем найти его на этом же объекте
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        if (triggerCollider != null)
        {
            // Убеждаемся, что на коллайдере включен режим триггера
            if (!triggerCollider.isTrigger)
            {
                Debug.Log($"[ProximityAudioController] Коллайдер '{triggerCollider.name}' не был отмечен как Is Trigger. Включаю автоматически.");
                triggerCollider.isTrigger = true;
            }

            // Если коллайдер на другом объекте, вешаем на него специальный прокси-скрипт для перенаправления событий
            if (triggerCollider.gameObject != gameObject)
            {
                AudioTriggerProxy proxy = triggerCollider.gameObject.GetComponent<AudioTriggerProxy>();
                if (proxy == null)
                {
                    proxy = triggerCollider.gameObject.AddComponent<AudioTriggerProxy>();
                }
                proxy.controller = this;
                Debug.Log($"[ProximityAudioController] Успешно подключен внешний триггер '{triggerCollider.gameObject.name}' к звуку на '{gameObject.name}'");
            }
        }
        else
        {
            Debug.LogWarning($"[ProximityAudioController] Коллайдер-триггер не назначен и не найден на объекте {gameObject.name}! Пожалуйста, перетащите его в поле Trigger Collider.", this);
        }

        lastPowerState = IsPowerWorking();

        // В начале игры звук выключен и громкость равна 0
        audioSource.volume = 0f;
        if (pauseWhenSilent && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    private bool IsPowerWorking()
    {
        if (targetGenerator != null)
        {
            return targetGenerator.isWorking;
        }
        return GeneratorController.IsGeneratorWorking;
    }

    private void Update()
    {
        if (requiresPower)
        {
            bool currentPower = IsPowerWorking();
            if (currentPower != lastPowerState)
            {
                lastPowerState = currentPower;
                if (currentPower)
                {
                    // Power turned ON
                    if (isPlayerInside)
                    {
                        StartFade(maxVolume, fadeInDuration);
                    }
                }
                else
                {
                    // Power turned OFF
                    StartFade(0f, fadeOutDuration);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Сработает, если коллайдер находится на этом же объекте
        if (triggerCollider != null && triggerCollider.gameObject == gameObject)
        {
            HandleTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Сработает, если коллайдер находится на этом же объекте
        if (triggerCollider != null && triggerCollider.gameObject == gameObject)
        {
            HandleTriggerExit(other);
        }
    }

    // Эти методы будут вызываться как локально, так и через прокси-скрипт с других объектов
    public void HandleTriggerEnter(Collider other)
    {
        bool isPlayer = other.CompareTag(playerTag) || 
                         other.transform.root.CompareTag(playerTag) || 
                         (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag));

        Debug.Log($"[ProximityAudioController] Объект '{other.name}' (Тег: '{other.tag}') вошел в триггер '{triggerCollider.gameObject.name}'. Подходит под Player: {isPlayer}");

        if (isPlayer)
        {
            isPlayerInside = true;
            if (audioSource.clip == null)
            {
                Debug.LogWarning($"[ProximityAudioController] В AudioSource на '{gameObject.name}' не назначен аудиоклип!", this);
            }
            if (!requiresPower || IsPowerWorking())
            {
                StartFade(maxVolume, fadeInDuration);
            }
        }
    }

    public void HandleTriggerExit(Collider other)
    {
        bool isPlayer = other.CompareTag(playerTag) || 
                         other.transform.root.CompareTag(playerTag) || 
                         (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag));

        Debug.Log($"[ProximityAudioController] Объект '{other.name}' вышел из триггера '{triggerCollider.gameObject.name}'. Подходит под Player: {isPlayer}");

        if (isPlayer)
        {
            isPlayerInside = false;
            StartFade(0f, fadeOutDuration);
        }
    }

    private void StartFade(float target, float duration)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeRoutine(target, duration));
    }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        if (target > 0f && !audioSource.isPlaying)
        {
            audioSource.UnPause();
            if (!audioSource.isPlaying) 
                audioSource.Play();
        }

        if (duration <= 0f)
        {
            audioSource.volume = target;
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                audioSource.volume = Mathf.Lerp(startVolume, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        audioSource.volume = target;

        if (target <= 0.001f && pauseWhenSilent && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }
}

/// <summary>
/// Вспомогательный класс-прокси.
/// Если триггер находится на другом объекте, этот скрипт перенаправляет события в основной контроллер.
/// </summary>
public class AudioTriggerProxy : MonoBehaviour
{
    public ProximityAudioController controller;

    private void OnTriggerEnter(Collider other)
    {
        if (controller != null)
        {
            controller.HandleTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller != null)
        {
            controller.HandleTriggerExit(other);
        }
    }
}
