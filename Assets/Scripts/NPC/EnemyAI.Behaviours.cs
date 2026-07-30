using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public partial class EnemyAI : MonoBehaviour
{
    // ── Behaviours ────────────────────────────────────────────────────────────
    private void BehaviourFriendly()
    {
        currentState = NPCState.Roam;
        agent.speed  = walkSpeed;
        if (alertLevel == AlertLevel.Detected && !wasVisible)
            StartCoroutine(SpottingReaction(false)); // Оборачивается, но не агрессирует
        Roam();
    }

    private void BehaviourNeutral(float dist)
    {
        agent.updateRotation = true;
        if (alertLevel <= AlertLevel.Suspicious) { currentState = NPCState.Roam; agent.speed = walkSpeed; Roam(); return; }
        if (currentTarget == null) return;

        if (dist < neutralPersonalSpace)
        {
            currentState = NPCState.KeepAway; agent.speed = sneakSpeed; agent.isStopped = false;
            MoveAwayFrom(currentTarget.position, neutralPersonalSpace + 2f);
        }
        else if (dist < neutralAlertRange)
        {
            currentState = NPCState.Idle; agent.isStopped = true; agent.updateRotation = false;
            FaceToward(currentTarget.position);
        }
        else { currentState = NPCState.Roam; agent.speed = walkSpeed; agent.isStopped = false; Roam(); }
    }

    private void BehaviourAggressive(float dist)
    {
        agent.updateRotation = true;

        switch (alertLevel)
        {
            case AlertLevel.Unaware:
                currentState = NPCState.Roam; agent.speed = walkSpeed; Roam();
                break;

            case AlertLevel.Suspicious:
                // Идёт проверить подозрительное место
                currentState = NPCState.Investigate; agent.speed = walkSpeed; agent.isStopped = false;
                if (Time.time >= nextPathResetTime)
                {
                    agent.SetDestination(lastKnownPos);
                }
                if (agent.remainingDistance <= agent.stoppingDistance + 0.5f && investigateCo == null)
                    investigateCo = StartCoroutine(LookAround(lastKnownPos));
                break;

            case AlertLevel.Alert:
                // Потерял — ищет по памяти
                currentState = NPCState.Investigate; agent.speed = walkSpeed * 1.2f; agent.isStopped = false;
                if (Time.time >= nextPathResetTime)
                {
                    if ((Time.time - lastSeenTime) < memoryDuration) agent.SetDestination(lastKnownPos);
                    else { currentState = NPCState.Roam; Roam(); }
                }
                break;

            case AlertLevel.Detected:
                if (currentTarget == null) { currentState = NPCState.Roam; agent.speed = walkSpeed; Roam(); break; }
                // Реакция при первом обнаружении
                if (!wasVisible && !reactionPlaying) { StartCoroutine(SpottingReaction(true)); return; }

                if (dist <= attackRange)
                {
                    currentState = NPCState.Attack; agent.isStopped = true; agent.updateRotation = false;
                    FaceToward(currentTarget.position);
                    if (Time.time >= nextAttackTime)
                    {
                        // Атака тратит стамину; если энергии нет — пропускаем удар
                        if (UseStamina(attackStaminaDrain))
                        {
                            nextAttackTime = Time.time + 1.6f;
                            PerformAttack();
                        }
                        else
                        {
                            // Нет сил — коротко отступаем
                            nextAttackTime = Time.time + 0.5f;
                        }
                    }
                }
                else
                {
                    currentState = NPCState.Chase; agent.isStopped = false;
                    // При нехватке стамины преследует шагом
                    agent.speed  = RunSpeed(); agent.updateRotation = true;
                    if (Time.time >= nextPathResetTime)
                    {
                        agent.SetDestination(currentTarget.position);
                    }
                }
                break;
        }
    }

    private void BehaviourScared(float dist)
    {
        agent.updateRotation = true;
        switch (alertLevel)
        {
            case AlertLevel.Unaware:
                currentState = NPCState.Roam; agent.speed = walkSpeed; Roam();
                break;

            case AlertLevel.Suspicious:
                // Слышит что-то — ОСТОРОЖНО пятится, не замирает
                currentState    = NPCState.Flee;
                agent.isStopped = false;
                agent.speed     = sneakSpeed;
                agent.updateRotation = true;
                // Обновляем путь раз в секунду чтобы не спамить NavMesh
                fleeRefreshTimer -= Time.deltaTime;
                if (fleeRefreshTimer <= 0f)
                {
                    fleeRefreshTimer = 1f;
                    MoveAwayFrom(lastKnownPos, neutralPersonalSpace + 3f);
                }
                break;

            case AlertLevel.Alert:
                // Потерял из вида но ещё напуган — быстро уходит
                currentState    = NPCState.Flee;
                agent.isStopped = false;
                agent.speed     = RunSpeed();
                fleeRefreshTimer -= Time.deltaTime;
                if (fleeRefreshTimer <= 0f)
                {
                    fleeRefreshTimer = 1.2f;
                    MoveAwayFrom(lastKnownPos, visionRange * 1.2f);
                }
                break;

            case AlertLevel.Detected:
                // ПАНИКА — без реакции, сразу бежим!
                currentState    = NPCState.Flee;
                agent.isStopped = false;
                agent.speed     = RunSpeed(); // учитываем стамину
                fleeRefreshTimer -= Time.deltaTime;
                if (fleeRefreshTimer <= 0f)
                {
                    fleeRefreshTimer = 0.6f;
                    Transform danger = currentTarget != null ? currentTarget : player;
                    if (danger != null)
                    {
                        MoveAwayFrom(danger.position, visionRange * 2f);
                    }
                }
                break;
        }
    }

    private void BehaviourSubmissive(float dist)
    {
        agent.updateRotation = true;
        if (!isFollowing) { currentState = NPCState.Roam; agent.speed = walkSpeed; Roam(); return; }

        if (dist > followMaxRange) { currentState = NPCState.Idle; agent.isStopped = true; return; }

        if (dist > followDistance + 0.8f)
        {
            currentState = NPCState.Follow; agent.isStopped = false;
            agent.speed  = dist > followDistance * 2.5f ? runSpeed : walkSpeed;
            if (Time.time >= nextPathResetTime)
            {
                Vector3 behind = player.position - player.forward * followDistance;
                agent.SetDestination(behind);
            }
        }
        else
        {
            currentState = NPCState.Idle; agent.isStopped = true; agent.updateRotation = false;
            FaceToward(player.position);
        }
    }

    private void HandleFollowInput()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (Input.GetKeyDown(cachedFollowKey) && dist <= hearingRange * 1.5f)
        {
            isFollowing = !isFollowing;
            if (animator != null) animator.SetBool(H_IsFollowing, isFollowing);
            Debug.Log(isFollowing ? $"[{name}] Следует за игроком" : $"[{name}] Свободен");
        }
    }

    // ── Shared Utilities ──────────────────────────────────────────────────────
    private Vector3 GetNextRoamDestination()
    {
        // 1. Выбираем из точек интереса (patrolPoints)
        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            List<Transform> validPoints = new List<Transform>();
            foreach (var pt in patrolPoints)
            {
                if (pt == null) continue;
                if (pt == lastVisitedPoint) continue;

                float dist = Vector3.Distance(transform.position, pt.position);
                if (dist >= minRoamDistance)
                {
                    validPoints.Add(pt);
                }
            }

            // Если все точки слишком близко или это была последняя посещенная, берем любую кроме последней
            if (validPoints.Count == 0)
            {
                foreach (var pt in patrolPoints)
                {
                    if (pt != null && pt != lastVisitedPoint)
                    {
                        validPoints.Add(pt);
                    }
                }
            }

            if (validPoints.Count > 0)
            {
                Transform chosen = validPoints[Random.Range(0, validPoints.Count)];
                lastVisitedPoint = chosen;

                // Добавляем случайный разброс вокруг точки патруля, чтобы все НПС не шли в одну координату
                Vector3 randomDir = Random.insideUnitSphere * 1.5f;
                randomDir.y = 0f;
                Vector3 targetPos = chosen.position + randomDir;

                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    return hit.position;
                }

                return chosen.position;
            }
        }

        // 2. Фаллбек на случайный поиск по NavMesh
        Vector3 center = roamAroundStart ? startPosition : transform.position;
        for (int i = 0; i < 15; i++)
        {
            Vector3 rnd = center + Random.insideUnitSphere * roamRadius;
            rnd.y = transform.position.y;

            float dist = Vector3.Distance(transform.position, rnd);
            if (dist >= minRoamDistance)
            {
                if (NavMesh.SamplePosition(rnd, out NavMeshHit h, roamRadius * 0.5f, NavMesh.AllAreas))
                {
                    return h.position;
                }
            }
        }

        // Если не удалось найти точку дальше minRoamDistance, пробуем любой радиус вокруг центра
        Vector3 fallbackRnd = center + Random.insideUnitSphere * roamRadius;
        fallbackRnd.y = transform.position.y;
        if (NavMesh.SamplePosition(fallbackRnd, out NavMeshHit hit2, roamRadius, NavMesh.AllAreas))
        {
            return hit2.position;
        }

        return transform.position;
    }

    private void Roam()
    {
        if (agent.pathPending) return;

        bool hasArrived = (agent.remainingDistance <= agent.stoppingDistance + 0.8f);
        bool isStuckOnOther = (agent.hasPath && agent.velocity.sqrMagnitude < 0.05f && agent.remainingDistance < 2.5f);

        if (hasArrived || isStuckOnOther)
        {
            if (!isRoamWaiting)
            {
                // Прибыли на точку или застряли об соседа/препятствие
                isRoamWaiting = true;
                nextRoamTime = Time.time + Random.Range(idlePauseMin, idlePauseMax);
                agent.isStopped = true;

                // С шансом 60% оглядываемся
                if (Random.value < 0.6f && animator != null)
                {
                    animator.SetTrigger(H_LookAround);
                }
            }

            // Ждем завершения таймера
            if (Time.time >= nextRoamTime)
            {
                Vector3 dest = GetNextRoamDestination();
                if (dest != transform.position)
                {
                    isRoamWaiting = false;
                    agent.isStopped = false;
                    agent.SetDestination(dest);
                }
            }
        }
        else
        {
            // Мы в пути
            agent.isStopped = false;
            isRoamWaiting = false;
        }
    }

    private void MoveAwayFrom(Vector3 source, float distance)
    {
        Vector3 dir = (transform.position - source).normalized;
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = Random.onUnitSphere;
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward; // последний fallback

        // Ищем точку с уменьшающимся радиусом — гарантируем попадание на NavMesh
        float[] radii = { distance, distance * 0.6f, distance * 0.3f, 5f };
        float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 180f };

        foreach (float r in radii)
        {
            foreach (float a in angles)
            {
                Vector3 rotDir  = Quaternion.Euler(0f, a, 0f) * dir;
                Vector3 target  = transform.position + rotDir.normalized * r;
                float sampleR   = Mathf.Max(r * 0.4f, 3f);
                if (NavMesh.SamplePosition(target, out NavMeshHit h, sampleR, NavMesh.AllAreas))
                {
                    agent.SetDestination(h.position);
                    return;
                }
            }
        }
    }

    private void PerformAttack()
    {
        if (animator != null)
        {
            animator.SetInteger(H_AttackIdx, Random.Range(0, 3));
            animator.ResetTrigger(H_Attack);
            animator.SetTrigger(H_Attack);
        }
        
        if (currentTarget == player)
        {
            playerStats?.TakeDamage(damage);
        }
        else if (targetNPC != null)
        {
            targetNPC.TakeDamage(damage, this.transform);
        }
    }

    private void DoIdleActions()
    {
        if (alertLevel != AlertLevel.Unaware) return;
        if (Time.time < nextIdleActionTime) return;
        nextIdleActionTime = Time.time + Random.Range(idleActionInterval * 0.7f, idleActionInterval * 1.3f);
        if (animator != null) animator.SetTrigger(H_LookAround);
    }

    // ── Coroutines ────────────────────────────────────────────────────────────
    private IEnumerator SpottingReaction(bool isHostile)
    {
        reactionPlaying = true;
        agent.isStopped = true;
        float t = 0f;
        float dur = isHostile ? 0.35f : 0.5f;
        Transform rotTarget = currentTarget != null ? currentTarget : player;
        while (t < dur)
        {
            if (rotTarget != null) FaceToward(rotTarget.position);
            t += Time.deltaTime;
            yield return null;
        }
        agent.isStopped = false;
        reactionPlaying = false;
    }

    private IEnumerator LookAround(Vector3 pos)
    {
        agent.isStopped = true;
        if (animator != null) animator.SetTrigger(H_LookAround);
        float elapsed = 0f;
        // Медленно озирается по сторонам
        float[] angles = { 45f, -90f, 45f };
        foreach (float a in angles)
        {
            Quaternion target = transform.rotation * Quaternion.Euler(0f, a, 0f);
            while (elapsed < 0.8f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 3f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            elapsed = 0f;
            yield return new WaitForSeconds(0.3f);
        }
        agent.isStopped = false;
        investigateCo   = null;
    }

    private IEnumerator TemporaryFlee(float dur, Transform fleeFrom)
    {
        NPCPersonality saved = personality;
        personality = NPCPersonality.Scared;
        alertLevel  = AlertLevel.Detected;
        if (fleeFrom != null)
        {
            currentTarget = fleeFrom;
            targetNPC = fleeFrom.GetComponent<EnemyAI>();
            lastKnownPos = fleeFrom.position;
            lastSeenTime = Time.time;
        }
        yield return new WaitForSeconds(dur);
        if (currentState != NPCState.Dead)
        {
            personality = saved;
            ResetTarget();
        }
        fleeCo = null;
    }
}
