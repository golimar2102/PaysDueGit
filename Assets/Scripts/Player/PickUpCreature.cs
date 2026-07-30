using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(NavMeshAgent))]
public class PickUpCreature : PickUpItem
{
    [Header("Настройки живого существа")]
    [Tooltip("Поведение ИИ, которое активируется после того, как существо бросят из инвентаря")]
    public EnemyAI.NPCPersonality personalityOnDrop = EnemyAI.NPCPersonality.Scared;

    private EnemyAI ai;
    private NavMeshAgent agent;

    private void Start()
    {
        ai = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();

        // Принудительно отключаем левитацию
        isFloating = false;
        originalFloatingState = false;

        // PickUpItem отключает Animator в Awake. Для живого существа мы должны его включить обратно!
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = true;
    }

    public override void Toss(Vector3 direction, float force)
    {
        // Отключаем ИИ перед броском, чтобы NavMeshAgent не конфликтовал с физикой Rigidbody
        if (ai != null) ai.enabled = false;
        if (agent != null) agent.enabled = false;

        base.Toss(direction, force);

        // Запускаем корутину ожидания приземления
        StartCoroutine(WaitForLanding());
    }

    private IEnumerator WaitForLanding()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        
        // Ждем небольшую задержку, чтобы Rigidbody точно начал движение (isKinematic = false)
        yield return new WaitForSeconds(0.1f);

        // Ждем, пока PickUpItem не отключит физику (когда скорость упадет)
        while (rb != null && !rb.isKinematic)
        {
            yield return null;
        }

        // Существо приземлилось! Активируем ИИ.
        ActivateAI();
    }

    private void ActivateAI()
    {
        if (agent == null || ai == null) return;

        // Ищем NavMesh поблизости, чтобы не провалиться сквозь землю
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            
            agent.enabled = true;
            ai.SetPersonality(personalityOnDrop);
            ai.enabled = true;

            // Возвращаем коллайдеру нормальное состояние (PickUpItem делает его триггером при приземлении)
            // Но живому существу нужен нормальный коллайдер, чтобы не проваливаться сквозь карту
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = false;
        }
        else
        {
            Debug.LogWarning($"[PickUpCreature] {gameObject.name} не смог найти NavMesh после броска! ИИ не активирован.");
        }
    }

    public override void PickUp()
    {
        // При подборе можно добавить звуки или эффекты
        base.PickUp();
    }
}
