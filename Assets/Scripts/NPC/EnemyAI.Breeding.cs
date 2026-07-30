using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public partial class EnemyAI : MonoBehaviour
{
    [Header("Настройки Воспитания и Возраста")]
    [Tooltip("Возраст НПС (лет)")]
    public int age = 18;
    [Tooltip("Сколько игровых дней длится 1 год возраста")]
    public float daysPerAgeYear = 7f;
    [Tooltip("Сколько игровых дней НПС может прожить при 0 сытости до смерти")]
    public float starvationGraceDays = 2.5f;
    [Tooltip("Расход сытости в день (при 100 maxHunger)")]
    public float hungerDepletionPerDay = 25f;

    [Header("Размножение")]
    public bool isReadyToBreed = false;
    public float breedCooldown = 30f;
    [Tooltip("Длительность размножения в секундах до рождения ребенка (настраивается в Инспекторе)")]
    public float breedingDuration = 2.5f;
    public float breedApproachDistance = 1.8f;
    public ParticleSystem heartParticlePrefab;
    [HideInInspector] public EnemyAI breedPartner = null;
    [HideInInspector] public bool isBreedingInProgress = false;

    private float breedCooldownTimer = 0f;
    private float starvationTimer = 0f;
    private float ageTimer = 0f;
    private ParticleSystem activeLoveParticles;
    private float breedingTimer = 0f;
    private bool childSpawned = false;

    public void InitializeAge()
    {
        if (StandartNPC)
        {
            if (age <= 0)
            {
                // Для новорожденных детей возраст 0 задается при спавне
            }
            else if (age == 18 && ageTimer == 0f)
            {
                // Припервой инициализации взрослого НПС выставляем рандом от 18 до 60
                age = Random.Range(18, 61);
            }

            if (age < 18)
            {
                transform.localScale = Vector3.one * 0.6f;
            }
        }
    }

    public void UpdateSlaveStatus()
    {
        if (currentState != NPCState.Enslaved) return;

        if (StandartNPC)
        {
            // Расчет прошедших игровых дней
            float daysPassed = 0f;
            if (DayNightCycle.Instance != null)
            {
                float hoursPassed = Time.deltaTime * DayNightCycle.Instance.timeMultiplier;
                daysPassed = hoursPassed / 24f;
            }
            else
            {
                daysPassed = Time.deltaTime / 120f; // Резервный таймер (2 мин = 1 день)
            }

            // 1. Убывание сытости
            if (hunger > 0f)
            {
                hunger -= hungerDepletionPerDay * daysPassed;
                if (hunger < 0f) hunger = 0f;
                starvationTimer = 0f;
            }
            else
            {
                // Голодание при 0 сытости
                starvationTimer += daysPassed;
                if (starvationTimer >= starvationGraceDays)
                {
                    Debug.Log($"[{name}] Умер от истощения (голод = 0 в течение {starvationGraceDays} дней)!");
                    Die();
                    return;
                }
            }

            // 2. Система старения
            ageTimer += daysPassed;
            if (ageTimer >= daysPerAgeYear)
            {
                ageTimer -= daysPerAgeYear;
                age++;
                Debug.Log($"[{name}] Отпраздновал день рождения! Новый возраст: {age} лет.");

                if (age >= 18)
                {
                    // Взросление: восстановление нормального размера
                    transform.localScale = Vector3.one;
                }
            }
        }

        // 3. Обновление поведения размножения при активном сближении (для всех раненных/пленных НПС!)
        if (isBreedingInProgress)
        {
            UpdateBreedingBehaviour();
        }
    }

    public bool TryFeed(InventoryItemData itemData)
    {
        if (currentState != NPCState.Enslaved || currentState == NPCState.Dead) return false;

        bool isFood = (itemData.category == ItemCategory.Food);
        bool isBiomass = (itemData.isConsumable && itemData.currentLiquidType == LiquidType.Biomass && itemData.currentAmount > 0);

        if (!isFood && !isBiomass) return false;

        // Включаем стандартный статус НПС при кормлении
        StandartNPC = true;

        // Восстанавливаем сытость
        hunger = Mathf.Min(100f, hunger + 50f);
        starvationTimer = 0f;

        Debug.Log($"[{name}] Покормлен! Сытость: {hunger}%");

        // Если взрослый, не готов к размножению и не на кулдауне -> переходит в готовность
        if (age >= 18 && !isReadyToBreed && Time.time >= breedCooldownTimer && !isBreedingInProgress)
        {
            isReadyToBreed = true;
            SpawnLoveParticles();
            
            if (currentSlaveryRoom != null)
            {
                currentSlaveryRoom.CheckRoomForBreedingPairs();
            }
        }

        return true;
    }

    public void StartBreedingSequence(EnemyAI partner)
    {
        breedPartner = partner;
        isBreedingInProgress = true;
        breedingTimer = 0f;
        childSpawned = false;

        // Игнорируем столкновения физики между родителями, чтобы они не толкались в стены!
        SetPhysicsIgnoreWithPartner(partner, true);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = walkSpeed;
        }
    }

    private void SetPhysicsIgnoreWithPartner(EnemyAI partner, bool ignore)
    {
        if (partner == null) return;
        Collider[] myCols = GetComponentsInChildren<Collider>();
        Collider[] partnerCols = partner.GetComponentsInChildren<Collider>();

        foreach (var c1 in myCols)
        {
            if (c1 == null) continue;
            foreach (var c2 in partnerCols)
            {
                if (c2 == null) continue;
                Physics.IgnoreCollision(c1, c2, ignore);
            }
        }
    }

    private void UpdateBreedingBehaviour()
    {
        if (breedPartner == null || breedPartner.currentState == NPCState.Dead)
        {
            ResetBreedingState();
            return;
        }

        float dist = Vector3.Distance(transform.position, breedPartner.transform.position);
        float stopDistance = Mathf.Max(breedApproachDistance, 2.5f);

        if (dist > stopDistance)
        {
            // Шагаем навстречу партнеру к средней точке
            Vector3 midPoint = (transform.position + breedPartner.transform.position) * 0.5f;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Выключаем уклонение Навмеша!
                agent.stoppingDistance = 0.5f;
                agent.SetDestination(midPoint);
            }
        }
        else
        {
            // Достигли позиции свидания - принудительно останавливаем
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            FaceToward(breedPartner.transform.position);
            breedingTimer += Time.deltaTime;

            if (activeLoveParticles == null)
            {
                SpawnLoveParticles();
            }

            if (breedingTimer >= breedingDuration && !childSpawned)
            {
                childSpawned = true;

                // Только один из родителей спавнит ребенка
                if (gender == NPCGender.Female || (gender == breedPartner.gender && GetInstanceID() < breedPartner.GetInstanceID()))
                {
                    SpawnChildNPC(breedPartner);
                }

                ResetBreedingState();
            }
        }
    }

    private void SpawnLoveParticles()
    {
        if (heartParticlePrefab != null && activeLoveParticles == null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.8f;
            activeLoveParticles = Instantiate(heartParticlePrefab, spawnPos, Quaternion.Euler(-90f, 0f, 0f), transform);
            Destroy(activeLoveParticles.gameObject, 5f);
        }
    }

    private void SpawnChildNPC(EnemyAI partner)
    {
        Vector3 midPoint = (transform.position + partner.transform.position) * 0.5f;
        Vector3 spawnPos = midPoint;

        // Гарантируем, что ребёнок спавнится на свободной поверхности NavMesh, а не в стене
        if (NavMesh.SamplePosition(midPoint, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject parentTemplate = (Random.value < 0.5f) ? gameObject : partner.gameObject;
        GameObject childGO = Instantiate(parentTemplate, spawnPos, transform.rotation);

        childGO.name = $"NPC_Child_{Random.Range(100, 999)}";
        childGO.transform.localScale = Vector3.one * 0.6f;

        EnemyAI childAI = childGO.GetComponent<EnemyAI>();
        if (childAI != null)
        {
            childAI.StandartNPC = true;
            childAI.age = 0;
            childAI.ageTimer = 0f;
            childAI.hunger = 100f;
            childAI.currentHealth = childAI.maxHealth;
            childAI.gender = (Random.value < 0.5f) ? NPCGender.Male : NPCGender.Female;
            childAI.isReadyToBreed = false;
            childAI.breedPartner = null;
            childAI.isBreedingInProgress = false;

            if (currentSlaveryRoom != null)
            {
                childAI.TransitionToEnslaved(currentSlaveryRoom);
            }
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ModifyKarma(10f);
        }

        Debug.Log($"[{name}] И [{partner.name}] Породили ребенка! Пол: {(childAI != null ? childAI.gender.ToString() : "???")}, Масштаб: 0.6, Карма +10");
    }

    public void ResetBreedingState()
    {
        if (agent != null)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        // Восстанавливаем столкновения физики с партнером
        if (breedPartner != null)
        {
            EnemyAI p = breedPartner;
            breedPartner = null;
            SetPhysicsIgnoreWithPartner(p, false);
            if (p.isBreedingInProgress)
            {
                p.ResetBreedingState(); // Сбрасываем и партнера одновременно!
            }
        }

        isReadyToBreed = false;
        isBreedingInProgress = false;
        breedPartner = null;
        breedCooldownTimer = Time.time + breedCooldown;
        childSpawned = false;
        breedingTimer = 0f;

        if (activeLoveParticles != null)
        {
            Destroy(activeLoveParticles.gameObject);
            activeLoveParticles = null;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }
}
