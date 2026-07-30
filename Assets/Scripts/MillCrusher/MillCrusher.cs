using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MillCrusher : MonoBehaviour
{
    [System.Serializable]
    public class CrusherRecipe
    {
        [Tooltip("Айди входящего предмета")]
        public int inputItemID;
        [Tooltip("Префаб выходящего предмета")]
        public GameObject outputPrefab;
        [Tooltip("Количество выходящего предмета")]
        public int outputAmount = 1;
    }

    [Header("Настройки рецептов")]
    public List<CrusherRecipe> recipes = new List<CrusherRecipe>();

    [Header("Зоны и точки спавна")]
    [Tooltip("Коллайдер зоны спавна (предметы спавнятся в случайных точках этой зоны)")]
    public Collider spawnZone;

    [Header("Настройки крови")]
    [Tooltip("Резервуар для заправки крови")]
    public WaterCoolerController bloodReservoir;
    [Tooltip("Сколько крови дает живой НПС при сдавливании")]
    public float bloodPerNPC = 25f;
    [Tooltip("Сколько крови дает труп НПС при сдавливании")]
    public float bloodPerCorpse = 20f;

    [Header("Эффекты")]
    [Tooltip("Звук сдавливания")]
    public AudioSource crushSound;
    [Tooltip("Частицы крови при сдавливании")]
    public ParticleSystem bloodParticles;

    [Header("Настройки постепенного заполнения")]
    [Tooltip("Скорость постепенного наполнения резервуара (единиц в секунду)")]
    public float fillSpeed = 10f;

    [Header("Тайминги сдавливания НПС")]
    [Tooltip("Задержка перед воспроизведением звука раздавливания после контакта (в секундах)")]
    public float soundPlayDelay = 0f;
    [Tooltip("Задержка перед нанесением урона и взрывом НПС после контакта (в секундах)")]
    public float crushDelay = 0.5f;

    private Coroutine fillCoroutine;
    private float pendingBloodToAdd = 0f;
    private HashSet<int> crushedItemIDs = new HashSet<int>();

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Проверяем предметы
        PickUpItem item = other.GetComponentInParent<PickUpItem>();
        if (item != null)
        {
            Debug.Log($"[MillCrusher] Обнаружен предмет '{item.itemName}' (ID: {item.itemID}) в триггере давилки.");
            CrushItem(item);
            return;
        }

        // 2. Проверяем НПС
        EnemyAI npc = other.GetComponentInParent<EnemyAI>();
        if (npc != null)
        {
            Debug.Log($"[MillCrusher] Обнаружен NPC '{npc.gameObject.name}' в триггере давилки.");
            CrushNPC(npc);
            return;
        }
    }

    public void CrushItem(PickUpItem item)
    {
        if (item == null) return;

        int instanceID = item.gameObject.GetInstanceID();
        if (crushedItemIDs.Contains(instanceID)) return;
        crushedItemIDs.Add(instanceID);
        StartCoroutine(RemoveIDFromSet(instanceID));

        // Ищем рецепт для этого предмета
        CrusherRecipe recipe = recipes.Find(r => r.inputItemID == item.itemID);
        if (recipe != null)
        {
            int inputAmount = item.amount;
            Debug.Log($"[MillCrusher] Раздавливание предмета: {item.itemName} (ID: {item.itemID}, Кол-во: {inputAmount}) -> Выход: {recipe.outputPrefab?.name} x {recipe.outputAmount * inputAmount}");
            
            // Воспроизводим эффекты мгновенно
            PlayCrushSound(item.transform.position);
            PlayBloodParticles(item.transform.position);

            // Спавним выходной предмет
            for (int i = 0; i < recipe.outputAmount * inputAmount; i++)
            {
                SpawnOutputItem(recipe.outputPrefab);
            }

            // Удаляем раздавленный предмет
            Destroy(item.gameObject);
        }
        else
        {
            Debug.LogWarning($"[MillCrusher] Рецепт для предмета с ID {item.itemID} ({item.itemName}) не найден!");
        }
    }

    private IEnumerator RemoveIDFromSet(int instanceID)
    {
        yield return null; // Ждем следующий кадр
        crushedItemIDs.Remove(instanceID);
    }

    public void CrushNPC(EnemyAI npc)
    {
        StartCoroutine(CrushNPCRoutine(npc));
    }

    private IEnumerator CrushNPCRoutine(EnemyAI npc)
    {
        if (npc == null) yield break;

        EnemyDeathEffects fx = npc.GetComponent<EnemyDeathEffects>();
        if (fx == null) yield break;

        bool isDead = (npc.currentState == EnemyAI.NPCState.Dead);

        // Помечаем, что НПС раздавлен мельницей
        fx.isCrushedByMill = true;

        // Отключаем управление и навигацию сразу, чтобы НПС застыл под колесом
        npc.enabled = false;
        var agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // Замораживаем физику тела (если это труп с Rigidbody), чтобы его не вытолкнуло во время задержки
        Rigidbody npcRb = npc.GetComponent<Rigidbody>();
        if (npcRb == null) npcRb = npc.GetComponentInChildren<Rigidbody>();
        if (npcRb != null)
        {
            npcRb.isKinematic = true;
        }

        // Запускаем корутину для воспроизведения звука с задержкой
        StartCoroutine(PlaySoundWithDelay(npc.transform.position, soundPlayDelay));

        // Ждем настроенную в инспекторе задержку перед нанесением урона
        if (crushDelay > 0f)
        {
            yield return new WaitForSeconds(crushDelay);
        }

        // Проверяем, не уничтожен ли объект за время задержки
        if (npc == null) yield break;

        // Воспроизводим эффекты крови (частицы) в момент взрыва
        PlayBloodParticles(npc.transform.position);

        // Начисляем кровь в резервуар (постепенно через корутину)
        if (bloodReservoir != null)
        {
            float addedBlood = isDead ? bloodPerCorpse : bloodPerNPC;
            pendingBloodToAdd += addedBlood;
            if (fillCoroutine == null)
            {
                fillCoroutine = StartCoroutine(FillReservoirOverTime());
            }
            Debug.Log($"[MillCrusher] Добавлено {addedBlood} ед. крови в очередь наполнения. Всего в очереди: {pendingBloodToAdd}");
        }

        // Наносим смертельный урон для запуска эффектов взрыва
        npc.TakeDamage(99999f, this.transform);
    }

    private IEnumerator PlaySoundWithDelay(Vector3 position, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        PlayCrushSound(position);
    }

    private void SpawnOutputItem(GameObject prefab)
    {
        if (prefab == null) return;

        Vector3 spawnPos;
        if (spawnZone != null)
        {
            spawnPos = GetRandomPointInCollider(spawnZone);
        }
        else
        {
            spawnPos = transform.position + Vector3.up * 0.2f;
        }

        GameObject spawned = Instantiate(prefab, spawnPos, Random.rotation);
        spawned.SetActive(true);

        PickUpItem pickup = spawned.GetComponent<PickUpItem>();
        if (pickup == null) pickup = spawned.GetComponentInChildren<PickUpItem>();
        if (pickup != null)
        {
            // Бросаем предмет с нулевой силой, чтобы он физически упал, а после приземления перешел в левитацию
            pickup.Toss(Vector3.zero, 0f);
        }
    }

    private Vector3 GetRandomPointInCollider(Collider col)
    {
        Bounds bounds = col.bounds;
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    private void PlayCrushSound(Vector3 pos)
    {
        if (crushSound != null)
        {
            crushSound.transform.position = pos;
            crushSound.Play();
        }
    }

    private void PlayBloodParticles(Vector3 pos)
    {
        if (bloodParticles != null)
        {
            bloodParticles.transform.position = pos;
            bloodParticles.Play();
        }
    }

    private IEnumerator FillReservoirOverTime()
    {
        while (pendingBloodToAdd > 0f && bloodReservoir != null)
        {
            if (bloodReservoir.currentWater <= 0.01f)
            {
                bloodReservoir.currentLiquidType = LiquidType.Blood;
            }

            float fillThisFrame = fillSpeed * Time.deltaTime;
            fillThisFrame = Mathf.Min(fillThisFrame, pendingBloodToAdd);

            float spaceLeft = bloodReservoir.maxWater - bloodReservoir.currentWater;
            if (spaceLeft <= 0f)
            {
                pendingBloodToAdd = 0f;
                break;
            }

            fillThisFrame = Mathf.Min(fillThisFrame, spaceLeft);

            bloodReservoir.currentWater += fillThisFrame;
            pendingBloodToAdd -= fillThisFrame;

            bloodReservoir.UpdateWaterVisual();
            bloodReservoir.UpdateLiquidMaterial();

            yield return null;
        }

        fillCoroutine = null;
    }
}
