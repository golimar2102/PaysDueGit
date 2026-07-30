using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class WorkbenchUI : MonoBehaviour
{
    [Header("База рецептов")]
    [Tooltip("Список всех доступных рецептов крафта")]
    public List<CraftingRecipe> recipeDatabase = new List<CraftingRecipe>();

    [Header("Кнопки Категорий")]
    [Tooltip("Связь категорий и физических кнопок в UI")]
    public List<CategoryButtonLink> categoryButtons = new List<CategoryButtonLink>();
    [Tooltip("Текстовое поле для отображения названия текущей категории")]
    public TextMeshProUGUI categoryNameText;

    [Header("Список Рецептов (Scroll View)")]
    [Tooltip("Контейнер Scroll View Content, куда будут спавниться элементы списка рецептов")]
    public Transform recipeListContent;
    [Tooltip("Преаб элемента списка рецептов")]
    public GameObject recipeListElementPrefab;

    [Header("Панель Деталей Рецепта")]
    [Tooltip("Текстовое поле названия выбранного предмета")]
    public TextMeshProUGUI selectedItemNameText;
    [Tooltip("Изображение иконки выбранного предмета")]
    public Image selectedItemImage;
    [Tooltip("Контейнер (Grid / Layout), куда спавнятся детальные ингредиенты")]
    public Transform ingredientsContainer;
    [Tooltip("Префаб ячейки ингредиента для детальной панели")]
    public GameObject ingredientSlotPrefab;

    [Header("Управление Крафтом")]
    [Tooltip("Кнопка запуска крафта")]
    public Button craftButton;
    [Tooltip("Кнопка увеличения количества крафта (+1)")]
    public Button plusButton;
    [Tooltip("Кнопка уменьшения количества крафта (-1)")]
    public Button minusButton;
    [Tooltip("Текстовое поле количества крафта (опционально)")]
    public TextMeshProUGUI craftAmountText;
    [Tooltip("Звук успешного крафта")]
    public AudioSource craftSuccessSound;
    [Tooltip("Звук неуспешного крафта (например, нет места или ресурсов)")]
    public AudioSource craftFailSound;

    [Header("Анимация Крафта")]
    [Tooltip("Transform элемент иконки предмета, который сжимается и разжимается при крафте")]
    public RectTransform itemIconTransform;
    [Tooltip("Transform круглого элемента, который делает круговой оборот при крафте")]
    public RectTransform circleTransform;
    [Tooltip("Длительность сжатия иконки (сек)")]
    public float shrinkDuration = 0.2f;
    [Tooltip("Длительность вращения круга (сек)")]
    public float rotationDuration = 0.35f;
    [Tooltip("Длительность разжатия иконки (сек)")]
    public float expandDuration = 0.2f;
    [Tooltip("Звук процесса вращения (опционально)")]
    public AudioSource craftProcessSound;

    [Header("Настройки Цвета Ингредиентов")]
    public Color colorEnough = Color.white;
    public Color colorNotEnough = Color.red;

    private CraftingCategory currentCategory = CraftingCategory.Weapon;
    private CraftingRecipe selectedRecipe = null;
    private List<GameObject> activeRecipeListElements = new List<GameObject>();
    private List<GameObject> activeIngredientSlots = new List<GameObject>();

    private int craftMultiplier = 1;
    private Coroutine craftCoroutine = null;
    private bool isCraftingAnimationRunning = false;
    private Vector3 originalIconScale = Vector3.one;
    private Quaternion originalRingRotation = Quaternion.identity;
    private RectTransform activeIconTarget = null;
    private RectTransform activeRingTarget = null;

    [System.Serializable]
    public struct CategoryButtonLink
    {
        public CraftingCategory category;
        public Button button;
    }

    void Awake()
    {
        // Привязываем ивенты клика к кнопкам категорий
        foreach (var link in categoryButtons)
        {
            if (link.button != null)
            {
                CraftingCategory cat = link.category;
                link.button.onClick.AddListener(() => SelectCategory(cat));
            }
        }

        if (craftButton != null)
        {
            craftButton.onClick.AddListener(CraftItem);
        }

        if (plusButton != null)
        {
            plusButton.onClick.AddListener(IncreaseCraftAmount);
        }

        if (minusButton != null)
        {
            minusButton.onClick.AddListener(DecreaseCraftAmount);
        }
    }

    void OnEnable()
    {
        InitializeUI();
    }

    void OnDisable()
    {
        if (craftCoroutine != null)
        {
            StopCoroutine(craftCoroutine);
            craftCoroutine = null;
        }
        ResetAnimationState();
    }

    public void InitializeUI()
    {
        SelectCategory(CraftingCategory.Weapon);
    }

    public void SelectCategory(CraftingCategory category)
    {
        currentCategory = category;

        // Обновляем название категории в UI
        if (categoryNameText != null)
        {
            categoryNameText.text = GetLocalizedCategoryName(category);
        }

        // Подсвечиваем активную кнопку (опционально можно настроить interacable/цвета)
        foreach (var link in categoryButtons)
        {
            if (link.button != null)
            {
                // Пример: делаем неактивной кнопку текущей категории
                link.button.interactable = (link.category != currentCategory);
            }
        }

        PopulateRecipeList();
    }

    private void PopulateRecipeList()
    {
        // Полностью очищаем все дочерние объекты из контента списка рецептов
        if (recipeListContent != null)
        {
            foreach (Transform child in recipeListContent)
            {
                Destroy(child.gameObject);
            }
        }
        activeRecipeListElements.Clear();

        if (recipeListContent == null || recipeListElementPrefab == null) return;

        // Фильтруем рецепты по текущей категории
        List<CraftingRecipe> filteredRecipes = recipeDatabase.FindAll(r => r != null && r.category == currentCategory);

        CraftingRecipe firstRecipe = null;

        foreach (CraftingRecipe recipe in filteredRecipes)
        {
            if (InventoryManager.Instance == null) continue;
            GameObject resultPrefab = InventoryManager.Instance.GetPrefabByID(recipe.resultItemID);
            if (resultPrefab == null) continue;

            PickUpItem pickUp = resultPrefab.GetComponent<PickUpItem>();
            if (pickUp == null) pickUp = resultPrefab.GetComponentInChildren<PickUpItem>();
            if (pickUp == null) continue;

            if (firstRecipe == null) firstRecipe = recipe;

            // Спавним элемент списка
            GameObject newElement = Instantiate(recipeListElementPrefab, recipeListContent, false);
            activeRecipeListElements.Add(newElement);

            // Настраиваем UI элемента списка рецептов
            RecipeListElementUI elementUI = newElement.GetComponent<RecipeListElementUI>();
            if (elementUI != null)
            {
                if (elementUI.itemNameText != null) elementUI.itemNameText.text = pickUp.itemName;
                if (elementUI.itemIcon != null) elementUI.itemIcon.sprite = pickUp.itemIcon;

                // Заполняем маленькие иконки ингредиентов для превью
                if (elementUI.ingredientsSummaryParent != null)
                {
                    // Находим шаблонный элемент (первый дочерний объект)
                    Transform template = null;
                    if (elementUI.ingredientsSummaryParent.childCount > 0)
                    {
                        template = elementUI.ingredientsSummaryParent.GetChild(0);
                    }

                    // Собираем все остальные дочерние объекты для немедленного удаления
                    List<GameObject> toDestroy = new List<GameObject>();
                    for (int i = 0; i < elementUI.ingredientsSummaryParent.childCount; i++)
                    {
                        Transform child = elementUI.ingredientsSummaryParent.GetChild(i);
                        if (template == null || child != template)
                        {
                            toDestroy.Add(child.gameObject);
                        }
                    }
                    foreach (var obj in toDestroy)
                    {
                        DestroyImmediate(obj);
                    }

                    if (template != null)
                    {
                        template.gameObject.SetActive(false); // Скрываем оригинальный шаблон
                    }

                    // Спавним копии шаблона для каждого обычного предмета-ингредиента
                    foreach (var ing in recipe.ingredients)
                    {
                        GameObject ingPrefab = InventoryManager.Instance.GetPrefabByID(ing.itemID);
                        if (ingPrefab != null)
                        {
                            PickUpItem ingPick = ingPrefab.GetComponent<PickUpItem>();
                            if (ingPick == null) ingPick = ingPrefab.GetComponentInChildren<PickUpItem>();

                            if (ingPick != null && ingPick.itemIcon != null && template != null)
                            {
                                GameObject copy = Instantiate(template.gameObject, elementUI.ingredientsSummaryParent, false);
                                copy.SetActive(true);

                                Image img = copy.GetComponent<Image>();
                                if (img == null) img = copy.GetComponentInChildren<Image>();
                                if (img != null) img.sprite = ingPick.itemIcon;

                                TextMeshProUGUI txt = copy.GetComponentInChildren<TextMeshProUGUI>();
                                if (txt != null) txt.text = ing.amount.ToString();
                            }
                        }
                    }

                    // Если рецепту требуется жидкость — добавляем доп. иконку жидкости в превью
                    if (recipe.requiresLiquid && recipe.requiredLiquid != LiquidType.None && template != null)
                    {
                        List<Sprite> vesselIcons = GetVesselIconsForLiquid(recipe.requiredLiquid);
                        if (vesselIcons != null && vesselIcons.Count > 0)
                        {
                            GameObject copy = Instantiate(template.gameObject, elementUI.ingredientsSummaryParent, false);
                            copy.SetActive(true);

                            Image img = copy.GetComponent<Image>();
                            if (img == null) img = copy.GetComponentInChildren<Image>();
                            if (img != null)
                            {
                                img.sprite = vesselIcons[0];
                                if (vesselIcons.Count > 1)
                                {
                                    IngredientIconCycler cycler = copy.GetComponent<IngredientIconCycler>();
                                    if (cycler == null) cycler = copy.AddComponent<IngredientIconCycler>();
                                    cycler.Init(img, vesselIcons, 1.2f);
                                }
                            }

                            TextMeshProUGUI txt = copy.GetComponentInChildren<TextMeshProUGUI>();
                            if (txt != null) txt.text = recipe.requiredLiquidAmount.ToString();
                        }
                    }
                }

                Button selectBtn = elementUI.selectButton;
                if (selectBtn == null) selectBtn = newElement.GetComponent<Button>();
                if (selectBtn == null) selectBtn = newElement.GetComponentInChildren<Button>();

                if (selectBtn != null)
                {
                    selectBtn.onClick.AddListener(() => {
                        SelectRecipe(recipe);
                    });
                }
            }
            else
            {
                // Альтернативный поиск компонентов, если скрипт-хелпер не навесили
                Button btn = newElement.GetComponent<Button>();
                if (btn == null) btn = newElement.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => {
                        SelectRecipe(recipe);
                    });
                }

                Image img = newElement.transform.Find("Icon")?.GetComponent<Image>();
                if (img != null) img.sprite = pickUp.itemIcon;

                TextMeshProUGUI txt = newElement.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (txt != null) txt.text = pickUp.itemName;
            }
        }

        // По умолчанию выбираем первый рецепт в категории
        if (firstRecipe != null)
        {
            SelectRecipe(firstRecipe);
        }
        else
        {
            ClearDetailsPanel();
        }
    }

    public void IncreaseCraftAmount()
    {
        if (isCraftingAnimationRunning || selectedRecipe == null) return;
        craftMultiplier++;
        SelectRecipe(selectedRecipe, resetMultiplier: false);
    }

    public void DecreaseCraftAmount()
    {
        if (isCraftingAnimationRunning || selectedRecipe == null) return;
        if (craftMultiplier > 1)
        {
            craftMultiplier--;
            SelectRecipe(selectedRecipe, resetMultiplier: false);
        }
    }

    public void SelectRecipe(CraftingRecipe recipe)
    {
        SelectRecipe(recipe, true);
    }

    public void SelectRecipe(CraftingRecipe recipe, bool resetMultiplier)
    {
        if (resetMultiplier)
        {
            craftMultiplier = 1;
        }

        selectedRecipe = recipe;

        if (recipe == null)
        {
            Debug.LogWarning("[WorkbenchUI] SelectRecipe called with null recipe.");
            ClearDetailsPanel();
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[WorkbenchUI] InventoryManager.Instance is null!");
            ClearDetailsPanel();
            return;
        }

        // Проверяем инспекторные ссылки
        if (selectedItemNameText == null) Debug.LogWarning("[WorkbenchUI] selectedItemNameText is not assigned in the Inspector!");
        if (selectedItemImage == null) Debug.LogWarning("[WorkbenchUI] selectedItemImage is not assigned in the Inspector!");
        if (ingredientsContainer == null) Debug.LogWarning("[WorkbenchUI] ingredientsContainer is not assigned in the Inspector!");
        if (ingredientSlotPrefab == null) Debug.LogWarning("[WorkbenchUI] ingredientSlotPrefab is not assigned in the Inspector!");

        GameObject resultPrefab = InventoryManager.Instance.GetPrefabByID(recipe.resultItemID);
        if (resultPrefab == null)
        {
            Debug.LogError($"[WorkbenchUI] Result item ID {recipe.resultItemID} (recipe: {recipe.recipeDeveloperName}) was not found in the InventoryManager database!");
            return;
        }

        PickUpItem pickUp = resultPrefab.GetComponent<PickUpItem>();
        if (pickUp == null) pickUp = resultPrefab.GetComponentInChildren<PickUpItem>();
        if (pickUp == null)
        {
            Debug.LogError($"[WorkbenchUI] PickUpItem component not found on result prefab for item ID {recipe.resultItemID}!");
            return;
        }

        // Заполняем основную панель деталей
        if (selectedItemNameText != null)
        {
            int totalResultAmount = recipe.resultAmount * craftMultiplier;
            if (totalResultAmount > 1)
            {
                selectedItemNameText.text = $"{pickUp.itemName} (x{totalResultAmount})";
            }
            else
            {
                selectedItemNameText.text = pickUp.itemName;
            }
        }
        if (selectedItemImage != null)
        {
            selectedItemImage.sprite = pickUp.itemIcon;
            selectedItemImage.enabled = true;
        }

        // Полностью очищаем контейнер ингредиентов от всех детей
        if (ingredientsContainer != null)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in ingredientsContainer)
            {
                children.Add(child.gameObject);
            }
            foreach (var child in children)
            {
                DestroyImmediate(child);
            }
        }
        activeIngredientSlots.Clear();

        bool hasAllIngredients = true;

        // Заполняем детальные ингредиенты
        if (ingredientsContainer != null && ingredientSlotPrefab != null)
        {
            // 1. Обычные предметы-ингредиенты
            foreach (var ing in recipe.ingredients)
            {
                GameObject ingPrefab = InventoryManager.Instance.GetPrefabByID(ing.itemID);
                if (ingPrefab == null)
                {
                    Debug.LogError($"[WorkbenchUI] Ingredient item ID {ing.itemID} was not found in InventoryManager database!");
                    continue;
                }

                PickUpItem ingPick = ingPrefab.GetComponent<PickUpItem>();
                if (ingPick == null) ingPick = ingPrefab.GetComponentInChildren<PickUpItem>();
                if (ingPick == null) continue;

                int requiredAmount = ing.amount * craftMultiplier;
                int playerAmount = InventoryManager.Instance.GetItemCount(ing.itemID);
                bool enough = playerAmount >= requiredAmount;
                if (!enough)
                {
                    hasAllIngredients = false;
                }

                GameObject slotObj = Instantiate(ingredientSlotPrefab, ingredientsContainer, false);
                activeIngredientSlots.Add(slotObj);

                IngredientSlotUI slotUI = slotObj.GetComponent<IngredientSlotUI>();

                Image iconImg = slotUI != null ? slotUI.ingredientIcon : null;
                TextMeshProUGUI nameTxt = slotUI != null ? slotUI.ingredientNameText : null;
                TextMeshProUGUI stockTxt = slotUI != null ? slotUI.stockText : null;

                if (iconImg == null) iconImg = slotObj.transform.Find("MaterialIcon")?.GetComponent<Image>();
                if (iconImg == null) iconImg = slotObj.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImg == null) iconImg = slotObj.GetComponentInChildren<Image>();

                if (nameTxt == null) nameTxt = slotObj.transform.Find("MaterialText")?.GetComponent<TextMeshProUGUI>();
                if (nameTxt == null) nameTxt = slotObj.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (nameTxt == null) nameTxt = slotObj.GetComponentInChildren<TextMeshProUGUI>();

                if (stockTxt == null) stockTxt = slotObj.transform.Find("count")?.GetComponent<TextMeshProUGUI>();
                if (stockTxt == null) stockTxt = slotObj.transform.Find("Stock")?.GetComponent<TextMeshProUGUI>();
                if (stockTxt == null && nameTxt != null)
                {
                    var allTexts = slotObj.GetComponentsInChildren<TextMeshProUGUI>();
                    foreach (var t in allTexts)
                    {
                        if (t != nameTxt)
                        {
                            stockTxt = t;
                            break;
                        }
                    }
                }

                if (iconImg != null) iconImg.sprite = ingPick.itemIcon;
                if (nameTxt != null) nameTxt.text = ingPick.itemName;
                if (stockTxt != null)
                {
                    stockTxt.text = $"{playerAmount} / {requiredAmount}";
                    stockTxt.color = enough ? colorEnough : colorNotEnough;
                }
            }

            // 2. ОТДЕЛЬНЫЙ СЛОТ ДЛЯ ЖИДКОСТИ (дополнительный ингредиент)
            if (recipe.requiresLiquid && recipe.requiredLiquid != LiquidType.None)
            {
                int requiredLiquidAmount = recipe.requiredLiquidAmount * craftMultiplier;
                int playerLiquid = InventoryManager.Instance.GetTotalLiquidAmount(recipe.requiredLiquid);
                bool enoughLiquid = playerLiquid >= requiredLiquidAmount;
                if (!enoughLiquid)
                {
                    hasAllIngredients = false;
                }

                GameObject slotObj = Instantiate(ingredientSlotPrefab, ingredientsContainer, false);
                activeIngredientSlots.Add(slotObj);

                IngredientSlotUI slotUI = slotObj.GetComponent<IngredientSlotUI>();

                Image iconImg = slotUI != null ? slotUI.ingredientIcon : null;
                TextMeshProUGUI nameTxt = slotUI != null ? slotUI.ingredientNameText : null;
                TextMeshProUGUI stockTxt = slotUI != null ? slotUI.stockText : null;

                if (iconImg == null) iconImg = slotObj.transform.Find("MaterialIcon")?.GetComponent<Image>();
                if (iconImg == null) iconImg = slotObj.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImg == null) iconImg = slotObj.GetComponentInChildren<Image>();

                if (nameTxt == null) nameTxt = slotObj.transform.Find("MaterialText")?.GetComponent<TextMeshProUGUI>();
                if (nameTxt == null) nameTxt = slotObj.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (nameTxt == null) nameTxt = slotObj.GetComponentInChildren<TextMeshProUGUI>();

                if (stockTxt == null) stockTxt = slotObj.transform.Find("count")?.GetComponent<TextMeshProUGUI>();
                if (stockTxt == null) stockTxt = slotObj.transform.Find("Stock")?.GetComponent<TextMeshProUGUI>();
                if (stockTxt == null && nameTxt != null)
                {
                    var allTexts = slotObj.GetComponentsInChildren<TextMeshProUGUI>();
                    foreach (var t in allTexts)
                    {
                        if (t != nameTxt)
                        {
                            stockTxt = t;
                            break;
                        }
                    }
                }

                List<Sprite> vesselIcons = GetVesselIconsForLiquid(recipe.requiredLiquid);
                if (vesselIcons != null && vesselIcons.Count > 0)
                {
                    if (iconImg != null) iconImg.sprite = vesselIcons[0];
                    if (vesselIcons.Count > 1 && iconImg != null)
                    {
                        IngredientIconCycler cycler = slotObj.GetComponent<IngredientIconCycler>();
                        if (cycler == null) cycler = slotObj.AddComponent<IngredientIconCycler>();
                        cycler.Init(iconImg, vesselIcons, 1.2f);
                    }
                }

                if (nameTxt != null)
                {
                    nameTxt.text = PlayerInteract.GetLocalizedLiquidName(recipe.requiredLiquid);
                }
                if (stockTxt != null)
                {
                    stockTxt.text = $"{playerLiquid} / {requiredLiquidAmount}";
                    stockTxt.color = enoughLiquid ? colorEnough : colorNotEnough;
                }
            }
        }

        // Активируем/деактивируем кнопки крафта и множителя
        if (craftButton != null)
        {
            craftButton.interactable = !isCraftingAnimationRunning && hasAllIngredients;
        }
        if (plusButton != null)
        {
            plusButton.interactable = !isCraftingAnimationRunning;
        }
        if (minusButton != null)
        {
            minusButton.interactable = !isCraftingAnimationRunning && (craftMultiplier > 1);
        }
        if (craftAmountText != null)
        {
            craftAmountText.text = craftMultiplier.ToString();
        }
    }

    private void ClearDetailsPanel()
    {
        selectedRecipe = null;
        craftMultiplier = 1;
        if (selectedItemNameText != null) selectedItemNameText.text = "";
        if (selectedItemImage != null)
        {
            selectedItemImage.sprite = null;
            selectedItemImage.enabled = false;
        }
        if (ingredientsContainer != null)
        {
            foreach (Transform child in ingredientsContainer)
            {
                Destroy(child.gameObject);
            }
        }
        activeIngredientSlots.Clear();

        if (craftButton != null) craftButton.interactable = false;
        if (plusButton != null) plusButton.interactable = false;
        if (minusButton != null) minusButton.interactable = false;
        if (craftAmountText != null) craftAmountText.text = "1";
    }

    private void ResetAnimationState()
    {
        if (activeIconTarget != null)
        {
            activeIconTarget.localScale = originalIconScale;
        }
        if (activeRingTarget != null)
        {
            activeRingTarget.localRotation = originalRingRotation;
        }
        isCraftingAnimationRunning = false;
    }

    public void CraftItem()
    {
        if (isCraftingAnimationRunning) return;

        if (selectedRecipe == null || InventoryManager.Instance == null)
        {
            if (craftFailSound != null) craftFailSound.Play();
            return;
        }

        // 1. Проверяем наличие обычных ресурсов
        foreach (var ing in selectedRecipe.ingredients)
        {
            if (InventoryManager.Instance.GetItemCount(ing.itemID) < ing.amount * craftMultiplier)
            {
                Debug.LogWarning($"Недостаточно предметов для крафта: ID {ing.itemID}");
                if (craftFailSound != null) craftFailSound.Play();
                return;
            }
        }

        // Проверяем наличие требуемой жидкости
        if (selectedRecipe.requiresLiquid && selectedRecipe.requiredLiquid != LiquidType.None)
        {
            if (InventoryManager.Instance.GetTotalLiquidAmount(selectedRecipe.requiredLiquid) < selectedRecipe.requiredLiquidAmount * craftMultiplier)
            {
                Debug.LogWarning($"Недостаточно жидкости для крафта: {selectedRecipe.requiredLiquid} (нужно {selectedRecipe.requiredLiquidAmount * craftMultiplier})");
                if (craftFailSound != null) craftFailSound.Play();
                return;
            }
        }

        if (craftCoroutine != null)
        {
            StopCoroutine(craftCoroutine);
        }
        craftCoroutine = StartCoroutine(CraftSequenceCoroutine());
    }

    private IEnumerator CraftSequenceCoroutine()
    {
        isCraftingAnimationRunning = true;
        if (craftButton != null) craftButton.interactable = false;
        if (plusButton != null) plusButton.interactable = false;
        if (minusButton != null) minusButton.interactable = false;

        activeIconTarget = itemIconTransform;
        if (activeIconTarget == null && selectedItemImage != null)
        {
            activeIconTarget = selectedItemImage.rectTransform;
        }

        activeRingTarget = circleTransform;
        if (activeRingTarget == null && activeIconTarget != null && activeIconTarget.parent != null)
        {
            activeRingTarget = activeIconTarget.parent as RectTransform;
        }

        if (activeIconTarget != null) originalIconScale = activeIconTarget.localScale;
        if (activeRingTarget != null) originalRingRotation = activeRingTarget.localRotation;

        // 1. Сжатие иконки предмета
        if (activeIconTarget != null && shrinkDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                activeIconTarget.localScale = Vector3.Lerp(originalIconScale, Vector3.zero, smoothT);
                yield return null;
            }
            activeIconTarget.localScale = Vector3.zero;
        }

        // 2. Резкий круговой оборот (360 градусов)
        if (activeRingTarget != null && rotationDuration > 0f)
        {
            if (craftProcessSound != null) craftProcessSound.Play();
            float elapsed = 0f;
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float angle = Mathf.Lerp(0f, -360f, smoothT);
                activeRingTarget.localRotation = originalRingRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            activeRingTarget.localRotation = originalRingRotation;
        }

        // 3. Разжатие иконки предмета обратно
        if (activeIconTarget != null && expandDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < expandDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / expandDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                activeIconTarget.localScale = Vector3.Lerp(Vector3.zero, originalIconScale, smoothT);
                yield return null;
            }
            activeIconTarget.localScale = originalIconScale;
        }

        ResetAnimationState();

        // 4. Списываем обычные предметы
        foreach (var ing in selectedRecipe.ingredients)
        {
            InventoryManager.Instance.RemoveItems(ing.itemID, ing.amount * craftMultiplier);
        }

        // 5. Списываем объем жидкости из сосудов
        if (selectedRecipe.requiresLiquid && selectedRecipe.requiredLiquid != LiquidType.None)
        {
            InventoryManager.Instance.DeductLiquid(selectedRecipe.requiredLiquid, selectedRecipe.requiredLiquidAmount * craftMultiplier);
        }

        // 6. Выдаем готовый предмет
        GameObject resultPrefab = InventoryManager.Instance.GetPrefabByID(selectedRecipe.resultItemID);
        if (resultPrefab != null)
        {
            PickUpItem pickUp = resultPrefab.GetComponent<PickUpItem>();
            if (pickUp == null) pickUp = resultPrefab.GetComponentInChildren<PickUpItem>();

            if (pickUp != null)
            {
                InventoryItemData craftedData = new InventoryItemData(pickUp);
                craftedData.amount = selectedRecipe.resultAmount * craftMultiplier;

                // Добавляем предмет игроку
                int leftover = InventoryManager.Instance.AddItemWithLeftover(craftedData);

                // Если инвентарь полон, выбрасываем избыток на пол
                if (leftover > 0)
                {
                    InventoryItemData leftoverData = craftedData.Clone();
                    leftoverData.amount = leftover;
                    InventoryManager.Instance.SpawnDroppedItem(leftoverData);
                }
            }
        }

        // 7. Воспроизводим звук успеха
        if (craftSuccessSound != null)
        {
            craftSuccessSound.Play();
        }

        craftCoroutine = null;

        // 8. Обновляем детали рецепта
        SelectRecipe(selectedRecipe, resetMultiplier: false);
    }

    private string GetLocalizedCategoryName(CraftingCategory category)
    {
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

        if (isEn)
        {
            switch (category)
            {
                case CraftingCategory.Weapon: return "Weapon";
                case CraftingCategory.Armor: return "Armor";
                case CraftingCategory.Tools: return "Tools";
                case CraftingCategory.Food: return "Food";
                case CraftingCategory.Light: return "Light";
                default: return category.ToString();
            }
        }
        else
        {
            switch (category)
            {
                case CraftingCategory.Weapon: return "Оружие";
                case CraftingCategory.Armor: return "Броня";
                case CraftingCategory.Tools: return "Инструменты";
                case CraftingCategory.Food: return "Еда";
                case CraftingCategory.Light: return "Свет";
                default: return category.ToString();
            }
        }
    }

    public static List<Sprite> GetVesselIconsForLiquid(LiquidType liquidType, int fallbackItemID = 0)
    {
        List<Sprite> vesselIcons = new List<Sprite>();

        if (InventoryManager.Instance != null)
        {
            // 1. Проверяем предметы в инвентаре игрока с этой жидкостью
            System.Action<InventorySlot> checkSlot = (slot) =>
            {
                if (slot != null && !slot.IsEmpty() && slot.itemData != null && slot.itemData.currentLiquidType == liquidType)
                {
                    Sprite icon = slot.itemData.itemIcon;
                    if (icon != null && !vesselIcons.Contains(icon))
                    {
                        vesselIcons.Add(icon);
                    }
                }
            };

            if (InventoryManager.Instance.hotbarSlots != null)
                foreach (var s in InventoryManager.Instance.hotbarSlots) checkSlot(s);
            if (InventoryManager.Instance.inventorySlots != null)
                foreach (var s in InventoryManager.Instance.inventorySlots) checkSlot(s);
            if (InventoryManager.Instance.waistSlots != null)
                foreach (var s in InventoryManager.Instance.waistSlots) checkSlot(s);

            // 2. Сканируем базу allItemsDatabase на префабы-сосуды
            if (InventoryManager.Instance.allItemsDatabase != null)
            {
                foreach (GameObject prefab in InventoryManager.Instance.allItemsDatabase)
                {
                    if (prefab == null) continue;

                    ConsumableItem cons = prefab.GetComponent<ConsumableItem>();
                    if (cons == null) cons = prefab.GetComponentInChildren<ConsumableItem>();

                    if (cons != null && (cons.type == ConsumableType.LiquidContainer || cons.type == ConsumableType.LampOil || (cons.liquidMaterials != null && cons.liquidMaterials.Count > 0)))
                    {
                        Sprite[] fillIcons = cons.GetFillIconsForLiquid(liquidType);
                        Sprite iconToAdd = null;
                        if (fillIcons != null && fillIcons.Length > 0)
                        {
                            iconToAdd = fillIcons[fillIcons.Length - 1]; // Иконка полного/заполненного сосуда
                        }
                        else
                        {
                            PickUpItem p = prefab.GetComponent<PickUpItem>();
                            if (p == null) p = prefab.GetComponentInChildren<PickUpItem>();
                            if (p != null) iconToAdd = p.itemIcon;
                        }

                        if (iconToAdd != null && !vesselIcons.Contains(iconToAdd))
                        {
                            vesselIcons.Add(iconToAdd);
                        }
                    }
                }
            }
        }

        // 3. Резервный фолбэк по fallbackItemID
        if (vesselIcons.Count == 0 && fallbackItemID > 0 && InventoryManager.Instance != null)
        {
            GameObject fallbackPrefab = InventoryManager.Instance.GetPrefabByID(fallbackItemID);
            if (fallbackPrefab != null)
            {
                PickUpItem p = fallbackPrefab.GetComponent<PickUpItem>();
                if (p == null) p = fallbackPrefab.GetComponentInChildren<PickUpItem>();
                if (p != null && p.itemIcon != null)
                {
                    vesselIcons.Add(p.itemIcon);
                }
            }
        }

        return vesselIcons;
    }
}
