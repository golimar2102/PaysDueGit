using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public partial class EnemyAI : MonoBehaviour
{
    public enum NPCPersonality { Friendly, Neutral, Aggressive, Scared, Submissive }
    public enum NPCState { Idle, Roam, Investigate, Follow, KeepAway, Chase, Attack, Flee, Dead, Stunned, Enslaved }
    public enum AlertLevel { Unaware, Suspicious, Alert, Detected }
    public enum NPCGender { Male, Female }
    public enum NPCEscapeState { None, GoingToDoor, PickingLock, GoingToTeleport, GoingToFinalExit }

    [Header("Личность")]
    public NPCPersonality personality = NPCPersonality.Aggressive;
    public NPCState currentState = NPCState.Roam;
    public AlertLevel alertLevel  = AlertLevel.Unaware;

    [Header("Отладка")]
    public bool disableAI = false;
    [Tooltip("Показывать текущий стейт и тревогу прямо в Инспекторе")]
    [SerializeField] private string _debugInfo = "";

    [Header("Ссылки")]
    public Transform player;
    public Animator  animator;
    [Tooltip("Пустой дочерний объект на уровне глаз (авто-создаётся если пусто)")]
    public Transform eyePoint;

    [Header("Статы")]
    public float maxHealth = 150f;
    public float currentHealth;
    public float damage     = 25f;

    [Header("Гендер и Размножение")]
    public NPCGender gender = NPCGender.Male;
    public bool StandartNPC = false;

    [Header("Статы StandartNPC")]
    public float hunger = 100f; // 0 to 100
    public float lust = 0f;
    public float maxStunLevel = 100f;
    public float currentStunLevel = 0f;
    public float stunRecoveryRate = 5f;

    [Header("Рабство и Побег (Внутреннее)")]
    public SlaveryRoomTrigger currentSlaveryRoom;
    public NPCEscapeState escapeState = NPCEscapeState.None;
    [HideInInspector] public bool isBackgroundEscaping = false;
    [HideInInspector] public float backgroundEscapeStartTime = 0f;
    private float escapeTimer = 0f;
    private float escapeCheckTimer = 0f;
    private int lockPickAttempts = 0;

    private int originalColliderDirection = 1;
    private Vector3 originalColliderCenter = new Vector3(0f, 0.9f, 0f);
    private float originalColliderHeight = 1.8f;
    private float originalColliderRadius = 0.4f;

    [Header("Выносливость")]
    [Tooltip("Максимальный запас энергии")]
    public float maxStamina = 100f;
    [Tooltip("Скорость восстановления энергии в секунду (когда не бежит)")]
    public float staminaRegenRate = 12f;
    [Tooltip("Задержка регена после траты (секунды)")]
    public float staminaRegenDelay = 2f;
    [Tooltip("Расход при беге (за секунду)")]
    public float runStaminaDrain = 8f;
    [Tooltip("Расход при каждом ударе")]
    public float attackStaminaDrain = 20f;
    [Tooltip("Расход при прыжке НПС (если есть)")]
    public float jumpStaminaDrain = 15f;

    [Header("Скорости")]
    public float walkSpeed  = 2.2f;
    public float sneakSpeed = 1.2f;
    public float runSpeed   = 5.8f;

    [Header("Зрение")]
    [Tooltip("Дальность прямого зрения")]
    public float visionRange   = 15f;
    [Tooltip("Угол конуса зрения")]
    [Range(30f, 360f)] public float fovAngle = 110f;
    [Tooltip("Ближняя зона (360°, игнорирует стены — чувствует присутствие)")]
    public float peripheralRange = 2.5f;
    [Tooltip("Маска слоёв-препятствий (стены, мебель)")]
    public LayerMask obstacleMask;

    [Header("Слух")]
    [Tooltip("Радиус слуха — реагирует на игрока без прямой видимости")]
    public float hearingRange = 7f;

    [Header("Тревога")]
    [Tooltip("Сколько секунд нужно смотреть на игрока чтобы засечь")]
    public float suspicionToDetectTime = 1.5f;
    [Tooltip("Как долго помнит последнюю позицию после потери")]
    public float memoryDuration = 10f;

    [Header("Дистанции")]
    public float attackRange        = 2.5f;
    public float neutralPersonalSpace = 5f;
    public float neutralAlertRange  = 9f;
    public float roamRadius         = 20f;
    [MinAttribute(0.5f)] public float idlePauseMin = 2f;
    [MinAttribute(1f)]   public float idlePauseMax = 6f;

    [Header("Патрулирование и точки интереса")]
    [Tooltip("Список точек интереса для патрулирования. Если пустой, то ищет объекты с тегом 'POI'.")]
    public List<Transform> patrolPoints = new List<Transform>();
    [Tooltip("Автоматически искать на сцене объекты с этим тегом, если список patrolPoints пуст")]
    public string poiTag = "POI";
    [Tooltip("Минимальная дистанция новой точки от текущей, чтобы ИИ не ходил туда-сюда на одном месте")]
    public float minRoamDistance = 8f;
    [Tooltip("Центрировать случайное блуждание вокруг начальной точки спавна (true) или вокруг текущего положения (false)")]
    public bool roamAroundStart = true;

    [Header("Взаимодействие с дверями")]
    [Tooltip("Радиус обнаружения дверей для автоматического открытия")]
    public float doorInteractionRadius = 1.5f;
    [Tooltip("Дистанция, на которую ИИ должен отойти от открытой им двери, чтобы закрыть её за собой")]
    public float doorCloseDistance = 2.8f;

    [Header("Submissive — следование")]
    public float followDistance  = 3f;
    public float followMaxRange  = 25f;

    [Header("Живость")]
    [Tooltip("Интервал случайных действий в покое (осмотреться и т.д.)")]
    public float idleActionInterval = 9f;

    [Header("Группировка")]
    [Tooltip("Идентификатор группы/фракции. Члены одной группы не нападают друг на друга по умолчанию.")]
    public int factionID = 0;

    public enum FriendlyFireReaction { Ignore, FightBack, Flee }
    [Tooltip("Реакция на урон от члена своей же группы")]
    public FriendlyFireReaction friendlyFireReaction = FriendlyFireReaction.FightBack;

    [Header("Текущая цель (Отладка/Внутреннее)")]
    [Tooltip("Текущая цель ИИ (Игрок или другой НПС)")]
    public Transform currentTarget;

    public static List<EnemyAI> allNPCs = new List<EnemyAI>();

    // ── Private ──────────────────────────────────────────────────────────────
    private NavMeshAgent agent;
    private PlayerStats  playerStats;
    private EnemyAI      targetNPC;
    private Transform    lastAttacker;
    private float        lastAttackedTime;
    private bool         targetVisible;
    private bool         targetHeard;
    private float        stuckTimer = 0f;
    private float        nextPathResetTime = 0f;

    private bool  playerVisible;
    private bool  playerHeard;
    private Vector3 lastKnownPos;
    private float   lastSeenTime  = -9999f;
    private float   suspicionTimer;
    private float   visionTimer;

    private float nextAttackTime;
    private float nextRoamTime;
    private float nextIdleActionTime;
    private float nextDoorCheckTime;

    private bool isFollowing;
    private bool reactionPlaying;
    private bool wasVisible;

    private Coroutine investigateCo;
    private Coroutine fleeCo;
    private KeyCode   cachedFollowKey;

    // Стамина (рантайм)
    [Header("Показатели рантайма")]
    [Tooltip("Текущий уровень выносливости ИИ")]
    public float currentStamina;
    private float staminaRegenTimer;
    private bool  isExhausted;          // true когда стамина = 0
    private float fleeRefreshTimer;     // чтобы не звать NavMesh каждый кадр при побеге
    
    private Vector3 startPosition;
    private bool isRoamWaiting;
    private Transform lastVisitedPoint;
    private List<DoorController> openedDoors = new List<DoorController>();

    private static bool showAllHP;

    // Animator hashes
    private static readonly int H_Speed       = Animator.StringToHash("Speed");
    private static readonly int H_AttackIdx   = Animator.StringToHash("AttackIndex");
    private static readonly int H_Attack      = Animator.StringToHash("Attack");
    private static readonly int H_Alert       = Animator.StringToHash("AlertLevel");
    private static readonly int H_IsFollowing = Animator.StringToHash("IsFollowing");
    private static readonly int H_LookAround  = Animator.StringToHash("LookAround");
    private static readonly int H_IsHitted    = Animator.StringToHash("IsHitted");
    private static readonly int H_IsStunned   = Animator.StringToHash("IsStunned");

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        agent         = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
        startPosition = transform.position;

        // Поиск точек интереса на сцене по тегу, если список пуст
        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            if (!string.IsNullOrEmpty(poiTag))
            {
                GameObject[] goPoints = GameObject.FindGameObjectsWithTag(poiTag);
                foreach (var go in goPoints)
                {
                    patrolPoints.Add(go.transform);
                }
            }
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Авто-создаём точку глаз если не задана
        if (eyePoint == null)
        {
            var go = new GameObject("EyePoint");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.up * 1.65f;
            eyePoint = go.transform;
        }

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { player = p.transform; playerStats = p.GetComponent<PlayerStats>(); }
        }
        else
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        agent.speed           = walkSpeed;
        agent.acceleration    = 28f;
        agent.angularSpeed    = 360f;
        agent.stoppingDistance = 0.3f;
        agent.updateRotation  = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = 50 + (gameObject.GetInstanceID() % 30);

        cachedFollowKey    = (KeyCode)PlayerPrefs.GetInt("Key_Toggle", (int)KeyCode.F);
        nextIdleActionTime = Time.time + Random.Range(3f, idleActionInterval);
        currentStamina     = maxStamina;
        InitializeAge();
    }

    void OnEnable()
    {
        if (!allNPCs.Contains(this)) allNPCs.Add(this);
        SettingsManager.OnSettingsSaved += RefreshKeyBindings;
        CheckBackgroundEscapeResume();
    }

    void OnDisable()
    {
        if (allNPCs.Contains(this)) allNPCs.Remove(this);
        SettingsManager.OnSettingsSaved -= RefreshKeyBindings;
    }

    void Update()
    {
        if (disableAI || currentState == NPCState.Dead || currentState == NPCState.Stunned)
        {
            if (currentState == NPCState.Stunned)
            {
                UpdateStunState();
            }
            if (disableAI) StopAgent();
            return;
        }
        if (player == null) return;

        UpdateSlaveStatus();

        // Bypassing AI during dialogue
        bool inDialogue = DialogueManager.Instance != null && 
                          DialogueManager.Instance.isDialogueActive && 
                          DialogueManager.Instance.CurrentNPC == GetComponent<NPCDialogue>();
        if (inDialogue)
        {
            StopAgent();
            Vector3 playerDir = player.position - transform.position;
            playerDir.y = 0f;
            if (playerDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(playerDir.normalized), Time.deltaTime * 10f);
            }
            return;
        }

        if (currentTarget != null && IsTargetDead())
        {
            ResetTarget();
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.P)) showAllHP = !showAllHP;
        if (currentState == NPCState.Enslaved)
        {
            _debugInfo = $"{personality} | {currentState} (Escape: {escapeState}) | {alertLevel}";
        }
        else
        {
            _debugInfo = $"{personality} | {currentState} | {alertLevel}";
        }
#endif
        // Vision throttle (every 0.15s)
        visionTimer -= Time.deltaTime;
        if (visionTimer <= 0f) { visionTimer = 0.15f; UpdateVision(); }

        UpdateAlertLevel();
        UpdateStamina();

        // Dynamic avoidance priority
        if (agent.enabled && agent.isOnNavMesh)
        {
            if (agent.isStopped || currentState == NPCState.Idle || currentState == NPCState.Attack)
            {
                agent.avoidancePriority = 99;
            }
            else
            {
                agent.avoidancePriority = 50 + (gameObject.GetInstanceID() % 30);
            }
        }

        // Stuck detection
        if (agent.enabled && agent.isOnNavMesh && agent.hasPath && !agent.isStopped)
        {
            if (agent.velocity.magnitude < 0.15f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 0.8f)
                {
                    EnemyAI blockingNPC = FindBlockingNPC();
                    if (blockingNPC != null)
                    {
                        NudgePathAround(blockingNPC);
                    }
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (personality == NPCPersonality.Submissive) HandleFollowInput();

        DoIdleActions();

        if (currentState != NPCState.Roam && currentState != NPCState.Enslaved)
        {
            isRoamWaiting = false;
        }

        // Авто-открытие дверей поблизости
        if (Time.time >= nextDoorCheckTime && currentState != NPCState.Dead)
        {
            nextDoorCheckTime = Time.time + 0.25f;
            CheckAndOpenNearbyDoors();
        }

        float dist;
        if (personality == NPCPersonality.Submissive)
        {
            dist = Vector3.Distance(transform.position, player.position);
        }
        else
        {
            dist = currentTarget != null ? Vector3.Distance(transform.position, currentTarget.position) : 0f;
        }

        if (currentState == NPCState.Enslaved)
        {
            BehaviourEnslaved(dist);
        }
        else
        {
            switch (personality)
            {
                case NPCPersonality.Friendly:   BehaviourFriendly();       break;
                case NPCPersonality.Neutral:    BehaviourNeutral(dist);    break;
                case NPCPersonality.Aggressive: BehaviourAggressive(dist); break;
                case NPCPersonality.Scared:     BehaviourScared(dist);     break;
                case NPCPersonality.Submissive: BehaviourSubmissive(dist); break;
            }
        }

        if (animator != null)
        {
            animator.SetFloat(H_Speed, agent.velocity.magnitude);
            animator.SetInteger(H_Alert, (int)alertLevel);
        }

        wasVisible = targetVisible;
    }

    // ── Stamina ───────────────────────────────────────────────────────────────
    private void UpdateStamina()
    {
        float speedThreshold = (walkSpeed + runSpeed) * 0.5f;
        bool isRunning = agent.velocity.magnitude > speedThreshold;

        if (isRunning)
        {
            currentStamina     -= runStaminaDrain * Time.deltaTime;
            staminaRegenTimer   = staminaRegenDelay;
            if (currentStamina <= 0f) { currentStamina = 0f; isExhausted = true; }
        }
        else
        {
            staminaRegenTimer -= Time.deltaTime;
            if (staminaRegenTimer <= 0f)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina >= maxStamina) { currentStamina = maxStamina; isExhausted = false; }
            }
        }
    }

    /// <summary>Вернуть максимальную скорость с учётом стамины.</summary>
    private float RunSpeed() => isExhausted ? walkSpeed : runSpeed;

    /// <summary>Потратить стамину на действие. Возвращает false если недостаточно.</summary>
    private bool UseStamina(float cost)
    {
        if (currentStamina < cost) return false;
        currentStamina   -= cost;
        staminaRegenTimer = staminaRegenDelay;
        if (currentStamina <= 0f) { currentStamina = 0f; isExhausted = true; }
        return true;
    }

    /// <summary>Вызвать снаружи если НПС прыгает.</summary>
    public void OnNPCJump() => UseStamina(jumpStaminaDrain);





    private void FaceToward(Vector3 target)
    {
        Vector3 dir = (target - transform.position); dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 8f);
    }



    private void StopAgent()
    {
        if (agent != null && agent.isOnNavMesh && !agent.isStopped) agent.isStopped = true;
        if (animator != null) animator.SetFloat(H_Speed, 0f);
    }



    // ── Public API ────────────────────────────────────────────────────────────
    public void TakeDamage(float amount, Transform attacker = null)
    {
        if (currentState == NPCState.Dead)
        {
            var fx = GetComponent<EnemyDeathEffects>();
            if (fx != null && fx.CanBeGibbed())
            {
                fx.DamageCorpse(amount);
            }
            return;
        }

        if (currentState == NPCState.Enslaved)
        {
            currentState = NPCState.Roam;
            escapeState = NPCEscapeState.None;
            currentSlaveryRoom = null;
            agent.isStopped = false;
        }
        currentHealth -= amount;
        if (StandartNPC && age < 18 && currentHealth < 1f)
        {
            currentHealth = 1f; // Детей нельзя убить атакой
        }
        if (currentHealth <= 0) { Die(); return; }

        if (animator != null)
        {
            animator.SetTrigger(H_IsHitted);
        }

        if (attacker != null)
        {
            lastAttacker = attacker;
            lastAttackedTime = Time.time;

            bool isPlayerAttacker = attacker.CompareTag("Player") || attacker.GetComponent<PlayerStats>() != null;

            if (isPlayerAttacker)
            {
                if (personality != NPCPersonality.Aggressive)
                {
                    if (fleeCo != null) StopCoroutine(fleeCo);
                    fleeCo = StartCoroutine(TemporaryFlee(6f, attacker));
                }
                else
                {
                    alertLevel = AlertLevel.Detected;
                    lastKnownPos = attacker.position;
                    lastSeenTime = Time.time;
                    currentTarget = attacker;
                    targetNPC = null;
                }
            }
            else
            {
                EnemyAI attackerNPC = attacker.GetComponent<EnemyAI>();
                if (attackerNPC != null)
                {
                    bool sameFaction = (attackerNPC.factionID == this.factionID);
                    if (sameFaction)
                    {
                        if (friendlyFireReaction == FriendlyFireReaction.FightBack)
                        {
                            alertLevel = AlertLevel.Detected;
                            lastKnownPos = attacker.position;
                            lastSeenTime = Time.time;
                            currentTarget = attacker;
                            targetNPC = attackerNPC;
                        }
                        else if (friendlyFireReaction == FriendlyFireReaction.Flee)
                        {
                            if (fleeCo != null) StopCoroutine(fleeCo);
                            fleeCo = StartCoroutine(TemporaryFlee(6f, attacker));
                        }
                    }
                    else
                    {
                        if (personality != NPCPersonality.Aggressive)
                        {
                            if (fleeCo != null) StopCoroutine(fleeCo);
                            fleeCo = StartCoroutine(TemporaryFlee(6f, attacker));
                        }
                        else
                        {
                            alertLevel = AlertLevel.Detected;
                            lastKnownPos = attacker.position;
                            lastSeenTime = Time.time;
                            currentTarget = attacker;
                            targetNPC = attackerNPC;
                        }
                    }
                }
            }
        }
        else
        {
            if (personality != NPCPersonality.Aggressive)
            {
                if (fleeCo != null) StopCoroutine(fleeCo);
                fleeCo = StartCoroutine(TemporaryFlee(6f, null));
            }
            else
            {
                alertLevel = AlertLevel.Detected;
            }
        }
    }

    public bool CanDamageCorpse()
    {
        if (currentState != NPCState.Dead) return false;
        var fx = GetComponent<EnemyDeathEffects>();
        return fx != null && fx.CanBeGibbed();
    }



    private EnemyAI FindBlockingNPC()
    {
        Vector3 forward = transform.forward;
        foreach (var npc in allNPCs)
        {
            if (npc == null || npc == this || npc.currentState == NPCState.Dead) continue;
            
            Vector3 toNPC = npc.transform.position - transform.position;
            float dist = toNPC.magnitude;
            
            if (dist <= 2.5f && Vector3.Angle(forward, toNPC) < 45f)
            {
                return npc;
            }
        }
        return null;
    }

    private void NudgePathAround(EnemyAI blockingNPC)
    {
        Vector3 toBlocking = (blockingNPC.transform.position - transform.position).normalized;
        Vector3 rightDir = new Vector3(-toBlocking.z, 0f, toBlocking.x);
        
        float sideSign = Random.value < 0.5f ? 1f : -1f;
        Vector3 nudgeOffset = rightDir * (blockingNPC.agent.radius + agent.radius + 1.2f) * sideSign;
        Vector3 nudgePos = blockingNPC.transform.position + nudgeOffset;
        
        if (NavMesh.SamplePosition(nudgePos, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            nextPathResetTime = Time.time + 1.2f;
        }
    }

    private void ResetTarget()
    {
        currentTarget = null;
        targetNPC = null;
        alertLevel = AlertLevel.Unaware;
        suspicionTimer = 0f;
        currentState = NPCState.Roam;
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
    }



    public void SetFollowing(bool v)
    {
        if (personality != NPCPersonality.Submissive) return;
        isFollowing = v;
        if (animator != null) animator.SetBool(H_IsFollowing, v);
    }

    public void SetPersonality(NPCPersonality p)
    {
        personality = p; isFollowing = false; alertLevel = AlertLevel.Unaware;
        currentState = NPCState.Roam;
    }

    public void RefreshKeyBindings()
    {
        cachedFollowKey = (KeyCode)PlayerPrefs.GetInt("Key_Toggle", (int)KeyCode.F);
    }



    private void Die()
    {
        currentState = NPCState.Dead; isFollowing = false;
        if (allNPCs.Contains(this)) allNPCs.Remove(this);
        if (investigateCo != null) StopCoroutine(investigateCo);
        if (fleeCo != null) StopCoroutine(fleeCo);
        if (agent.enabled) agent.enabled = false;

        // Отключаем диалог и моргание глаз при смерти
        var dialogue = GetComponent<NPCDialogue>();
        if (dialogue == null) dialogue = GetComponentInChildren<NPCDialogue>();
        if (dialogue != null) dialogue.enabled = false;

        var blink = GetComponent<SkinnedBlinkController>();
        if (blink == null) blink = GetComponentInChildren<SkinnedBlinkController>();
        if (blink != null)
        {
            blink.StopAllCoroutines();
            blink.enabled = false;
        }

        var fx = GetComponent<EnemyDeathEffects>();
        if (fx != null)
        {
            fx.TriggerDeathEffects(animator);
        }
        else
        {
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Destroy(gameObject, 3f);
        }
    }

    // ── Stun & Enslavement Implementation ──────────────────────────────────────
    public void TakeStunDamage(float amount, Transform attacker = null)
    {
        if (currentState == NPCState.Dead || currentState == NPCState.Stunned) return;
        if (!StandartNPC) return;

        currentStunLevel += amount;
        if (animator != null)
        {
            animator.SetTrigger(H_IsHitted);
        }

        if (currentStunLevel >= maxStunLevel)
        {
            currentStunLevel = maxStunLevel;
            Stun();
            return;
        }

        // Реакция на удар: либо убегает, либо дерется
        if (attacker != null)
        {
            lastAttacker = attacker;
            lastAttackedTime = Time.time;

            bool isPlayerAttacker = attacker.CompareTag("Player") || attacker.GetComponent<PlayerStats>() != null;
            if (isPlayerAttacker)
            {
                if (currentState == NPCState.Enslaved)
                {
                    // Выходим из состояния рабства, если атакованы
                    currentState = NPCState.Roam;
                    escapeState = NPCEscapeState.None;
                    currentSlaveryRoom = null;
                    agent.isStopped = false;
                }

                // Выбираем реакцию: Scared или Aggressive
                if (personality != NPCPersonality.Scared && personality != NPCPersonality.Aggressive)
                {
                    personality = Random.value < 0.5f ? NPCPersonality.Scared : NPCPersonality.Aggressive;
                }

                if (personality == NPCPersonality.Scared)
                {
                    if (fleeCo != null) StopCoroutine(fleeCo);
                    fleeCo = StartCoroutine(TemporaryFlee(6f, attacker));
                }
                else
                {
                    alertLevel = AlertLevel.Detected;
                    lastKnownPos = attacker.position;
                    lastSeenTime = Time.time;
                    currentTarget = attacker;
                    targetNPC = null;
                }
            }
        }
    }

    public void Stun()
    {
        currentState = NPCState.Stunned;
        isFollowing = false;
        escapeState = NPCEscapeState.None; // Abort escape on stun
        if (investigateCo != null) StopCoroutine(investigateCo);
        if (fleeCo != null) StopCoroutine(fleeCo);

        // Отключаем NavMeshAgent
        if (agent.enabled) agent.enabled = false;

        // Включаем анимацию стана
        if (animator != null)
        {
            animator.SetBool(H_IsStunned, true);
        }

        // Делаем коллайдеры триггерами (как при смерти)
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            originalColliderDirection = capsule.direction;
            originalColliderCenter = capsule.center;
            originalColliderHeight = capsule.height;
            originalColliderRadius = capsule.radius;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != null) col.isTrigger = true;
        }

        // Добавляем NPCCorpse для подбора
        var corpseComponent = GetComponent<NPCCorpse>();
        if (corpseComponent == null)
        {
            corpseComponent = gameObject.AddComponent<NPCCorpse>();
        }
        corpseComponent.InitializeCorpse();

        // Disable NPCDialogue
        var dialogue = GetComponent<NPCDialogue>();
        if (dialogue == null) dialogue = GetComponentInChildren<NPCDialogue>();
        if (dialogue != null) dialogue.enabled = false;

        Debug.Log($"[{name}] Оглушен!");
    }

    private void UpdateStunState()
    {
        if (!StandartNPC) return;

        var corpse = GetComponent<NPCCorpse>();
        bool isCarried = corpse != null && NPCCorpse.carriedCorpse == corpse;
        bool isInteracted = corpse != null && (corpse.currentTable != null || corpse.currentGrinder != null);

        if (!isCarried && !isInteracted)
        {
            currentStunLevel -= stunRecoveryRate * Time.deltaTime;
            if (currentStunLevel <= 0f)
            {
                currentStunLevel = 0f;
                WakeUp();
            }
        }
    }

    public void WakeUp()
    {
        currentState = NPCState.Roam;
        
        // Remove or disable NPCCorpse
        var corpse = GetComponent<NPCCorpse>();
        if (corpse != null)
        {
            corpse.SetHighlight(false);
            Destroy(corpse);
        }

        // Re-enable NavMeshAgent
        if (agent != null)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(transform.position);
            }
        }

        // Обновляем startPosition на текущую точку пробуждения
        startPosition = transform.position;

        // Reset Rigidbody physics
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Enable animator
        if (animator != null)
        {
            animator.SetBool(H_IsStunned, false);
            animator.Rebind();
        }

        // Restore capsule collider properties
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.direction = originalColliderDirection;
            capsule.center = originalColliderCenter;
            capsule.height = originalColliderHeight;
            capsule.radius = originalColliderRadius;
        }

        // Restore colliders
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col != null) col.isTrigger = false;
        }

        // Reset personality/state to Neutral or Aggressive
        if (personality != NPCPersonality.Aggressive && personality != NPCPersonality.Neutral)
        {
            personality = Random.value < 0.5f ? NPCPersonality.Neutral : NPCPersonality.Aggressive;
        }
        
        alertLevel = AlertLevel.Unaware;
        ResetTarget();
        isRoamWaiting = false;

        // Проверяем, проснулся ли НПС внутри комнаты рабства (SlaveryRoomTrigger)
        bool foundRoom = false;
        Physics.SyncTransforms();

        if (currentSlaveryRoom != null)
        {
            Collider roomCollider = currentSlaveryRoom.GetComponent<Collider>();
            if (roomCollider != null && roomCollider.bounds.Contains(transform.position))
            {
                TransitionToEnslaved(currentSlaveryRoom);
                foundRoom = true;
            }
        }

        if (!foundRoom && capsule != null)
        {
            Collider[] overlapped = Physics.OverlapCapsule(
                transform.position + Vector3.up * capsule.radius,
                transform.position + Vector3.up * (capsule.height - capsule.radius),
                capsule.radius,
                ~0,
                QueryTriggerInteraction.Collide
            );
            foreach (var col in overlapped)
            {
                var room = col.GetComponent<SlaveryRoomTrigger>();
                if (room != null)
                {
                    TransitionToEnslaved(room);
                    foundRoom = true;
                    break;
                }
            }
        }
        if (!foundRoom)
        {
            currentSlaveryRoom = null;
        }

        // Enable NPCDialogue
        var dialogue = GetComponent<NPCDialogue>();
        if (dialogue == null) dialogue = GetComponentInChildren<NPCDialogue>();
        if (dialogue != null) dialogue.enabled = true;

        Debug.Log($"[{name}] Проснулся после оглушения!");
    }



    // ── Debug ─────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showAllHP || Camera.main == null || currentState == NPCState.Dead) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
        if (sp.z <= 0) return;
        var style = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = personality == NPCPersonality.Aggressive ? Color.red : Color.cyan;
        string lbl = $"{personality}\n❤{currentHealth:F0} | {alertLevel}";
        if (personality == NPCPersonality.Submissive) lbl += isFollowing ? "\n[Следует]" : "\n[Свободен]";
        GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 50, 120, 70), lbl, style);
    }

    private void OnDrawGizmosSelected()
    {
        // Зрение — жёлтый конус
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // Периферия — белый
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, peripheralRange);

        // Слух — синий
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, hearingRange);

        // Атака — красный
        if (personality == NPCPersonality.Aggressive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        // FOV лучи
        Vector3 fovL = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward * visionRange;
        Vector3 fovR = Quaternion.Euler(0,  fovAngle * 0.5f, 0) * transform.forward * visionRange;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, fovL);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, fovR);

        // Нейтральные зоны
        if (personality == NPCPersonality.Neutral)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, neutralPersonalSpace);
            Gizmos.color = new Color(0f, 0.4f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, neutralAlertRange);
        }

        // Submissive
        if (personality == NPCPersonality.Submissive)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, followDistance);
            Gizmos.color = new Color(0f, 0.8f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, followMaxRange);
        }

        // Линия к последней известной позиции
        if (Application.isPlaying && alertLevel >= AlertLevel.Alert)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, lastKnownPos);
            Gizmos.DrawSphere(lastKnownPos, 0.3f);
        }
    }
#endif
}