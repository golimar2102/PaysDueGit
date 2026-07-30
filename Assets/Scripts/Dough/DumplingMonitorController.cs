using System.Collections;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class DumplingMonitorController : MonoBehaviour
{
    [System.Serializable]
    public class DumplingMonitorSettings
    {
        [Tooltip("Тип мяса (например: Beef, Pork, Canine, Feline, Avian)")]
        public string meatType;
        
        [Tooltip("Компонент текста на экране телевизора (работает как с 3D TextMeshPro, так и с TextMeshProUGUI)")]
        public TMP_Text screenText;

        [Tooltip("Знаменатель (число после дроби, по дефолту 20)")]
        public int denominatorValue = 20;

        [Tooltip("Коллайдер этого телевизора для клика мышкой")]
        public Collider monitorCollider;

        [Tooltip("Трансформ логотипа/модели черепа, который увеличивается при наведении")]
        public Transform logoTransform;

        [Tooltip("Префаб предмета, который спавнится при успешной сдаче")]
        public GameObject spawnItemPrefab;

        [Tooltip("Точка спавна предмета")]
        public Transform itemSpawnAnchor;

        [Tooltip("Система частиц на сцене, которая проигрывается при сдаче")]
        public ParticleSystem sceneParticleSystem;

        [Tooltip("Задержка запуска системы частиц после начала анимации (в секундах)")]
        public float particlePlayDelay = 0.5f;

        [Tooltip("Задержка появления предмета после начала анимации (в секундах)")]
        public float spawnDelay = 1.0f;

        [System.NonSerialized]
        public Vector3 originalScale = Vector3.one;
    }

    [Header("Настройки мониторов пельменей")]
    [Tooltip("Список из 5 мониторов для каждого типа пельменей")]
    public List<DumplingMonitorSettings> dumplingMonitors = new List<DumplingMonitorSettings>()
    {
        new DumplingMonitorSettings { meatType = "Beef" },
        new DumplingMonitorSettings { meatType = "Pork" },
        new DumplingMonitorSettings { meatType = "Canine" },
        new DumplingMonitorSettings { meatType = "Feline" },
        new DumplingMonitorSettings { meatType = "Avian" }
    };

    [Header("Анимация Логотипов при наведении")]
    [Tooltip("Во сколько раз увеличивается логотип при наведении мыши")]
    public float logoHoverScaleMultiplier = 1.2f;
    [Tooltip("Скорость изменения размера логотипа")]
    public float logoScaleSpeed = 8f;

    [Header("Настройки НПС")]
    [Tooltip("Аниматор персонажа НПС")]
    public Animator npcAnimator;
    [Tooltip("Имя триггера для проигрывания анимации сдачи")]
    public string npcAnimTriggerName = "Deliver";

    private DumplingMonitorSettings hoveredMonitor = null;

    [Header("Отладка (Текущее кол-во пельменей)")]
    public int debugBeefCount;
    public int debugPorkCount;
    public int debugCanineCount;
    public int debugFelineCount;
    public int debugAvianCount;

    private int lastBeefCount;
    private int lastPorkCount;
    private int lastCanineCount;
    private int lastFelineCount;
    private int lastAvianCount;

    private PickUpItem lastHoveredItem = null;
    private List<PickUpItem> spawnedItems = new List<PickUpItem>();

    void OnEnable()
    {
        DumplingCounter.OnCountsChanged += UpdateAllMonitorsUI;
        InitializeTrackedCounts();
        UpdateAllMonitorsUI();
    }

    void OnDisable()
    {
        DumplingCounter.OnCountsChanged -= UpdateAllMonitorsUI;
        ResetAllLogoScalesImmediate();
        if (lastHoveredItem != null)
        {
            HighlightItem(lastHoveredItem, false);
            lastHoveredItem = null;
        }
    }

    void Start()
    {
        // Кэшируем исходные размеры логотипов
        foreach (var monitor in dumplingMonitors)
        {
            if (monitor != null && monitor.logoTransform != null)
            {
                monitor.originalScale = monitor.logoTransform.localScale;
            }
        }
        InitializeTrackedCounts();
        UpdateAllMonitorsUI();
    }

    private void InitializeTrackedCounts()
    {
        lastBeefCount = debugBeefCount = DumplingCounter.GetCount("Beef");
        lastPorkCount = debugPorkCount = DumplingCounter.GetCount("Pork");
        lastCanineCount = debugCanineCount = DumplingCounter.GetCount("Canine");
        lastFelineCount = debugFelineCount = DumplingCounter.GetCount("Feline");
        lastAvianCount = debugAvianCount = DumplingCounter.GetCount("Avian");
    }

    private void SyncInspectorCounts()
    {
        // Beef
        int currentBeef = DumplingCounter.GetCount("Beef");
        if (debugBeefCount != lastBeefCount)
        {
            DumplingCounter.SetCount("Beef", debugBeefCount);
            lastBeefCount = debugBeefCount;
        }
        else if (currentBeef != lastBeefCount)
        {
            debugBeefCount = currentBeef;
            lastBeefCount = currentBeef;
        }

        // Pork
        int currentPork = DumplingCounter.GetCount("Pork");
        if (debugPorkCount != lastPorkCount)
        {
            DumplingCounter.SetCount("Pork", debugPorkCount);
            lastPorkCount = debugPorkCount;
        }
        else if (currentPork != lastPorkCount)
        {
            debugPorkCount = currentPork;
            lastPorkCount = currentPork;
        }

        // Canine
        int currentCanine = DumplingCounter.GetCount("Canine");
        if (debugCanineCount != lastCanineCount)
        {
            DumplingCounter.SetCount("Canine", debugCanineCount);
            lastCanineCount = debugCanineCount;
        }
        else if (currentCanine != lastCanineCount)
        {
            debugCanineCount = currentCanine;
            lastCanineCount = currentCanine;
        }

        // Feline
        int currentFeline = DumplingCounter.GetCount("Feline");
        if (debugFelineCount != lastFelineCount)
        {
            DumplingCounter.SetCount("Feline", debugFelineCount);
            lastFelineCount = debugFelineCount;
        }
        else if (currentFeline != lastFelineCount)
        {
            debugFelineCount = currentFeline;
            lastFelineCount = currentFeline;
        }

        // Avian
        int currentAvian = DumplingCounter.GetCount("Avian");
        if (debugAvianCount != lastAvianCount)
        {
            DumplingCounter.SetCount("Avian", debugAvianCount);
            lastAvianCount = debugAvianCount;
        }
        else if (currentAvian != lastAvianCount)
        {
            debugAvianCount = currentAvian;
            lastAvianCount = currentAvian;
        }
    }

    void Update()
    {
        // Синхронизируем инспектор и статические счетчики в реальном времени
        SyncInspectorCounts();

        // Работает только когда игрок находится в режиме просмотра смены локации
        if (LocationTransitionController.activeTransition == null || !LocationTransitionController.activeTransition.isViewing)
        {
            ResetAllLogoScales();
            if (lastHoveredItem != null)
            {
                HighlightItem(lastHoveredItem, false);
                lastHoveredItem = null;
            }
            return;
        }

        PerformHoverAndClick();
    }

    private void HighlightItem(PickUpItem item, bool state)
    {
        if (item == null) return;

        Outline outlineComp = item.GetComponent<Outline>();
        if (outlineComp == null) outlineComp = item.GetComponentInChildren<Outline>(true);

        if (state)
        {
            if (outlineComp == null)
            {
                outlineComp = item.gameObject.AddComponent<Outline>();
            }
            outlineComp.OutlineMode = Outline.Mode.OutlineAll;
            outlineComp.OutlineColor = new Color(1f, 0.84f, 0f); // Gold color
            outlineComp.OutlineWidth = 10f;
            outlineComp.enabled = true;
        }
        else
        {
            if (outlineComp != null)
            {
                outlineComp.enabled = false;
            }
        }
    }

    private void PerformHoverAndClick()
    {
        if (Camera.main == null) return;

        // Очищаем удаленные/подобранные предметы
        spawnedItems.RemoveAll(item => item == null || item.isPickedUp);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        // Получаем все пересечения луча
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        // Сортируем по расстоянию
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        DumplingMonitorSettings currentHitMonitor = null;
        PickUpItem currentHitItem = null;

        foreach (var hit in hits)
        {
            // Игнорируем не-интерактивные триггеры (зоны света, звука и т.д.), чтобы они не блокировали луч
            if (hit.collider.isTrigger)
            {
                bool isInteractable = hit.collider.GetComponentInParent<PickUpItem>() != null ||
                                     hit.collider.GetComponentInChildren<PickUpItem>(true) != null ||
                                     hit.collider.GetComponentInParent<LocationTransitionController>() != null ||
                                     hit.collider.GetComponentInParent<DumplingMonitorController>() != null;

                foreach (var monitor in dumplingMonitors)
                {
                    if (monitor != null && monitor.monitorCollider == hit.collider)
                    {
                        isInteractable = true;
                        break;
                    }
                }

                if (!isInteractable) continue;
            }

            // 1. Проверяем попадание по поднимаемым предметам
            PickUpItem hitPickup = hit.collider.GetComponent<PickUpItem>();
            if (hitPickup == null) hitPickup = hit.collider.GetComponentInChildren<PickUpItem>(true);
            if (hitPickup == null) hitPickup = hit.collider.GetComponentInParent<PickUpItem>();

            if (hitPickup != null)
            {
                currentHitItem = hitPickup;
                break; // Нашли ближайший предмет
            }

            // 2. Проверяем попадание по мониторам
            foreach (var monitor in dumplingMonitors)
            {
                if (monitor != null && monitor.monitorCollider != null)
                {
                    if (hit.collider == monitor.monitorCollider || hit.transform.IsChildOf(monitor.monitorCollider.transform))
                    {
                        currentHitMonitor = monitor;
                        break;
                    }
                }
            }

            if (currentHitMonitor != null)
            {
                break; // Нашли ближайший монитор
            }
        }

        // Подсветка предметов при наведении
        if (currentHitItem != lastHoveredItem)
        {
            if (lastHoveredItem != null) HighlightItem(lastHoveredItem, false);
            if (currentHitItem != null) HighlightItem(currentHitItem, true);
            lastHoveredItem = currentHitItem;
        }

        // Клик по предмету для подбора
        if (currentHitItem != null && Input.GetKeyDown(KeyCode.Mouse0))
        {
            int initialAmount = currentHitItem.amount;
            currentHitItem.PickUp();
            if (currentHitItem == null || currentHitItem.isPickedUp || currentHitItem.amount < initialAmount)
            {
                HighlightItem(currentHitItem, false);
                if (lastHoveredItem == currentHitItem) lastHoveredItem = null;
            }
            return;
        }

        // Наведение на мониторы
        hoveredMonitor = currentHitMonitor;

        // Плавная интерполяция размеров логотипов
        foreach (var monitor in dumplingMonitors)
        {
            if (monitor == null || monitor.logoTransform == null) continue;

            Vector3 targetScale = monitor.originalScale;
            if (monitor == hoveredMonitor)
            {
                targetScale = monitor.originalScale * logoHoverScaleMultiplier;
            }

            monitor.logoTransform.localScale = Vector3.Lerp(monitor.logoTransform.localScale, targetScale, Time.deltaTime * logoScaleSpeed);
        }

        // Клик по монитору
        if (hoveredMonitor != null && Input.GetKeyDown(KeyCode.Mouse0))
        {
            TryDeliverDumplings(hoveredMonitor);
        }
    }

    private void TryDeliverDumplings(DumplingMonitorSettings monitor)
    {
        int currentCount = DumplingCounter.GetCount(monitor.meatType);
        if (currentCount >= monitor.denominatorValue)
        {
            // Отнимаем знаменатель от числителя
            if (DumplingCounter.Deduct(monitor.meatType, monitor.denominatorValue))
            {
                // Запускаем анимацию у НПС
                if (npcAnimator != null && !string.IsNullOrEmpty(npcAnimTriggerName))
                {
                    npcAnimator.SetTrigger(npcAnimTriggerName);
                }

                // Запускаем корутину для проигрывания партиклов с задержкой
                StartCoroutine(PlayParticleWithDelay(monitor));

                // Запускаем корутину для спавна предмета с задержкой
                StartCoroutine(SpawnItemWithDelay(monitor));

                Debug.Log($"[DumplingMonitor] Сдано {monitor.denominatorValue} пельменей типа {monitor.meatType}.");
            }
        }
        else
        {
            Debug.Log($"[DumplingMonitor] Недостаточно пельменей типа {monitor.meatType} для сдачи. Нужно {monitor.denominatorValue}, есть {currentCount}.");
        }
    }

    private IEnumerator PlayParticleWithDelay(DumplingMonitorSettings monitor)
    {
        if (monitor.sceneParticleSystem != null)
        {
            if (monitor.particlePlayDelay > 0f)
            {
                yield return new WaitForSeconds(monitor.particlePlayDelay);
            }
            monitor.sceneParticleSystem.Play(true);
        }
    }

    private IEnumerator SpawnItemWithDelay(DumplingMonitorSettings monitor)
    {
        if (monitor.spawnDelay > 0f)
        {
            yield return new WaitForSeconds(monitor.spawnDelay);
        }

        if (monitor.spawnItemPrefab != null && monitor.itemSpawnAnchor != null)
        {
            GameObject spawned = Instantiate(monitor.spawnItemPrefab, monitor.itemSpawnAnchor.position, monitor.itemSpawnAnchor.rotation);
            
            Rigidbody rb = spawned.GetComponent<Rigidbody>();
            if (rb == null) rb = spawned.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            PickUpItem pickup = spawned.GetComponent<PickUpItem>();
            if (pickup == null) pickup = spawned.GetComponentInChildren<PickUpItem>(true);
            if (pickup != null)
            {
                spawnedItems.Add(pickup);
            }
        }
    }

    private void ResetAllLogoScales()
    {
        hoveredMonitor = null;
        foreach (var monitor in dumplingMonitors)
        {
            if (monitor != null && monitor.logoTransform != null)
            {
                monitor.logoTransform.localScale = Vector3.Lerp(monitor.logoTransform.localScale, monitor.originalScale, Time.deltaTime * logoScaleSpeed);
            }
        }
    }

    private void ResetAllLogoScalesImmediate()
    {
        hoveredMonitor = null;
        foreach (var monitor in dumplingMonitors)
        {
            if (monitor != null && monitor.logoTransform != null)
            {
                monitor.logoTransform.localScale = monitor.originalScale;
            }
        }
    }

    public void UpdateAllMonitorsUI()
    {
        foreach (var monitorSettings in dumplingMonitors)
        {
            if (monitorSettings == null) continue;

            int currentCount = DumplingCounter.GetCount(monitorSettings.meatType);

            if (monitorSettings.screenText != null)
            {
                monitorSettings.screenText.text = $"{currentCount} / {monitorSettings.denominatorValue}";
            }
        }
    }
}
