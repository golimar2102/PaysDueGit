using UnityEngine;
using TMPro;
using UnityEngine.Localization;
using System.Collections;

public partial class PlayerInteract : MonoBehaviour
{
    [Header("Настройки взаимодействия")]
    public Camera playerCamera;
    public float interactDistance = 8f;

    [System.Serializable]
    public class LocalizedActionStrings
    {
        public LocalizedString timePrefix;
        public LocalizedString doorAction;
        public LocalizedString harvestAction;
        public LocalizedString pickUpAction;
        public LocalizedString enter;
        public LocalizedString exit;
        public LocalizedString toggle;
        public LocalizedString locked;
        public LocalizedString unlockAction;
        public LocalizedString lockAction;
        public LocalizedString talkAction;
        public LocalizedString sitAction;
    }

    [Header("Локализация подсказок")]
    public LocalizedActionStrings actions;

    [Header("Локализация жидкостей")]
    public System.Collections.Generic.List<LocalizedLiquidMapping> localizedLiquids;

    [Header("Анимация")]
    public Animator animator;

    [Header("UI (Текст на экране)")]
    public TextMeshProUGUI interactText;

    public static PlayerInteract Instance { get; private set; }

    private PickUpItem currentLookItem;
    private TeleportDoor currentHoveredDoor;
    private NPCCorpse currentLookCorpse;
    private MeatGrinderController currentLookMeatGrinder;
    private WorldToggleDevice currentLookToggleDevice;
    private GeneratorDoorController currentLookGeneratorDoor;
    private LiquidSource currentLookLiquidSource;
    private ItemSource currentLookItemSource;
    private StorageContainer currentLookStorage;
    private TVChairController currentLookTVChair;
    private DoughRollingController currentLookDoughRolling;
    private WorkbenchController currentLookWorkbench;
    private VendingMachineController currentLookVendingMachine;
    private LocationTransitionController currentLookLocationTransition;
    private PeepholeController currentLookPeephole;
    private WaterCoolerPipe currentLookWaterCoolerPipe;
    private WaterCoolerTap currentLookWaterCoolerTap;
    private SinkTapController currentLookSinkTap;
    private ButcheringTableController currentLookButcheringTable;
    private IndustrialMeatGrinder currentLookIndustrialMeatGrinder;
    private TrashSortingButton currentLookTrashSortingButton;

    private KeyCode cachedInteractKey;
    private KeyCode cachedToggleKey;

    public KeyCode InteractKey => cachedInteractKey;
    public KeyCode ToggleKey => cachedToggleKey;

    private Collider lastHitCollider;
    private PickUpItem        cachedItem;
    private DoorController    cachedDoor;
    private PlantedPlant      cachedPlant;
    private WorldClock        cachedClock;
    private WorldToggleDevice cachedToggleDevice;
    private TeleportDoor      cachedTeleportDoor;
    private WindowController  cachedWindow;
    private NPCDialogue       cachedNpc;
    private MeatGrinderController cachedMeatGrinder;
    private GeneratorDoorController cachedGeneratorDoor;
    private LiquidSource cachedLiquidSource;
    private ItemSource   cachedItemSource;
    private StorageContainer cachedStorage;
    private TVChairController cachedTVChair;
    private DoughRollingController cachedDoughRolling;
    private WorkbenchController cachedWorkbench;
    private VendingMachineController cachedVendingMachine;
    private LocationTransitionController cachedLocationTransition;
    private PeepholeController cachedPeephole;
    private WaterCoolerPipe cachedWaterCoolerPipe;
    private WaterCoolerTap cachedWaterCoolerTap;
    private SinkTapController cachedSinkTap;
    private NPCCorpse       cachedCorpse;
    private ButcheringTableController cachedButcheringTable;
    private IndustrialMeatGrinder cachedIndustrialMeatGrinder;
    private EnemyAI         cachedEnemyAI;
    private TrashSortingButton cachedTrashSortingButton;

    private static readonly int InteractHash = Animator.StringToHash("Interact");

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        RefreshKeyBindings();
    }

    void OnEnable()
    {
        lastHitCollider = null;
        RemoveHighlight();
    }

    void OnDisable()
    {
        RemoveHighlight();
    }

    public void RefreshKeyBindings()
    {
        cachedInteractKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
        cachedToggleKey   = (KeyCode)PlayerPrefs.GetInt("Key_Toggle",   (int)KeyCode.F);
    }
}

[System.Serializable]
public struct LocalizedLiquidMapping
{
    public LiquidType liquidType;
    public UnityEngine.Localization.LocalizedString localizedName;
}