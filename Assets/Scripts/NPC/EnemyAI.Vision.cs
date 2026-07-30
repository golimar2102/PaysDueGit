using UnityEngine;

public partial class EnemyAI : MonoBehaviour
{
    // ── Vision ────────────────────────────────────────────────────────────────
    private void UpdateVision()
    {
        if (currentTarget != null && IsTargetDead())
        {
            ResetTarget();
        }

        Transform bestTarget = ChooseTarget();
        if (bestTarget != currentTarget)
        {
            bool keepCurrent = false;
            if (currentTarget != null && bestTarget == null)
            {
                keepCurrent = (Time.time - lastSeenTime) < memoryDuration;
            }

            if (!keepCurrent)
            {
                currentTarget = bestTarget;
                targetNPC = currentTarget != null ? currentTarget.GetComponent<EnemyAI>() : null;

                if (currentTarget == null)
                {
                    alertLevel = AlertLevel.Unaware;
                    suspicionTimer = 0f;
                }
            }
        }

        if (currentTarget != null)
        {
            targetVisible = CheckSight(currentTarget);
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            targetHeard = dist <= hearingRange;

            if (targetVisible || targetHeard)
            {
                lastKnownPos = currentTarget.position;
                lastSeenTime = Time.time;
            }
        }
        else
        {
            targetVisible = false;
            targetHeard = false;
        }

        playerVisible = CheckSight(player);
        playerHeard = player != null && Vector3.Distance(transform.position, player.position) <= hearingRange;
    }

    private bool CheckSight(Transform target)
    {
        if (target == null) return false;
        Vector3 eye      = eyePoint.position;
        float eyeOffset = target.CompareTag("Player") ? 0.9f : 1.0f;
        Vector3 targetPos   = target.position + Vector3.up * eyeOffset;
        Vector3 toTarget = targetPos - eye;
        float   dist     = toTarget.magnitude;

        if (dist <= peripheralRange) return true;

        if (dist > visionRange) return false;

        if (Vector3.Angle(transform.forward, toTarget) > fovAngle * 0.5f) return false;

        if (Physics.Raycast(eye, toTarget.normalized, dist - 0.15f, obstacleMask)) return false;

        return true;
    }

    // ── Alert Level ───────────────────────────────────────────────────────────
    private void UpdateAlertLevel()
    {
        if (currentTarget != null && targetVisible)
        {
            suspicionTimer += Time.deltaTime;
            if (suspicionTimer >= suspicionToDetectTime || Vector3.Distance(transform.position, currentTarget.position) <= peripheralRange)
                alertLevel = AlertLevel.Detected;
            else if (alertLevel == AlertLevel.Unaware)
                alertLevel = AlertLevel.Suspicious;
        }
        else if (currentTarget != null && targetHeard && alertLevel == AlertLevel.Unaware)
        {
            alertLevel = AlertLevel.Suspicious;
            suspicionTimer = 0f;
        }
        else
        {
            suspicionTimer = Mathf.Max(0f, suspicionTimer - Time.deltaTime * 0.5f);
            bool hasMem = (Time.time - lastSeenTime) < memoryDuration;
            if (!hasMem) alertLevel = AlertLevel.Unaware;
            else if (alertLevel == AlertLevel.Detected) alertLevel = AlertLevel.Alert;
        }
    }

    private bool CanSeeOrHear(Transform target)
    {
        if (target == null) return false;
        
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= peripheralRange) return true;
        
        if (dist <= hearingRange) return true;
        
        if (dist <= visionRange)
        {
            Vector3 eye = eyePoint.position;
            float eyeOffset = target.CompareTag("Player") ? 0.9f : 1.0f;
            Vector3 targetPos = target.position + Vector3.up * eyeOffset;
            Vector3 toTarget = targetPos - eye;
            
            if (Vector3.Angle(transform.forward, toTarget) <= fovAngle * 0.5f)
            {
                if (!Physics.Raycast(eye, toTarget.normalized, dist - 0.15f, obstacleMask))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private Transform ChooseTarget()
    {
        Transform attacker = null;
        if (lastAttacker != null && (Time.time - lastAttackedTime < memoryDuration))
        {
            bool attackerAlive = false;
            if (lastAttacker == player)
            {
                attackerAlive = playerStats != null && !playerStats.IsDead;
            }
            else
            {
                EnemyAI attNPC = lastAttacker.GetComponent<EnemyAI>();
                attackerAlive = attNPC != null && attNPC.currentState != NPCState.Dead;
            }

            if (attackerAlive && CanSeeOrHear(lastAttacker))
            {
                attacker = lastAttacker;
            }
        }

        if (currentTarget != null && !IsTargetDead())
        {
            if (attacker != null && currentTarget != attacker)
            {
                return attacker;
            }

            if (CanSeeOrHear(currentTarget))
            {
                return currentTarget;
            }
        }

        if (attacker != null)
        {
            return attacker;
        }

        Transform closestTarget = null;
        float closestDist = float.MaxValue;

        if (player != null && playerStats != null && !playerStats.IsDead)
        {
            if (personality == NPCPersonality.Aggressive || personality == NPCPersonality.Scared)
            {
                if (CanSeeOrHear(player))
                {
                    float dist = Vector3.Distance(transform.position, player.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestTarget = player;
                    }
                }
            }
        }

        if (personality == NPCPersonality.Aggressive)
        {
            foreach (var npc in allNPCs)
            {
                if (npc == null || npc == this || npc.currentState == NPCState.Dead) continue;

                if (npc.factionID != this.factionID)
                {
                    if (CanSeeOrHear(npc.transform))
                    {
                        float dist = Vector3.Distance(transform.position, npc.transform.position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestTarget = npc.transform;
                        }
                    }
                }
            }
        }

        return closestTarget;
    }

    public bool IsTargetDead()
    {
        if (currentTarget == null) return true;
        if (currentTarget == player)
        {
            return playerStats != null && playerStats.IsDead;
        }
        if (targetNPC != null)
        {
            return targetNPC.currentState == NPCState.Dead;
        }
        return true;
    }
}
