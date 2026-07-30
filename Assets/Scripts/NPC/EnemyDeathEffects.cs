using UnityEngine;

// Класс для детальной настройки мяса
[System.Serializable]
public class MeatConfig
{
    [Tooltip("Префаб куска мяса (например: рука, голова, ребро)")]
    public GameObject meatPrefab;
    [Tooltip("Сколько ИМЕННО ЭТИХ префабов вылетит при взрыве")]
    [Range(1, 20)] public int spawnCount = 1;
}

[System.Serializable]
public class LootConfig
{
    [Tooltip("Префаб предмета (патроны, аптечка)")]
    public GameObject lootPrefab;
    [Tooltip("Шанс выпадения ИМЕННО ЭТОГО предмета (0 - никогда, 100 - всегда)")]
    [Range(0f, 100f)] public float dropChance = 50f;
    [Tooltip("Минимальное количество")]
    [Range(1, 10)] public int minAmount = 1;
    [Tooltip("Максимальное количество")]
    [Range(1, 10)] public int maxAmount = 1;
}

public class EnemyDeathEffects : MonoBehaviour
{
    [Header("Расчлененка (Gore)")] 
    public bool useGore = true;
    public GameObject bloodMistPrefab;
    public GameObject bloodDecalPrefab;

    [Tooltip("Список кусков мяса. Настрой сколько и чего должно вылетать!")]
    public MeatConfig[] meatConfigs;
    
    [Range(0f, 50f)] public float explosionForce = 15f;
    [Range(0f, 10f)] public float explosionRadius = 5f;
    [Tooltip("Сила подброса ВВЕРХ")] 
    [Range(0f, 10f)] public float upwardsModifier = 3f;

    public Vector3 explosionOffset = new Vector3(0, 1f, 0);
    public float meatLifetime = 6f;
    public float corpseLifetime = 10f;
    
    [Header("Дроп (Лут)")] 
    [Tooltip("Список возможного лута при обычной (анимированной) смерти. Шанс и количество настраиваются индивидуально!")]
    public LootConfig[] lootTable;
    [Tooltip("Список лута при взрыве (gore/gibs). Если пустой, используется стандартная таблица.")]
    public LootConfig[] gibLootTable;

    [Header("Настройки трупа")]
    [Tooltip("Здоровье трупа для взрыва")]
    public float corpseHealth = 50f;
    [Tooltip("Дропать ли лут при взрыве трупа")]
    public bool dropLootOnCorpseGib = true;

    [Header("Настройки анимации смерти")]
    [Tooltip("Использовать индекс анимации смерти (как для атаки)")]
    public bool useDeathIndex = true;
    public string deathIndexParam = "DeathIndex";
    public string deathTriggerParam = "Death";
    public int deathIndexMax = 3;
    [Tooltip("Ветки триггеров на случай, если не используется DeathIndex")]
    public string[] deathTriggers = new string[] { "Die_Axe", "Die_Head", "Die_Sliced" };

    private float currentCorpseHealth;
    private bool isCorpseStateActive = false;
    private Coroutine corpseLifetimeCo;
    private Animator cachedAnimator;
    [HideInInspector] public bool isCrushedByMill = false;

    // Эта функция вызывается из EnemyAI в момент смерти
    public void TriggerDeathEffects(Animator enemyAnimator)
    {
        Debug.Log("[DeathEffects] Враг умер! Начинаем спавн лута...");
        cachedAnimator = enemyAnimator;

        if (!isCrushedByMill)
        {
            if (useGore)
            {
                HandleLootDrop(gibLootTable != null && gibLootTable.Length > 0 ? gibLootTable : lootTable);
            }
            else
            {
                HandleLootDrop(lootTable);
            }
        }
        else
        {
            Debug.Log("[DeathEffects] Враг раздавлен мельницей. Спавн лута заблокирован!");
        }

        HandleGoreAndCorpse(enemyAnimator);
    }

    private void HandleLootDrop(LootConfig[] selectedLootTable)
    {
        if (selectedLootTable == null || selectedLootTable.Length == 0)
        {
            Debug.Log("[DeathEffects] Таблица лута пуста!");
            return;
        }

        foreach (var lootInfo in selectedLootTable)
        {
            if (lootInfo.lootPrefab != null && Random.Range(0f, 100f) <= lootInfo.dropChance)
            {
                int dropCount = Random.Range(lootInfo.minAmount, lootInfo.maxAmount + 1);
                Debug.Log($"[DeathEffects] Спавним лут: {lootInfo.lootPrefab.name} ({dropCount} шт.)");
                
                for (int i = 0; i < dropCount; i++)
                {
                    Vector3 spawnPos = transform.position + Vector3.up * 1f + Random.insideUnitSphere * 0.3f;
                    GameObject loot = Instantiate(lootInfo.lootPrefab, spawnPos, Random.rotation);
                    
                    // ПРИНУДИТЕЛЬНО ВКЛЮЧАЕМ ЛУТ (если префаб был сохранен выключенным)
                    loot.SetActive(true);
                    
                    PickUpItem pickup = loot.GetComponent<PickUpItem>();
                    if (pickup == null) pickup = loot.GetComponentInChildren<PickUpItem>();

                    if (pickup != null)
                    {
                        Vector3 tossDirection = (Random.onUnitSphere * 0.5f + Vector3.up * upwardsModifier).normalized;
                        pickup.Toss(tossDirection, explosionForce * 0.5f);
                    }
                }
            }
        }
    }

    private void HandleGoreAndCorpse(Animator enemyAnimator)
    {
        if (useGore || isCrushedByMill)
        {
            Debug.Log("[DeathEffects] Запускаем расчлененку!");
            SpawnGibs(enemyAnimator);
            Debug.Log("[DeathEffects] Тело уничтожено.");
            Destroy(gameObject, 0.1f); 
        }
        else 
        {
            if (enemyAnimator != null)
            {
                enemyAnimator.SetBool("IsDead", true);
                if (useDeathIndex)
                {
                    int deathIdx = Random.Range(0, deathIndexMax);
                    enemyAnimator.SetInteger(deathIndexParam, deathIdx);
                    enemyAnimator.ResetTrigger(deathTriggerParam);
                    enemyAnimator.SetTrigger(deathTriggerParam);
                }
                else
                {
                    if (deathTriggers != null && deathTriggers.Length > 0)
                    {
                        enemyAnimator.SetTrigger(deathTriggers[Random.Range(0, deathTriggers.Length)]);
                    }
                }
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.isTrigger = true;
            }

            // Добавляем компонент подбора трупа
            var corpseComponent = GetComponent<NPCCorpse>();
            if (corpseComponent == null)
            {
                corpseComponent = gameObject.AddComponent<NPCCorpse>();
            }
            corpseComponent.InitializeCorpse();

            currentCorpseHealth = corpseHealth;
            isCorpseStateActive = true;
            corpseLifetimeCo = StartCoroutine(CorpseLifetimeRoutine());
        }
    }

    private void SpawnGibs(Animator enemyAnimator)
    {
        if (enemyAnimator != null) enemyAnimator.gameObject.SetActive(false);

        if (bloodMistPrefab != null)
        {
            GameObject mist = Instantiate(bloodMistPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
            mist.SetActive(true);
        }

        if (meatConfigs != null && meatConfigs.Length > 0)
        {
            PhysicsMaterial bouncyMat = new PhysicsMaterial("BouncyMeat");
            bouncyMat.bounciness = 0.2f;
            bouncyMat.bounceCombine = PhysicsMaterialCombine.Maximum;

            Vector3 explosionEpicenter = transform.position + explosionOffset;

            foreach (var meatInfo in meatConfigs)
            {
                if (meatInfo.meatPrefab == null) continue;

                for (int i = 0; i < meatInfo.spawnCount; i++)
                {
                    Vector3 spawnPos = transform.position + Vector3.up * 1.5f + Random.insideUnitSphere * 0.5f;
                    GameObject meat = Instantiate(meatInfo.meatPrefab, spawnPos, Random.rotation);
                    
                    meat.SetActive(true);
                    
                    Collider col = meat.GetComponent<Collider>();
                    if (col != null && col is MeshCollider meshCollider)
                    {
                        meshCollider.convex = true;
                    }
                    else if (col == null)
                    {
                        SphereCollider sc = meat.AddComponent<SphereCollider>();
                        sc.radius = 0.15f;
                        col = sc;
                    }
                    col.material = bouncyMat; 

                    Rigidbody rb = meat.GetComponent<Rigidbody>();
                    if (rb == null) rb = meat.AddComponent<Rigidbody>();
                    rb.interpolation = RigidbodyInterpolation.Interpolate; 
                    
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    
                    MeatChunk chunkScript = meat.GetComponent<MeatChunk>();
                    if (chunkScript == null) chunkScript = meat.AddComponent<MeatChunk>();
                    
                    chunkScript.bloodDecalPrefab = bloodDecalPrefab;
                    chunkScript.lifetime = meatLifetime; 

                    rb.AddExplosionForce(explosionForce, explosionEpicenter, explosionRadius, upwardsModifier, ForceMode.Impulse);
                    rb.AddForce(Vector3.up * (explosionForce * 0.4f), ForceMode.Impulse);
                    rb.freezeRotation = true; 
                }
            }
        }
    }

    private System.Collections.IEnumerator CorpseLifetimeRoutine()
    {
        yield return new WaitForSeconds(corpseLifetime);
        CleanupCorpse();
    }

    private void CleanupCorpse()
    {
        isCorpseStateActive = false;
        Destroy(gameObject);
    }

    public bool CanBeGibbed()
    {
        var corpseComp = GetComponent<NPCCorpse>();
        bool isCarried = corpseComp != null && NPCCorpse.carriedCorpse == corpseComp;
        return isCorpseStateActive && !useGore && !isCarried;
    }

    public void PauseCorpseLifetime(bool pause)
    {
        if (pause)
        {
            if (corpseLifetimeCo != null)
            {
                StopCoroutine(corpseLifetimeCo);
                corpseLifetimeCo = null;
            }
        }
        else
        {
            if (corpseLifetimeCo == null && isCorpseStateActive)
            {
                corpseLifetimeCo = StartCoroutine(CorpseLifetimeRoutine());
            }
        }
    }

    public void DamageCorpse(float amount)
    {
        if (!CanBeGibbed()) return;

        currentCorpseHealth -= amount;
        Debug.Log($"[DeathEffects] Труп получил {amount} урона. Оставшееся ХП трупа: {currentCorpseHealth}");

        if (currentCorpseHealth <= 0f)
        {
            ExplodeCorpse();
        }
    }

    private void ExplodeCorpse()
    {
        if (corpseLifetimeCo != null) StopCoroutine(corpseLifetimeCo);
        isCorpseStateActive = false;

        Debug.Log("[DeathEffects] Труп взорван!");
        SpawnGibs(cachedAnimator);

        if (dropLootOnCorpseGib && !isCrushedByMill)
        {
            HandleLootDrop(gibLootTable != null && gibLootTable.Length > 0 ? gibLootTable : lootTable);
        }

        Destroy(gameObject, 0.1f);
    }
}