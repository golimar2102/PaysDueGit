using UnityEngine;

public class ZoneTrigger : MonoBehaviour
{
    [Header("Настройки зоны")]
    [Tooltip("Зона, которая устанавливается при входе в этот триггер")]
    public GameZone zoneOnEnter = GameZone.Barn;

    [Tooltip("Зона, которая устанавливается при выходе из этого триггера")]
    public GameZone zoneOnExit = GameZone.Farm;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
        {
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.SetZone(zoneOnEnter);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
        {
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.SetZone(zoneOnExit);
            }
        }
    }
}