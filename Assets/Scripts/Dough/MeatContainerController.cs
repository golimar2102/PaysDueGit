using UnityEngine;

public class MeatContainerController : MonoBehaviour
{
    public static MeatContainerController Instance;

    [Tooltip("ID запущенного перетаскивания фарша (-1 если не тащим)")]
    public static int activeDraggedMeatItemID = -1;

    [Tooltip("Ячейка, над которой сейчас находится курсор")]
    public static MeatContainerCell hoveredCell = null;

    [Tooltip("Заготовка теста, над которой сейчас находится курсор")]
    public static GameObject hoveredBlank = null;

    [Tooltip("Наведен ли курсор именно на мясной шарик, лежащий на заготовке")]
    public static bool hoveredMeatBall = false;

    [Tooltip("Ячейка, из которой сейчас перетаскивается мясной шарик")]
    public static MeatContainerCell currentlyDraggingCell = null;

    [Tooltip("Созданный призрак мясного шарика при перетаскивании")]
    public static GameObject dragGhostInstance = null;

    [Header("Ячейки контейнера (5 штук)")]
    public MeatContainerCell[] cells = new MeatContainerCell[5];

    void Awake()
    {
        Instance = this;
        // Автоматически находим все ячейки в сцене, если они не были настроены вручную в инспекторе
        if (cells == null || cells.Length == 0 || IsAllCellsNull())
        {
            cells = FindObjectsByType<MeatContainerCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }

    private bool IsAllCellsNull()
    {
        if (cells == null) return true;
        foreach (var cell in cells)
        {
            if (cell != null) return false;
        }
        return true;
    }

    void Start()
    {
        Debug.Log($"[MeatDrag] MeatContainerController initialized with {cells.Length} cells:");
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null)
            {
                Debug.Log($"  - Cell [{i}]: GameObject={cells[i].gameObject.name}, requiredItemID={cells[i].requiredItemID}");
            }
            else
            {
                Debug.LogWarning($"  - Cell [{i}]: NULL");
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        hoveredCell = null;
        hoveredBlank = null;
        hoveredMeatBall = false;
        currentlyDraggingCell = null;
        if (dragGhostInstance != null)
        {
            Destroy(dragGhostInstance);
        }
    }

    void Update()
    {
        // Сбрасываем все при выходе из-за стола
        bool isPlayerViewing = DoughRollingController.activeRollingBoard != null && DoughRollingController.activeRollingBoard.isViewing;
        if (!isPlayerViewing)
        {
            hoveredCell = null;
            hoveredBlank = null;
            hoveredMeatBall = false;
            currentlyDraggingCell = null;
            if (dragGhostInstance != null)
            {
                Destroy(dragGhostInstance);
            }
        }
    }

    /// <summary>
    /// Проверяет, является ли переданный ID предмета одним из зарегистрированных видов мяса
    /// </summary>
    public static bool IsMeatItem(int itemID)
    {
        if (Instance == null || Instance.cells == null) return false;
        foreach (var cell in Instance.cells)
        {
            if (cell != null && cell.requiredItemID == itemID) return true;
        }
        return false;
    }

    /// <summary>
    /// Вызывается из InventorySlot при начале перетаскивания мяса
    /// </summary>
    public static void OnMeatDragStartedAll(int itemID)
    {
        activeDraggedMeatItemID = itemID;
    }

    /// <summary>
    /// Вызывается из InventorySlot при окончании перетаскивания
    /// </summary>
    public static void OnMeatDragEndedAll()
    {
        activeDraggedMeatItemID = -1;
    }
}
