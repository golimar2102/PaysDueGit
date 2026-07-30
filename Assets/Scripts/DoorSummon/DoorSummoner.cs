using UnityEngine;

public class DoorSummoner : MonoBehaviour
{
    public static DoorSummoner Instance { get; private set; }

    [Header("Настройки призыва")]
    [Tooltip("Префаб двери, который будет падать (должен иметь DoorController)")]
    public GameObject doorPrefab;

    [Tooltip("Ссылка на кнопку призыва в интерфейсе инвентаря")]
    public UnityEngine.UI.Button summonButton;

    [Tooltip("Дистанция перед игроком для спавна двери по умолчанию")]
    public float summonDistance = 3.0f;

    [Tooltip("Высота спавна двери в небе (откуда она начнет падать)")]
    public float spawnHeight = 10.0f;

    [Tooltip("Высота проверки открытого неба (потолка)")]
    public float skyCheckDistance = 10.0f;

    [Tooltip("Максимальный радиус поиска альтернативного места")]
    public float maxSearchRadius = 5.0f;

    [Tooltip("Смещение вращения при спавне (чтобы настроить правильное направление к игроку)")]
    public Vector3 spawnRotationOffset = Vector3.zero;

    [Header("Настройки скорости падения")]
    [Tooltip("Начальная скорость падения двери вниз")]
    public float initialFallSpeed = 12.0f;

    [Tooltip("Дополнительная сила притяжения (ускорение) для ускорения падения")]
    public float extraGravity = 20.0f;

    [Header("Настройки Телепорта (сцена -> префаб)")]
    [Tooltip("Точка назначения для телепорта (трансформ на сцене)")]
    public Transform teleportDestinationPoint;
    [Tooltip("Зона освещения, в которую перенесет игрока")]
    public GameZone teleportTargetZone = GameZone.Farm;

    [Tooltip("Ссылка на объект Blocked (визуал заблокированной кнопки)")]
    public GameObject blockedVisual;

    [Header("Размеры двери для проверки препятствий")]
    public float doorWidth = 1.2f;
    public float doorHeight = 2.2f;
    public float doorDepth = 0.4f;

    [Header("Настройки физики и слоев")]
    [Tooltip("Слои, которые считаются препятствием (стены, шкафы, коробки и т.д.)")]
    public LayerMask obstacleLayers;

    [Tooltip("Слои земли/пола, на которые может упасть дверь")]
    public LayerMask groundLayers;

    [Header("Звуковые эффекты")]
    public AudioClip fallingSound;
    public AudioClip landingSound;
    public AudioClip openingSound;

    [Tooltip("Задержка перед открытием двери после приземления")]
    public float openDelay = 0.5f;

    // Ссылка на текущую активную дверь на сцене
    private GameObject activeDoorInstance;
    public GameObject ActiveDoorInstance => activeDoorInstance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Подписываемся на клик по кнопке призыва
        if (summonButton != null)
        {
            summonButton.onClick.RemoveListener(SummonDoor);
            summonButton.onClick.AddListener(SummonDoor);
        }

        // Если маски слоев не настроены, задаем дефолтные (все слои кроме UI и IgnoreRaycast)
        if (groundLayers == 0)
        {
            groundLayers = ~((1 << 5) | (1 << 2));
        }
        if (obstacleLayers == 0)
        {
            obstacleLayers = ~((1 << 5) | (1 << 2));
        }
    }

    private void Update()
    {
        bool isOutside = true;
        if (DayNightCycle.Instance != null)
        {
            isOutside = (DayNightCycle.Instance.currentZone == GameZone.Farm);
        }

        // Включаем или выключаем визуальную заглушку блокировки
        if (blockedVisual != null && blockedVisual.activeSelf != !isOutside)
        {
            blockedVisual.SetActive(!isOutside);
        }

        // Блокируем кликабельность кнопки
        if (summonButton != null && summonButton.interactable != isOutside)
        {
            summonButton.interactable = isOutside;
        }
    }

    /// <summary>
    /// Публичный метод для вызова из UI-кнопки (onClick в инспекторе)
    /// </summary>
    public void SummonDoor()
    {
        // Проверка: вызов возможен ТОЛЬКО на улице (Outside)
        if (DayNightCycle.Instance != null && DayNightCycle.Instance.currentZone != GameZone.Farm)
        {
            Debug.LogWarning("[DoorSummoner] Невозможно призвать дверь, находясь внутри помещения!");
            return;
        }

        if (TrySummon())
        {
            // Закрываем инвентарь, чтобы игрок увидел падение двери
            if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
            {
                InventoryManager.Instance.ToggleInventory();
            }
        }
    }

    /// <summary>
    /// Основная логика поиска места и призыва
    /// </summary>
    public bool TrySummon()
    {
        if (doorPrefab == null)
        {
            Debug.LogError("[DoorSummoner] Префаб двери не назначен в инспекторе!");
            return false;
        }

        // 1. Ищем игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[DoorSummoner] Игрок с тегом 'Player' не найден на сцене!");
            return false;
        }

        Transform playerTransform = player.transform;

        // 2. Рассчитываем горизонтальное направление взгляда игрока
        Vector3 forward = playerTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        // 3. Вычисляем идеальную точку перед игроком
        Vector3 targetPos = playerTransform.position + forward * summonDistance;

        // Вращение двери: лицом к игроку
        Vector3 lookDir = playerTransform.position - targetPos;
        lookDir.y = 0f;
        Quaternion doorRotation = Quaternion.identity;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            doorRotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(spawnRotationOffset);
        }

        Vector3 spawnPos = Vector3.zero;
        bool found = false;

        // Проверяем идеальную позицию
        if (TryGetGroundPosition(targetPos, out Vector3 groundPos) &&
            !HasObstacle(groundPos, doorRotation, playerTransform) &&
            !HasCeiling(groundPos, playerTransform))
        {
            spawnPos = groundPos;
            found = true;
        }
        else
        {
            // Поиск по спирали/окрестности, если идеальное место занято или под крышей
            float[] searchDistances = new float[] { 1.5f, 3.0f, 4.5f };
            float[] searchAngles = new float[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

            foreach (float dist in searchDistances)
            {
                foreach (float angle in searchAngles)
                {
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                    Vector3 candidatePos = targetPos + dir * dist;

                    if (TryGetGroundPosition(candidatePos, out Vector3 candidateGround) &&
                        !HasObstacle(candidateGround, doorRotation, playerTransform) &&
                        !HasCeiling(candidateGround, playerTransform))
                    {
                        spawnPos = candidateGround;
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
        }

        if (found)
        {
            // 4. Если на сцене уже есть призванная дверь, уничтожаем её перед новым призывом
            if (activeDoorInstance != null)
            {
                Destroy(activeDoorInstance);
            }

            // Спавним дверь в небе над найденной точкой
            Vector3 skySpawnPos = spawnPos + Vector3.up * spawnHeight;
            activeDoorInstance = Instantiate(doorPrefab, skySpawnPos, doorRotation);

            // Настраиваем телепорт на созданной двери, если компонент присутствует
            TeleportDoor tpDoor = activeDoorInstance.GetComponent<TeleportDoor>();
            if (tpDoor == null)
            {
                tpDoor = activeDoorInstance.GetComponentInChildren<TeleportDoor>();
            }

            if (tpDoor != null)
            {
                tpDoor.destinationPoint = teleportDestinationPoint;
                tpDoor.targetZone = teleportTargetZone;
                Debug.Log("[DoorSummoner] Ссылка на точку телепортации успешно передана двери.");
            }

            // Добавляем и инициализируем компонент призыва на созданном объекте
            SummonedDoor summoned = activeDoorInstance.AddComponent<SummonedDoor>();
            summoned.Initialize(spawnPos, playerTransform.position, fallingSound, landingSound, openingSound, openDelay, initialFallSpeed, extraGravity);

            Debug.Log($"[DoorSummoner] Дверь успешно призвана в точке: {spawnPos}.");
            return true;
        }
        else
        {
            Debug.LogWarning("[DoorSummoner] Не удалось найти подходящее свободное место с открытым небом поблизости!");
            return false;
        }
    }

    private bool TryGetGroundPosition(Vector3 candidate, out Vector3 groundPos)
    {
        groundPos = candidate;
        float castHeight = 15f;
        Ray ray = new Ray(candidate + Vector3.up * castHeight, Vector3.down);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, castHeight * 2f, groundLayers))
        {
            groundPos = hit.point;
            return true;
        }
        return false;
    }

    private bool HasObstacle(Vector3 pos, Quaternion rotation, Transform playerTransform)
    {
        // Проверяем бокс препятствий. Приподнимаем центр проверки на 0.1м, чтобы не пересекать плоский пол.
        Vector3 halfExtents = new Vector3(doorWidth / 2f, (doorHeight / 2f) - 0.05f, doorDepth / 2f);
        Vector3 center = pos + Vector3.up * ((doorHeight / 2f) + 0.1f);

        Collider[] colliders = Physics.OverlapBox(center, halfExtents, rotation, obstacleLayers, QueryTriggerInteraction.Ignore);
        foreach (var col in colliders)
        {
            // Игнорируем самого игрока
            if (col.CompareTag("Player") || col.transform.root == playerTransform.root)
                continue;

            // Игнорируем триггеры
            if (col.isTrigger)
                continue;

            return true;
        }
        return false;
    }

    private bool HasCeiling(Vector3 pos, Transform playerTransform)
    {
        RaycastHit hitUp;
        // Пускаем луч вверх из точки чуть выше земли
        if (Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.up, out hitUp, skyCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            if (hitUp.collider != null && !hitUp.collider.CompareTag("Player") && hitUp.collider.transform.root != playerTransform.root)
            {
                // На пути луча есть препятствие (потолок, крыша, мост)
                return true;
            }
        }
        return false;
    }
}
