using UnityEngine;
using System.Collections.Generic;

public class SlaveryRoomTrigger : MonoBehaviour
{
    [Header("Точки блуждания внутри комнаты (Wander Points)")]
    public List<Transform> wanderPoints = new List<Transform>();

    [Header("Двери для побега")]
    [Tooltip("Дверь комнаты, которую НПС будет пытаться взломать")]
    public DoorController exitDoor;
    [Tooltip("Дверь-телепорт, через которую НПС перейдет на другую локацию")]
    public TeleportDoor teleportDoor;

    [Header("Настройки побега")]
    [Tooltip("Точка на другой локации, до которой бежит НПС после телепортации перед исчезновением")]
    public Transform postTeleportEscapeTarget;

    [Tooltip("Шанс побега при очередной проверке (0.05 = 5%)")]
    [Range(0f, 1f)] public float escapeChance = 0.05f;

    [Tooltip("Интервал между проверками побега в секундах")]
    public float escapeCheckInterval = 30f;

    [Tooltip("Длительность одной попытки взлома двери в секундах")]
    public float lockPickAttemptDuration = 5f;

    [Tooltip("Шанс успеха взлома за одну попытку (0.3 = 30%)")]
    [Range(0f, 1f)] public float lockPickSuccessChance = 0.3f;

    private void OnTriggerEnter(Collider other)
    {
        TryEnslaveNPC(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryEnslaveNPC(other);
    }

    private void TryEnslaveNPC(Collider other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null && enemy.StandartNPC)
        {
            if (enemy.currentState != EnemyAI.NPCState.Dead && 
                enemy.currentState != EnemyAI.NPCState.Stunned && 
                enemy.currentState != EnemyAI.NPCState.Enslaved)
            {
                enemy.TransitionToEnslaved(this);
            }
        }
    }

    public void CheckRoomForBreedingPairs()
    {
        EnemyAI maleCandidate = null;
        EnemyAI femaleCandidate = null;

        foreach (var npc in EnemyAI.allNPCs)
        {
            if (npc == null || npc.currentState != EnemyAI.NPCState.Enslaved) continue;
            if (npc.currentSlaveryRoom != this) continue;
            if (!npc.isReadyToBreed || npc.isBreedingInProgress || npc.breedPartner != null) continue;
            if (npc.age < 18) continue; // Дети не размножаются

            if (npc.gender == EnemyAI.NPCGender.Male && maleCandidate == null)
            {
                maleCandidate = npc;
            }
            else if (npc.gender == EnemyAI.NPCGender.Female && femaleCandidate == null)
            {
                femaleCandidate = npc;
            }

            if (maleCandidate != null && femaleCandidate != null)
            {
                break;
            }
        }

        if (maleCandidate != null && femaleCandidate != null)
        {
            Debug.Log($"[SlaveryRoomTrigger] Найдена пара для размножения: Мужчина ({maleCandidate.name}) и Женщина ({femaleCandidate.name})!");
            maleCandidate.StartBreedingSequence(femaleCandidate);
            femaleCandidate.StartBreedingSequence(maleCandidate);
        }
    }

    private float checkPairsTimer = 0f;

    private void Update()
    {
        checkPairsTimer -= Time.deltaTime;
        if (checkPairsTimer <= 0f)
        {
            checkPairsTimer = 2.0f;
            CheckRoomForBreedingPairs();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Рисуем связи для наглядности в редакторе
        Gizmos.color = Color.magenta;
        
        if (exitDoor != null)
        {
            Gizmos.DrawLine(transform.position, exitDoor.transform.position);
        }

        if (teleportDoor != null)
        {
            Gizmos.DrawLine(transform.position, teleportDoor.transform.position);
        }

        if (postTeleportEscapeTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, postTeleportEscapeTarget.position);
            Gizmos.DrawWireSphere(postTeleportEscapeTarget.position, 1f);
        }

        Gizmos.color = Color.green;
        foreach (var pt in wanderPoints)
        {
            if (pt != null)
            {
                Gizmos.DrawLine(transform.position, pt.position);
                Gizmos.DrawWireSphere(pt.position, 0.5f);
            }
        }
    }
}
