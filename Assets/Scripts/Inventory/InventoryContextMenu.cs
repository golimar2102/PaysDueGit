using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Localization;

public class InventoryContextMenu : MonoBehaviour
{
    public static InventoryContextMenu Instance { get; private set; }

    [Header("UI Elements Reference")]
    [Tooltip("Корневая панель контекстного меню. Если пусто, используется этот GameObject.")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button pourButton;
    [SerializeField] private Button drinkButton;
    [SerializeField] private Canvas parentCanvas;

    [Header("Локализация (Необязательно)")]
    [SerializeField] private LocalizedString locPourOut;
    [SerializeField] private LocalizedString locDrink;

    [Header("Текстовые элементы кнопок (Определятся автоматически, если пусто)")]
    [SerializeField] private TextMeshProUGUI pourText;
    [SerializeField] private TextMeshProUGUI drinkText;

    private InventorySlot currentSlot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (menuPanel == null)
        {
            menuPanel = gameObject;
        }
    }

    void Start()
    {
        // Поиск кнопок по иерархии, если они не заданы в инспекторе
        if (pourButton == null)
        {
            pourButton = transform.Find("PourButton")?.GetComponent<Button>();
        }
        if (drinkButton == null)
        {
            drinkButton = transform.Find("DrinkButton")?.GetComponent<Button>();
        }

        // Поиск текстовых полей кнопок
        if (pourText == null && pourButton != null)
        {
            pourText = pourButton.transform.Find("PourText")?.GetComponent<TextMeshProUGUI>();
            if (pourText == null)
            {
                pourText = pourButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
        if (drinkText == null && drinkButton != null)
        {
            drinkText = drinkButton.transform.Find("DrinkText")?.GetComponent<TextMeshProUGUI>();
            if (drinkText == null)
            {
                drinkText = drinkButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        // Привязка событий клика
        if (pourButton != null)
        {
            pourButton.onClick.RemoveAllListeners();
            pourButton.onClick.AddListener(OnPourClicked);
        }
        else
        {
            Debug.LogWarning("[InventoryContextMenu] Кнопка PourButton не назначена и не найдена!");
        }

        if (drinkButton != null)
        {
            drinkButton.onClick.RemoveAllListeners();
            drinkButton.onClick.AddListener(OnDrinkClicked);
        }
        else
        {
            Debug.LogWarning("[InventoryContextMenu] Кнопка DrinkButton не назначена и не найдена!");
        }

        // Обновление локализованных надписей
        UpdateLocalizationText();

        Hide();
    }

    public void UpdateLocalizationText()
    {
        if (pourText != null)
        {
            if (locPourOut != null && !locPourOut.IsEmpty)
                pourText.text = locPourOut.GetLocalizedString();
            else
                pourText.text = "Вылить жидкость";
        }

        if (drinkText != null)
        {
            if (locDrink != null && !locDrink.IsEmpty)
                drinkText.text = locDrink.GetLocalizedString();
            else
                drinkText.text = "Выпить";
        }
    }

    public void Show(InventorySlot slot, Vector2 screenPosition)
    {
        Debug.Log($"[InventoryContextMenu] Show called. Slot: {slot.name}, ScreenPos: {screenPosition}");
        currentSlot = slot;
        
        GameObject targetPanel = menuPanel != null ? menuPanel : gameObject;
        targetPanel.SetActive(true);
        targetPanel.transform.SetAsLastSibling();

        Canvas canvas = parentCanvas;
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
        if (canvas == null)
        {
            Debug.LogError("[InventoryContextMenu] parentCanvas is null! Please assign it in the Inspector or place the menu inside a Canvas.");
            return;
        }

        RectTransform rect = targetPanel.GetComponent<RectTransform>();
        RectTransform parentRect = rect.parent as RectTransform;
        if (parentRect == null)
        {
            Debug.LogError("[InventoryContextMenu] parentRect is null!");
            return;
        }

        // Принудительно ставим пивот в верхний левый угол (0, 1) для позиционирования вправо-вниз от курсора
        rect.pivot = new Vector2(0f, 1f);

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        Debug.Log($"[InventoryContextMenu] Canvas renderMode: {canvas.renderMode}, Cam: {cam}");
        Vector2 localPos;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, cam, out localPos))
        {
            float menuWidth = rect.rect.width;
            float menuHeight = rect.rect.height;

            // Изначально смещаем позицию вправо-вниз (как тултип предмета)
            Vector2 finalLocalPos = localPos + new Vector2(20f, -20f);
            Debug.Log($"[InventoryContextMenu] localPos: {localPos}, finalLocalPos before clamping: {finalLocalPos}, menu size: {menuWidth}x{menuHeight}");

            // Границы родительского контейнера в его локальных координатах
            float parentWidth = parentRect.rect.width;
            float parentHeight = parentRect.rect.height;

            float minX = -parentWidth * parentRect.pivot.x;
            float maxX = parentWidth * (1f - parentRect.pivot.x);
            float minY = -parentHeight * parentRect.pivot.y;
            float maxY = parentHeight * (1f - parentRect.pivot.y);
            Debug.Log($"[InventoryContextMenu] parent bounds: X[{minX}, {maxX}], Y[{minY}, {maxY}]");

            // Проверка правой границы
            if (finalLocalPos.x + menuWidth > maxX)
            {
                finalLocalPos.x = localPos.x - menuWidth - 20f;
                Debug.Log($"[InventoryContextMenu] Clamping right. New X: {finalLocalPos.x}");
            }
            // Ограничиваем левую границу
            if (finalLocalPos.x < minX)
            {
                finalLocalPos.x = minX;
                Debug.Log($"[InventoryContextMenu] Clamping left. New X: {finalLocalPos.x}");
            }

            // Проверка нижней границы
            if (finalLocalPos.y - menuHeight < minY)
            {
                // Сдвигаем вверх, чтобы меню не уходило за нижний край
                finalLocalPos.y = minY + menuHeight;
                Debug.Log($"[InventoryContextMenu] Clamping bottom. New Y: {finalLocalPos.y}");
            }
            // Ограничиваем верхнюю границу
            if (finalLocalPos.y > maxY)
            {
                finalLocalPos.y = maxY;
                Debug.Log($"[InventoryContextMenu] Clamping top. New Y: {finalLocalPos.y}");
            }

            rect.localPosition = finalLocalPos;
            Debug.Log($"[InventoryContextMenu] Assigned localPosition: {rect.localPosition}");
        }
        else
        {
            Debug.LogError("[InventoryContextMenu] ScreenPointToLocalPointInRectangle returned false!");
        }
    }

    public void Hide()
    {
        GameObject targetPanel = menuPanel != null ? menuPanel : gameObject;
        targetPanel.SetActive(false);
        currentSlot = null;
    }

    private void OnPourClicked()
    {
        if (currentSlot == null || currentSlot.IsEmpty() || currentSlot.itemData == null) return;
        
        PourOutLiquid(currentSlot.itemData);
        currentSlot.UpdateSlotUI();

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
        }

        Hide();
    }

    private void OnDrinkClicked()
    {
        if (currentSlot == null || currentSlot.IsEmpty() || currentSlot.itemData == null) return;

        DrinkLiquid(currentSlot.itemData);
        currentSlot.UpdateSlotUI();

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SelectSlot(InventoryManager.Instance.selectedSlotIndex);
        }

        Hide();
    }

    public void PourOutLiquid(InventoryItemData data)
    {
        data.currentLiquidType = LiquidType.None;
        data.currentAmount = 0;
        data.itemName = data.baseItemName;

        GameObject prefab = InventoryManager.Instance != null ? InventoryManager.Instance.GetPrefabByID(data.itemID) : null;
        if (prefab != null)
        {
            ConsumableItem cons = prefab.GetComponent<ConsumableItem>();
            if (cons != null)
            {
                Sprite[] emptyIcons = cons.GetFillIconsForLiquid(LiquidType.None);
                if (emptyIcons != null && emptyIcons.Length > 0)
                {
                    data.itemIcon = emptyIcons[0];
                    data.fillIcons = emptyIcons;
                }
                else
                {
                    data.itemIcon = cons.fillIcons != null && cons.fillIcons.Length > 0 ? cons.fillIcons[0] : prefab.GetComponent<PickUpItem>().itemIcon;
                    data.fillIcons = cons.fillIcons;
                }
            }
        }
        Debug.Log($"[InventoryContextMenu] Pour out liquid from {data.baseItemName}. Amount: {data.currentAmount}");
    }

    public void DrinkLiquid(InventoryItemData data)
    {
        if (data.currentAmount <= 0) return;

        int amountToConsume = Mathf.Min(data.amountPerUse, data.currentAmount);

        if (PlayerStats.Instance != null)
        {
            float thirstRestored = 0f;
            float damageToTake = 0f;

            switch (data.currentLiquidType)
            {
                case LiquidType.CleanWater:
                    thirstRestored = amountToConsume;
                    break;
                case LiquidType.DirtyWater:
                    thirstRestored = amountToConsume * 0.5f;
                    damageToTake = amountToConsume * 0.1f;
                    break;
                case LiquidType.Blood:
                    thirstRestored = amountToConsume * 0.2f;
                    damageToTake = amountToConsume * 0.15f;
                    break;
                case LiquidType.Oil:
                case LiquidType.Biomass:
                    damageToTake = amountToConsume * 0.5f;
                    break;
            }

            if (thirstRestored > 0f)
            {
                PlayerStats.Instance.QuenchThirst(thirstRestored);
            }
            if (damageToTake > 0f)
            {
                PlayerStats.Instance.TakeDamage(damageToTake);
            }
        }

        data.currentAmount -= amountToConsume;

        if (data.currentAmount <= 0)
        {
            PourOutLiquid(data);
        }
        else
        {
            if (data.fillIcons != null && data.fillIcons.Length > 0)
            {
                float fillPercentage = Mathf.Clamp01((float)data.currentAmount / data.maxAmount);
                int index = Mathf.RoundToInt(fillPercentage * (data.fillIcons.Length - 1));
                data.itemIcon = data.fillIcons[index];
            }
        }

        Debug.Log($"[InventoryContextMenu] Drank from {data.baseItemName}. Liquid: {data.currentLiquidType}, remaining: {data.currentAmount}");
    }

    void Update()
    {
        GameObject targetPanel = menuPanel != null ? menuPanel : gameObject;
        if (targetPanel != null && targetPanel.activeSelf && Input.GetMouseButtonDown(0))
        {
            Canvas canvas = parentCanvas != null ? parentCanvas : GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform rectTrans = targetPanel.GetComponent<RectTransform>();
                if (!RectTransformUtility.RectangleContainsScreenPoint(rectTrans, Input.mousePosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera))
                {
                    Hide();
                }
            }
        }
    }
}
