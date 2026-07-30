using UnityEngine;

[DisallowMultipleComponent]
public class TerraformTool : MonoBehaviour
{
    private const int MaxRayHits = 32;
    private const float VolumeCacheRefreshInterval = 1f;

    [Header("Input")]
    public KeyCode digKey = KeyCode.Mouse0;
    public KeyCode addKey = KeyCode.Mouse1;
    public bool blockWhenInventoryOrDialogueOpen = true;

    [Header("Brush")]
    public float range = 8f;
    [Min(0.05f)] public float brushRadius = 1.4f;
    [Range(0f, 1f)] public float hardness = 1f;
    [Min(0.01f)] public float repeatRate = 0.06f;
    [Range(0f, 1f)] public float surfaceOffset = 0.45f;
    public bool continuousStroke = true;
    [Min(0.1f)] public float maxStrokeSegmentLength = 12f;
    public bool autoExpandRangeForLargeBrush = true;
    [Min(0f)] public float rayBackstep = 0.75f;
    public LayerMask terraformMask = ~0;
    public bool ignoreOwnerColliders = true;

    [Header("Volume Raycast")]
    public bool useVolumeRaycast = true;
    public TerraformVolume targetVolume;
    [Min(0.01f)] public float volumeRayStep = 0.15f;
    [Min(1)] public int volumeRayBinarySteps = 7;

    [Header("References")]
    public Camera playerCamera;
    public Animator animator;
    public string useAnimationTrigger = "Dig";
    public AudioSource useSound;

    [Header("Debug")]
    public bool drawBrushGizmo = true;

    private float nextUseTime;
    private Vector3 lastBrushCenter;
    private bool hasLastBrushCenter;
    private bool hasStrokeCenter;
    private Vector3 previousStrokeCenter;
    private TerraformOperation previousStrokeOperation;
    private readonly RaycastHit[] rayHits = new RaycastHit[MaxRayHits];
    private TerraformVolume[] cachedVolumes;
    private float nextVolumeCacheRefreshTime;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        RefreshVolumeCache();
    }

    private void Update()
    {
        if (ShouldBlockInput())
        {
            hasLastBrushCenter = false;
            return;
        }

        bool wantsDig = Input.GetKey(digKey);
        bool wantsAdd = Input.GetKey(addKey);

        if (!wantsDig && !wantsAdd)
        {
            hasLastBrushCenter = false;
            hasStrokeCenter = false;
            return;
        }

        if (Time.time < nextUseTime)
        {
            return;
        }

        TerraformOperation operation = wantsAdd ? TerraformOperation.Add : TerraformOperation.Subtract;

        if (TryGetBrushTarget(operation, out TerraformVolume volume, out Vector3 brushCenter))
        {
            bool canUseStroke = continuousStroke &&
                                hasStrokeCenter &&
                                previousStrokeOperation == operation &&
                                Vector3.Distance(previousStrokeCenter, brushCenter) <= maxStrokeSegmentLength;

            if (canUseStroke)
            {
                volume.ModifyCapsule(previousStrokeCenter, brushCenter, brushRadius, operation, hardness);
            }
            else
            {
                volume.ModifySphere(brushCenter, brushRadius, operation, hardness);
            }

            previousStrokeCenter = brushCenter;
            previousStrokeOperation = operation;
            hasStrokeCenter = true;
            lastBrushCenter = brushCenter;
            hasLastBrushCenter = true;
            nextUseTime = Time.time + repeatRate;

            if (useSound != null)
            {
                useSound.Play();
            }

            if (animator != null && !string.IsNullOrEmpty(useAnimationTrigger))
            {
                animator.SetTrigger(useAnimationTrigger);
            }
        }
        else
        {
            hasLastBrushCenter = false;
            hasStrokeCenter = false;
        }
    }

    private bool ShouldBlockInput()
    {
        if (!blockWhenInventoryOrDialogueOpen)
        {
            return false;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            return true;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive)
        {
            return true;
        }

        return false;
    }

    private bool TryGetBrushTarget(TerraformOperation operation, out TerraformVolume volume, out Vector3 brushCenter)
    {
        volume = null;
        brushCenter = Vector3.zero;

        if (playerCamera == null)
        {
            return false;
        }

        Vector3 direction = playerCamera.transform.forward.normalized;
        Ray ray = new Ray(playerCamera.transform.position - (direction * rayBackstep), direction);
        float castRange = range + rayBackstep;

        if (autoExpandRangeForLargeBrush)
        {
            castRange = Mathf.Max(castRange, (brushRadius * 2.5f) + rayBackstep);
        }

        if (useVolumeRaycast && TryGetVolumeRaycastTarget(ray, castRange, operation, out volume, out brushCenter))
        {
            return true;
        }

        if (!TryGetTerraformHit(ray, castRange, out RaycastHit hit, out volume))
        {
            return false;
        }

        float offset = brushRadius * surfaceOffset;
        brushCenter = operation == TerraformOperation.Subtract
            ? hit.point + (direction * offset)
            : hit.point - (direction * offset);

        return true;
    }

    private bool TryGetVolumeRaycastTarget(
        Ray ray,
        float castRange,
        TerraformOperation operation,
        out TerraformVolume volume,
        out Vector3 brushCenter)
    {
        volume = null;
        brushCenter = Vector3.zero;

        float bestDistance = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;

        if (targetVolume != null)
        {
            if (!TryRaycastSingleVolume(targetVolume, ray, castRange, out Vector3 point, out float distance))
            {
                return false;
            }

            volume = targetVolume;
            bestPoint = point;
            bestDistance = distance;
        }
        else
        {
            TerraformVolume[] volumes = GetCachedVolumes();

            for (int i = 0; i < volumes.Length; i++)
            {
                TerraformVolume candidate = volumes[i];
                if (candidate == null)
                {
                    continue;
                }

                if (TryRaycastSingleVolume(candidate, ray, castRange, out Vector3 point, out float distance) &&
                    distance < bestDistance)
                {
                    volume = candidate;
                    bestPoint = point;
                    bestDistance = distance;
                }
            }
        }

        if (volume == null)
        {
            return false;
        }

        float offset = brushRadius * surfaceOffset;
        brushCenter = operation == TerraformOperation.Subtract
            ? bestPoint + (ray.direction * offset)
            : bestPoint - (ray.direction * offset);

        return true;
    }

    private bool TryRaycastSingleVolume(
        TerraformVolume volume,
        Ray ray,
        float castRange,
        out Vector3 point,
        out float distance)
    {
        point = Vector3.zero;
        distance = 0f;

        if (volume == null ||
            !volume.WorldBounds.IntersectRay(ray, out float boundsDistance) ||
            boundsDistance > castRange)
        {
            return false;
        }

        float maxDistance = Mathf.Min(castRange, boundsDistance + volume.WorldBounds.size.magnitude);
        if (!volume.TryRaycastSurface(ray, maxDistance, volumeRayStep, volumeRayBinarySteps, out point, out _))
        {
            return false;
        }

        distance = Vector3.Distance(ray.origin, point);
        return distance <= castRange;
    }

    private TerraformVolume[] GetCachedVolumes()
    {
        if (cachedVolumes == null || Time.time >= nextVolumeCacheRefreshTime)
        {
            RefreshVolumeCache();
        }

        return cachedVolumes;
    }

    private void RefreshVolumeCache()
    {
        cachedVolumes = FindObjectsOfType<TerraformVolume>();
        nextVolumeCacheRefreshTime = Time.time + VolumeCacheRefreshInterval;
    }

    private bool TryGetTerraformHit(Ray ray, float castRange, out RaycastHit bestHit, out TerraformVolume bestVolume)
    {
        bestHit = default;
        bestVolume = null;

        int hitCount = Physics.RaycastNonAlloc(ray, rayHits, castRange, terraformMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = rayHits[i];

            if (hit.collider == null || ShouldIgnoreHit(hit.collider))
            {
                continue;
            }

            TerraformVolume hitVolume = GetTerraformVolume(hit.collider);
            if (hitVolume == null || hit.distance >= bestDistance)
            {
                continue;
            }

            bestHit = hit;
            bestVolume = hitVolume;
            bestDistance = hit.distance;
        }

        return bestVolume != null;
    }

    private bool ShouldIgnoreHit(Collider hitCollider)
    {
        return ignoreOwnerColliders &&
               transform.root != null &&
               hitCollider.transform.root == transform.root;
    }

    private static TerraformVolume GetTerraformVolume(Collider hitCollider)
    {
        TerraformChunk chunk = hitCollider.GetComponentInParent<TerraformChunk>();
        if (chunk != null)
        {
            return chunk.Volume;
        }

        return hitCollider.GetComponentInParent<TerraformVolume>();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBrushGizmo || !hasLastBrushCenter)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(lastBrushCenter, brushRadius);
    }
}
