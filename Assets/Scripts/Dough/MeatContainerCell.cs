using UnityEngine;
using TMPro;

public class MeatContainerCell : MonoBehaviour
{
    [Header("Настройки предмета")]
    [Tooltip("ID предмета фарша, который подходит в эту ячейку")]
    public int requiredItemID;

    [Tooltip("Название типа мяса (Beef, Pork, Canine, Feline, Avian)")]
    public string meatTypeName = "Beef";

    [Tooltip("Моделька фарша внутри ячейки")]
    public GameObject mincedMeatVisual;

    [Tooltip("Префаб порции фарша, который спавнится на заготовке теста")]
    public GameObject meatFillingPrefab;

    [Header("Настройки крышки")]
    [Tooltip("Трансформ крышки/заслонки, которая приоткрывается")]
    public Transform lidTransform;

    [Tooltip("Использовать вращение для анимации крышки")]
    public bool useRotation = true;
    public Vector3 closedLidLocalRotation = Vector3.zero;
    public Vector3 dragLidLocalRotation = new Vector3(-25f, 0f, 0f);

    [Tooltip("Использовать перемещение для анимации крышки")]
    public bool useTranslation = false;
    public Vector3 closedLidLocalPosition = Vector3.zero;
    public Vector3 dragLidLocalPosition = new Vector3(0f, 0.03f, 0f);

    [Tooltip("Скорость анимации крышки")]
    public float animationSpeed = 6f;

    [Header("Настройки порций")]
    [Tooltip("Сколько порций дает один выложенный предмет фарша")]
    public int portionsPerFill = 5;

    [Tooltip("Максимальная вместимость порций в ячейке")]
    public int maxPortions = 100;

    [Tooltip("Компонент TextMeshPro для отображения количества (например, 5/100)")]
    public TextMeshPro counterText;

    [Header("Звуки")]
    [Tooltip("Звук укладывания фарша в контейнер")]
    public AudioSource fillSound;
    [Tooltip("Звук взятия порции фарша")]
    public AudioSource takeSound;

    [Header("Состояние")]
    public bool isFilled = false;
    public int currentPortions = 0;

    private Outline cellOutline;

    void Awake()
    {
        DeduceMeatTypeName();
    }

    private void DeduceMeatTypeName()
    {
        // Выполняем автоопределение только если значение по умолчанию "Beef" или пустое
        if (meatTypeName == "Beef" || string.IsNullOrEmpty(meatTypeName))
        {
            if (requiredItemID == 37) meatTypeName = "Beef";
            else if (requiredItemID == 38) meatTypeName = "Pork";
            else if (requiredItemID == 39) meatTypeName = "Canine";
            else if (requiredItemID == 40) meatTypeName = "Feline";
            else if (requiredItemID == 41) meatTypeName = "Avian";
            else
            {
                // Попытка определить по имени объекта
                string lowerName = gameObject.name.ToLower();
                if (lowerName.Contains("beef")) meatTypeName = "Beef";
                else if (lowerName.Contains("pork")) meatTypeName = "Pork";
                else if (lowerName.Contains("canine") || lowerName.Contains("wolf") || lowerName.Contains("dog")) meatTypeName = "Canine";
                else if (lowerName.Contains("feline") || lowerName.Contains("cat")) meatTypeName = "Feline";
                else if (lowerName.Contains("avian") || lowerName.Contains("bird") || lowerName.Contains("chicken")) meatTypeName = "Avian";
            }
        }
    }

    void Start()
    {
        if (mincedMeatVisual != null)
        {
            mincedMeatVisual.SetActive(isFilled);
        }

        // Автоматически добавляем BoxCollider, если на объекте нет коллайдера для клика/дропа
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // Попытка найти Outline
        cellOutline = GetComponent<Outline>();
        if (cellOutline == null && mincedMeatVisual != null)
        {
            cellOutline = mincedMeatVisual.GetComponent<Outline>();
        }
        if (cellOutline != null)
        {
            cellOutline.enabled = false;
        }

        // Поиск текста счетчика
        if (counterText == null)
        {
            counterText = GetComponentInChildren<TextMeshPro>();
        }
        UpdateText();
    }

    void Update()
    {
        bool isPlayerViewing = DoughRollingController.activeRollingBoard != null && DoughRollingController.activeRollingBoard.isViewing;

        // Анимация крышки (открывается при наведении, при таскании порции из нее, или при наведении на заготовку, которая была заполнена этой ячейкой)
        if (lidTransform != null)
        {
            Vector3 targetRotation = closedLidLocalRotation;
            Vector3 targetPosition = closedLidLocalPosition;

            bool isHovered = (MeatContainerController.hoveredCell == this);
            bool isDraggingMe = (MeatContainerController.currentlyDraggingCell == this);
            
            bool isHoveredBlankOrigin = false;
            if (MeatContainerController.hoveredBlank != null)
            {
                DoughCircleState state = MeatContainerController.hoveredBlank.GetComponent<DoughCircleState>();
                if (state != null && state.originCell == this)
                {
                    isHoveredBlankOrigin = true;
                }
            }

            // Условия открытия крышки:
            // 1. Если ячейка заполнена И (наведена мышь, или тащим из нее фарш, или наведена заполненная этой ячейкой заготовка)
            // 2. ИЛИ если ячейка пуста и мы тащим подходящий фарш из инвентаря для заполнения
            bool shouldOpenLid = isPlayerViewing && (
                (isFilled && (isHovered || isDraggingMe || isHoveredBlankOrigin)) ||
                (MeatContainerController.activeDraggedMeatItemID == requiredItemID && currentPortions < maxPortions)
            );

            if (shouldOpenLid)
            {
                targetRotation = dragLidLocalRotation;
                targetPosition = dragLidLocalPosition;
            }

            if (useRotation)
            {
                lidTransform.localRotation = Quaternion.Lerp(
                    lidTransform.localRotation, 
                    Quaternion.Euler(targetRotation), 
                    Time.deltaTime * animationSpeed
                );
            }

            if (useTranslation)
            {
                lidTransform.localPosition = Vector3.Lerp(
                    lidTransform.localPosition, 
                    targetPosition, 
                    Time.deltaTime * animationSpeed
                );
            }
        }
    }

    public void FillCell()
    {
        isFilled = true;
        currentPortions = Mathf.Min(currentPortions + portionsPerFill, maxPortions);
        if (mincedMeatVisual != null)
        {
            mincedMeatVisual.SetActive(true);
        }

        if (fillSound != null)
        {
            fillSound.Play();
        }
        UpdateText();
    }

    public void TakePortion()
    {
        if (!isFilled) return;

        currentPortions--;
        if (takeSound != null)
        {
            takeSound.Play();
        }

        if (currentPortions <= 0)
        {
            currentPortions = 0;
            isFilled = false;
            if (mincedMeatVisual != null)
            {
                mincedMeatVisual.SetActive(false);
            }
        }
        UpdateText();
    }

    public void ReturnPortion()
    {
        isFilled = true;
        currentPortions = Mathf.Min(currentPortions + 1, maxPortions);
        if (mincedMeatVisual != null)
        {
            mincedMeatVisual.SetActive(true);
        }

        if (fillSound != null)
        {
            fillSound.Play();
        }
        UpdateText();
    }

    public void ResetCell()
    {
        isFilled = false;
        currentPortions = 0;
        if (mincedMeatVisual != null)
        {
            mincedMeatVisual.SetActive(false);
        }
        UpdateText();
    }

    private void UpdateText()
    {
        if (counterText != null)
        {
            if (currentPortions > 0)
            {
                counterText.text = $"{currentPortions}/{maxPortions}";
            }
            else
            {
                counterText.text = "";
            }
        }
    }
}
