using UnityEngine;
using System.Collections;
using UnityEngine.Localization;

public enum NPCType
{
    Canine,
    Feline,
    Beef,
    Pork,
    Avian
}

public class NPCCorpse : MonoBehaviour
{
    public static NPCCorpse carriedCorpse = null;

    [Header("Тип NPC")]
    public NPCType npcType = NPCType.Canine;

    [HideInInspector]
    public ButcheringTableController currentTable = null;

    [HideInInspector]
    public IndustrialMeatGrinder currentGrinder = null;

    [Header("Состояние разделки")]
    public bool isChestCut = false;
    public bool isChestSpread = false;
    public bool isButchered = false;

    [HideInInspector]
    public System.Collections.Generic.List<string> extractedOrganNames = new System.Collections.Generic.List<string>();

    [Header("Локализация")]
    public LocalizedString localizedPrompt;
    public string defaultPrompt = "Подобрать тело";

    [Header("Настройки удержания в руках")]
    [Tooltip("Позиция трупа в руках относительно камеры")]
    public Vector3 holdPositionOffset = new Vector3(0f, -0.6f, 1.5f);
    [Tooltip("Поворот трупа в руках относительно камеры")]
    public Vector3 holdRotationOffset = new Vector3(0f, 90f, 0f);
    [Tooltip("Масштаб трупа в руках")]
    public Vector3 holdScaleOffset = Vector3.one;

    [Header("Настройки броска")]
    [Tooltip("Сила броска вперед")]
    public float throwForwardForce = 3f;
    [Tooltip("Сила броска вверх")]
    public float throwUpwardForce = 1f;

    [Header("Настройки коллайдера при смерти")]
    public bool adjustColliderOnDeath = true;
    [Tooltip("0 = X-ось, 1 = Y-ось, 2 = Z-ось")]
    public int deadColliderDirection = 2; 
    public Vector3 deadColliderCenter = new Vector3(0f, 0.2f, 0f);
    public float deadColliderHeight = 1.8f;
    public float deadColliderRadius = 0.4f;

    [Header("Настройки разделки туш")]
    [Tooltip("Дополнительные высокополигональные модели, которые включаются только при разделке на столе")]
    public GameObject[] extraButcheringModels;

    public string promptText
    {
        get
        {
            if (localizedPrompt != null && !localizedPrompt.IsEmpty)
                return localizedPrompt.GetLocalizedString();
            return defaultPrompt;
        }
    }

    private Collider[] colliders;
    private Rigidbody rb;
    private EnemyDeathEffects deathEffects;
    [HideInInspector] public Transform originalParent;
    private Outline outline;
    private Vector3 originalScale;

    private Vector3 GetSafeDeadColliderCenter()
    {
        Vector3 center = deadColliderCenter;
        if (deadColliderDirection == 0 || deadColliderDirection == 2)
        {
            float minCenterY = deadColliderRadius;
            if (center.y < minCenterY)
            {
                center.y = minCenterY;
            }
        }
        return center;
    }

    public void InitializeCorpse()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; 

        deathEffects = GetComponent<EnemyDeathEffects>();
        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        SetExtraButcheringModelsActive(false);

        EnemyAI enemyAI = GetComponent<EnemyAI>();
        bool isDeadOrStunned = enemyAI == null || enemyAI.currentState == EnemyAI.NPCState.Dead || enemyAI.currentState == EnemyAI.NPCState.Stunned;

        // Поворачиваем коллайдер лежачего трупа ТОЛЬКО если мертв или оглушен
        if (adjustColliderOnDeath && isDeadOrStunned)
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = deadColliderDirection;
                capsule.center = GetSafeDeadColliderCenter();
                capsule.height = deadColliderHeight;
                capsule.radius = deadColliderRadius;
            }
        }
    }

    void Start()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        rb = GetComponent<Rigidbody>();
        deathEffects = GetComponent<EnemyDeathEffects>();
        outline = GetComponent<Outline>();
        SetExtraButcheringModelsActive(false);
    }

    void Update()
    {
        if (carriedCorpse == this)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                Drop();
            }
        }
    }

    void LateUpdate()
    {
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null && enemyAI.currentState != EnemyAI.NPCState.Dead && enemyAI.currentState != EnemyAI.NPCState.Stunned)
        {
            return;
        }

        if (currentTable == null)
        {
            if (isChestSpread || isButchered)
            {
                SetExtraButcheringModelsActive(true);
                SetBlendShapeWeight("TorsoOpen", 100f);
            }
            else if (isChestCut)
            {
                SetExtraButcheringModelsActive(true);
                SetBlendShapeWeight("TorsoOpen", 30f);
            }
            else
            {
                SetExtraButcheringModelsActive(false);
                SetBlendShapeWeight("TorsoOpen", 0f);
            }

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = deadColliderDirection;
                capsule.center = GetSafeDeadColliderCenter();
                capsule.height = deadColliderHeight;
                capsule.radius = deadColliderRadius;
            }
        }
        else
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = deadColliderDirection;
                capsule.center = GetSafeDeadColliderCenter();
                capsule.height = deadColliderHeight;
                capsule.radius = deadColliderRadius;
            }
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null) outline.enabled = isHighlighted;
    }

    public void PickUp(GameObject player)
    {
        if (carriedCorpse != null) return;
        carriedCorpse = this;

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.UnequipAll();
        }
        if (InventoryManager.Instance != null)
        {
            if (InventoryManager.Instance.equippedItemNameText != null)
            {
                InventoryManager.Instance.equippedItemNameText.text = "";
            }
        }

        // Останавливаем таймер исчезновения трупа
        if (deathEffects != null)
        {
            deathEffects.PauseCorpseLifetime(true);
        }

        // Сохраняем родителя и масштаб
        originalParent = transform.parent;
        originalScale = transform.localScale;

        // Крепим к камере игрока
        Transform holdPoint = GetOrCreateHoldPoint(player);
        transform.SetParent(holdPoint);
        transform.localPosition = holdPositionOffset;
        transform.localRotation = Quaternion.Euler(holdRotationOffset);
        transform.localScale = holdScaleOffset;

        // Отключаем физику и коллизии
        rb.isKinematic = true;
        rb.useGravity = false;

        SetCollidersTriggerState(true);
        SetCollidersEnabled(false);
    }

    public void Drop()
    {
        if (carriedCorpse != this) return;

        // Отсоединяем от камеры
        transform.SetParent(originalParent);
        transform.localScale = originalScale;

        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        // Гарантируем спавн над землей/террейном
        Vector3 currentPos = transform.position;
        if (Physics.Raycast(currentPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit groundHit, 5.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            float minSafeY = groundHit.point.y + 0.4f;
            if (currentPos.y < minSafeY)
            {
                currentPos.y = minSafeY;
                transform.position = currentPos;
            }
        }

        // Запускаем таймер исчезновения заново
        if (deathEffects != null)
        {
            deathEffects.PauseCorpseLifetime(false);
        }

        SetCollidersEnabled(true);
        SetCollidersTriggerState(false);

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.direction = deadColliderDirection;
            capsule.center = GetSafeDeadColliderCenter();
            capsule.height = deadColliderHeight;
            capsule.radius = deadColliderRadius;
        }

        IgnorePlayerAndNPCObjects(true);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        GameObject player = null;
        Camera cam = null;
        if (PlayerInteract.Instance != null)
        {
            player = PlayerInteract.Instance.gameObject;
            cam = PlayerInteract.Instance.playerCamera;
        }
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (player != null && cam != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.linearDamping = 0f; // Убираем сопротивление воздуха

            Vector3 forceDirection = cam.transform.forward * throwForwardForce + Vector3.up * throwUpwardForce;
            rb.AddForce(forceDirection, ForceMode.VelocityChange);
        }
        

        StartCoroutine(WaitForLanding());

        StartCoroutine(ClearCarriedCorpseAtEndOfFrame());
    }

    private IEnumerator ClearCarriedCorpseAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        if (carriedCorpse == this)
        {
            carriedCorpse = null;
        }
    }

    private IEnumerator WaitForLanding()
    {
        yield return new WaitForSeconds(0.4f);

        float timeout = 5f;
        float elapsed = 0f;

        // Ждем пока тело реально приземлится и остановится
        while (rb != null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            bool isNearGround = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore);
            bool isSlow = rb.linearVelocity.sqrMagnitude < 0.1f;

            if (isNearGround && isSlow)
            {
                break;
            }

            yield return null;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        SetCollidersTriggerState(true);

        // Возвращаем физические столкновения
        IgnorePlayerAndNPCObjects(false);
    }

    private Transform GetOrCreateHoldPoint(GameObject player)
    {
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;

        Transform holdPoint = cam.transform.Find("CorpseHoldPoint");
        if (holdPoint == null)
        {
            GameObject go = new GameObject("CorpseHoldPoint");
            holdPoint = go.transform;
            holdPoint.SetParent(cam.transform);
            holdPoint.localPosition = Vector3.zero;
            holdPoint.localRotation = Quaternion.identity;
        }
        return holdPoint;
    }

    private void SetCollidersTriggerState(bool isTrigger)
    {
        Collider[] myColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in myColliders)
        {
            if (col != null) col.isTrigger = isTrigger;
        }
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        Collider[] myColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in myColliders)
        {
            if (col != null) col.enabled = isEnabled;
        }
    }

    private void IgnorePlayerAndNPCObjects(bool ignore)
    {
        Collider[] myColliders = GetComponentsInChildren<Collider>(true);
        if (myColliders == null || myColliders.Length == 0) return;

        // Игнорируем коллайдеры игрока
        GameObject player = null;
        if (PlayerInteract.Instance != null)
        {
            player = PlayerInteract.Instance.gameObject;
        }
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player != null)
        {
            Transform playerRoot = player.transform.root;
            Collider[] playerColliders = playerRoot.GetComponentsInChildren<Collider>(true);
            CharacterController playerCC = playerRoot.GetComponentInChildren<CharacterController>(true);

            foreach (var col in myColliders)
            {
                if (col == null) continue;

                if (playerCC != null)
                {
                    Physics.IgnoreCollision(col, playerCC, ignore);
                }

                foreach (var pCol in playerColliders)
                {
                    if (pCol != null && pCol != col)
                        Physics.IgnoreCollision(col, pCol, ignore);
                }
            }
        }

        // Игнорируем коллайдеры других NPC
        EnemyAI[] npcControllers = FindObjectsOfType<EnemyAI>(true);
        foreach (var npc in npcControllers)
        {
            if (npc != null && npc.gameObject != this.gameObject)
            {
                Transform npcRoot = npc.transform.root;
                Collider[] npcColliders = npcRoot.GetComponentsInChildren<Collider>(true);
                CharacterController npcCC = npcRoot.GetComponentInChildren<CharacterController>(true);

                foreach (var col in myColliders)
                {
                    if (col == null) continue;

                    if (npcCC != null)
                    {
                        Physics.IgnoreCollision(col, npcCC, ignore);
                    }

                    foreach (var nCol in npcColliders)
                    {
                        if (nCol != null && nCol != col)
                            Physics.IgnoreCollision(col, nCol, ignore);
                    }
                }
            }
        }
    }

    public void SetExtraButcheringModelsActive(bool active)
    {
        if (extraButcheringModels != null)
        {
            foreach (var m in extraButcheringModels)
            {
                if (m != null)
                {
                    m.SetActive(active);
                }
            }
        }

        Transform torsoInside = FindChildRecursive(transform, "TorsoInside");
        if (torsoInside != null)
        {
            torsoInside.gameObject.SetActive(active);
        }
    }

    private Transform FindChildRecursive(Transform parent, string nameToFind)
    {
        if (parent.name == nameToFind) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), nameToFind);
            if (result != null) return result;
        }
        return null;
    }

    public void SetBlendShapeWeight(string shapeName, float weight)
    {
        Transform torsoBody = FindChildRecursive(transform, "TorsoBody");
        if (torsoBody != null)
        {
            SkinnedMeshRenderer smr = torsoBody.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                int blendShapeCount = smr.sharedMesh.blendShapeCount;
                for (int i = 0; i < blendShapeCount; i++)
                {
                    string nameInMesh = smr.sharedMesh.GetBlendShapeName(i);
                    if (nameInMesh.Equals(shapeName, System.StringComparison.OrdinalIgnoreCase) || 
                        nameInMesh.EndsWith("." + shapeName, System.StringComparison.OrdinalIgnoreCase) ||
                        nameInMesh.EndsWith("_" + shapeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        smr.SetBlendShapeWeight(i, weight);
                        return;
                    }
                }
            }
        }

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in renderers)
        {
            if (smr != null && smr.sharedMesh != null)
            {
                int blendShapeCount = smr.sharedMesh.blendShapeCount;
                for (int i = 0; i < blendShapeCount; i++)
                {
                    string nameInMesh = smr.sharedMesh.GetBlendShapeName(i);
                    if (nameInMesh.Equals(shapeName, System.StringComparison.OrdinalIgnoreCase) || 
                        nameInMesh.EndsWith("." + shapeName, System.StringComparison.OrdinalIgnoreCase) ||
                        nameInMesh.EndsWith("_" + shapeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        smr.SetBlendShapeWeight(i, weight);
                    }
                }
            }
        }
    }
}

