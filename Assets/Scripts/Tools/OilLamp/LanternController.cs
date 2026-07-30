using UnityEngine;

public class LanternController : MonoBehaviour
{
    [Header("Свет и Эффекты")]
    public Light[] lanternLight;
    public ParticleSystem flameParticles;
    public AudioSource toggleSound;

    [Header("Плавное включение")]
    public float fadeSpeed = 3f;
    private float[] initialLightIntensities;
    private float currentFade = 0f;
    private float targetFade = 0f;

    [Header("Управление")]
    public string useKeyPref = "Key_Shoot";
    public KeyCode defaultUseKey = KeyCode.Mouse0;
    private bool isLit = false;
    public bool IsLit => isLit;

    // Кэшированная клавиша — читаем PlayerPrefs один раз
    private KeyCode cachedUseKey;

    [Header("Топливо")]
    public float maxFuel = 100f;
    public float currentFuel = 100f;
    [Tooltip("Сколько топлива тратится в секунду (Например, 1f = хватит на 100 секунд)")]
    public float fuelDrainRate = 1f;

    [Header("Настройки Свечи")]
    [Tooltip("Если включено, предмет расходуется как свеча: при 0% прочности тратится 1 шт. из стака, а при достижении 0 шт. предмет исчезает.")]
    public bool destroyOnDepletion = false;

    // Ссылка на данные инвентаря активной лампы (устанавливается из InventoryManager)
    [HideInInspector] public InventoryItemData activeItemData;

    [Header("Визуал Топлива (Окошко)")]
    [Tooltip("Перетащи сюда ПУСТОЙ ОБЪЕКТ (Fuel_Pivot), внутри которого лежит кубик жидкости")]
    public Transform fuelVisualPivot;
    private Vector3 initialFuelScale;

    [Header("Процедурная тряска ручки")]
    public float rotationSway = 1.5f;
    public float idleSwayAmount = 0.5f;
    public float smoothDamp = 0.1f;

    [Header("Ссылки (Авто-поиск)")]
    public Transform cameraTransform;

    private Vector3 lastEulerAngles;
    private float currentVelocity;
    private float currentSwayZ;
    private Quaternion startRotation;

    void Start()
    {
        startRotation = transform.localRotation;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            lastEulerAngles = cameraTransform.eulerAngles;

        if (fuelVisualPivot != null)
            initialFuelScale = fuelVisualPivot.localScale;

        // Кэшируем клавишу один раз
        cachedUseKey = (KeyCode)PlayerPrefs.GetInt(useKeyPref, (int)defaultUseKey);

        if (lanternLight != null)
        {
            initialLightIntensities = new float[lanternLight.Length];
            for (int i = 0; i < lanternLight.Length; i++)
            {
                if (lanternLight[i] != null)
                {
                    initialLightIntensities[i] = lanternLight[i].intensity;
                    lanternLight[i].intensity = isLit ? initialLightIntensities[i] : 0f;
                    lanternLight[i].enabled = isLit;
                }
            }
        }

        targetFade = isLit ? 1f : 0f;
        currentFade = targetFade;

        UpdateLightState();
    }

    /// <summary>Вызвать из SettingsManager после изменения привязок клавиш.</summary>
    public void RefreshKeyBinding()
    {
        cachedUseKey = (KeyCode)PlayerPrefs.GetInt(useKeyPref, (int)defaultUseKey);
    }

    void Update()
    {
        HandleInput();
        HandleFuel();
        HandleLightFade();
    }

    private void HandleLightFade()
    {
        if (lanternLight == null || initialLightIntensities == null) return;

        if (Mathf.Abs(currentFade - targetFade) > 0.001f)
        {
            currentFade = Mathf.MoveTowards(currentFade, targetFade, Time.deltaTime * fadeSpeed);

            for (int i = 0; i < lanternLight.Length; i++)
            {
                if (lanternLight[i] != null)
                {
                    lanternLight[i].intensity = initialLightIntensities[i] * currentFade;
                    lanternLight[i].enabled = lanternLight[i].intensity > 0.001f;
                }
            }
        }
    }

    void LateUpdate()
    {
        ApplyHandleSway();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(cachedUseKey))
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen) return;

            if (!isLit && currentFuel <= 0f)
            {
                Debug.Log("Нет керосина!");
                return;
            }

            ToggleLantern();
        }
    }

    private void ToggleLantern()
    {
        isLit = !isLit;
        UpdateLightState();
        if (toggleSound != null) toggleSound.Play();
    }

    void OnDisable()
    {
        // При выключении (смена предмета) — сохраняем топливо в слот инвентаря
        if (activeItemData != null)
            activeItemData.lanternFuel = currentFuel;

        // Выключаем лампу при убирании из рук
        isLit = false;
        targetFade = 0f;
        currentFade = 0f; // Сбрасываем текущее затухание, чтобы при следующем доставании лампа не светилась

        if (lanternLight != null)
        {
            for (int i = 0; i < lanternLight.Length; i++)
            {
                if (lanternLight[i] != null)
                {
                    lanternLight[i].intensity = 0f;
                    lanternLight[i].enabled = false;
                }
            }
        }

        if (flameParticles != null)
        {
            // Очищаем частицы, так как объект убирается из рук
            flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void HandleFuel()
    {
        if (isLit)
        {
            currentFuel -= fuelDrainRate * Time.deltaTime;
            if (currentFuel <= 0f)
            {
                currentFuel = 0f;
                isLit = false;
                UpdateLightState();

                if (destroyOnDepletion && activeItemData != null)
                {
                    ConsumeOneActiveItem();
                    return;
                }
            }
            
            // Сохраняем топливо в слот инвентаря в реальном времени
            if (activeItemData != null)
            {
                activeItemData.lanternFuel = currentFuel;
                // Обновляем полоску прочности в реальном времени на UI
                if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
                {
                    int idx = InventoryManager.Instance.selectedSlotIndex;
                    if (idx >= 0 && idx < InventoryManager.Instance.hotbarSlots.Length)
                    {
                        var slot = InventoryManager.Instance.hotbarSlots[idx];
                        if (slot != null && slot.itemData == activeItemData)
                        {
                            slot.UpdateSlotUI();
                        }
                    }
                }
            }
        }

        if (fuelVisualPivot != null)
        {
            float fuelPercent = currentFuel / maxFuel;
            fuelVisualPivot.localScale = new Vector3(
                initialFuelScale.x,
                initialFuelScale.y,
                initialFuelScale.z * fuelPercent
            );
        }
    }

    private void ConsumeOneActiveItem()
    {
        if (InventoryManager.Instance == null || activeItemData == null) return;

        int selectedIdx = InventoryManager.Instance.selectedSlotIndex;
        if (selectedIdx < 0 || selectedIdx >= InventoryManager.Instance.hotbarSlots.Length) return;

        InventorySlot activeSlot = InventoryManager.Instance.hotbarSlots[selectedIdx];
        if (activeSlot == null || activeSlot.itemData != activeItemData) return;

        if (activeItemData.isStackable && activeItemData.amount > 1)
        {
            activeItemData.amount--;
            activeItemData.lanternFuel = maxFuel;
            currentFuel = maxFuel;

            activeSlot.UpdateSlotUI();
            
            // Перевыбираем слот, чтобы обновить имя (xКоличество) на UI
            InventoryManager.Instance.SelectSlot(selectedIdx);
        }
        else
        {
            activeSlot.ClearSlot();
            InventoryManager.Instance.SelectSlot(selectedIdx);
        }
    }

    private void UpdateLightState()
    {
        targetFade = isLit ? 1f : 0f;

        if (flameParticles != null)
        {
            if (isLit)
            {
                flameParticles.Play(true);
            }
            else
            {
                // Плавно останавливаем генерацию новых частиц, старые догорают
                flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void ApplyHandleSway()
    {
        float turnVelocity = 0f;
        if (cameraTransform != null && Time.deltaTime > 0f)
        {
            float deltaY = Mathf.DeltaAngle(lastEulerAngles.y, cameraTransform.eulerAngles.y);
            turnVelocity = (deltaY / Time.deltaTime);
            lastEulerAngles = cameraTransform.eulerAngles;
        }

        float targetZ = turnVelocity * rotationSway * 0.01f;
        targetZ = Mathf.Clamp(targetZ, -15f, 15f);

        currentSwayZ = Mathf.SmoothDamp(currentSwayZ, targetZ, ref currentVelocity, smoothDamp);
        float idleSway = Mathf.Sin(Time.time * 3f) * idleSwayAmount;

        transform.localRotation = startRotation * Quaternion.Euler(0, 0, currentSwayZ + idleSway);
    }

    public void AddFuel(float amount)
    {
        currentFuel += amount;
        if (currentFuel > maxFuel) currentFuel = maxFuel;
        // Сохраняем сразу в данные слота
        if (activeItemData != null)
            activeItemData.lanternFuel = currentFuel;
        Debug.Log($"Фонарь заправлен! Топливо: {currentFuel}");
    }

    /// <summary>
    /// Вызывается из InventoryManager при экипировке лампы.
    /// Устанавливает ссылку на данные слота и загружает топливо.
    /// </summary>
    public void SetActiveItemData(InventoryItemData data)
    {
        activeItemData = data;
        if (data != null)
        {
            if (data.lanternFuel < 0f)
                data.lanternFuel = maxFuel; // первое взятие = полный бак
            currentFuel = data.lanternFuel;
        }
    }
}