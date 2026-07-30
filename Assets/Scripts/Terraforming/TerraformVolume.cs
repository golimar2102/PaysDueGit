using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TerraformVolume : MonoBehaviour
{
    private const string ChunkNamePrefix = "TerraformChunk_";

    public enum InitialShapeMode
    {
        HeightField,
        SolidBox
    }

    public enum ColliderUpdateMode
    {
        EveryMeshRebuild,
        Delayed,
        Manual
    }

    public enum PerformancePreset
    {
        Custom,
        Low,
        Medium,
        High
    }

    [Header("Chunks")]
    public Vector3Int chunkCounts = new Vector3Int(4, 2, 4);
    [Min(4)] public int chunkResolution = 16;
    [Min(0.05f)] public float voxelSize = 0.75f;
    public bool generateOnAwake = true;
    public bool buildColliders = true;

    [Header("Rendering")]
    public Material terrainMaterial;
    [Min(0.001f)] public float uvScale = 0.25f;
    public bool smoothNormals = true;
    public bool slopeAwareUvs = true;

    [Header("Performance")]
    public PerformancePreset performancePreset = PerformancePreset.Medium;
    public bool rebuildChunksOverTime = true;
    [Min(1)] public int maxChunkRebuildsPerFrame = 2;
    public bool rebuildFocusedChunkImmediately = true;
    public bool useFrameTimeBudget = true;
    [Min(0.1f)] public float maxTerraformMillisecondsPerFrame = 2.5f;
    public ColliderUpdateMode colliderUpdateMode = ColliderUpdateMode.Delayed;
    public bool updateFocusedChunkColliderImmediately = true;
    [Min(0f)] public float colliderUpdateDelay = 0.12f;
    [Min(1)] public int maxColliderUpdatesPerFrame = 1;

    [Header("Debug")]
    public bool drawChunkGizmos = true;
    public bool drawChunkGizmosOnlyWhenSelected = true;
    public Color volumeBoundsGizmoColor = new Color(0.1f, 0.8f, 1f, 0.65f);
    public Color chunkBoundsGizmoColor = new Color(1f, 0.85f, 0.1f, 0.35f);

    [Header("Runtime Stats")]
    [SerializeField] private int pendingMeshRebuilds;
    [SerializeField] private int pendingColliderUpdates;
    [SerializeField] private int meshRebuildsLastFrame;
    [SerializeField] private int colliderUpdatesLastFrame;

    [Header("Initial Shape")]
    public InitialShapeMode initialShapeMode = InitialShapeMode.HeightField;
    public int seed = 1337;
    public float terrainBaseHeight = 10f;
    public float terrainNoiseScale = 0.08f;
    public float terrainNoiseAmplitude = 2.5f;
    public bool generateCaves = true;
    public float caveNoiseScale = 0.12f;
    [Range(0f, 1f)] public float caveThreshold = 0.58f;
    public float caveCarveStrength = 8f;
    public float solidBoxCavePadding = 1.5f;
    public bool keepBottomSolid = true;

    private readonly Dictionary<Vector3Int, TerraformChunk> chunks = new Dictionary<Vector3Int, TerraformChunk>();
    private readonly Queue<TerraformChunk> rebuildQueue = new Queue<TerraformChunk>();
    private readonly HashSet<TerraformChunk> queuedRebuilds = new HashSet<TerraformChunk>();
    private readonly Queue<TerraformChunk> colliderQueue = new Queue<TerraformChunk>();
    private readonly HashSet<TerraformChunk> queuedColliders = new HashSet<TerraformChunk>();
    private readonly Dictionary<TerraformChunk, float> colliderReadyTimes = new Dictionary<TerraformChunk, float>();
    private bool hasGenerated;

    public float ChunkSize
    {
        get { return chunkResolution * voxelSize; }
    }

    public IEnumerable<TerraformChunk> Chunks
    {
        get { return chunks.Values; }
    }

    public Bounds WorldBounds
    {
        get
        {
            Vector3 localSize = GetLocalVolumeSize();
            Vector3 worldCenter = transform.TransformPoint(localSize * 0.5f);
            Vector3 worldSize = transform.TransformVector(localSize);
            worldSize = new Vector3(Mathf.Abs(worldSize.x), Mathf.Abs(worldSize.y), Mathf.Abs(worldSize.z));
            return new Bounds(worldCenter, worldSize);
        }
    }

    private void Awake()
    {
        ApplyPerformancePreset();

        if (Application.isPlaying && generateOnAwake)
        {
            Generate();
        }
    }

    private void OnValidate()
    {
        ApplyPerformancePreset();
    }

    private void Update()
    {
        meshRebuildsLastFrame = 0;
        colliderUpdatesLastFrame = 0;

        float frameDeadline = GetFrameDeadline();

        if (rebuildChunksOverTime)
        {
            ProcessMeshRebuildQueue(frameDeadline);
        }

        ProcessColliderRebuildQueue(frameDeadline);
        RefreshRuntimeStats();
    }

    [ContextMenu("Apply Performance Preset")]
    public void ApplyPerformancePreset()
    {
        switch (performancePreset)
        {
            case PerformancePreset.Low:
                rebuildChunksOverTime = true;
                maxChunkRebuildsPerFrame = 1;
                rebuildFocusedChunkImmediately = false;
                useFrameTimeBudget = true;
                maxTerraformMillisecondsPerFrame = 1.5f;
                colliderUpdateMode = ColliderUpdateMode.Delayed;
                updateFocusedChunkColliderImmediately = false;
                colliderUpdateDelay = 0.25f;
                maxColliderUpdatesPerFrame = 1;
                break;

            case PerformancePreset.Medium:
                rebuildChunksOverTime = true;
                maxChunkRebuildsPerFrame = 2;
                rebuildFocusedChunkImmediately = true;
                useFrameTimeBudget = true;
                maxTerraformMillisecondsPerFrame = 2.5f;
                colliderUpdateMode = ColliderUpdateMode.Delayed;
                updateFocusedChunkColliderImmediately = false;
                colliderUpdateDelay = 0.18f;
                maxColliderUpdatesPerFrame = 1;
                break;

            case PerformancePreset.High:
                rebuildChunksOverTime = true;
                maxChunkRebuildsPerFrame = 5;
                rebuildFocusedChunkImmediately = true;
                useFrameTimeBudget = true;
                maxTerraformMillisecondsPerFrame = 4f;
                colliderUpdateMode = ColliderUpdateMode.Delayed;
                updateFocusedChunkColliderImmediately = true;
                colliderUpdateDelay = 0.1f;
                maxColliderUpdatesPerFrame = 3;
                break;
        }
    }

    private float GetFrameDeadline()
    {
        if (!useFrameTimeBudget)
        {
            return float.PositiveInfinity;
        }

        return Time.realtimeSinceStartup + (Mathf.Max(0.1f, maxTerraformMillisecondsPerFrame) * 0.001f);
    }

    private bool IsFrameBudgetSpent(float frameDeadline)
    {
        return useFrameTimeBudget && Time.realtimeSinceStartup >= frameDeadline;
    }

    private void ProcessMeshRebuildQueue(float frameDeadline)
    {
        int rebuildsThisFrame = Mathf.Max(1, maxChunkRebuildsPerFrame);
        while (rebuildsThisFrame > 0 && rebuildQueue.Count > 0 && !IsFrameBudgetSpent(frameDeadline))
        {
            TerraformChunk chunk = rebuildQueue.Dequeue();

            if (chunk != null && queuedRebuilds.Remove(chunk))
            {
                RebuildChunkMesh(chunk, false);
                meshRebuildsLastFrame++;
                rebuildsThisFrame--;
            }
        }
    }

    private void ProcessColliderRebuildQueue(float frameDeadline)
    {
        if (!Application.isPlaying ||
            !buildColliders ||
            colliderUpdateMode != ColliderUpdateMode.Delayed ||
            colliderQueue.Count == 0)
        {
            return;
        }

        int updatesThisFrame = Mathf.Max(1, maxColliderUpdatesPerFrame);
        int pendingChecks = colliderQueue.Count;

        while (updatesThisFrame > 0 &&
               pendingChecks > 0 &&
               colliderQueue.Count > 0 &&
               !IsFrameBudgetSpent(frameDeadline))
        {
            TerraformChunk chunk = colliderQueue.Dequeue();
            pendingChecks--;

            if (chunk == null)
            {
                continue;
            }

            float readyTime = 0f;
            colliderReadyTimes.TryGetValue(chunk, out readyTime);

            if (Time.time < readyTime)
            {
                colliderQueue.Enqueue(chunk);
                continue;
            }

            if (queuedColliders.Remove(chunk))
            {
                colliderReadyTimes.Remove(chunk);
                chunk.UpdateColliderMesh();
                colliderUpdatesLastFrame++;
                updatesThisFrame--;
            }
        }
    }

    private void RefreshRuntimeStats()
    {
        pendingMeshRebuilds = queuedRebuilds.Count;
        pendingColliderUpdates = queuedColliders.Count;
    }

    private void OnDrawGizmos()
    {
        if (drawChunkGizmos && !drawChunkGizmosOnlyWhenSelected)
        {
            DrawChunkGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (drawChunkGizmos && drawChunkGizmosOnlyWhenSelected)
        {
            DrawChunkGizmos();
        }
    }

    [ContextMenu("Generate Chunks")]
    public void Generate()
    {
        chunkCounts = new Vector3Int(
            Mathf.Max(1, chunkCounts.x),
            Mathf.Max(1, chunkCounts.y),
            Mathf.Max(1, chunkCounts.z));
        chunkResolution = Mathf.Max(4, chunkResolution);
        voxelSize = Mathf.Max(0.05f, voxelSize);

        ClearGeneratedChunks();
        chunks.Clear();
        rebuildQueue.Clear();
        queuedRebuilds.Clear();
        ClearColliderQueue();

        for (int z = 0; z < chunkCounts.z; z++)
        {
            for (int y = 0; y < chunkCounts.y; y++)
            {
                for (int x = 0; x < chunkCounts.x; x++)
                {
                    Vector3Int coordinates = new Vector3Int(x, y, z);
                    TerraformChunk chunk = CreateChunk(coordinates);
                    chunks.Add(coordinates, chunk);
                }
            }
        }

        hasGenerated = true;
    }

    [ContextMenu("Clear Generated Chunks")]
    public void ClearGeneratedChunks()
    {
        rebuildQueue.Clear();
        queuedRebuilds.Clear();
        ClearColliderQueue();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            bool isTerraformChunk = child.GetComponent<TerraformChunk>() != null ||
                                    child.name.StartsWith(ChunkNamePrefix);

            if (!isTerraformChunk)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        chunks.Clear();
        hasGenerated = false;
    }

    public void DigSphere(Vector3 worldCenter, float radius, float hardness = 1f)
    {
        ModifySphere(worldCenter, radius, TerraformOperation.Subtract, hardness);
    }

    public void AddSphere(Vector3 worldCenter, float radius, float hardness = 1f)
    {
        ModifySphere(worldCenter, radius, TerraformOperation.Add, hardness);
    }

    public void DigCapsule(Vector3 worldStart, Vector3 worldEnd, float radius, float hardness = 1f)
    {
        ModifyCapsule(worldStart, worldEnd, radius, TerraformOperation.Subtract, hardness);
    }

    public void AddCapsule(Vector3 worldStart, Vector3 worldEnd, float radius, float hardness = 1f)
    {
        ModifyCapsule(worldStart, worldEnd, radius, TerraformOperation.Add, hardness);
    }

    public bool TryRaycastSurface(
        Ray ray,
        float maxDistance,
        float stepSize,
        int binarySearchSteps,
        out Vector3 point,
        out Vector3 normal)
    {
        point = Vector3.zero;
        normal = Vector3.up;

        if (!hasGenerated || chunks.Count == 0)
        {
            Generate();
        }

        maxDistance = Mathf.Max(0.01f, maxDistance);
        stepSize = Mathf.Max(voxelSize * 0.35f, stepSize);
        binarySearchSteps = Mathf.Max(1, binarySearchSteps);

        float previousDistance = 0f;
        float previousDensity = SampleDensityOrEmpty(ray.origin);

        if (previousDensity > 0f)
        {
            point = ray.origin;
            normal = EstimateSurfaceNormal(point);
            return true;
        }

        for (float distance = stepSize; distance <= maxDistance; distance += stepSize)
        {
            Vector3 samplePoint = ray.GetPoint(distance);
            float density = SampleDensityOrEmpty(samplePoint);

            if (previousDensity <= 0f && density > 0f)
            {
                float surfaceDistance = RefineSurfaceDistance(ray, previousDistance, distance, binarySearchSteps);
                point = ray.GetPoint(surfaceDistance);
                normal = EstimateSurfaceNormal(point);
                return true;
            }

            previousDistance = distance;
            previousDensity = density;
        }

        return false;
    }

    public void ModifySphere(Vector3 worldCenter, float radius, TerraformOperation operation, float hardness = 1f)
    {
        if (!hasGenerated || chunks.Count == 0)
        {
            Generate();
        }

        radius = Mathf.Max(0.01f, radius);
        hardness = Mathf.Clamp01(hardness);

        if (hardness <= 0f)
        {
            return;
        }

        float radiusSqr = radius * radius;

        Bounds brushBounds = new Bounds(worldCenter, Vector3.one * (radius * 2f));
        GetChunkRange(brushBounds, out int minX, out int maxX, out int minY, out int maxY, out int minZ, out int maxZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!chunks.TryGetValue(new Vector3Int(x, y, z), out TerraformChunk chunk) || chunk == null)
                    {
                        continue;
                    }

                    Bounds bounds = chunk.WorldBounds;
                    bounds.Expand(voxelSize * 2f);

                    if (bounds.SqrDistance(worldCenter) > radiusSqr)
                    {
                        continue;
                    }

                    if (chunk.ApplyBrush(worldCenter, radius, operation, hardness))
                    {
                        if (Application.isPlaying && rebuildChunksOverTime)
                        {
                            if (rebuildFocusedChunkImmediately && chunk.WorldBounds.Contains(worldCenter))
                            {
                                RebuildNow(chunk);
                            }
                            else
                            {
                                QueueRebuild(chunk);
                            }
                        }
                        else
                        {
                            RebuildChunkMesh(chunk, true);
                        }
                    }
                }
            }
        }
    }

    public void ModifyCapsule(
        Vector3 worldStart,
        Vector3 worldEnd,
        float radius,
        TerraformOperation operation,
        float hardness = 1f)
    {
        if (!hasGenerated || chunks.Count == 0)
        {
            Generate();
        }

        radius = Mathf.Max(0.01f, radius);
        hardness = Mathf.Clamp01(hardness);

        if (hardness <= 0f)
        {
            return;
        }

        Bounds capsuleBounds = new Bounds(worldStart, Vector3.zero);
        capsuleBounds.Encapsulate(worldEnd);
        capsuleBounds.Expand((radius + voxelSize) * 2f);
        GetChunkRange(capsuleBounds, out int minX, out int maxX, out int minY, out int maxY, out int minZ, out int maxZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!chunks.TryGetValue(new Vector3Int(x, y, z), out TerraformChunk chunk) || chunk == null)
                    {
                        continue;
                    }

                    Bounds bounds = chunk.WorldBounds;
                    bounds.Expand((radius + voxelSize) * 2f);

                    if (!SegmentIntersectsBounds(worldStart, worldEnd, bounds))
                    {
                        continue;
                    }

                    if (chunk.ApplyCapsuleBrush(worldStart, worldEnd, radius, operation, hardness))
                    {
                        if (Application.isPlaying && rebuildChunksOverTime)
                        {
                            if (rebuildFocusedChunkImmediately &&
                                (chunk.WorldBounds.Contains(worldStart) || chunk.WorldBounds.Contains(worldEnd)))
                            {
                                RebuildNow(chunk);
                            }
                            else
                            {
                                QueueRebuild(chunk);
                            }
                        }
                        else
                        {
                            RebuildChunkMesh(chunk, true);
                        }
                    }
                }
            }
        }
    }

    private void QueueRebuild(TerraformChunk chunk)
    {
        if (chunk == null || queuedRebuilds.Contains(chunk))
        {
            return;
        }

        queuedRebuilds.Add(chunk);
        rebuildQueue.Enqueue(chunk);
    }

    private void RebuildNow(TerraformChunk chunk)
    {
        if (chunk == null)
        {
            return;
        }

        queuedRebuilds.Remove(chunk);
        RebuildChunkMesh(chunk, true);
    }

    private void RebuildChunkMesh(TerraformChunk chunk, bool immediateCollider)
    {
        if (chunk == null)
        {
            return;
        }

        bool updateColliderNow = buildColliders &&
                                 (colliderUpdateMode == ColliderUpdateMode.EveryMeshRebuild ||
                                  immediateCollider &&
                                  updateFocusedChunkColliderImmediately &&
                                  colliderUpdateMode != ColliderUpdateMode.Manual);

        chunk.RebuildMesh(updateColliderNow);

        if (updateColliderNow)
        {
            queuedColliders.Remove(chunk);
            colliderReadyTimes.Remove(chunk);
        }

        if (!buildColliders)
        {
            queuedColliders.Remove(chunk);
            colliderReadyTimes.Remove(chunk);
            chunk.DisableCollider();
            return;
        }

        if (!updateColliderNow &&
            (colliderUpdateMode == ColliderUpdateMode.Delayed ||
             colliderUpdateMode == ColliderUpdateMode.Manual))
        {
            QueueColliderRebuild(chunk);
        }
    }

    private void QueueColliderRebuild(TerraformChunk chunk)
    {
        if (chunk == null || !buildColliders)
        {
            return;
        }

        colliderReadyTimes[chunk] = Time.time + colliderUpdateDelay;

        if (queuedColliders.Contains(chunk))
        {
            return;
        }

        queuedColliders.Add(chunk);
        colliderQueue.Enqueue(chunk);
    }

    private void ClearColliderQueue()
    {
        colliderQueue.Clear();
        queuedColliders.Clear();
        colliderReadyTimes.Clear();
    }

    [ContextMenu("Rebuild Queued Colliders Now")]
    public void RebuildQueuedCollidersNow()
    {
        if (!buildColliders)
        {
            return;
        }

        foreach (TerraformChunk chunk in queuedColliders)
        {
            if (chunk != null)
            {
                chunk.UpdateColliderMesh();
            }
        }

        ClearColliderQueue();
    }

    private static bool SegmentIntersectsBounds(Vector3 start, Vector3 end, Bounds bounds)
    {
        if (bounds.Contains(start) || bounds.Contains(end))
        {
            return true;
        }

        Vector3 delta = end - start;
        float length = delta.magnitude;

        if (length < 0.0001f)
        {
            return bounds.SqrDistance(start) <= 0.0001f;
        }

        return bounds.IntersectRay(new Ray(start, delta / length), out float distance) &&
               distance <= length;
    }

    private void GetChunkRange(
        Bounds worldBounds,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY,
        out int minZ,
        out int maxZ)
    {
        Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Vector3 worldMin = worldBounds.min;
        Vector3 worldMax = worldBounds.max;

        for (int z = 0; z <= 1; z++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int x = 0; x <= 1; x++)
                {
                    Vector3 worldCorner = new Vector3(
                        x == 0 ? worldMin.x : worldMax.x,
                        y == 0 ? worldMin.y : worldMax.y,
                        z == 0 ? worldMin.z : worldMax.z);
                    Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                    localMin = Vector3.Min(localMin, localCorner);
                    localMax = Vector3.Max(localMax, localCorner);
                }
            }
        }

        float inverseChunkSize = 1f / ChunkSize;
        minX = Mathf.Clamp(Mathf.FloorToInt(localMin.x * inverseChunkSize), 0, chunkCounts.x - 1);
        minY = Mathf.Clamp(Mathf.FloorToInt(localMin.y * inverseChunkSize), 0, chunkCounts.y - 1);
        minZ = Mathf.Clamp(Mathf.FloorToInt(localMin.z * inverseChunkSize), 0, chunkCounts.z - 1);
        maxX = Mathf.Clamp(Mathf.FloorToInt(localMax.x * inverseChunkSize), 0, chunkCounts.x - 1);
        maxY = Mathf.Clamp(Mathf.FloorToInt(localMax.y * inverseChunkSize), 0, chunkCounts.y - 1);
        maxZ = Mathf.Clamp(Mathf.FloorToInt(localMax.z * inverseChunkSize), 0, chunkCounts.z - 1);
    }

    private float RefineSurfaceDistance(Ray ray, float emptyDistance, float solidDistance, int steps)
    {
        float low = emptyDistance;
        float high = solidDistance;

        for (int i = 0; i < steps; i++)
        {
            float mid = (low + high) * 0.5f;
            float density = SampleDensityOrEmpty(ray.GetPoint(mid));

            if (density > 0f)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        return high;
    }

    private Vector3 EstimateSurfaceNormal(Vector3 worldPosition)
    {
        float step = Mathf.Max(0.01f, voxelSize * 0.5f);
        float dx = SampleDensityOrEmpty(worldPosition + (Vector3.right * step)) -
                   SampleDensityOrEmpty(worldPosition - (Vector3.right * step));
        float dy = SampleDensityOrEmpty(worldPosition + (Vector3.up * step)) -
                   SampleDensityOrEmpty(worldPosition - (Vector3.up * step));
        float dz = SampleDensityOrEmpty(worldPosition + (Vector3.forward * step)) -
                   SampleDensityOrEmpty(worldPosition - (Vector3.forward * step));

        Vector3 normal = -new Vector3(dx, dy, dz);

        if (normal.sqrMagnitude < 0.00001f)
        {
            return Vector3.up;
        }

        return normal.normalized;
    }

    private float SampleDensityOrEmpty(Vector3 worldPosition)
    {
        return TrySampleDensity(worldPosition, out float density) ? density : -1f;
    }

    public bool TrySampleDensity(Vector3 worldPosition, out float density)
    {
        density = -1f;

        if (!hasGenerated || chunks.Count == 0)
        {
            return false;
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        float size = ChunkSize;
        Vector3 totalSize = GetLocalVolumeSize();

        if (localPosition.x < 0f || localPosition.y < 0f || localPosition.z < 0f ||
            localPosition.x > totalSize.x || localPosition.y > totalSize.y || localPosition.z > totalSize.z)
        {
            return false;
        }

        int x = Mathf.Clamp(Mathf.FloorToInt(localPosition.x / size), 0, chunkCounts.x - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(localPosition.y / size), 0, chunkCounts.y - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(localPosition.z / size), 0, chunkCounts.z - 1);
        Vector3Int coordinates = new Vector3Int(x, y, z);

        if (!chunks.TryGetValue(coordinates, out TerraformChunk chunk) || chunk == null)
        {
            return false;
        }

        density = chunk.SampleDensityWorld(worldPosition);
        return true;
    }

    internal float GetInitialDensity(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        float seedOffset = seed * 0.137f;

        if (initialShapeMode == InitialShapeMode.SolidBox)
        {
            float solidDensity = GetSolidBoxDensity(localPosition);

            if (generateCaves && solidDensity > solidBoxCavePadding)
            {
                solidDensity = ApplyCaveCarving(localPosition, solidDensity, seedOffset);
            }

            return solidDensity;
        }

        float heightNoise = FractalNoise2D(
            localPosition.x * terrainNoiseScale + seedOffset,
            localPosition.z * terrainNoiseScale - seedOffset,
            3);

        float terrainHeight = terrainBaseHeight + ((heightNoise - 0.5f) * 2f * terrainNoiseAmplitude);
        float terrainDensity = terrainHeight - localPosition.y;

        if (generateCaves && localPosition.y < terrainHeight - voxelSize)
        {
            terrainDensity = ApplyCaveCarving(localPosition, terrainDensity, seedOffset);
        }

        if (keepBottomSolid)
        {
            float floorDensity = (voxelSize * 1.5f) - localPosition.y;
            terrainDensity = Mathf.Max(terrainDensity, floorDensity);
        }

        return terrainDensity;
    }

    private float ApplyCaveCarving(Vector3 localPosition, float density, float seedOffset)
    {
        Vector3 cavePosition = (localPosition * caveNoiseScale) + new Vector3(seedOffset, -seedOffset, seedOffset * 0.5f);
        float caveNoise = FractalNoise3D(cavePosition, 4);
        float carveAmount = Mathf.InverseLerp(caveThreshold, 1f, caveNoise) * caveCarveStrength;
        return density - carveAmount;
    }

    private float GetSolidBoxDensity(Vector3 localPosition)
    {
        Vector3 volumeSize = GetLocalVolumeSize();
        float distanceToMinX = localPosition.x;
        float distanceToMinY = localPosition.y;
        float distanceToMinZ = localPosition.z;
        float distanceToMaxX = volumeSize.x - localPosition.x;
        float distanceToMaxY = volumeSize.y - localPosition.y;
        float distanceToMaxZ = volumeSize.z - localPosition.z;

        return Mathf.Min(
            distanceToMinX,
            distanceToMinY,
            distanceToMinZ,
            distanceToMaxX,
            distanceToMaxY,
            distanceToMaxZ);
    }

    private Vector3 GetLocalVolumeSize()
    {
        return new Vector3(
            Mathf.Max(1, chunkCounts.x) * ChunkSize,
            Mathf.Max(1, chunkCounts.y) * ChunkSize,
            Mathf.Max(1, chunkCounts.z) * ChunkSize);
    }

    private TerraformChunk CreateChunk(Vector3Int coordinates)
    {
        GameObject chunkObject = new GameObject(ChunkNamePrefix + coordinates.x + "_" + coordinates.y + "_" + coordinates.z);
        chunkObject.transform.SetParent(transform, false);
        chunkObject.transform.localPosition = new Vector3(
            coordinates.x * ChunkSize,
            coordinates.y * ChunkSize,
            coordinates.z * ChunkSize);

        TerraformChunk chunk = chunkObject.AddComponent<TerraformChunk>();
        chunk.Initialize(this, coordinates);

        MeshRenderer meshRenderer = chunkObject.GetComponent<MeshRenderer>();
        if (terrainMaterial != null)
        {
            meshRenderer.sharedMaterial = terrainMaterial;
        }

        return chunk;
    }

    private static float FractalNoise2D(float x, float y, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float normalization = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return normalization > 0f ? value / normalization : 0f;
    }

    private static float FractalNoise3D(Vector3 position, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float normalization = 0f;

        for (int i = 0; i < octaves; i++)
        {
            Vector3 p = position * frequency;
            float xy = Mathf.PerlinNoise(p.x, p.y);
            float yz = Mathf.PerlinNoise(p.y + 17.31f, p.z - 9.73f);
            float xz = Mathf.PerlinNoise(p.x - 41.17f, p.z + 23.19f);
            float yx = Mathf.PerlinNoise(p.y - 13.11f, p.x + 5.91f);

            value += ((xy + yz + xz + yx) * 0.25f) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return normalization > 0f ? value / normalization : 0f;
    }

    private void DrawChunkGizmos()
    {
        Vector3Int safeChunkCounts = new Vector3Int(
            Mathf.Max(1, chunkCounts.x),
            Mathf.Max(1, chunkCounts.y),
            Mathf.Max(1, chunkCounts.z));
        float safeChunkSize = Mathf.Max(4, chunkResolution) * Mathf.Max(0.05f, voxelSize);
        Vector3 totalSize = new Vector3(
            safeChunkCounts.x * safeChunkSize,
            safeChunkCounts.y * safeChunkSize,
            safeChunkCounts.z * safeChunkSize);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = volumeBoundsGizmoColor;
        Gizmos.DrawWireCube(totalSize * 0.5f, totalSize);

        Gizmos.color = chunkBoundsGizmoColor;
        Vector3 chunkSize = Vector3.one * safeChunkSize;

        for (int z = 0; z < safeChunkCounts.z; z++)
        {
            for (int y = 0; y < safeChunkCounts.y; y++)
            {
                for (int x = 0; x < safeChunkCounts.x; x++)
                {
                    Vector3 chunkCenter = new Vector3(
                        (x + 0.5f) * safeChunkSize,
                        (y + 0.5f) * safeChunkSize,
                        (z + 0.5f) * safeChunkSize);
                    Gizmos.DrawWireCube(chunkCenter, chunkSize);
                }
            }
        }

        Gizmos.matrix = previousMatrix;
    }
}
