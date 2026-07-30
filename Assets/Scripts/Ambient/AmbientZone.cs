using UnityEngine;

/// <summary>
/// Опциональный компонент для разметки локальных триггерных зон с индивидуальным эмбиентом.
/// Переопределяет стандартный эмбиент зоны DayNightCycle, если игрок находится внутри триггера.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AmbientZone : MonoBehaviour
{
    [Header("Настройки эмбиента зоны")]
    [Tooltip("Аудиоклип для этой зоны (если null, в триггере будет тишина)")]
    public AudioClip ambientClip;
    [Range(0f, 1f)]
    [Tooltip("Громкость для этого эмбиента")]
    public float volumeMultiplier = 1.0f;
    [Tooltip("Приоритет зоны. Если зоны пересекаются, выберется зона с наибольшим приоритетом")]
    public int priority = 0;

    [Header("Параметры фильтрации игрока")]
    [Tooltip("Тег игрока, по которому срабатывает триггер")]
    public string playerTag = "Player";

    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
        {
            Debug.Log($"[AmbientZone] Коллайдер на объекте {gameObject.name} не настроен как Is Trigger. Включаю автоматически.");
            zoneCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            if (AmbientManager.Instance != null)
            {
                AmbientManager.Instance.RegisterTriggerZone(this);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            if (AmbientManager.Instance != null)
            {
                AmbientManager.Instance.UnregisterTriggerZone(this);
            }
        }
    }

    /// <summary>
    /// Проверяет, является ли вошедший объект игроком.
    /// </summary>
    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag) || 
               other.transform.root.CompareTag(playerTag) || 
               other.GetComponentInParent<PlayerMovement>() != null;
    }
}
