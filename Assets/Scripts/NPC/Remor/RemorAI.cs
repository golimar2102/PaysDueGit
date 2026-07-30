using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Скрипт монстра Remor.
/// Управляет логикой галлюцинаций (заготовок) при снижении рассудка ниже 100,
/// входом через дверь при рассудке = 0, патрулированием, обнаружением игрока,
/// разгоном в погоню, скримером с фиксацией камеры и телепортацией игрока.
/// </summary>

[RequireComponent(typeof(NavMeshAgent))]
public class RemorAI : MonoBehaviour
{
    public enum RemorState
    {
        Inactive,            // Рассудок > 0, монстр физически выключен/ждет
        EnteringLocation,    // Открывает дверь и заходит на карту
        Searching,           // Блуждает по карте в поиске игрока
        FollowingPlayer,     // Идет шагом за игроком
        GettingReady,        // Игрок смотрит на Remor: проигрывается GetReady
        Chasing,             // Игрок смотрел до конца GetReady: Remor бежит исключительно на игрока
        PerformingJumpscare  // Столкновение: скример, фиксация камеры и телепорт
    }

    [System.Serializable]
    public class RemorPlaceholderEntry
    {
        public string name = "Заготовка";
        [Tooltip("Объект-заготовка (статичный монстр на сцене)")]
        public GameObject placeholderObject;
        [Tooltip("Включать заготовку, если рассудок МЕНЬШЕ ИЛИ РАВЕН этому значению")]
        public float maxSanityThreshold = 99f;
        [Tooltip("Отключать заготовку, если рассудок МЕНЬШЕ ИЛИ РАВЕН этому значению (например 0)")]
        public float minSanityThreshold = 0f;
    }

    [Header("Текущее состояние (Отладка)")]
    public RemorState currentState = RemorState.Inactive;

    [Header("Заготовки (Галлюцинации)")]
    [Tooltip("Список статичных заготовок монстра, которые появляются/исчезают при снижении рассудка")]
    public List<RemorPlaceholderEntry> placeholders = new List<RemorPlaceholderEntry>();

    [Header("Настройки Спавна и Входной Двери")]
    [Tooltip("Зона, в которой проживает/активен Remor")]
    public GameZone remorZone = GameZone.Apartment;
    [Tooltip("Точки спавна в комнатах для случайного появления, если рассудок упал до 0 снаружи")]
    public List<Transform> randomInsideSpawnPoints = new List<Transform>();

    [Tooltip("Transform самой входной двери (без DoorController), если она открывается вращением")]
    public Transform entranceDoorTransform;
    [Tooltip("Ось вращения входной двери (обычно (0,0,1) для Z или (0,1,0) для Y)")]
    public Vector3 entranceDoorOpenAxis = new Vector3(0f, 0f, 1f);
    [Tooltip("Угол открывания двери в градусах (например 90 или -90)")]
    public float entranceDoorOpenAngle = 90f;
    public float entranceDoorSwingSpeed = 4f;

    [Tooltip("Опциональный Animator на входной двери")]
    public Animator entranceDoorAnimator;
    public string entranceDoorOpenTrigger = "Open";
    public string entranceDoorCloseTrigger = "Close";

    [Tooltip("События UnityEvent при открытии/закрытии входной двери (можно привязать любой свой скрипт/действие)")]
    public UnityEngine.Events.UnityEvent onEntranceDoorOpen;
    public UnityEngine.Events.UnityEvent onEntranceDoorClose;

    [Tooltip("Опциональная ссылка на DoorController (если используется обычная дверь)")]
    public DoorController entranceDoor;
    [Tooltip("Точка спавна за дверью (откуда Remor начинает идти)")]
    public Transform entranceSpawnPoint;
    [Tooltip("Длительность анимации входа (монстр стоит на месте во время проигрывания этой анимации)")]
    public float enterAnimDuration = 2.5f;
    [Tooltip("Дистанция, на которую монстр проходит вперед при входе на карту после анимации")]
    public float entranceWalkDistance = 2.5f;
    [Tooltip("Задержка перед закрытием двери за монстром (сек)")]
    public float doorCloseDelay = 3f;

    [Header("Выбивание Игрока из Глазка (Peephole Knockback)")]
    [Tooltip("Звук удара/выбивания двери при выходе из глазка")]
    public AudioClip peepholeKnockbackSound;
    [Tooltip("Сила отбрасывания игрока назад от двери (в метрах)")]
    public float knockbackDistance = 2.5f;
    [Tooltip("Длительность падения/отлета на спину (сек)")]
    public float fallOnBackDuration = 0.6f;
    [Tooltip("Длительность подъема игрока на ноги (сек)")]
    public float getUpDuration = 1.0f;

    [Tooltip("Опциональный Animator камеры (если вы сделаете свою анимацию камеры в Unity)")]
    public Animator cameraAnimator;
    public string cameraKnockbackTrigger = "Knockback";

    [Header("Настройки Скорости и Поиска")]
    public float walkSpeed = 2.5f;
    public float chaseSpeed = 6.5f;
    [Tooltip("Точки интереса для патрулирования. Если список пуст, ищет по тегу poiTag")]
    public List<Transform> patrolPoints = new List<Transform>();
    public string poiTag = "POI";
    public float idlePauseMin = 2f;
    public float idlePauseMax = 5f;

    [Header("Настройки NavMeshAgent (Анти-Дрифт и Срезание Углов)")]
    [Tooltip("Ускорение агента (60-100 убирает дрифт при поворотах и разгоне)")]
    public float agentAcceleration = 60f;
    [Tooltip("Скорость поворота агента (500-720 делает повороты на углах резкими и точными)")]
    public float agentAngularSpeed = 500f;
    [Tooltip("Радиус физического тела агента (0.45-0.6 не позволяет срезать углы лестниц и стен впритычку)")]
    public float agentRadius = 0.5f;
    [Tooltip("Качество обхода препятствий")]
    public ObstacleAvoidanceType avoidanceQuality = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

    [Header("Центрирование и Авто-Открытие Дверей")]
    [Tooltip("Автоматически открывать закрытые двери поблизости")]
    public bool autoOpenDoors = true;
    public float doorInteractionRadius = 2.0f;
    public float doorCloseDistance = 3.5f;

    [Tooltip("Включить отталкивание от стен и перил (чтобы монстр шёл по центру прохода и не лип к краям)")]
    public bool useWallRepulsion = true;
    [Tooltip("Минимальная дистанция до стены/перил")]
    public float minWallDistance = 0.85f;
    [Tooltip("Сила отталкивания от стены в центр прохода")]
    public float wallRepulsionForce = 2.5f;

    [Header("Механика Взгляда (GetReady & Разгон)")]
    [Tooltip("Угол конуса взгляда игрока (в градусах), при попадании в который считается, что игрок смотрит на Remor")]
    public float playerLookFov = 45f;
    [Tooltip("Длительность непрерывного взгляда игрока на Remor для активации бега (сек)")]
    public float getReadyDuration = 2.0f;

    [Header("Зрение и Обнаружение")]
    public float visionRange = 15f;
    [Range(30f, 360f)] public float fovAngle = 110f;
    [Tooltip("Ближняя зона 360° (чувствует игрока спиной на этой дистанции)")]
    public float peripheralRange = 2.5f;
    public LayerMask obstacleMask;

    [Header("Скример и Телепортация")]
    [Tooltip("Точка в пространстве перед лицом монстра, где будет находиться камера во время скримера (Empty child монстра)")]
    public Transform jumpscareCameraPoint;
    [Tooltip("Точка на голове/лице монстра, куда смотрит камера при скримере")]
    public Transform headPoint;
    [Tooltip("Целевая точка (empty child), куда телепортируется игрок после скримера")]
    public Transform teleportTarget;
    [Tooltip("Длительность скримера в секундах")]
    public float jumpscareDuration = 2.0f;
    public AudioClip jumpscareSound;
    [Tooltip("Восстанавливать ли рассудок игроку после скримера (например 100). Если 0 — рассудок не меняется.")]
    public float restoreSanityAfterJumpscare = 100f;
    [Tooltip("Сбрасывать ли монстра в состояние ожидания после скримера")]
    public bool resetMonsterAfterJumpscare = true;

    [Header("Аниматор и Параметры")]
    public Animator animator;
    public string speedAnimParam = "Speed";
    public string enterAnimTrigger = "Enter";
    public string getReadyAnimTrigger = "GetReady";
    public string chaseAnimBool = "IsChasing";
    public string jumpscareAnimTrigger = "Jumpscare";

    [Header("Поворот Головы за Игроком (Head LookAt)")]
    [Tooltip("Включить слежение головой за игроком")]
    public bool useHeadLookAt = true;
    [Tooltip("Максимальный угол поворота головы от направления груди/тела (в градусах)")]
    public float maxHeadTurnAngle = 75f;
    [Tooltip("Скорость плавного поворота головы")]
    public float headTurnSpeed = 8f;

    [Header("Аудио")]
    public AudioSource audioSource;

    // Внутренние ссылки
    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerStats playerStats;
    private float nextRoamTime = 0f;
    private bool hasEntered = false;
    private bool isGettingReady = false;
    private Coroutine currentCoroutine;

    private float nextDoorCheckTime = 0f;
    private List<DoorController> openedDoors = new List<DoorController>();
    private Dictionary<DoorController, float> openedDoorTimes = new Dictionary<DoorController, float>();
    private Quaternion originalEntranceDoorRotation = Quaternion.identity;
    private Quaternion originalHeadLocalRotation = Quaternion.identity;
    private Vector3 jumpscareCamLocalOffset = Vector3.zero;
    private bool hasJumpscareCamOffset = false;
    private float ignoreGazeTimer = 0f;

    // Animator Hashes
    private int h_Speed;
    private int h_Enter;
    private int h_GetReadyTrigger;
    private int h_IsChasing;
    private int h_Jumpscare;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (agent != null)
        {
            agent.acceleration = agentAcceleration;
            agent.angularSpeed = agentAngularSpeed;
            agent.radius = agentRadius;
            agent.obstacleAvoidanceType = avoidanceQuality;
            agent.autoBraking = false;
        }

        if (obstacleMask == 0)
        {
            obstacleMask = LayerMask.GetMask("Default", "Ground", "Obstacle", "Wall");
            if (obstacleMask == 0) obstacleMask = ~0;
        }

        // Кэшируем хэши анимаций
        if (!string.IsNullOrEmpty(speedAnimParam)) h_Speed = Animator.StringToHash(speedAnimParam);
        if (!string.IsNullOrEmpty(enterAnimTrigger)) h_Enter = Animator.StringToHash(enterAnimTrigger);
        if (!string.IsNullOrEmpty(getReadyAnimTrigger)) h_GetReadyTrigger = Animator.StringToHash(getReadyAnimTrigger);
        if (!string.IsNullOrEmpty(chaseAnimBool)) h_IsChasing = Animator.StringToHash(chaseAnimBool);
        if (!string.IsNullOrEmpty(jumpscareAnimTrigger)) h_Jumpscare = Animator.StringToHash(jumpscareAnimTrigger);
    }

    void Start()
    {
        if (entranceDoorTransform != null)
        {
            originalEntranceDoorRotation = entranceDoorTransform.localRotation;
        }

        if (headPoint != null)
        {
            originalHeadLocalRotation = headPoint.localRotation;
        }

        if (jumpscareCameraPoint != null)
        {
            // Сохраняем точный локальный офсет точки камеры относительно КОРНЕВОГО GameObject до запуска анимации костей
            jumpscareCamLocalOffset = transform.InverseTransformPoint(jumpscareCameraPoint.position);
            hasJumpscareCamOffset = true;
        }
        // Ищем игрока
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            playerStats = p.GetComponent<PlayerStats>();
        }

        // Поиск точек POI если не заданы
        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            if (!string.IsNullOrEmpty(poiTag))
            {
                GameObject[] points = GameObject.FindGameObjectsWithTag(poiTag);
                foreach (var pt in points)
                {
                    if (pt != null) patrolPoints.Add(pt.transform);
                }
            }
        }

        // Изначально монстр выключен физически
        SetPhysicalActive(false);
    }

    void Update()
    {
        // Поиск игрока если ещё не найден
        if (playerStats == null)
        {
            if (PlayerStats.Instance != null)
            {
                playerStats = PlayerStats.Instance;
                playerTransform = playerStats.transform;
            }
            else return;
        }

        float sanity = playerStats.currentSanity;

        // 1. Управление заготовками (галлюцинациями)
        UpdatePlaceholders(sanity);

        // 2. Управление спавном/входом при рассудке <= 0
        if (sanity <= 0f && !hasEntered && currentState == RemorState.Inactive)
        {
            // Проверяем, находится ли игрок прямо сейчас в целевой зоне Ремора
            bool isPlayerInZone = (DayNightCycle.Instance != null && DayNightCycle.Instance.currentZone == remorZone);

            if (isPlayerInZone)
            {
                // Игрок был в этой зоне: проигрываем полную сценку входа через дверь
                StartCoroutine(EntranceSequence());
            }
            else
            {
                // Игрок телепортировался снаружи: тихо спавним Ремора в случайной внутрикомнатной точке
                SpawnAtRandomInsidePoint();
            }
            return;
        }

        // Если не активен или находится в процессе анимаций входа/скримера
        if (currentState == RemorState.Inactive || 
            currentState == RemorState.EnteringLocation || 
            currentState == RemorState.PerformingJumpscare)
        {
            return;
        }

        // 3. Основной AI цикл (Searching / FollowingPlayer / GettingReady / Chasing)
        if (currentState == RemorState.Searching)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkSpeed;
            }
            if (animator != null && h_IsChasing != 0) animator.SetBool(h_IsChasing, false);

            // Проверяем видимость игрока
            if (CanSeePlayer())
            {
                currentState = RemorState.FollowingPlayer;
                return;
            }

            // Блуждание по точкам
            PatrolLogic();
        }
        else if (currentState == RemorState.FollowingPlayer)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkSpeed;
                if (playerTransform != null) agent.SetDestination(playerTransform.position);
            }
            if (animator != null && h_IsChasing != 0) animator.SetBool(h_IsChasing, false);

            // Если игрок поворачивается и смотрит на Remor
            if (!isGettingReady && IsPlayerLookingAtRemor())
            {
                StartCoroutine(GetReadySequence());
                return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= 1.8f)
            {
                TriggerJumpscare(playerTransform.gameObject);
            }
        }
        else if (currentState == RemorState.GettingReady)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= 1.8f)
            {
                TriggerJumpscare(playerTransform.gameObject);
            }
        }
        else if (currentState == RemorState.Chasing)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                if (playerTransform != null) agent.SetDestination(playerTransform.position);
            }
            if (animator != null && h_IsChasing != 0) animator.SetBool(h_IsChasing, true);

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= 1.8f)
            {
                TriggerJumpscare(playerTransform.gameObject);
            }
        }

        // Авто-открытие дверей поблизости
        if (Time.time >= nextDoorCheckTime)
        {
            nextDoorCheckTime = Time.time + 0.2f;
            CheckAndOpenNearbyDoors();
        }

        // Центрирование на лестницах и коридорах (отталкивание от стен/перил)
        ApplyWallRepulsion();

        // Обновляем параметр скорости в аниматоре
        if (animator != null && h_Speed != 0 && agent.enabled && agent.isOnNavMesh)
        {
            animator.SetFloat(h_Speed, agent.velocity.magnitude);
        }
    }

    void LateUpdate()
    {
        // Плавный поворот головы за игроком с ограничением угла
        if (useHeadLookAt && headPoint != null && playerTransform != null && 
            currentState != RemorState.Inactive && currentState != RemorState.EnteringLocation && currentState != RemorState.PerformingJumpscare)
        {
            Vector3 targetEyePos = playerTransform.position + Vector3.up * 1.5f;
            Vector3 dirToPlayer = targetEyePos - headPoint.position;
            float dist = dirToPlayer.magnitude;

            if (dist <= visionRange)
            {
                float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer.normalized);

                if (angleToPlayer <= maxHeadTurnAngle)
                {
                    Quaternion targetWorldRot = Quaternion.LookRotation(dirToPlayer.normalized, transform.up);
                    headPoint.rotation = Quaternion.Slerp(headPoint.rotation, targetWorldRot, Time.deltaTime * headTurnSpeed);
                }
                else
                {
                    headPoint.localRotation = Quaternion.Slerp(headPoint.localRotation, originalHeadLocalRotation, Time.deltaTime * headTurnSpeed);
                }
            }
            else
            {
                headPoint.localRotation = Quaternion.Slerp(headPoint.localRotation, originalHeadLocalRotation, Time.deltaTime * headTurnSpeed);
            }
        }
    }

    /// <summary>
    /// Включает и выключает заготовки в зависимости от рассудка
    /// </summary>
    private void UpdatePlaceholders(float sanity)
    {
        if (placeholders == null) return;

        foreach (var entry in placeholders)
        {
            if (entry == null || entry.placeholderObject == null) continue;

            // Если монстр уже вошел физически (рассудок = 0), все заготовки выключаются
            if (hasEntered || sanity <= 0f)
            {
                if (entry.placeholderObject.activeSelf) entry.placeholderObject.SetActive(false);
                continue;
            }

            // Заготовка активна, если рассудок находится в диапазоне (minThreshold, maxThreshold]
            bool shouldBeActive = (sanity < 100f) && 
                                 (sanity <= entry.maxSanityThreshold) && 
                                 (sanity > entry.minSanityThreshold);

            if (entry.placeholderObject.activeSelf != shouldBeActive)
            {
                entry.placeholderObject.SetActive(shouldBeActive);
            }
        }
    }

    /// <summary>
    /// Спавнит Remor в случайной точке внутри комнаты без анимации открывания входной двери.
    /// Вызывается, если рассудок упал до 0 вне локации Remor, и игрок вошел в нее позже.
    /// </summary>
    private void SpawnAtRandomInsidePoint()
    {
        hasEntered = true;

        // Отключаем все заготовки
        foreach (var entry in placeholders)
        {
            if (entry != null && entry.placeholderObject != null)
                entry.placeholderObject.SetActive(false);
        }

        Transform spawnPoint = entranceSpawnPoint;

        // Ищем случайную валидную точку из списка точек засады
        if (randomInsideSpawnPoints != null && randomInsideSpawnPoints.Count > 0)
        {
            List<Transform> validPoints = new List<Transform>();
            foreach (var pt in randomInsideSpawnPoints)
            {
                if (pt != null) validPoints.Add(pt);
            }

            if (validPoints.Count > 0)
            {
                spawnPoint = validPoints[Random.Range(0, validPoints.Count)];
            }
        }

        // Позиционируем на выбранной точке
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        // Включаем визуал и коллайдеры монстра
        SetPhysicalActive(true);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        // Устанавливаем таймер задержки реагирования на взгляд
        ignoreGazeTimer = Time.time + 1.0f;

        // Переходим в режим поиска
        currentState = RemorState.Searching;
        Debug.Log($"[RemorAI] Скрытно затаился в случайной точке зоны {remorZone}: {(spawnPoint != null ? spawnPoint.name : "Default")}");
    }

    /// <summary>
    /// Последовательность входа монстра на локацию
    /// </summary>
    private IEnumerator EntranceSequence()
    {
        currentState = RemorState.EnteringLocation;
        hasEntered = true;

        // Отключаем все заготовки
        foreach (var entry in placeholders)
        {
            if (entry != null && entry.placeholderObject != null)
                entry.placeholderObject.SetActive(false);
        }

        // 1. ПРОВЕРКА ГЛАЗКА: Если игрок смотрит в глазок (Peephole), выбиваем его от двери и сбиваем с ног
        if (PeepholeController.activePeephole != null && PeepholeController.activePeephole.isViewing)
        {
            yield return StartCoroutine(HandlePeepholeKnockback());
        }

        // Позиционируем на точке спавна
        if (entranceSpawnPoint != null)
        {
            transform.position = entranceSpawnPoint.position;
            transform.rotation = entranceSpawnPoint.rotation;
        }

        // Включаем монстра
        SetPhysicalActive(true);

        // Останавливаем NavMeshAgent на время проигрывания анимации появления
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        // Проигрываем анимацию входа (если задан триггер)
        if (animator != null && h_Enter != 0)
        {
            animator.SetTrigger(h_Enter);
        }

        // Открываем входную дверь через скрипт (без обязательного DoorController)
        StartCoroutine(OpenEntranceDoorScripted());

        // Во время проигрывания анимации входа плавно физически перемещаем монстра вперед через проход
        float moveSpeed = (enterAnimDuration > 0f && entranceWalkDistance > 0f) ? (entranceWalkDistance / enterAnimDuration) : 0f;
        float elapsed = 0f;

        while (elapsed < enterAnimDuration)
        {
            elapsed += Time.deltaTime;
            if (moveSpeed > 0f)
            {
                Vector3 moveDelta = transform.forward * moveSpeed * Time.deltaTime;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.Move(moveDelta);
                }
                else
                {
                    transform.position += moveDelta;
                }
            }
            yield return null;
        }

        // По завершении анимации входа включаем стандартный AI поиск
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        yield return new WaitForSeconds(doorCloseDelay);

        // Закрываем входную дверь
        StartCoroutine(CloseEntranceDoorScripted());

        // Задаем задержку игнорирования взгляда после входа (3 сек), чтобы анимации не ломались
        ignoreGazeTimer = Time.time + 3.0f;

        // Переходим в режим поиска
        currentState = RemorState.Searching;
    }

    /// <summary>
    /// Скриптовое открытие входной двери (поддерживает Transform вращение, Animator, UnityEvent и DoorController)
    /// </summary>
    private IEnumerator OpenEntranceDoorScripted()
    {
        onEntranceDoorOpen?.Invoke();

        if (entranceDoorAnimator != null && !string.IsNullOrEmpty(entranceDoorOpenTrigger))
        {
            entranceDoorAnimator.SetTrigger(entranceDoorOpenTrigger);
        }

        if (entranceDoorTransform != null)
        {
            Vector3 axis = entranceDoorOpenAxis.sqrMagnitude > 0.001f ? entranceDoorOpenAxis.normalized : Vector3.forward;
            Quaternion startRot = entranceDoorTransform.localRotation;
            Quaternion targetRot = originalEntranceDoorRotation * Quaternion.AngleAxis(entranceDoorOpenAngle, axis);
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * entranceDoorSwingSpeed;
                entranceDoorTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            entranceDoorTransform.localRotation = targetRot;
        }

        if (entranceDoor != null)
        {
            entranceDoor.UnlockDoor();
            if (!entranceDoor.isOpen) entranceDoor.TryOpenDoor(transform.position);
        }
    }

    /// <summary>
    /// Скриптовое закрытие входной двери
    /// </summary>
    private IEnumerator CloseEntranceDoorScripted()
    {
        onEntranceDoorClose?.Invoke();

        if (entranceDoorAnimator != null && !string.IsNullOrEmpty(entranceDoorCloseTrigger))
        {
            entranceDoorAnimator.SetTrigger(entranceDoorCloseTrigger);
        }

        if (entranceDoorTransform != null)
        {
            Quaternion startRot = entranceDoorTransform.localRotation;
            Quaternion targetRot = originalEntranceDoorRotation;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * entranceDoorSwingSpeed;
                entranceDoorTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            entranceDoorTransform.localRotation = targetRot;
        }

        if (entranceDoor != null && entranceDoor.isOpen)
        {
            entranceDoor.TryOpenDoor(transform.position);
        }
    }

    /// <summary>
    /// Обработка выбивания игрока из глазка двери при появлении Ремора
    /// </summary>
    private IEnumerator HandlePeepholeKnockback()
    {
        PeepholeController peephole = PeepholeController.activePeephole;

        if (peephole != null && peephole.isViewing)
        {
            peephole.ExitTransition();
            yield return new WaitForSeconds(0.25f);
        }

        if (playerTransform == null) yield break;

        PlayerMovement pm = playerTransform.GetComponent<PlayerMovement>() ?? playerTransform.GetComponentInChildren<PlayerMovement>();
        MouseLook ml = playerTransform.GetComponent<MouseLook>() ?? playerTransform.GetComponentInChildren<MouseLook>() ?? playerTransform.GetComponentInParent<MouseLook>();
        Camera playerCam = Camera.main;
        if (playerCam == null && pm != null) playerCam = pm.GetComponentInChildren<Camera>();

        // Блокируем управление на время падения
        if (pm != null) pm.enabled = false;
        if (ml != null) ml.enabled = false;

        // Звук удара выбивания двери
        if (audioSource != null && peepholeKnockbackSound != null)
        {
            audioSource.PlayOneShot(peepholeKnockbackSound);
        }

        // Если у пользователя настроен свой Animator на камере, вызываем триггер анимации камеры
        if (cameraAnimator != null && !string.IsNullOrEmpty(cameraKnockbackTrigger))
        {
            cameraAnimator.SetTrigger(cameraKnockbackTrigger);
        }

        // Направление физического толчка: строго назад от того места, куда игрок смотрел в глазок
        Vector3 pushDir = -playerTransform.forward;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.001f) pushDir = -transform.forward;
        pushDir.Normalize();

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        float pushSpeed = (fallOnBackDuration > 0f) ? (knockbackDistance / fallOnBackDuration) : 0f;

        Vector3 startCamLocalPos = playerCam != null ? playerCam.transform.localPosition : Vector3.up * 3f;
        Quaternion startCamLocalRot = playerCam != null ? playerCam.transform.localRotation : Quaternion.identity;

        Vector3 groundCamPos = new Vector3(startCamLocalPos.x, Mathf.Max(startCamLocalPos.y * 0.35f, 0.8f), startCamLocalPos.z);
        Quaternion fallCamRot = Quaternion.Euler(-65f, 0f, 0f);

        // --- Фаза 1: Плавный физический отлет назад и откидывание на спину ---
        float elapsed = 0f;
        while (elapsed < fallOnBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallOnBackDuration;
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            // Плавное физическое движение тела назад по полу (БЕЗ телепортации!)
            Vector3 moveDelta = pushDir * pushSpeed * (1f - t * 0.5f) * Time.deltaTime;
            if (cc != null && cc.enabled)
            {
                cc.Move(moveDelta);
            }
            else
            {
                playerTransform.position += moveDelta;
            }

            // Плавный поворот и опускание камеры (если не используется свой Animator на камере)
            if (playerCam != null && cameraAnimator == null)
            {
                playerCam.transform.localPosition = Vector3.Lerp(startCamLocalPos, groundCamPos, easeT);
                playerCam.transform.localRotation = Quaternion.Slerp(startCamLocalRot, fallCamRot, easeT);
            }

            yield return null;
        }

        // Пауза лежа на спине (0.3 сек)
        yield return new WaitForSeconds(0.3f);

        // --- Фаза 2: Плавный подъем с пола на ноги ---
        elapsed = 0f;
        while (elapsed < getUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / getUpDuration;
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            if (playerCam != null && cameraAnimator == null)
            {
                playerCam.transform.localPosition = Vector3.Lerp(groundCamPos, startCamLocalPos, easeT);
                playerCam.transform.localRotation = Quaternion.Slerp(fallCamRot, startCamLocalRot, easeT);
            }

            yield return null;
        }

        if (playerCam != null && cameraAnimator == null)
        {
            playerCam.transform.localPosition = startCamLocalPos;
            playerCam.transform.localRotation = startCamLocalRot;
        }

        // Возвращаем управление игроку
        if (pm != null) pm.enabled = true;
        if (ml != null) ml.enabled = true;
    }

    /// <summary>
    /// Патрулирование по точкам интереса
    /// </summary>
    private void PatrolLogic()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            if (Time.time >= nextRoamTime)
            {
                Vector3 targetPos = transform.position;

                if (patrolPoints != null && patrolPoints.Count > 0)
                {
                    Transform pt = patrolPoints[Random.Range(0, patrolPoints.Count)];
                    if (pt != null) targetPos = pt.position;
                }
                else
                {
                    Vector3 randomDir = Random.insideUnitSphere * 15f;
                    randomDir += transform.position;
                    if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                    {
                        targetPos = hit.position;
                    }
                }

                agent.SetDestination(targetPos);
                nextRoamTime = Time.time + Random.Range(idlePauseMin, idlePauseMax) + 5f;
            }
        }
    }

    /// <summary>
    /// Корутина подготовки/угрозы при взгляде игрока
    /// </summary>
    private IEnumerator GetReadySequence()
    {
        if (currentState == RemorState.EnteringLocation || 
            currentState == RemorState.Inactive || 
            currentState == RemorState.PerformingJumpscare || 
            Time.time < ignoreGazeTimer)
        {
            yield break;
        }

        isGettingReady = true;
        currentState = RemorState.GettingReady;

        // Останавливаем движение агента
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Вызываем триггер анимации GetReady
        if (animator != null && h_GetReadyTrigger != 0)
        {
            animator.SetTrigger(h_GetReadyTrigger);
        }

        float elapsed = 0f;
        bool gazeMaintained = true;

        while (elapsed < getReadyDuration)
        {
            elapsed += Time.deltaTime;

            // Поворачиваем тело лицом к игроку
            if (playerTransform != null)
            {
                Vector3 lookDir = playerTransform.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
                }
            }

            // Если игрок отвел взгляд во время стойки
            if (!IsPlayerLookingAtRemor())
            {
                gazeMaintained = false;
                break;
            }

            yield return null;
        }

        if (gazeMaintained)
        {
            // Игрок смотрел весь период — ВКЛЮЧАЕМ ПОЛНЫЙ БЕГ!
            currentState = RemorState.Chasing;
            if (animator != null && h_IsChasing != 0) animator.SetBool(h_IsChasing, true);

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                if (playerTransform != null) agent.SetDestination(playerTransform.position);
            }
        }
        else
        {
            // Игрок отвел взгляд — ВОЗВРАЩАЕМСЯ К ШАГУ
            currentState = RemorState.FollowingPlayer;
            if (animator != null && h_IsChasing != 0) animator.SetBool(h_IsChasing, false);

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkSpeed;
                if (playerTransform != null) agent.SetDestination(playerTransform.position);
            }
        }

        isGettingReady = false;
    }

    /// <summary>
    /// Проверка прямой видимости игрока
    /// </summary>
    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.6f;
        Vector3 targetPos = playerTransform.position + Vector3.up * 1.2f;
        Vector3 dirToPlayer = targetPos - eyePos;
        float distToPlayer = dirToPlayer.magnitude;

        if (distToPlayer > visionRange) return false;

        // Ближняя зона (360 градусов)
        if (distToPlayer <= peripheralRange)
        {
            if (!Physics.Raycast(eyePos, dirToPlayer.normalized, distToPlayer, obstacleMask))
            {
                return true;
            }
        }

        // Конус зрения FOV
        float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
        if (angle <= fovAngle * 0.5f)
        {
            if (!Physics.Raycast(eyePos, dirToPlayer.normalized, distToPlayer, obstacleMask))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет, смотрит ли игрок непосредственно на Remor
    /// </summary>
    public bool IsPlayerLookingAtRemor()
    {
        if (currentState == RemorState.Inactive || 
            currentState == RemorState.EnteringLocation || 
            currentState == RemorState.PerformingJumpscare || 
            Time.time < ignoreGazeTimer)
        {
            return false;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null && playerTransform != null)
        {
            mainCam = playerTransform.GetComponentInChildren<Camera>();
        }
        if (mainCam == null) return false;

        Vector3 targetPos = headPoint != null ? headPoint.position : transform.position + Vector3.up * 1.6f;
        Vector3 dirToRemor = targetPos - mainCam.transform.position;
        float dist = dirToRemor.magnitude;

        if (dist > visionRange) return false;

        float angle = Vector3.Angle(mainCam.transform.forward, dirToRemor.normalized);
        if (angle <= playerLookFov * 0.5f)
        {
            if (!Physics.Raycast(mainCam.transform.position, dirToRemor.normalized, out RaycastHit hit, dist, obstacleMask))
            {
                return true;
            }
            else
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Запуск скримера при столкновении с игроком
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            TriggerJumpscare(other.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<PlayerMovement>() != null)
        {
            TriggerJumpscare(collision.gameObject);
        }
    }

    public void TriggerJumpscare(GameObject playerObj)
    {
        if (currentState == RemorState.PerformingJumpscare || currentState == RemorState.Inactive) return;

        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(DoJumpscareSequence(playerObj));
    }

    /// <summary>
    /// Корутина скримера, фиксации камеры и телепортации
    /// </summary>
    private IEnumerator DoJumpscareSequence(GameObject playerObj)
    {
        currentState = RemorState.PerformingJumpscare;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        // Ищем компоненты управления игрока
        PlayerMovement pm = playerObj.GetComponent<PlayerMovement>() ?? playerObj.GetComponentInChildren<PlayerMovement>();
        MouseLook ml = playerObj.GetComponent<MouseLook>() ?? playerObj.GetComponentInChildren<MouseLook>() ?? playerObj.GetComponentInParent<MouseLook>();

        Camera playerCam = Camera.main;
        if (playerCam == null && pm != null)
        {
            playerCam = pm.GetComponentInChildren<Camera>();
        }

        // Отключаем управление игрока
        if (pm != null) pm.enabled = false;
        if (ml != null) ml.enabled = false;

        // Поворачиваем монстра лицом к игроку
        Vector3 dirToPlayer = playerObj.transform.position - transform.position;
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }

        // Запускаем анимацию скримера
        if (animator != null && h_Jumpscare != 0)
        {
            animator.SetTrigger(h_Jumpscare);
        }

        // Воспроизводим звук скримера
        if (audioSource != null && jumpscareSound != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }

        // Сбрасываем поворот головы в исходное вертикальное состояние
        if (headPoint != null)
        {
            headPoint.localRotation = originalHeadLocalRotation;
        }

        // Сохраняем исходные локальные координаты камеры для восстановления после скримера
        Vector3 savedCamLocalPos = Vector3.up * 1.6f;
        Quaternion savedCamLocalRot = Quaternion.identity;
        if (playerCam != null)
        {
            savedCamLocalPos = playerCam.transform.localPosition;
            savedCamLocalRot = playerCam.transform.localRotation;
        }

        // Запуск фиксации и позиционирования камеры в точке скримера
        float elapsed = 0f;
        while (elapsed < jumpscareDuration)
        {
            elapsed += Time.deltaTime;

            if (playerCam != null)
            {
                if (headPoint != null)
                {
                    headPoint.localRotation = originalHeadLocalRotation;
                }

                Vector3 headPos = headPoint != null ? headPoint.position : (transform.position + Vector3.up * 1.6f);
                Vector3 targetCamPos;
                Quaternion targetCamRot;

                if (hasJumpscareCamOffset)
                {
                    // Точный расчет от тела монстра (без влияния поворотов скелета и смещений костей в анимации)
                    targetCamPos = transform.TransformPoint(jumpscareCamLocalOffset);
                    Vector3 lookDir = (headPos - targetCamPos).normalized;
                    targetCamRot = (lookDir.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(lookDir) : transform.rotation;
                }
                else if (jumpscareCameraPoint != null)
                {
                    targetCamPos = jumpscareCameraPoint.position;
                    Vector3 lookDir = (headPos - targetCamPos).normalized;
                    targetCamRot = (lookDir.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(lookDir) : jumpscareCameraPoint.rotation;
                }
                else
                {
                    // Фаллбек: размещаем камеру на расстоянии 1.5м ровно перед лицом монстра
                    targetCamPos = transform.position + transform.forward * 1.5f + Vector3.up * 1.6f;
                    Vector3 lookDir = (headPos - targetCamPos).normalized;
                    targetCamRot = Quaternion.LookRotation(lookDir);
                }

                playerCam.transform.position = Vector3.Lerp(playerCam.transform.position, targetCamPos, Time.deltaTime * 20f);
                playerCam.transform.rotation = Quaternion.Slerp(playerCam.transform.rotation, targetCamRot, Time.deltaTime * 20f);
            }

            yield return null;
        }

        // Телепортируем игрока
        if (teleportTarget != null && pm != null)
        {
            pm.Teleport(teleportTarget);
        }
        else if (teleportTarget != null)
        {
            playerObj.transform.position = teleportTarget.position;
            playerObj.transform.rotation = teleportTarget.rotation;
            Physics.SyncTransforms();
        }

        // Восстанавливаем исходную позицию и поворот камеры игрока
        if (playerCam != null)
        {
            playerCam.transform.localPosition = savedCamLocalPos;
            playerCam.transform.localRotation = savedCamLocalRot;
        }

        // Включаем обратно управление игроком
        if (pm != null) pm.enabled = true;
        if (ml != null) ml.enabled = true;

        // Восстанавливаем рассудок если задано
        if (restoreSanityAfterJumpscare > 0f && playerStats != null)
        {
            playerStats.RestoreSanity(restoreSanityAfterJumpscare);
        }

        // Сброс монстра
        if (resetMonsterAfterJumpscare)
        {
            ResetToInactiveState();
        }
        else
        {
            currentState = RemorState.Searching;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        }
    }

    /// <summary>
    /// Сбрасывает состояние монстра в неактивное
    /// </summary>
    public void ResetToInactiveState()
    {
        currentState = RemorState.Inactive;
        hasEntered = false;
        SetPhysicalActive(false);

        // Возвращаем на точку спавна
        if (entranceSpawnPoint != null)
        {
            transform.position = entranceSpawnPoint.position;
            transform.rotation = entranceSpawnPoint.rotation;
        }
    }

    /// <summary>
    /// Автоматическое открытие закрытых/запертых дверей поблизости и их закрытие при отдалении
    /// </summary>
    private void CheckAndOpenNearbyDoors()
    {
        if (!autoOpenDoors) return;

        // 1. Находим закрытые двери в радиусе (включая триггеры)
        Collider[] colliders = Physics.OverlapSphere(transform.position + Vector3.up * 1f, doorInteractionRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var col in colliders)
        {
            if (col == null) continue;

            DoorController door = col.GetComponentInParent<DoorController>() ?? col.GetComponent<DoorController>() ?? col.GetComponentInChildren<DoorController>();
            if (door != null && !door.isOpen)
            {
                if (door.isLocked)
                {
                    door.UnlockDoor();
                }

                // Открываем дверь от себя
                door.TryOpenDoor(transform.position);

                if (!openedDoors.Contains(door))
                {
                    openedDoors.Add(door);
                }
                openedDoorTimes[door] = Time.time;
            }
        }

        // 2. Закрываем двери за собой ТОЛЬКО через время и при реальном отдалении
        for (int i = openedDoors.Count - 1; i >= 0; i--)
        {
            DoorController door = openedDoors[i];
            if (door == null || !door.isOpen)
            {
                if (door != null) openedDoorTimes.Remove(door);
                openedDoors.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(transform.position, door.transform.position);
            openedDoorTimes.TryGetValue(door, out float openTime);

            // Закрываем дверь только если с момента открытия прошло минимум 4 секунды и монстр ушел дальше 4.5м
            if (Time.time >= openTime + 4.0f && dist >= Mathf.Max(doorCloseDistance, 4.5f))
            {
                door.TryOpenDoor(transform.position);
                openedDoors.RemoveAt(i);
                openedDoorTimes.Remove(door);
            }
        }
    }

    /// <summary>
    /// Автоматическое отталкивание от стен и перил для центрирования в коридорах и на лестницах
    /// </summary>
    private void ApplyWallRepulsion()
    {
        if (!useWallRepulsion || agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped) return;

        Vector3 eyePos = transform.position + Vector3.up * 0.9f;
        Vector3 rightDir = transform.right;
        Vector3 leftDir = -transform.right;

        Vector3 pushDir = Vector3.zero;

        // Бросаем лучи влево и вправо
        if (Physics.Raycast(eyePos, rightDir, out RaycastHit hitRight, minWallDistance, obstacleMask))
        {
            float pushFactor = 1f - (hitRight.distance / minWallDistance);
            pushDir += -rightDir * pushFactor * wallRepulsionForce;
        }

        if (Physics.Raycast(eyePos, leftDir, out RaycastHit hitLeft, minWallDistance, obstacleMask))
        {
            float pushFactor = 1f - (hitLeft.distance / minWallDistance);
            pushDir += rightDir * pushFactor * wallRepulsionForce;
        }

        if (pushDir.sqrMagnitude > 0.01f)
        {
            agent.Move(pushDir * Time.deltaTime);
        }
    }

    /// <summary>
    /// Включает/выключает визуал и коллайдеры самого монстра
    /// </summary>
    private void SetPhysicalActive(bool active)
    {
        if (agent != null) agent.enabled = active;

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = active;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            if (rend != null) rend.enabled = active;
        }
    }

    // ── Gizmos в редакторе (как в EnemyAI) ──────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.6f;

        // 1. Зрение — жёлтая сфера и лучи FOV
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Vector3 fovL = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward * visionRange;
        Vector3 fovR = Quaternion.Euler(0, fovAngle * 0.5f, 0) * transform.forward * visionRange;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(eyePos, fovL);
        Gizmos.DrawRay(eyePos, fovR);

        // 2. Периферия (360°) — белый
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, peripheralRange);

        // 3. Радиус взаимодействия с дверями — зелёный
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, doorInteractionRadius);

        // 4. Лучи отталкивания от стен — циан
        if (useWallRepulsion)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.right * minWallDistance);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, -transform.right * minWallDistance);
        }

        // 5. Зона скримера/атаки — красный
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.8f);

        // Точка камеры скримера и вектор взгляда на голову
        if (jumpscareCameraPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(jumpscareCameraPoint.position, 0.25f);

            Vector3 headPos = headPoint != null ? headPoint.position : (transform.position + Vector3.up * 1.6f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(jumpscareCameraPoint.position, headPos);
        }
        if (headPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(headPoint.position, 0.2f);
        }

        // 6. Точки патрулирования — синие сферы и линии
        if (patrolPoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (var pt in patrolPoints)
            {
                if (pt != null)
                {
                    Gizmos.DrawSphere(pt.position, 0.4f);
                    Gizmos.DrawLine(transform.position, pt.position);
                }
            }
        }
    }
#endif
}
