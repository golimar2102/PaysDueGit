using UnityEngine;

/// <summary>
/// Зона мгновенной смерти (Kill Zone / Hazard).
/// Убивает игрока (через PlayerStats) или НПС (через EnemyAI) при попадании в триггер.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Наносить ли мгновенный огромный урон (true) или просто вызывать методы смерти.")]
    public bool instantKill = true;

    [Tooltip("Урон, наносимый при входе в триггер (если instantKill выключен)")]
    public float damageAmount = 9999f;

    [Header("Связь с Мельницей")]
    [Tooltip("Ссылка на скрипт давилки мельницы (если этот KillZone — часть колес мельницы)")]
    public MillCrusher crusher;

    private Collider zoneCollider;

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
        {
            Debug.Log($"[KillZone] Коллайдер на объекте {gameObject.name} не настроен как Is Trigger. Включаю автоматически.");
            zoneCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 0. Проверяем предметы
        PickUpItem item = other.GetComponentInParent<PickUpItem>();
        if (item != null)
        {
            Debug.Log($"[KillZone] Обнаружен предмет '{item.itemName}' (ID: {item.itemID}) в зоне смерти.");
            if (crusher != null)
            {
                crusher.CrushItem(item);
            }
            else
            {
                Debug.LogWarning("[KillZone] Ссылка на MillCrusher (crusher) пуста!");
            }
            return;
        }

        // 1. Проверяем игрока
        PlayerStats player = other.GetComponentInParent<PlayerStats>();
        if (player == null)
        {
            player = other.GetComponentInChildren<PlayerStats>();
        }

        if (player != null)
        {
            Debug.Log($"[KillZone] Игрок '{player.gameObject.name}' вошел в зону смерти.");
            if (instantKill)
            {
                player.TakeDamage(player.maxHealth * 10f);
            }
            else
            {
                player.TakeDamage(damageAmount);
            }
            return;
        }

        // 2. Проверяем НПС (ИИ)
        EnemyAI npc = other.GetComponentInParent<EnemyAI>();
        if (npc == null)
        {
            npc = other.GetComponentInChildren<EnemyAI>();
        }

        if (npc != null)
        {
            Debug.Log($"[KillZone] НПС '{npc.gameObject.name}' вошел в зону смерти.");
            if (crusher != null)
            {
                crusher.CrushNPC(npc);
            }
            else
            {
                if (instantKill)
                {
                    npc.TakeDamage(npc.maxHealth * 10f);
                }
                else
                {
                    npc.TakeDamage(damageAmount);
                }
            }
            return;
        }
    }
}
