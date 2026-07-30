using UnityEngine;
using System.Collections.Generic;

public partial class EnemyAI : MonoBehaviour
{
    public static EnemyAI carriedSlave = null;
    private Transform originalSlaveParent;

    public void TransitionToEnslaved(SlaveryRoomTrigger room)
    {
        if (currentState == NPCState.Dead || currentState == NPCState.Stunned) return;

        ResetTarget();

        currentSlaveryRoom = room;
        currentState = NPCState.Enslaved;
        escapeState = NPCEscapeState.None;
        escapeCheckTimer = Time.time + Random.Range(5f, room.escapeCheckInterval);
        
        // Make docile
        personality = NPCPersonality.Neutral;
        
        Debug.Log($"[{name}] Переведен в состояние Enslaved в комнате {room.gameObject.name}!");
    }

    public void PickUpSlave(GameObject player)
    {
        if (carriedSlave != null || NPCCorpse.carriedCorpse != null) return;
        if (currentState == NPCState.Dead) return;

        carriedSlave = this;

        // Убираем оружие из рук игрока
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.UnequipAll();
        }
        if (InventoryManager.Instance != null && InventoryManager.Instance.equippedItemNameText != null)
        {
            InventoryManager.Instance.equippedItemNameText.text = "";
        }

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        originalSlaveParent = transform.parent;

        Transform holdPoint = GetOrCreateSlaveHoldPoint(player);
        transform.SetParent(holdPoint);
        transform.localPosition = new Vector3(0f, -0.6f, 1.8f);
        transform.localRotation = Quaternion.identity; // Стоя вертикально

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = false;
        }

        Debug.Log($"[{name}] Взят на руки в вертикальном положении!");
    }

    public void DropSlave()
    {
        if (carriedSlave != this) return;

        transform.SetParent(originalSlaveParent);

        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
        {
            transform.position = hit.point;
        }

        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = true;
        }

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        carriedSlave = null;
        Debug.Log($"[{name}] Опущен с рук!");
    }

    private Transform GetOrCreateSlaveHoldPoint(GameObject player)
    {
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;

        Transform holdPoint = cam.transform.Find("SlaveHoldPoint");
        if (holdPoint == null)
        {
            GameObject go = new GameObject("SlaveHoldPoint");
            holdPoint = go.transform;
            holdPoint.SetParent(cam.transform);
            holdPoint.localPosition = Vector3.zero;
            holdPoint.localRotation = Quaternion.identity;
        }
        return holdPoint;
    }

    private void BehaviourEnslaved(float dist)
    {
        if (carriedSlave == this)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                DropSlave();
            }
            return;
        }

        if (isBreedingInProgress)
        {
            return;
        }

        if (currentSlaveryRoom == null)
        {
            currentState = NPCState.Roam;
            return;
        }

        agent.updateRotation = true;

        // Если в побеге
        if (escapeState != NPCEscapeState.None)
        {
            UpdateEscapeSequence();
            return;
        }

        // Периодическая проверка побега
        if (Time.time >= escapeCheckTimer)
        {
            escapeCheckTimer = Time.time + currentSlaveryRoom.escapeCheckInterval;
            
            // Проверяем: игрок не виден
            if (!playerVisible)
            {
                if (Random.value <= currentSlaveryRoom.escapeChance)
                {
                    StartEscapeAttempt();
                    return;
                }
            }
        }

        // Обычное блуждание внутри комнаты
        if (agent.pathPending) return;

        bool hasArrived = (agent.remainingDistance <= agent.stoppingDistance + 0.8f);
        bool isStuckOnOther = (agent.hasPath && agent.velocity.sqrMagnitude < 0.05f && agent.remainingDistance < 2.5f);

        if (hasArrived || isStuckOnOther)
        {
            if (!isRoamWaiting)
            {
                isRoamWaiting = true;
                nextRoamTime = Time.time + Random.Range(idlePauseMin, idlePauseMax);
                agent.isStopped = true;

                if (Random.value < 0.6f && animator != null)
                {
                    animator.SetTrigger(H_LookAround);
                }
            }

            if (Time.time >= nextRoamTime)
            {
                Vector3 dest = GetSlaveryRoomWanderDestination();
                if (dest != transform.position)
                {
                    isRoamWaiting = false;
                    agent.isStopped = false;
                    agent.speed = walkSpeed;
                    agent.SetDestination(dest);
                }
            }
        }
        else
        {
            agent.isStopped = false;
            isRoamWaiting = false;
        }
    }

    [Header("Настройки Блуждания в Рабстве")]
    [Tooltip("Радиус случайного разброса вокруг точек блуждания в Инспекторе (настраивается для комнаты рабства)")]
    public float slaveryWanderRadius = 1.5f;

    private Vector3 GetSlaveryRoomWanderDestination()
    {
        if (currentSlaveryRoom == null || currentSlaveryRoom.wanderPoints == null || currentSlaveryRoom.wanderPoints.Count == 0)
        {
            return transform.position;
        }

        List<Transform> valid = new List<Transform>();
        foreach (var pt in currentSlaveryRoom.wanderPoints)
        {
            if (pt != null && pt != lastVisitedPoint)
            {
                valid.Add(pt);
            }
        }

        if (valid.Count == 0)
        {
            foreach (var pt in currentSlaveryRoom.wanderPoints)
            {
                if (pt != null) valid.Add(pt);
            }
        }

        if (valid.Count > 0)
        {
            Transform chosen = valid[Random.Range(0, valid.Count)];
            lastVisitedPoint = chosen;

            // Добавляем случайный разброс вокруг точки, чтобы каждый НПС шёл в свою уникальную координату
            Vector3 randomDir = Random.insideUnitSphere * slaveryWanderRadius;
            randomDir.y = 0f;
            Vector3 targetPos = chosen.position + randomDir;

            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, slaveryWanderRadius + 1.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return hit.position;
            }

            return chosen.position;
        }

        return transform.position;
    }

    private void StartEscapeAttempt()
    {
        if (currentSlaveryRoom == null || currentSlaveryRoom.exitDoor == null) return;

        isBackgroundEscaping = true;
        backgroundEscapeStartTime = Time.time;

        escapeState = NPCEscapeState.GoingToDoor;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(currentSlaveryRoom.exitDoor.transform.position);
        }
        lockPickAttempts = 0;
        
        Debug.Log($"[{name}] Начал попытку побега! Бежит к двери.");
    }

    private void UpdateEscapeSequence()
    {
        if (currentSlaveryRoom == null)
        {
            escapeState = NPCEscapeState.None;
            return;
        }

        switch (escapeState)
        {
            case NPCEscapeState.GoingToDoor:
                if (currentSlaveryRoom.exitDoor == null)
                {
                    escapeState = NPCEscapeState.None;
                    return;
                }
                
                agent.speed = runSpeed;
                agent.SetDestination(currentSlaveryRoom.exitDoor.transform.position);

                if (!agent.pathPending && agent.remainingDistance <= doorInteractionRadius + 0.5f)
                {
                    agent.isStopped = true;
                    escapeState = NPCEscapeState.PickingLock;
                    escapeTimer = Time.time + currentSlaveryRoom.lockPickAttemptDuration;
                    lockPickAttempts++;
                    Debug.Log($"[{name}] Достиг двери. Начинает попытку взлома №{lockPickAttempts}...");
                }
                break;

            case NPCEscapeState.PickingLock:
                if (currentSlaveryRoom.exitDoor == null)
                {
                    agent.isStopped = false;
                    escapeState = NPCEscapeState.None;
                    return;
                }

                FaceToward(currentSlaveryRoom.exitDoor.transform.position);

                if (Time.time >= escapeTimer)
                {
                    // Проверяем шанс успеха взлома
                    if (Random.value <= currentSlaveryRoom.lockPickSuccessChance)
                    {
                        Debug.Log($"[{name}] Успешно взломал дверь после {lockPickAttempts} попыток!");
                        currentSlaveryRoom.exitDoor.isLocked = false;
                        currentSlaveryRoom.exitDoor.TryOpenDoor(transform.position);

                        agent.isStopped = false;
                        if (currentSlaveryRoom.teleportDoor != null)
                        {
                            escapeState = NPCEscapeState.GoingToTeleport;
                            agent.SetDestination(currentSlaveryRoom.teleportDoor.transform.position);
                        }
                        else
                        {
                            // Если нет телепорта, бежим сразу на финальную точку
                            escapeState = NPCEscapeState.GoingToFinalExit;
                            if (currentSlaveryRoom.postTeleportEscapeTarget != null)
                                agent.SetDestination(currentSlaveryRoom.postTeleportEscapeTarget.position);
                            else
                                Destroy(gameObject); // Исчезает
                        }
                    }
                    else
                    {
                        Debug.Log($"[{name}] Попытка взлома №{lockPickAttempts} провалилась. Начинает новую попытку...");
                        escapeTimer = Time.time + currentSlaveryRoom.lockPickAttemptDuration;
                        lockPickAttempts++;
                    }
                }
                break;

            case NPCEscapeState.GoingToTeleport:
                if (currentSlaveryRoom.teleportDoor == null)
                {
                    escapeState = NPCEscapeState.GoingToFinalExit;
                    if (currentSlaveryRoom.postTeleportEscapeTarget != null)
                        agent.SetDestination(currentSlaveryRoom.postTeleportEscapeTarget.position);
                    return;
                }

                agent.speed = runSpeed;
                agent.SetDestination(currentSlaveryRoom.teleportDoor.transform.position);

                if (!agent.pathPending && agent.remainingDistance <= 2.0f)
                {
                    // Телепортация
                    Vector3 destPos = currentSlaveryRoom.teleportDoor.destinationPoint.position;
                    Debug.Log($"[{name}] Телепортируется через дверь побега!");
                    if (agent.enabled)
                    {
                        agent.Warp(destPos);
                    }
                    else
                    {
                        transform.position = destPos;
                    }

                    escapeState = NPCEscapeState.GoingToFinalExit;
                    if (currentSlaveryRoom.postTeleportEscapeTarget != null)
                    {
                        agent.SetDestination(currentSlaveryRoom.postTeleportEscapeTarget.position);
                    }
                    else
                    {
                        // Если нет финальной точки, исчезает сразу после телепорта
                        Destroy(gameObject);
                    }
                }
                break;

            case NPCEscapeState.GoingToFinalExit:
                if (currentSlaveryRoom.postTeleportEscapeTarget == null)
                {
                    Destroy(gameObject);
                    return;
                }

                agent.speed = runSpeed;
                agent.SetDestination(currentSlaveryRoom.postTeleportEscapeTarget.position);

                if (!agent.pathPending && agent.remainingDistance <= 2.5f)
                {
                    Debug.Log($"[{name}] Достиг выхода и исчез навсегда!");
                    Destroy(gameObject);
                }
                break;
        }
    }

    private void CheckAndOpenNearbyDoors()
    {
        // 1. Проверяем наличие дверей в радиусе для автоматического открытия
        Collider[] colliders = Physics.OverlapSphere(transform.position + Vector3.up * 1f, doorInteractionRadius);
        foreach (var col in colliders)
        {
            DoorController door = col.GetComponentInParent<DoorController>();
            if (door != null && !door.isOpen && !door.isLocked)
            {
                // ИИ открывает дверь "от себя"
                door.TryOpenDoor(transform.position);

                // Запоминаем открытую ИИ дверь
                if (!openedDoors.Contains(door))
                {
                    openedDoors.Add(door);
                }
            }
        }

        // 2. Проверяем ранее открытые нами двери, чтобы закрыть их за собой
        for (int i = openedDoors.Count - 1; i >= 0; i--)
        {
            DoorController door = openedDoors[i];
            if (door == null)
            {
                openedDoors.RemoveAt(i);
                continue;
            }

            // Если дверь уже закрыли (например, игрок), убираем из списка
            if (!door.isOpen)
            {
                openedDoors.RemoveAt(i);
                continue;
            }

            // Проверяем расстояние от ИИ до двери
            float dist = Vector3.Distance(transform.position, door.transform.position);
            if (dist >= doorCloseDistance)
            {
                // Закрываем дверь за собой
                door.TryOpenDoor(transform.position);
                openedDoors.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Проверяет статус фонового побега при включении GameObject на сцене (OnEnable)
    /// </summary>
    public void CheckBackgroundEscapeResume()
    {
        if (currentState != NPCState.Enslaved || !isBackgroundEscaping || currentSlaveryRoom == null) return;

        float elapsed = Time.time - backgroundEscapeStartTime;
        float lockDuration = currentSlaveryRoom.lockPickAttemptDuration;
        float teleportDuration = 3.0f;
        float finalRunDuration = 10.0f;
        float totalDuration = lockDuration + teleportDuration + finalRunDuration;

        if (elapsed >= totalDuration)
        {
            // Раб окончательно сбежал, пока локация была неактивна
            Debug.Log($"[{name}] Окончательно сбежал за время отсутствия игрока!");
            if (allNPCs.Contains(this)) allNPCs.Remove(this);
            Destroy(gameObject);
            return;
        }

        // Если раб еще в процессе побега, позиционируем его на нужной фазе
        if (elapsed < lockDuration)
        {
            // Фаза 1: Взлом двери в рабской комнате
            if (currentSlaveryRoom.exitDoor != null)
            {
                transform.position = currentSlaveryRoom.exitDoor.transform.position;
            }
            escapeState = NPCEscapeState.PickingLock;
            escapeTimer = Time.time + (lockDuration - elapsed);
            Debug.Log($"[{name}] Восстановлен у двери на фазе взлома (осталось {lockDuration - elapsed:F1} сек)");
        }
        else if (elapsed < lockDuration + teleportDuration)
        {
            // Фаза 2: Телепортация на вторую локацию
            if (currentSlaveryRoom.teleportDoor != null && currentSlaveryRoom.teleportDoor.destinationPoint != null)
            {
                transform.position = currentSlaveryRoom.teleportDoor.destinationPoint.position;
            }
            escapeState = NPCEscapeState.GoingToFinalExit;
            Debug.Log($"[{name}] Восстановлен на выходе из двери-телепорта на 2-й локации");
        }
        else
        {
            // Фаза 3: Бег по 2-й локации к финальному выходу
            if (currentSlaveryRoom.teleportDoor != null && currentSlaveryRoom.teleportDoor.destinationPoint != null)
            {
                transform.position = currentSlaveryRoom.teleportDoor.destinationPoint.position;
            }
            escapeState = NPCEscapeState.GoingToFinalExit;

            if (agent != null && agent.enabled && agent.isOnNavMesh && currentSlaveryRoom.postTeleportEscapeTarget != null)
            {
                agent.isStopped = false;
                agent.speed = runSpeed;
                agent.SetDestination(currentSlaveryRoom.postTeleportEscapeTarget.position);
            }
            Debug.Log($"[{name}] Восстановлен бегущим к финальному выходу на 2-й локации!");
        }
    }

    /// <summary>
    /// Статический метод для просчета фонового побега рабов в неактивных зонах
    /// </summary>
    public static void ProcessBackgroundSlaveryEscapes()
    {
        if (allNPCs == null || allNPCs.Count == 0) return;

        for (int i = allNPCs.Count - 1; i >= 0; i--)
        {
            if (i >= allNPCs.Count) continue;
            EnemyAI npc = allNPCs[i];
            if (npc == null || npc.currentState != NPCState.Enslaved || npc.currentSlaveryRoom == null) continue;

            // Проверяем, отключена ли локация НПС
            bool isInactive = !npc.gameObject.activeInHierarchy || !npc.currentSlaveryRoom.gameObject.activeInHierarchy;
            if (!isInactive) continue;

            float lockDuration = npc.currentSlaveryRoom.lockPickAttemptDuration;
            float teleportDuration = 3.0f;
            float finalRunDuration = 10.0f;
            float totalDuration = lockDuration + teleportDuration + finalRunDuration;

            if (npc.isBackgroundEscaping)
            {
                float elapsed = Time.time - npc.backgroundEscapeStartTime;
                if (elapsed >= totalDuration)
                {
                    Debug.Log($"[{npc.name}] Успешно сбежал в фоновом режиме пока игрока не было на локации!");
                    allNPCs.RemoveAt(i);
                    Destroy(npc.gameObject);
                }
            }
            else
            {
                // Проверяем интервал побега при неактивной зоне
                if (Time.time >= npc.escapeCheckTimer)
                {
                    npc.escapeCheckTimer = Time.time + npc.currentSlaveryRoom.escapeCheckInterval;

                    if (Random.value <= npc.currentSlaveryRoom.escapeChance)
                    {
                        npc.isBackgroundEscaping = true;
                        npc.backgroundEscapeStartTime = Time.time;
                        Debug.Log($"[{npc.name}] Запустил фоновый побег в неактивной зоне рабства!");
                    }
                }
            }
        }
    }
}
