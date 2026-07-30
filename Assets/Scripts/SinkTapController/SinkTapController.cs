using UnityEngine;
using UnityEngine.Localization;

public class SinkTapController : MonoBehaviour
{
    [Header("Бак с водой (Источник)")]
    [Tooltip("Контроллер бака, откуда берется вода")]
    public WaterCoolerController tankController;

    [Header("Визуальные элементы крана")]
    [Tooltip("Ручка крана для анимации")]
    public Transform handleTransform;
    [Tooltip("Угол поворота в закрытом состоянии (Euler)")]
    public Vector3 handleClosedRotation = Vector3.zero;
    [Tooltip("Угол поворота в открытом состоянии (Euler)")]
    public Vector3 handleOpenRotation = new Vector3(-30f, 0f, 0f);
    [Tooltip("Скорость перехода ручки между состояниями")]
    public float handleTransitionSpeed = 5f;

    [Space]
    [Tooltip("Объект струи воды")]
    public GameObject waterStreamObj;
    [Tooltip("Рендерер струи воды для смены материала")]
    public Renderer waterStreamRenderer;

    [Header("Настройки раковины")]
    [Tooltip("Плоскость воды в раковине")]
    public Transform sinkWaterPlane;
    [Tooltip("Рендерер плоскости воды в раковине для смены материала")]
    public Renderer sinkWaterRenderer;
    [Tooltip("Компонент LiquidSource на плоскости воды")]
    public LiquidSource sinkLiquidSource;
    [Tooltip("Масштаб по оси Z при пустой раковине")]
    public float minSinkWaterScaleZ = 0f;
    [Tooltip("Масштаб по оси Z при полной раковине")]
    public float maxSinkWaterScaleZ = 1f;
    [Tooltip("Максимальный объем воды в раковине")]
    public float maxSinkCapacity = 10f;
    [Tooltip("Текущий объем воды в раковине")]
    public float currentSinkWater = 0f;

    [Header("Скорости потока")]
    [Tooltip("Скорость расхода воды из бака в секунду")]
    public float tankDrainRate = 1f;
    [Tooltip("Скорость наполнения раковины в секунду при течи из крана")]
    public float sinkFillRate = 1f;
    [Tooltip("Скорость слива раковины в секунду (0 - вода не сливается)")]
    public float sinkDrainRate = 0.2f;

    [Header("Аудио")]
    [Tooltip("Аудиоисточник для звука текущей воды (зацикленный)")]
    public AudioSource tapAudio;

    [Header("Локализация")]
    [Tooltip("Обводка интерактивного элемента")]
    public Outline outline;
    [Tooltip("Локализованный текст включения крана")]
    public LocalizedString openPrompt;
    [Tooltip("Локализованный текст выключения крана")]
    public LocalizedString closePrompt;
    [Tooltip("Локализованный текст зачерпывания воды из раковины")]
    public LocalizedString scoopPrompt;

    [HideInInspector]
    public bool isOpen = false;

    private Vector3 initialSinkWaterScale;

    void Awake()
    {
        if (outline == null)
        {
            outline = GetComponent<Outline>();
        }
        if (outline == null)
        {
            outline = GetComponentInChildren<Outline>(true);
        }
        SetHighlight(false);

        if (waterStreamObj != null)
        {
            waterStreamObj.SetActive(false);
        }

        if (sinkWaterPlane != null)
        {
            initialSinkWaterScale = sinkWaterPlane.localScale;
            sinkWaterPlane.gameObject.SetActive(currentSinkWater > 0f);
            if (sinkLiquidSource == null)
            {
                sinkLiquidSource = sinkWaterPlane.GetComponent<LiquidSource>();
            }
        }
    }

    void Update()
    {
        // 1. Анимация ручки крана (только вращение)
        if (handleTransform != null)
        {
            handleTransform.localRotation = Quaternion.Slerp(
                handleTransform.localRotation, 
                Quaternion.Euler(isOpen ? handleOpenRotation : handleClosedRotation), 
                Time.deltaTime * handleTransitionSpeed
            );
        }

        // 2. Логика течи воды
        bool hasWaterInTank = tankController != null && tankController.currentWater > 0f && tankController.currentLiquidType != LiquidType.None;
        bool isStreamRunning = isOpen && hasWaterInTank;

        if (waterStreamObj != null)
        {
            waterStreamObj.SetActive(isStreamRunning);
        }

        // 3. Звук
        if (tapAudio != null)
        {
            if (isStreamRunning)
            {
                if (!tapAudio.isPlaying)
                {
                    tapAudio.loop = true;
                    tapAudio.Play();
                }
            }
            else
            {
                if (tapAudio.isPlaying)
                {
                    tapAudio.Stop();
                }
            }
        }

        // 4. Обновление материалов воды
        if (isStreamRunning)
        {
            UpdateVisuals();
        }

        // 5. Баланс воды (Наполнение / Слив раковины)
        float sinkChange = 0f;
        if (isStreamRunning)
        {
            float drained = tankDrainRate * Time.deltaTime;
            float actualDrained = Mathf.Min(drained, tankController.currentWater);
            tankController.currentWater -= actualDrained;

            if (tankController.currentWater <= 0f)
            {
                tankController.currentWater = 0f;
                tankController.currentLiquidType = LiquidType.None;
            }

            sinkChange += (actualDrained / tankDrainRate) * sinkFillRate;
        }

        if (sinkDrainRate > 0f)
        {
            sinkChange -= sinkDrainRate * Time.deltaTime;
        }

        currentSinkWater = Mathf.Clamp(currentSinkWater + sinkChange, 0f, maxSinkCapacity);

        // Синхронизация с компонентом LiquidSource на раковине
        if (sinkLiquidSource != null)
        {
            // Если игрок зачерпнул воду напрямую через LiquidSource (amount уменьшился в коде)
            if (sinkLiquidSource.remainingAmount < Mathf.FloorToInt(currentSinkWater))
            {
                currentSinkWater = sinkLiquidSource.remainingAmount;
            }

            // Обновляем параметры LiquidSource
            sinkLiquidSource.remainingAmount = Mathf.RoundToInt(currentSinkWater);
            sinkLiquidSource.liquidType = tankController != null ? tankController.currentLiquidType : LiquidType.None;
            sinkLiquidSource.isInfinite = false;
        }

        // 6. Масштабирование воды в раковине (только по оси Z)
        if (sinkWaterPlane != null)
        {
            bool shouldBeActive = currentSinkWater > 0.01f;
            if (sinkWaterPlane.gameObject.activeSelf != shouldBeActive)
            {
                sinkWaterPlane.gameObject.SetActive(shouldBeActive);
            }

            if (shouldBeActive)
            {
                float currentZScale = Mathf.Lerp(minSinkWaterScaleZ, maxSinkWaterScaleZ, currentSinkWater / maxSinkCapacity);
                Vector3 newScale = initialSinkWaterScale;
                newScale.z = currentZScale;
                sinkWaterPlane.localScale = newScale;
            }
        }
    }

    private void UpdateVisuals()
    {
        if (tankController == null) return;

        LiquidType currentLiquid = tankController.currentLiquidType;
        Material liquidMat = null;

        if (tankController.liquidMaterials != null)
        {
            foreach (var mapping in tankController.liquidMaterials)
            {
                if (mapping.liquidType == currentLiquid)
                {
                    liquidMat = mapping.material;
                    break;
                }
            }
        }

        if (liquidMat != null)
        {
            if (waterStreamRenderer != null && waterStreamRenderer.sharedMaterial != liquidMat)
            {
                waterStreamRenderer.sharedMaterial = liquidMat;
            }
            if (sinkWaterRenderer != null && sinkWaterRenderer.sharedMaterial != liquidMat)
            {
                sinkWaterRenderer.sharedMaterial = liquidMat;
            }
        }
    }

    public void ToggleTap()
    {
        isOpen = !isOpen;
    }

    public void SetHighlight(bool active)
    {
        if (outline != null)
        {
            outline.enabled = active;
        }
    }

    public string GetInteractPrompt(string keyName)
    {
        if (isOpen)
        {
            if (closePrompt != null && !closePrompt.IsEmpty)
            {
                return closePrompt.GetLocalizedString(new { key = keyName });
            }
        }
        else
        {
            if (openPrompt != null && !openPrompt.IsEmpty)
            {
                return openPrompt.GetLocalizedString(new { key = keyName });
            }
        }

        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        if (isOpen)
        {
            return isEn 
                ? $"<color=#FFD700>[{keyName}]</color> Turn off tap" 
                : $"<color=#FFD700>[{keyName}]</color> Выключить кран";
        }
        else
        {
            return isEn 
                ? $"<color=#FFD700>[{keyName}]</color> Turn on tap" 
                : $"<color=#FFD700>[{keyName}]</color> Включить кран";
        }
    }

    public void ScoopWater(InventorySlot slot)
    {
        if (slot == null || slot.itemData == null || currentSinkWater <= 0.01f || tankController == null) return;

        InventoryItemData data = slot.itemData;
        int maxCap = data.maxAmount;
        int current = data.currentAmount;
        int needed = maxCap - current;

        if (needed <= 0) return;

        LiquidType liquidInSink = tankController.currentLiquidType;
        if (liquidInSink == LiquidType.None) return;

        if (data.currentLiquidType != LiquidType.None && data.currentLiquidType != liquidInSink) return;

        // Рассчитываем сколько зачерпнем (в целых единицах)
        int amountToScoop = Mathf.Min(needed, Mathf.FloorToInt(currentSinkWater));
        if (amountToScoop <= 0)
        {
            amountToScoop = 1;
        }

        float actualScoop = Mathf.Min(amountToScoop, currentSinkWater);

        currentSinkWater -= actualScoop;
        if (sinkLiquidSource != null)
        {
            sinkLiquidSource.remainingAmount = Mathf.RoundToInt(currentSinkWater);
        }
        data.currentAmount += Mathf.RoundToInt(actualScoop);
        data.currentLiquidType = liquidInSink;

        if (string.IsNullOrEmpty(data.baseItemName))
        {
            data.baseItemName = data.itemName;
        }
        string liquidSuffix = PlayerInteract.GetLocalizedLiquidName(liquidInSink);
        data.itemName = $"{data.baseItemName} ({liquidSuffix})";

        if (InventoryManager.Instance != null)
        {
            GameObject prefab = InventoryManager.Instance.GetPrefabByID(data.itemID);
            if (prefab != null)
            {
                ConsumableItem cons = prefab.GetComponent<ConsumableItem>();
                if (cons != null)
                {
                    Sprite[] customIcons = cons.GetFillIconsForLiquid(liquidInSink);
                    data.fillIcons = (customIcons != null && customIcons.Length > 0) ? customIcons : cons.fillIcons;
                }
            }
        }

        if (data.fillIcons != null && data.fillIcons.Length > 0)
        {
            float fillPct = Mathf.Clamp01((float)data.currentAmount / data.maxAmount);
            int idx = Mathf.RoundToInt(fillPct * (data.fillIcons.Length - 1));
            data.itemIcon = data.fillIcons[idx];
        }

        slot.UpdateSlotUI();
    }

    public string GetScoopPrompt(string keyName)
    {
        if (tankController == null) return "";
        string liquidName = PlayerInteract.GetLocalizedLiquidName(tankController.currentLiquidType);

        if (scoopPrompt != null && !scoopPrompt.IsEmpty)
        {
            return scoopPrompt.GetLocalizedString(new { key = keyName, liquid = liquidName });
        }

        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        return isEn 
            ? $"<color=#FFD700>[{keyName}]</color> Gather {liquidName} from sink" 
            : $"<color=#FFD700>[{keyName}]</color> Набрать {liquidName} из раковины";
    }
}
