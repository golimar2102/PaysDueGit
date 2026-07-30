using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class TerraformChunk : MonoBehaviour
{
    private static readonly Vector3Int[] CubeCorners =
    {
        new Vector3Int(0, 0, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(1, 0, 1),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 1, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, 1, 1),
        new Vector3Int(0, 1, 1)
    };

    private static readonly int[,] Tetrahedra =
    {
        { 0, 5, 1, 6 },
        { 0, 1, 2, 6 },
        { 0, 2, 3, 6 },
        { 0, 3, 7, 6 },
        { 0, 7, 4, 6 },
        { 0, 4, 5, 6 }
    };

    private TerraformVolume volume;
    private Vector3Int coordinates;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private Mesh mesh;
    private float[] densities;
    private int sampleCount;
    private readonly List<Vector3> vertices = new List<Vector3>(4096);
    private readonly List<int> triangles = new List<int>(8192);
    private readonly List<Vector2> uvs = new List<Vector2>(4096);
    private readonly List<Vector3> normals = new List<Vector3>(4096);
    private readonly int[] solid = new int[4];
    private readonly int[] empty = new int[4];
    private readonly Vector3[] cubePositions = new Vector3[8];
    private readonly float[] cubeDensities = new float[8];
    private readonly Vector3[] tetraPositions = new Vector3[4];
    private readonly float[] tetraDensities = new float[4];

    public TerraformVolume Volume
    {
        get { return volume; }
    }

    public Vector3Int Coordinates
    {
        get { return coordinates; }
    }

    public Bounds WorldBounds
    {
        get
        {
            Vector3 localCenter = Vector3.one * (volume.ChunkSize * 0.5f);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 worldSize = transform.TransformVector(Vector3.one * volume.ChunkSize);
            worldSize = new Vector3(Mathf.Abs(worldSize.x), Mathf.Abs(worldSize.y), Mathf.Abs(worldSize.z));
            return new Bounds(worldCenter, worldSize);
        }
    }

    public void Initialize(TerraformVolume owner, Vector3Int chunkCoordinates)
    {
        volume = owner;
        coordinates = chunkCoordinates;
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        AllocateDensityField();
        FillInitialDensityField();
        RebuildMesh(true);
    }

    public bool ApplyBrush(Vector3 worldCenter, float radius, TerraformOperation operation, float hardness)
    {
        if (densities == null || volume == null)
        {
            return false;
        }

        bool changed = false;
        float radiusSqr = radius * radius;
        float clampedHardness = Mathf.Clamp01(hardness);
        Bounds brushBounds = new Bounds(worldCenter, Vector3.one * (radius * 2f));
        GetSampleRange(brushBounds, out int minX, out int maxX, out int minY, out int maxY, out int minZ, out int maxZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 localPosition = SampleLocalPosition(x, y, z);
                    Vector3 worldPosition = transform.TransformPoint(localPosition);
                    float sqrDistance = (worldPosition - worldCenter).sqrMagnitude;

                    if (sqrDistance > radiusSqr)
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(sqrDistance);
                    float sphereDensity = radius - distance;
                    int index = DensityIndex(x, y, z);
                    float current = densities[index];
                    float target = operation == TerraformOperation.Subtract
                        ? Mathf.Min(current, -sphereDensity)
                        : Mathf.Max(current, sphereDensity);
                    float next = Mathf.Lerp(current, target, clampedHardness);

                    if (!Mathf.Approximately(current, next))
                    {
                        densities[index] = next;
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    public bool ApplyCapsuleBrush(
        Vector3 worldStart,
        Vector3 worldEnd,
        float radius,
        TerraformOperation operation,
        float hardness)
    {
        if (densities == null || volume == null)
        {
            return false;
        }

        bool changed = false;
        float radiusSqr = radius * radius;
        float clampedHardness = Mathf.Clamp01(hardness);
        Bounds brushBounds = new Bounds(worldStart, Vector3.zero);
        brushBounds.Encapsulate(worldEnd);
        brushBounds.Expand(radius * 2f);
        GetSampleRange(brushBounds, out int minX, out int maxX, out int minY, out int maxY, out int minZ, out int maxZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 localPosition = SampleLocalPosition(x, y, z);
                    Vector3 worldPosition = transform.TransformPoint(localPosition);
                    Vector3 closestPoint = ClosestPointOnSegment(worldStart, worldEnd, worldPosition);
                    float sqrDistance = (worldPosition - closestPoint).sqrMagnitude;

                    if (sqrDistance > radiusSqr)
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(sqrDistance);
                    float capsuleDensity = radius - distance;
                    int index = DensityIndex(x, y, z);
                    float current = densities[index];
                    float target = operation == TerraformOperation.Subtract
                        ? Mathf.Min(current, -capsuleDensity)
                        : Mathf.Max(current, capsuleDensity);
                    float next = Mathf.Lerp(current, target, clampedHardness);

                    if (!Mathf.Approximately(current, next))
                    {
                        densities[index] = next;
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    internal float SampleDensityWorld(Vector3 worldPosition)
    {
        return SampleDensityLocal(transform.InverseTransformPoint(worldPosition));
    }

    public void RebuildMesh()
    {
        RebuildMesh(true);
    }

    public void RebuildMesh(bool updateCollider)
    {
        if (volume == null || densities == null)
        {
            return;
        }

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Terraform Mesh " + coordinates;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        normals.Clear();

        BuildSurface(vertices, triangles, uvs, normals);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds();

        if (volume.smoothNormals && normals.Count == vertices.Count)
        {
            mesh.SetNormals(normals);
        }
        else
        {
            mesh.RecalculateNormals();
        }

        if (!volume.buildColliders)
        {
            DisableCollider();
        }
        else if (updateCollider)
        {
            UpdateColliderMesh();
        }
    }

    public void UpdateColliderMesh()
    {
        if (meshCollider == null || mesh == null)
        {
            return;
        }

        meshCollider.enabled = true;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    public void DisableCollider()
    {
        if (meshCollider == null)
        {
            return;
        }

        meshCollider.sharedMesh = null;
        meshCollider.enabled = false;
    }

    private void AllocateDensityField()
    {
        sampleCount = volume.chunkResolution + 1;
        densities = new float[sampleCount * sampleCount * sampleCount];
    }

    private void FillInitialDensityField()
    {
        for (int z = 0; z < sampleCount; z++)
        {
            for (int y = 0; y < sampleCount; y++)
            {
                for (int x = 0; x < sampleCount; x++)
                {
                    Vector3 worldPosition = transform.TransformPoint(SampleLocalPosition(x, y, z));
                    densities[DensityIndex(x, y, z)] = volume.GetInitialDensity(worldPosition);
                }
            }
        }
    }

    private void BuildSurface(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Vector3> normals)
    {
        for (int z = 0; z < volume.chunkResolution; z++)
        {
            for (int y = 0; y < volume.chunkResolution; y++)
            {
                for (int x = 0; x < volume.chunkResolution; x++)
                {
                    for (int corner = 0; corner < CubeCorners.Length; corner++)
                    {
                        Vector3Int offset = CubeCorners[corner];
                        int sx = x + offset.x;
                        int sy = y + offset.y;
                        int sz = z + offset.z;

                        cubePositions[corner] = SampleLocalPosition(sx, sy, sz);
                        cubeDensities[corner] = densities[DensityIndex(sx, sy, sz)];
                    }

                    for (int tetra = 0; tetra < Tetrahedra.GetLength(0); tetra++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int cubeIndex = Tetrahedra[tetra, i];
                            tetraPositions[i] = cubePositions[cubeIndex];
                            tetraDensities[i] = cubeDensities[cubeIndex];
                        }

                        PolygoniseTetrahedron(tetraPositions, tetraDensities, solid, empty, vertices, triangles, uvs, normals);
                    }
                }
            }
        }
    }

    private void PolygoniseTetrahedron(
        Vector3[] positions,
        float[] values,
        int[] solid,
        int[] empty,
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        List<Vector3> normals)
    {
        int solidCount = 0;
        int emptyCount = 0;

        for (int i = 0; i < 4; i++)
        {
            if (values[i] > 0f)
            {
                solid[solidCount] = i;
                solidCount++;
            }
            else
            {
                empty[emptyCount] = i;
                emptyCount++;
            }
        }

        if (solidCount == 0 || solidCount == 4)
        {
            return;
        }

        if (solidCount == 1)
        {
            int s = solid[0];
            Vector3 a = InterpolateSurface(positions[s], positions[empty[0]], values[s], values[empty[0]]);
            Vector3 b = InterpolateSurface(positions[s], positions[empty[1]], values[s], values[empty[1]]);
            Vector3 c = InterpolateSurface(positions[s], positions[empty[2]], values[s], values[empty[2]]);
            AddTriangleOriented(a, b, c, positions[s], vertices, triangles, uvs, normals);
            return;
        }

        if (solidCount == 3)
        {
            int e = empty[0];
            Vector3 a = InterpolateSurface(positions[e], positions[solid[0]], values[e], values[solid[0]]);
            Vector3 b = InterpolateSurface(positions[e], positions[solid[1]], values[e], values[solid[1]]);
            Vector3 c = InterpolateSurface(positions[e], positions[solid[2]], values[e], values[solid[2]]);
            Vector3 solidCenter = (positions[solid[0]] + positions[solid[1]] + positions[solid[2]]) / 3f;
            AddTriangleOriented(a, b, c, solidCenter, vertices, triangles, uvs, normals);
            return;
        }

        Vector3 p0 = InterpolateSurface(positions[solid[0]], positions[empty[0]], values[solid[0]], values[empty[0]]);
        Vector3 p1 = InterpolateSurface(positions[solid[1]], positions[empty[0]], values[solid[1]], values[empty[0]]);
        Vector3 p2 = InterpolateSurface(positions[solid[1]], positions[empty[1]], values[solid[1]], values[empty[1]]);
        Vector3 p3 = InterpolateSurface(positions[solid[0]], positions[empty[1]], values[solid[0]], values[empty[1]]);
        Vector3 quadSolidCenter = (positions[solid[0]] + positions[solid[1]]) * 0.5f;

        AddTriangleOriented(p0, p1, p2, quadSolidCenter, vertices, triangles, uvs, normals);
        AddTriangleOriented(p0, p2, p3, quadSolidCenter, vertices, triangles, uvs, normals);
    }

    private void AddTriangleOriented(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 solidReference,
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        List<Vector3> normals)
    {
        Vector3 triangleCenter = (a + b + c) / 3f;
        Vector3 desiredNormal = triangleCenter - solidReference;
        Vector3 currentNormal = Vector3.Cross(b - a, c - a);

        if (Vector3.Dot(currentNormal, desiredNormal) < 0f)
        {
            Vector3 swap = b;
            b = c;
            c = swap;
            currentNormal = -currentNormal;
        }

        int startIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        Vector3 faceNormal = currentNormal.sqrMagnitude > 0.00001f ? currentNormal.normalized : BuildNormal(triangleCenter);

        uvs.Add(BuildUv(a, faceNormal));
        uvs.Add(BuildUv(b, faceNormal));
        uvs.Add(BuildUv(c, faceNormal));

        normals.Add(BuildNormal(a));
        normals.Add(BuildNormal(b));
        normals.Add(BuildNormal(c));
    }

    private Vector3 BuildNormal(Vector3 localPosition)
    {
        Vector3 normal = -EstimateDensityGradientLocal(localPosition);

        if (normal.sqrMagnitude < 0.00001f)
        {
            return Vector3.up;
        }

        return normal.normalized;
    }

    private Vector3 EstimateDensityGradientLocal(Vector3 localPosition)
    {
        float step = volume.voxelSize;
        float dx = SampleDensityLocal(localPosition + Vector3.right * step) -
                   SampleDensityLocal(localPosition - Vector3.right * step);
        float dy = SampleDensityLocal(localPosition + Vector3.up * step) -
                   SampleDensityLocal(localPosition - Vector3.up * step);
        float dz = SampleDensityLocal(localPosition + Vector3.forward * step) -
                   SampleDensityLocal(localPosition - Vector3.forward * step);

        return new Vector3(dx, dy, dz) / (step * 2f);
    }

    private float SampleDensityLocal(Vector3 localPosition)
    {
        float gx = Mathf.Clamp(localPosition.x / volume.voxelSize, 0f, volume.chunkResolution);
        float gy = Mathf.Clamp(localPosition.y / volume.voxelSize, 0f, volume.chunkResolution);
        float gz = Mathf.Clamp(localPosition.z / volume.voxelSize, 0f, volume.chunkResolution);

        int x0 = Mathf.FloorToInt(gx);
        int y0 = Mathf.FloorToInt(gy);
        int z0 = Mathf.FloorToInt(gz);
        int x1 = Mathf.Min(x0 + 1, volume.chunkResolution);
        int y1 = Mathf.Min(y0 + 1, volume.chunkResolution);
        int z1 = Mathf.Min(z0 + 1, volume.chunkResolution);

        float tx = gx - x0;
        float ty = gy - y0;
        float tz = gz - z0;

        float c000 = densities[DensityIndex(x0, y0, z0)];
        float c100 = densities[DensityIndex(x1, y0, z0)];
        float c010 = densities[DensityIndex(x0, y1, z0)];
        float c110 = densities[DensityIndex(x1, y1, z0)];
        float c001 = densities[DensityIndex(x0, y0, z1)];
        float c101 = densities[DensityIndex(x1, y0, z1)];
        float c011 = densities[DensityIndex(x0, y1, z1)];
        float c111 = densities[DensityIndex(x1, y1, z1)];

        float c00 = Mathf.Lerp(c000, c100, tx);
        float c10 = Mathf.Lerp(c010, c110, tx);
        float c01 = Mathf.Lerp(c001, c101, tx);
        float c11 = Mathf.Lerp(c011, c111, tx);
        float c0 = Mathf.Lerp(c00, c10, ty);
        float c1 = Mathf.Lerp(c01, c11, ty);
        return Mathf.Lerp(c0, c1, tz);
    }

    private Vector2 BuildUv(Vector3 localPosition, Vector3 faceNormal)
    {
        if (!volume.slopeAwareUvs)
        {
            return new Vector2(localPosition.x * volume.uvScale, localPosition.z * volume.uvScale);
        }

        Vector3 absoluteNormal = new Vector3(Mathf.Abs(faceNormal.x), Mathf.Abs(faceNormal.y), Mathf.Abs(faceNormal.z));

        if (absoluteNormal.y >= absoluteNormal.x && absoluteNormal.y >= absoluteNormal.z)
        {
            return new Vector2(localPosition.x * volume.uvScale, localPosition.z * volume.uvScale);
        }

        if (absoluteNormal.x >= absoluteNormal.z)
        {
            return new Vector2(localPosition.z * volume.uvScale, localPosition.y * volume.uvScale);
        }

        return new Vector2(localPosition.x * volume.uvScale, localPosition.y * volume.uvScale);
    }

    private Vector3 SampleLocalPosition(int x, int y, int z)
    {
        return new Vector3(
            x * volume.voxelSize,
            y * volume.voxelSize,
            z * volume.voxelSize);
    }

    private int DensityIndex(int x, int y, int z)
    {
        return x + (sampleCount * (y + (sampleCount * z)));
    }

    private void GetSampleRange(
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

        float inverseVoxelSize = 1f / volume.voxelSize;
        minX = Mathf.Clamp(Mathf.FloorToInt(localMin.x * inverseVoxelSize) - 1, 0, sampleCount - 1);
        minY = Mathf.Clamp(Mathf.FloorToInt(localMin.y * inverseVoxelSize) - 1, 0, sampleCount - 1);
        minZ = Mathf.Clamp(Mathf.FloorToInt(localMin.z * inverseVoxelSize) - 1, 0, sampleCount - 1);
        maxX = Mathf.Clamp(Mathf.CeilToInt(localMax.x * inverseVoxelSize) + 1, 0, sampleCount - 1);
        maxY = Mathf.Clamp(Mathf.CeilToInt(localMax.y * inverseVoxelSize) + 1, 0, sampleCount - 1);
        maxZ = Mathf.Clamp(Mathf.CeilToInt(localMax.z * inverseVoxelSize) + 1, 0, sampleCount - 1);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 segment = end - start;
        float lengthSqr = segment.sqrMagnitude;

        if (lengthSqr < 0.000001f)
        {
            return start;
        }

        float t = Vector3.Dot(point - start, segment) / lengthSqr;
        return start + (segment * Mathf.Clamp01(t));
    }

    private static Vector3 InterpolateSurface(Vector3 a, Vector3 b, float valueA, float valueB)
    {
        float delta = valueB - valueA;

        if (Mathf.Abs(delta) < 0.00001f)
        {
            return (a + b) * 0.5f;
        }

        float t = Mathf.Clamp01(-valueA / delta);
        return Vector3.Lerp(a, b, t);
    }
}
