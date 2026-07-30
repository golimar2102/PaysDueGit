using UnityEngine;

public class WeaponDamage : MonoBehaviour
{
    [Header("Настройки урона")]
    public float damage = 40f;
    public float attackForce = 8f;
    public bool isStunWeapon = false;

    [Tooltip("Слой врагов (Enemy)")]
    public LayerMask enemyLayer;

    [Header("Откуда бьём")]
    [Tooltip("Перетащи сюда свою FPS-камеру (FPS_Camera)")]
    public Camera playerCamera;

    [Header("Настройки попадания")]
    public float raycastDistance = 3.5f;        // насколько далеко бьёт оружие
    public float sphereRadius = 0.6f;           // дополнительный "толстый" луч

    [Header("Отложенный урон")]
    public bool useDelayedDamage = true;
    public float damageDelay = 0.18f;

    private bool isDamageEnabled = false;
    private float damageTimer = 0f;

    // === Animation Events ===
    public void EnableDamage()
    {
        isDamageEnabled = true;
        damageTimer = useDelayedDamage ? damageDelay : 0f;
    }

    public void DisableDamage()
    {
        isDamageEnabled = false;
    }

    private void Update()
    {
        if (!isDamageEnabled) return;

        if (useDelayedDamage)
        {
            damageTimer -= Time.deltaTime;
            if (damageTimer <= 0f)
            {
                DealDamageFromCamera();
                isDamageEnabled = false;   // наносим только 1 раз за взмах
            }
        }
        else
        {
            DealDamageFromCamera();
            isDamageEnabled = false;
        }
    }

    private void DealDamageFromCamera()
    {
        if (playerCamera == null)
        {
            Debug.LogError("WeaponDamage: Не назначена Player Camera!");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        // Основной точный Raycast от камеры
        if (Physics.Raycast(ray, out hit, raycastDistance, enemyLayer))
        {
            TryDamageEnemy(hit.collider);
            return;
        }

        // Если точный луч не попал — делаем "толстый" луч (как будто оружие имеет ширину)
        if (Physics.SphereCast(ray, sphereRadius, out hit, raycastDistance, enemyLayer))
        {
            TryDamageEnemy(hit.collider);
        }
    }

    private void TryDamageEnemy(Collider col)
    {
        EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
        if (enemy == null) return;
        if (isStunWeapon)
        {
            if (enemy.currentState == EnemyAI.NPCState.Dead || enemy.currentState == EnemyAI.NPCState.Stunned) return;
            enemy.TakeStunDamage(damage, transform.root);
        }
        else
        {
            if (enemy.currentState == EnemyAI.NPCState.Dead && !enemy.CanDamageCorpse()) return;
            enemy.TakeDamage(damage, transform.root);
        }

        // Отталкивание
        Rigidbody rb = col.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceDir = (col.transform.position - playerCamera.transform.position).normalized;
            rb.AddForce(forceDir * attackForce + Vector3.up * 3f, ForceMode.Impulse);
        }

        Debug.Log($"[WeaponDamage] УРОН {damage} по {enemy.gameObject.name} (от камеры)");
    }

    // Отладка в редакторе
    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * raycastDistance);
    }
}