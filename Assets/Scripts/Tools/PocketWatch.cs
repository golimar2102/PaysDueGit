using UnityEngine;

public class PocketWatch : MonoBehaviour
{
    [Header("Стрелки часов")]
    [Tooltip("Перетащи сюда дочерний объект часовой стрелки")]
    public Transform hourHand;
    [Tooltip("Перетащи сюда дочерний объект минутной стрелки")]
    public Transform minuteHand;
    [Tooltip("Перетащи сюда дочерний объект секундной стрелки (если есть)")]
    public Transform secondHand;

    [Header("Настройки вращения")]
    [Tooltip("Вокруг какой оси крутятся стрелки? Попробуй X: 1, Y: 1 или Z: 1")]
    public Vector3 rotationAxis = new Vector3(0, 0, 1);
    [Tooltip("Поставь галочку, если время идет в обратную сторону")]
    public bool reverseRotation = false;

    [Header("Анимация")]
    public Animator animator;
    [Tooltip("Имя параметра (Bool) в Аниматоре для открытия крышки")]
    public string isOpenParam = "IsOpen";
    private bool isOpen = false;

    [Header("Звуки (Опционально)")]
    public AudioSource openSound;
    public AudioSource closeSound;
    [Tooltip("Звук тиканья (включится, когда часы открыты)")]
    public AudioSource tickingSound;

    [Header("Управление")]
    [Tooltip("Ключ кнопки из настроек. По умолчанию Mouse1 (ПКМ - прицел/использование)")]
    public string useKeyPref = "Key_Aim";
    public KeyCode defaultUseKey = KeyCode.Mouse1;

    // Кэшированные данные
    private KeyCode cachedUseKey;
    private int isOpenParamHash;

    private Vector3 hourStartEuler;
    private Vector3 minuteStartEuler;
    private Vector3 secondStartEuler;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();

        // Кэшируем клавишу и хэш параметра аниматора
        cachedUseKey    = (KeyCode)PlayerPrefs.GetInt(useKeyPref, (int)defaultUseKey);
        isOpenParamHash = Animator.StringToHash(isOpenParam);

        if (hourHand != null)   hourStartEuler   = hourHand.localEulerAngles;
        if (minuteHand != null) minuteStartEuler = minuteHand.localEulerAngles;
        if (secondHand != null) secondStartEuler = secondHand.localEulerAngles;

        if (tickingSound != null) tickingSound.Stop();
    }

    /// <summary>Вызвать из SettingsManager после изменения привязок клавиш.</summary>
    public void RefreshKeyBinding()
    {
        cachedUseKey = (KeyCode)PlayerPrefs.GetInt(useKeyPref, (int)defaultUseKey);
    }

    void Update()
    {
        HandleInput();
    }

    void LateUpdate()
    {
        UpdateClockHands();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(cachedUseKey))
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen) return;
            ToggleWatch();
        }
    }

    private void ToggleWatch()
    {
        isOpen = !isOpen;

        if (animator != null)
            animator.SetBool(isOpenParamHash, isOpen);

        if (isOpen)
        {
            if (openSound != null) openSound.Play();
            if (tickingSound != null) tickingSound.Play();
        }
        else
        {
            if (closeSound != null) closeSound.Play();
            if (tickingSound != null) tickingSound.Stop();
        }
    }

    private void UpdateClockHands()
    {
        if (DayNightCycle.Instance == null)
        {
            Debug.LogWarning("Карманные часы: Не могу найти скрипт DayNightCycle на сцене!");
            return;
        }

        float time = DayNightCycle.Instance.timeOfDay;

        float dir = reverseRotation ? -1f : 1f;

        float hourAngle   = (time % 12f) * 30f * dir;
        float minuteAngle = (time % 1f)  * 360f * dir;
        float secondAngle = ((time * 60f) % 1f) * 360f * dir;

        if (hourHand != null)
            hourHand.localEulerAngles = hourStartEuler + (rotationAxis * hourAngle);

        if (minuteHand != null)
            minuteHand.localEulerAngles = minuteStartEuler + (rotationAxis * minuteAngle);

        if (secondHand != null)
            secondHand.localEulerAngles = secondStartEuler + (rotationAxis * secondAngle);
    }

    void OnDisable()
    {
        isOpen = false;
        if (animator != null) animator.SetBool(isOpenParamHash, false);
        if (tickingSound != null) tickingSound.Stop();
    }
}