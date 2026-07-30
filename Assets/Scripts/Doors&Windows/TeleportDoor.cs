using UnityEngine;
using System.Collections; 

public class TeleportDoor : MonoBehaviour
{
    [Header("Настройки Телепорта")]
    [Tooltip("Пустой объект или BoxCollider внутри дома, куда появится игрок")]
    public Transform destinationPoint;

    [Header("Квартирная дверь (Apartment Setup)")]
    [Tooltip("Если true, эта дверь является выходом из квартиры и будет искать заспавненную через DoorSummoner дверь на улице")]
    public bool isApartmentDoor = false;

    [Header("Освещение (День/Ночь)")]
    [Tooltip("Зона, в которую ведет этот телепорт")]
    public GameZone targetZone = GameZone.Farm;

    [Header("Визуал (Раздвижные створки)")]
    public Transform leftDoor;
    public Transform rightDoor;
    [Tooltip("На сколько метров створки разъедутся в стороны")]
    public float slideDistance = 0.2f;
    [Tooltip("Ось раздвигания (Обычно X(1,0,0) или Z(0,0,1))")]
    public Vector3 slideAxis = new Vector3(1, 0, 0);
    public float slideSpeed = 6f;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private bool isHovered = false;

    [Header("Обводка (Outline)")]
    [Tooltip("Перетащи сюда объекты с компонентом Outline. Если оставить пустым, скрипт найдет их сам.")]
    public Outline[] outlines;

    void Start()
    {
        if (leftDoor != null)
        {
            leftClosedPos = leftDoor.localPosition;
            leftOpenPos = leftClosedPos - (slideAxis.normalized * slideDistance);
        }
        if (rightDoor != null)
        {
            rightClosedPos = rightDoor.localPosition;
            rightOpenPos = rightClosedPos + (slideAxis.normalized * slideDistance);
        }

        if (outlines == null || outlines.Length == 0)
        {
            outlines = GetComponentsInChildren<Outline>(true); 
        }

        SetOutlineState(false);
    }

    void Update()
    {
        if (leftDoor != null)
        {
            Vector3 target = isHovered ? leftOpenPos : leftClosedPos;
            leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, target, Time.deltaTime * slideSpeed);
        }

        if (rightDoor != null)
        {
            Vector3 target = isHovered ? rightOpenPos : rightClosedPos;
            rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, target, Time.deltaTime * slideSpeed);
        }
    }

    public void SetHover(bool state)
    {
        if (isHovered == state) return; 
        
        isHovered = state;
        SetOutlineState(state);
    }

    private void SetOutlineState(bool state)
    {
        if (outlines == null) return;

        foreach (Outline outline in outlines)
        {
            if (outline != null) outline.enabled = state;
        }
    }

    public void DoTeleport(GameObject player)
    {
        Transform targetPoint = destinationPoint;
        bool hasSummonedTarget = false;
        Vector3 targetPos = Vector3.zero;
        Quaternion targetRot = Quaternion.identity;

        if (isApartmentDoor)
        {
            if (DoorSummoner.Instance != null && DoorSummoner.Instance.ActiveDoorInstance != null)
            {
                GameObject summonedDoor = DoorSummoner.Instance.ActiveDoorInstance;

                // Ищем дочернюю точку спавна (SpawnPoint/DestinationPoint и т.д.)
                Transform customDest = summonedDoor.transform.Find("spawnPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("SpawnPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("destinationPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("DestinationPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("Spawn");
                if (customDest == null) customDest = summonedDoor.transform.Find("Destination");

                if (customDest != null)
                {
                    targetPoint = customDest;
                }
                else
                {
                    // Если специальной точки нет, телепортируем перед дверью (чтобы не застрять в ее коллайдере)
                    targetPos = summonedDoor.transform.position + summonedDoor.transform.forward * 1.5f;
                    targetRot = summonedDoor.transform.rotation;
                    hasSummonedTarget = true;
                }
            }
            else
            {
                Debug.LogWarning("[TeleportDoor] DoorSummoner.Instance или ActiveDoorInstance не найден. Телепорт на дефолтную точку.");
            }
        }

        if (targetPoint == null && !hasSummonedTarget)
        {
            Debug.LogError("Не указана точка телепортации!");
            return;
        }
        
        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm == null) pm = player.GetComponentInChildren<PlayerMovement>();

        if (pm != null)
        {
            if (hasSummonedTarget)
            {
                pm.Teleport(targetPos, targetRot);
            }
            else
            {
                pm.Teleport(targetPoint);
            }
            Debug.Log("Телепорт через PlayerMovement выполнен успешно!");
        }
        else
        {
            Debug.LogWarning("Скрипт PlayerMovement не найден! Попытка грубого телепорта.");
            if (hasSummonedTarget)
            {
                player.transform.position = targetPos;
                player.transform.rotation = targetRot;
            }
            else
            {
                player.transform.position = targetPoint.position;
                player.transform.rotation = targetPoint.rotation;
            }
            Physics.SyncTransforms();
        }
        
        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.SetZone(targetZone);
            Debug.Log($"Игрок переместился в зону {targetZone}.");
        }
        else
        {
            Debug.LogWarning("DayNightCycle.Instance не найден! Освещение не переключено.");
        }
    }
}