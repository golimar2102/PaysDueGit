using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Localization;

public class StorageContainer : MonoBehaviour
{
    [Header("Настройки названия хранилища")]
    [Tooltip("Локализованное название хранилища для отображения в UI")]
    public LocalizedString localizedContainerName;
    [Tooltip("Локализованная подсказка для открытия")]
    public LocalizedString localizedOpenPrompt;

    [Header("Настройки инвентаря")]
    [Tooltip("Количество слотов в этом хранилище")]
    [Range(1, 40)]
    public int slotCount = 12;

    [Tooltip("Список предметов, изначально находящихся в хранилище (можно заполнить в инспекторах)")]
    public List<InitialStorageItem> initialItems = new List<InitialStorageItem>();

    // Текущий список сохраненных предметов (по индексу слота)
    [HideInInspector]
    public List<InventoryItemData> storedItems = new List<InventoryItemData>();

    [System.Serializable]
    public class InitialStorageItem
    {
        [Tooltip("ID предмета из базы (allItemsDatabase)")]
        public int itemID;
        [Tooltip("Количество")]
        public int amount = 1;
        [Tooltip("Топливо/Прочность (если применимо, иначе -1)")]
        public float fuel = -1f;
        [Tooltip("Текущее заполнение расходника (если применимо)")]
        public int currentConsumableAmount = 0;
        [Tooltip("Индекс слота (от 0 до slotCount-1). Если -1, положится в первый свободный.")]
        public int slotIndex = -1;
    }

    [System.Serializable]
    public class MovingPart
    {
        [Tooltip("Объект дверцы/ящика, который будет двигаться")]
        public Transform partTransform;

        public enum MovementType { Rotate, Translate }
        [Tooltip("Тип движения: поворот или сдвиг")]
        public MovementType movementType = MovementType.Rotate;

        [Tooltip("Локальные углы поворота или позиция в закрытом состоянии")]
        public Vector3 closedOffset;
        [Tooltip("Локальные углы поворота или позиция в открытом состоянии")]
        public Vector3 openOffset;
    }

    [Header("Настройки физических дверей / ящиков")]
    [Tooltip("Список всех двигающихся частей при открытии")]
    public List<MovingPart> movingParts = new List<MovingPart>();
    [Tooltip("Скорость открытия/закрытия")]
    public float transitionSpeed = 5f;
    [Tooltip("Кривая перехода (необязательно)")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Ориентиры для 3D предметов (Физическое отображение)")]
    [Tooltip("Массив пустых объектов (pivots), куда спавнится 3D модель предмета в зависимости от его слота")]
    public Transform[] itemPivotPoints;

    [Header("Звуки")]
    [Tooltip("Звук открытия")]
    public AudioSource openSound;
    [Tooltip("Звук закрытия")]
    public AudioSource closeSound;

    [Header("Подсветка")]
    [Tooltip("Компонент Outline для подсветки объекта при наведении")]
    public Outline outline;

    [Header("Настройки смещения инвентаря игрока")]
    [Tooltip("Изменять ли положение и масштаб инвентаря при открытии?")]
    public bool customizeInventoryLayout = true;
    [Tooltip("Смещение инвентаря на экране")]
    public Vector2 inventoryOffsetPosition = new Vector2(-300f, 0f);
    [Tooltip("Масштаб инвентаря")]
    public Vector3 inventoryScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    [Tooltip("Смещение хотбара на экране")]
    public Vector2 hotbarOffsetPosition = new Vector2(-300f, 25f);
    [Tooltip("Масштаб хотбара")]
    public Vector3 hotbarScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    [Tooltip("Элементы интерфейса HUD, которые будут скрыты при открытии хранилища")]
    public GameObject[] objectsToHide;

    // Состояние открытия
    [HideInInspector]
    public bool isOpened = false;

    private bool initialItemsPopulated = false;

    private float currentInterpolation = 0f;
    private float targetInterpolation = 0f;

    // Массив под спавненные визуальные 3D объекты
    private GameObject[] spawnedItemVisuals;

    void Awake()
    {
        if (outline == null) outline = GetComponentInChildren<Outline>(true);
        if (outline != null) outline.enabled = false;
    }

    void Start()
    {
        // Инициализируем список предметов
        if (storedItems.Count == 0)
        {
            for (int i = 0; i < slotCount; i++)
            {
                storedItems.Add(null);
            }
        }
        else if (storedItems.Count < slotCount)
        {
            while (storedItems.Count < slotCount)
            {
                storedItems.Add(null);
            }
        }

        // Загружаем начальные предметы
        PopulateInitialItems();

        // Устанавливаем части в закрытое состояние
        UpdateMovingParts(0f);

        // Спавним 3D визуальные модели
        SyncVisualItems();
    }

    private void PopulateInitialItems()
    {
        if (InventoryManager.Instance == null) return;

        initialItemsPopulated = true;

        foreach (var init in initialItems)
        {
            GameObject prefab = InventoryManager.Instance.GetPrefabByID(init.itemID);
            if (prefab == null) continue;

            PickUpItem pickUp = prefab.GetComponent<PickUpItem>();
            if (pickUp == null) pickUp = prefab.GetComponentInChildren<PickUpItem>();
            if (pickUp == null) continue;

            // Создаем InventoryItemData
            InventoryItemData data = new InventoryItemData(pickUp);
            data.amount = init.amount;
            if (init.fuel >= 0) data.lanternFuel = init.fuel;
            if (data.isConsumable && init.currentConsumableAmount > 0)
            {
                data.currentAmount = init.currentConsumableAmount;
            }

            // Кладем в слот
            if (init.slotIndex >= 0 && init.slotIndex < slotCount)
            {
                storedItems[init.slotIndex] = data;
            }
            else
            {
                // Ищем первый пустой
                for (int i = 0; i < slotCount; i++)
                {
                    if (storedItems[i] == null)
                    {
                        storedItems[i] = data;
                        break;
                    }
                }
            }
        }
    }

    void Update()
    {
        if (!initialItemsPopulated && InventoryManager.Instance != null)
        {
            PopulateInitialItems();
            SyncVisualItems();
        }

        if (currentInterpolation != targetInterpolation)
        {
            currentInterpolation = Mathf.MoveTowards(currentInterpolation, targetInterpolation, Time.deltaTime * transitionSpeed);
            float t = transitionCurve.keys.Length > 0 ? transitionCurve.Evaluate(currentInterpolation) : currentInterpolation;
            UpdateMovingParts(t);
        }
    }

    private void UpdateMovingParts(float t)
    {
        foreach (var part in movingParts)
        {
            if (part.partTransform == null) continue;

            if (part.movementType == MovingPart.MovementType.Rotate)
            {
                part.partTransform.localRotation = Quaternion.Euler(Vector3.Lerp(part.closedOffset, part.openOffset, t));
            }
            else
            {
                part.partTransform.localPosition = Vector3.Lerp(part.closedOffset, part.openOffset, t);
            }
        }
    }

    public void SetHighlight(bool active)
    {
        if (outline != null) outline.enabled = active;
    }

    public void Open()
    {
        if (isOpened) return;
        isOpened = true;
        targetInterpolation = 1f;

        if (openSound != null) openSound.Play();

        // Открываем UI хранилища
        if (StorageUI.Instance != null)
        {
            StorageUI.Instance.Open(this);
        }
    }

    public void Close()
    {
        if (!isOpened) return;
        isOpened = false;
        targetInterpolation = 0f;

        if (closeSound != null) closeSound.Play();

        // Сохраняем предметы из UI обратно в контейнер
        if (StorageUI.Instance != null && StorageUI.Instance.activeContainer == this)
        {
            StorageUI.Instance.SaveActiveContainerData();
        }
    }

    public void UpdateStoredItems(List<InventoryItemData> newItems)
    {
        storedItems = new List<InventoryItemData>(newItems);
        SyncVisualItems();
    }

    public void SyncVisualItems()
    {
        if (spawnedItemVisuals == null)
        {
            spawnedItemVisuals = new GameObject[slotCount];
        }
        else if (spawnedItemVisuals.Length != slotCount)
        {
            // На случай если размер изменился, подгоняем массив с чисткой
            GameObject[] oldVisuals = spawnedItemVisuals;
            spawnedItemVisuals = new GameObject[slotCount];
            for (int i = 0; i < oldVisuals.Length; i++)
            {
                if (i < slotCount)
                {
                    spawnedItemVisuals[i] = oldVisuals[i];
                }
                else if (oldVisuals[i] != null)
                {
                    Destroy(oldVisuals[i]);
                }
            }
        }

        for (int i = 0; i < slotCount; i++)
        {
            if (itemPivotPoints == null || i >= itemPivotPoints.Length || itemPivotPoints[i] == null) continue;

            InventoryItemData data = (storedItems != null && i < storedItems.Count) ? storedItems[i] : null;

            if (data != null && data.amount > 0)
            {
                // Если модель уже заспавнена
                if (spawnedItemVisuals[i] != null)
                {
                    PickUpItem existing = spawnedItemVisuals[i].GetComponent<PickUpItem>();
                    if (existing == null) existing = spawnedItemVisuals[i].GetComponentInChildren<PickUpItem>();
                    
                    if (existing != null && existing.itemID == data.itemID)
                    {
                        // Уже нужный предмет заспавнен, ничего делать не надо
                        continue;
                    }
                    
                    Destroy(spawnedItemVisuals[i]);
                    spawnedItemVisuals[i] = null;
                }

                // Спавним 3D модель
                if (InventoryManager.Instance != null)
                {
                    GameObject prefab = InventoryManager.Instance.GetPrefabByID(data.itemID);
                    if (prefab != null)
                    {
                        GameObject spawned = Instantiate(prefab, itemPivotPoints[i]);
                        spawned.transform.localPosition = Vector3.zero;
                        spawned.transform.localRotation = Quaternion.identity;
                        spawned.transform.localScale = prefab.transform.localScale;

                        // Отключаем физику и коллайдеры
                        if (spawned.TryGetComponent<Rigidbody>(out var rb))
                        {
                            rb.isKinematic = true;
                            rb.detectCollisions = false;
                        }
                        foreach (var col in spawned.GetComponentsInChildren<Collider>())
                        {
                            col.enabled = false;
                        }
                        foreach (var pu in spawned.GetComponentsInChildren<PickUpItem>())
                        {
                            pu.enabled = false;
                        }
                        foreach (var outl in spawned.GetComponentsInChildren<Outline>())
                        {
                            outl.enabled = false;
                        }

                        spawnedItemVisuals[i] = spawned;
                    }
                }
            }
            else
            {
                // Предмета нет в слоте, удаляем визуализацию
                if (spawnedItemVisuals[i] != null)
                {
                    Destroy(spawnedItemVisuals[i]);
                    spawnedItemVisuals[i] = null;
                }
            }
        }
    }
}
