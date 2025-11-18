using UnityEngine;
using UnityEngine.InputSystem; // new Input System
using System.Collections.Generic;
using MarchingCubes;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour
{
    
    [Header("Volume Settings")]
    public int size = 25; // +/- range on each axis
    public float spacing = 1.5f; // distance between sample points
    [Header("Porosity Noise (Foam)")]
    public float noiseScale = 0.12f; // smaller = larger pores
    [Range(0f, 1f)]
    public float poreThreshold = 0.50f; // iso-level (higher = more solid, smaller pores)

    [Header("Appearance Randomness")]
    [Range(0f, 1f)]
    public float positionJitter = 0.3f; // breaks grid alignment

    [Header("Collision / Walls")]
    public LayerMask obstacleMask;
    public bool blockBehindWalls = true;

    [Header("Regeneration")]
    public bool enableRegenerationHotkey = true;
    public KeyCode regenerateKey = KeyCode.Return; // Enter

    public ComputeShader marchingCubesShader;

    // internal
    private MeshFilter meshFilter;
    private Vector3 origin;
    private float[,,] densityField;
    private MeshBuilder builder;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    void Start()
    {
        origin = transform.position;
        RegenerateScaffold();
    }

    void Update()
    {
        if (!enableRegenerationHotkey) return;

        if (Keyboard.current != null &&
            Keyboard.current.enterKey.wasPressedThisFrame)
        {
            RegenerateScaffold();
        }
    }

    private void RegenerateScaffold()
    {
        BuildDensityField();
        BuildMeshFromDensity();
    }

    // -----------------------------
    // STEP 1: Build 3D density field
    // -----------------------------
    private void BuildDensityField()
    {
        int dim = size * 2 + 1;
        densityField = new float[dim, dim, dim];
        // --------------------------
        // Grid to Density Pass
        // --------------------------
        for (int ix = 0; ix < dim; ix++)
        for (int iy = 0; iy < dim; iy++)
        for (int iz = 0; iz < dim; iz++)
        {
            int gx = ix - size;
            int gy = iy - size;
            int gz = iz - size;

            Vector3 localPos = new Vector3(gx, gy, gz);

            Vector3 worldPos = origin + localPos * spacing;
            Vector3 jitteredPos = worldPos + Random.insideUnitSphere * (spacing * positionJitter);

            // ---------------------
            // 3D MULTI-SCALE NOISE
            // ---------------------

            // Large-scale cell structure (big pores)
            float w1 = Worley3D(localPos * noiseScale * 0.4f);
            float w2 = Worley3D(localPos * noiseScale * 0.8f);

            // Fine-scale details
            float p1 = Perlin3D(localPos * noiseScale * 1.2f);
            float p2 = Perlin3D(localPos * noiseScale * 2.5f);

            // Blend noise into a foam-like field (0–1 range)
            float noise =
                0.45f * (1f - w1) +   // large smooth structures
                0.25f * (1f - w2) +   // mid-range structure
                0.20f * p1 +          // fine noise
                0.10f * p2;           // very fine detail

            noise = Mathf.Clamp01(noise);

            // ---------------------
            // WALL COLLISION
            // ---------------------
            if (Physics.CheckSphere(jitteredPos, spacing * 0.45f, obstacleMask))
            {
                noise = 0f;
            }
            else if (blockBehindWalls)
            {
                Vector3 dir = (jitteredPos - origin).normalized;
                float dist = Vector3.Distance(origin, jitteredPos);

                if (Physics.Raycast(origin, dir, dist, obstacleMask))
                    noise = 0f;
            }

            // ---------------------
            // SIGNED DENSITY
            // ---------------------
            densityField[ix, iy, iz] = noise - poreThreshold;
        }

        // --------------------------
        // Gaussian Blur (1D – X Pass)
        // --------------------------
        GaussianBlurX(dim);
        GaussianBlurY(dim);
        GaussianBlurZ(dim);

        // Debug Density Range
        float minV = 999f, maxV = -999f;
        foreach (float v in densityField)
        {
            if (v < minV) minV = v;
            if (v > maxV) maxV = v;
        }
        Debug.Log($"Density range after blur: {minV} → {maxV}");
    }

    // -----------------------------
    // STEP 2: Marching Cubes on density -> Mesh
    // -----------------------------
    private void BuildMeshFromDensity()
    {
        int dim = size * 2 + 1;
        int voxelCount = dim * dim * dim;
        float[] flat = new float[voxelCount];

        int idx = 0;
        for (int x = 0; x < dim; x++)
        for (int y = 0; y < dim; y++)
        for (int z = 0; z < dim; z++)
            flat[idx++] = densityField[x, y, z];

        ComputeBuffer voxelBuffer = new ComputeBuffer(voxelCount, sizeof(float));
        voxelBuffer.SetData(flat);

        int maxTris = voxelCount * 5;

        // Dispose old builder if regenerating
        if (builder != null)
        {
            builder.Dispose();
            builder = null;
        }

        // Create new builder and run compute shader
        builder = new MeshBuilder(dim, dim, dim, maxTris, marchingCubesShader);
        builder.BuildIsosurface(voxelBuffer, 0f, spacing);

        // IMPORTANT: keep the mesh alive
        meshFilter.sharedMesh = builder.Mesh;

        voxelBuffer.Dispose();
    }

    private Vector3 RandomFloat3(Vector3 seed)
    {
        float x = Mathf.Abs(Mathf.Sin(Vector3.Dot(seed, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f);
        float y = Mathf.Abs(Mathf.Sin(Vector3.Dot(seed, new Vector3(93.9898f, 67.345f, 12.345f))) * 24634.6345f);
        float z = Mathf.Abs(Mathf.Sin(Vector3.Dot(seed, new Vector3(56.123f, 98.765f, 43.543f))) * 35734.7345f);

        return new Vector3(x % 1f, y % 1f, z % 1f);
    }

    private float Worley3D(Vector3 p)
    {
        // Cell coordinates
        int cx = Mathf.FloorToInt(p.x);
        int cy = Mathf.FloorToInt(p.y);
        int cz = Mathf.FloorToInt(p.z);
        float minDist = 9999f;

        // Check surrounding 27 cells
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Vector3 cell = new Vector3(cx + dx, cy + dy, cz + dz);

            // Random seed per cell
            Vector3 r = RandomFloat3(cell);

            // Feature point inside the cell
            Vector3 feature = cell + r;

            float dist = Vector3.Distance(p, feature);
            if (dist < minDist)
                minDist = dist;
        }

        return minDist;
    }

    // -----------------------
    // Helper: 3D Perlin Noise
    // -----------------------
    private float Perlin3D(Vector3 p)
    {
        float xy = Mathf.PerlinNoise(p.x, p.y);
        float yz = Mathf.PerlinNoise(p.y, p.z);
        float zx = Mathf.PerlinNoise(p.z, p.x);
        return (xy + yz + zx) / 3f;
    }

    // -----------------------
    // Helper: 1D Gaussian Blur
    // -----------------------
    private void GaussianBlurX(int dim)
    {
        int[] kernel = { 1, 4, 6, 4, 1 };
        int kHalf = 2;
        float[,,] blurred = new float[dim, dim, dim];

        for (int x = 0; x < dim; x++)
        for (int y = 0; y < dim; y++)
        for (int z = 0; z < dim; z++)
        {
            float sum = 0;
            float weight = 0;

            for (int k = -kHalf; k <= kHalf; k++)
            {
                int xx = Mathf.Clamp(x + k, 0, dim - 1);
                float w = kernel[k + kHalf];
                sum += densityField[xx, y, z] * w;
                weight += w;
            }

            blurred[x, y, z] = sum / weight;
        }

        densityField = blurred;
    }

    private void GaussianBlurY(int dim)
    {
        int[] kernel = { 1, 4, 6, 4, 1 };
        int kHalf = 2; 
        float[,,] blurred = new float[dim, dim, dim];

        for (int x = 0; x < dim; x++)
        for (int y = 0; y < dim; y++)
        for (int z = 0; z < dim; z++)
        {
            float sum = 0;
            float weight = 0;

            for (int k = -kHalf; k <= kHalf; k++)
            {
                int yy = Mathf.Clamp(y + k, 0, dim - 1);
                float w = kernel[k + kHalf];
                sum += densityField[x, yy, z] * w;
                weight += w;
            }

            blurred[x, y, z] = sum / weight;
        }

        densityField = blurred;
    }
    private void GaussianBlurZ(int dim)
    {
        int[] kernel = { 1, 4, 6, 4, 1 };
        int kHalf = 2;
        float[,,] blurred = new float[dim, dim, dim];

        for (int x = 0; x < dim; x++)
        for (int y = 0; y < dim; y++)
        for (int z = 0; z < dim; z++)
        {
            float sum = 0;
            float weight = 0;

            for (int k = -kHalf; k <= kHalf; k++)
            {
                int zz = Mathf.Clamp(z + k, 0, dim - 1);
                float w = kernel[k + kHalf];
                sum += densityField[x, y, zz] * w;
                weight += w;
            }

            blurred[x, y, z] = sum / weight;
        }

        densityField = blurred;
    }
}