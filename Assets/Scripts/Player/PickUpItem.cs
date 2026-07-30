using UnityEngine;
using UnityEngine.Localization; 

public enum ItemCategory
{
    None,
    Weapon,
    Armor,
    Tools,
    Food
}

[RequireComponent(typeof(Collider))]
public class PickUpItem : MonoBehaviour
{
    [Header("Настройки предмета (Локализация)")]
    public LocalizedString localizedItemName; 
    public Sprite itemIcon;
    public int itemID = -1;
    public ItemCategory category = ItemCategory.None;

    public string itemName
    {
        get
        {
            if (localizedItemName.IsEmpty) return "Неизвестно"; 
            string baseName = localizedItemName.GetLocalizedString();

            ConsumableItem cons = GetComponent<ConsumableItem>();
            if (cons != null && cons.currentLiquidType != LiquidType.None)
            {
                bool isDefaultLampOil = cons.type == ConsumableType.LampOil && cons.currentLiquidType == LiquidType.Oil;
                if (!isDefaultLampOil)
                {
                    string liquidSuffix = PlayerInteract.GetLocalizedLiquidName(cons.currentLiquidType);
                    return $"{baseName} ({liquidSuffix})";
                }
            }
            return baseName;
        }
    }

    [Header("Стаки (Слияние)")]
    [Tooltip("Можно ли складывать эти предметы в одну ячейку?")]
    public bool isStackable = false;
    [Tooltip("Сколько штук лежит в этой конкретной кучке на полу")]
    public int amount = 1;
    [Tooltip("Максимальное количество в одной ячейке инвентаря")]
    public int maxStackSize = 30;

    [Header("Размещение (Placement)")]
    [Tooltip("Если true, этот предмет можно ставить ТОЛЬКО на специальные PlacementPoint (Например, мясорубку на стол)")]
    public bool requiresSnapPoint = false;
    [Tooltip("Смещение позиции при установке на точку размещения (PlacementPoint)")]
    public Vector3 placementOffset = Vector3.zero;

    [Header("Для выброса из инвентаря")]
    public GameObject itemPrefab;

    // Топливо фонаря (хранится на пропе при выбросе и восстанавливается при подборе)
    [HideInInspector] public float lanternFuel = -1f;

    [Header("Настройки броска (Физика)")]
    public float throwMass = 0.8f;
    public float throwGravityScale = 2f;
    public float throwDrag = 0f;
    public float throwSpinForce = 0.5f;

    [Header("Аркадная левитация")]  
    public bool isFloating = true;
    public float spinSpeed = 45f;
    public float floatAmplitude = 0.2f;
    public float floatSpeed = 2f;

    private Outline outline; 
    private Collider col;
    private Rigidbody rb;
    [HideInInspector] public bool isPickedUp = false;
    public bool isPlacedOnSnapPoint = false;
    public bool isPlaced = false;
    [Tooltip("Удалять ли объект при подборе? Если это пресет на сцене, снимите галочку, чтобы он просто скрывался.")]
    public bool destroyOnPickUp = true;
    private Vector3 startPos;

    private bool isThrown = false;
    private float stopTimer = 0f;
    protected bool originalFloatingState;
    private Vector3 customGravity;

    void Awake()
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>(); 
        
        startPos = transform.position;
        originalFloatingState = isFloating; 

        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    void Update()
    {
        if (isThrown)
        {
            if (rb != null && rb.linearVelocity.sqrMagnitude < 0.1f)
            {
                stopTimer += Time.deltaTime;
                if (stopTimer > 0.5f) 
                {
                    isThrown = false;
                    rb.isKinematic = true; 
                    if (col != null) col.isTrigger = true; 
                    
                    startPos = transform.position + new Vector3(0f, floatAmplitude + 0.05f, 0f); 
                    isFloating = originalFloatingState; 
                }
            }
            else
            {
                stopTimer = 0f; 
            }
        }
        else if (isFloating)
        {
            transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime, Space.World);
            transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * floatSpeed) * floatAmplitude, 0f);
        }
    }

    void FixedUpdate()
    {
        if (isThrown && rb != null && !rb.isKinematic)
        {
            customGravity = Physics.gravity * throwGravityScale;
            rb.useGravity = false;
            rb.AddForce(customGravity, ForceMode.Acceleration);
        }
    }

    // --- ВОССТАНОВЛЕНА ФУНКЦИЯ ПОДСВЕТКИ ---
    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null) outline.enabled = isHighlighted;
    }

    public virtual void Toss(Vector3 direction, float force)
    {
        isThrown = true;
        isFloating = false; 
        stopTimer = 0f;

        if (col != null) 
        {
            col.isTrigger = false;
            
            if (col is MeshCollider meshCollider)
            {
                meshCollider.convex = true;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
                foreach (Collider pCol in playerColliders)
                {
                    Physics.IgnoreCollision(col, pCol, true);
                }
            }
        }

        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.isKinematic = false;
        rb.mass = throwMass;
        rb.linearDamping = throwDrag; 
        rb.angularDamping = 0.5f; 
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; 
        
        rb.AddForce(direction * force, ForceMode.Impulse);
        
        if (throwSpinForce > 0f)
        {
            Vector3 randomSpin = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            rb.AddTorque(randomSpin * throwSpinForce, ForceMode.Impulse);
        }
    }

    public virtual void PickUp()
    {
        if (isPickedUp) return; 

        if (itemID == 87)
        {
            int coinsToAdd = amount > 0 ? amount : 1;
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddCoins(coinsToAdd);
            }
            isPickedUp = true;
            if (destroyOnPickUp)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
                isPlaced = false;
                isPlacedOnSnapPoint = false;
            }
            return;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryItemData data = new InventoryItemData(this);
            int leftover = InventoryManager.Instance.AddItemWithLeftover(data);

            if (leftover == 0)
            {
                isPickedUp = true;
                if (destroyOnPickUp)
                {
                    Destroy(gameObject); 
                }
                else
                {
                    gameObject.SetActive(false);
                    isPlaced = false;
                    isPlacedOnSnapPoint = false;
                }
            }
            else if (leftover < amount)
            {
                amount = leftover;
                Debug.Log($"Частично подобрано: {itemName}. На полу осталось: {leftover}");
            }
            else
            {
                Debug.Log("Инвентарь полон! Не могу взять " + itemName);
            }
        }
    }

    public virtual void RestoreData(InventoryItemData data)
    {
        amount = data.amount;
        isStackable = data.isStackable;
        maxStackSize = data.maxStackSize;
        lanternFuel = data.lanternFuel; // Сохраняем топливо лампы на брошенном пропе
        
        ConsumableItem cons = GetComponent<ConsumableItem>();
        if (cons == null) cons = GetComponentInChildren<ConsumableItem>();

        if (cons != null && data.isConsumable)
        {
            cons.currentAmount = data.currentAmount;
            cons.currentLiquidType = data.currentLiquidType;
            cons.UpdateVisuals();
        }
    }
}