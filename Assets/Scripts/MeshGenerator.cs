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

    [Header("Scaffold Reference")]
    public ScaffoldConnector connectorReference;

    [Header("SDF Blending")]
    [Range(0.3f, 0.8f)]
    [Tooltip("Controls smoothness at cylinder joints. Lower = sharp, Higher = organic")]
    public float blendRadius = 0.5f;

    public ComputeShader marchingCubesShader;

    // internal
    private MeshFilter meshFilter;
    private Vector3 origin;
    private float[,,] densityField;
    private MeshBuilder builder;
    private int xRes, yRes, zRes; // cache dimensions

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    void Start()
    {
        origin = transform.position;
        // DON'T auto-regenerate - wait for ScaffoldGenerator
        // RegenerateScaffold();
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

    public void RegenerateFromScaffold()
    {
        Debug.Log("=== MeshGenerator: Starting regeneration from scaffold data ===");
        RegenerateScaffold();
    }

    private void RegenerateScaffold()
    {
        // Initialize dimensions and density field
        xRes = size * 2 + 1;
        yRes = size * 2 + 1;
        zRes = size * 2 + 1;
        densityField = new float[xRes, yRes, zRes];

        BuildDensityField();
        BuildMeshFromDensity();
    }

    // -----------------------------
    // STEP 1: Build 3D density field
    // -----------------------------
    private void BuildDensityField()
    {
        if (connectorReference == null)
        {
            Debug.LogError("MeshGenerator: ScaffoldConnector reference is not set! Assign it in the Inspector.");
            return;
        }

        List<(Vector3 start, Vector3 end, float radius)> cylinders = connectorReference.GetCylinderData();
        
        if (cylinders.Count == 0)
        {
            Debug.LogWarning("MeshGenerator: No cylinder data available. Run ScaffoldGenerator first.");
            return;
        }

        Debug.Log($"Building SDF density field from {cylinders.Count} cylinders...");
        Debug.Log($"Grid resolution: {xRes}x{yRes}x{zRes}, spacing: {spacing}");

        int totalVoxels = xRes * yRes * zRes;
        int processedVoxels = 0;
        int lastPercent = 0;

        for (int ix = 0; ix < xRes; ix++)
        {
            for (int iy = 0; iy < yRes; iy++)
            {
                for (int iz = 0; iz < zRes; iz++)
                {
                    Vector3 worldPos = new Vector3(
                        (ix - size) * spacing + origin.x,
                        (iy - size) * spacing + origin.y,
                        (iz - size) * spacing + origin.z
                    );

                    float minDist = float.MaxValue;

                    // Calculate minimum distance to all cylinders with smooth blending
                    foreach (var cylinder in cylinders)
                    {
                        float dist = SDFCapsule(worldPos, cylinder.start, cylinder.end, cylinder.radius);
                        
                        if (minDist == float.MaxValue)
                        {
                            minDist = dist;
                        }
                        else
                        {
                            // Smooth blending creates organic joints like image 175741
                            minDist = SmoothMin(minDist, dist, blendRadius);
                        }
                    }

                    // Store negative distance (inside = negative, outside = positive)
                    // Isosurface at 0 creates the mesh boundary
                    densityField[ix, iy, iz] = -minDist;
                    
                    processedVoxels++;
                    int percent = (processedVoxels * 100) / totalVoxels;
                    if (percent > lastPercent && percent % 10 == 0)
                    {
                        Debug.Log($"Density field progress: {percent}%");
                        lastPercent = percent;
                    }
                }
            }
        }

        // Apply Gaussian blur for additional smoothing
        Debug.Log("Applying Gaussian blur...");
        ApplyBlur();
        
        Debug.Log("SDF density field built successfully.");
    }

    // -----------------------
    // Helper: Apply 3D Gaussian Blur
    // -----------------------
    private void ApplyBlur()
    {
        int dim = xRes; // assuming cubic volume
        GaussianBlurX(dim);
        GaussianBlurY(dim);
        GaussianBlurZ(dim);
    }

    // -----------------------------
    // STEP 2: Marching Cubes on density -> Mesh
    // -----------------------------
    private void BuildMeshFromDensity()
    {
        Debug.Log("=== Building mesh from density field ===");
        
        int dim = size * 2 + 1;
        int voxelCount = dim * dim * dim;
        float[] flat = new float[voxelCount];

        int idx = 0;
        for (int x = 0; x < dim; x++)
        for (int y = 0; y < dim; y++)
        for (int z = 0; z < dim; z++)
            flat[idx++] = densityField[x, y, z];

        Debug.Log($"Flattened {voxelCount} voxels into buffer");

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
        Debug.Log("Creating MeshBuilder and running marching cubes...");
        builder = new MeshBuilder(dim, dim, dim, maxTris, marchingCubesShader);
        builder.BuildIsosurface(voxelBuffer, 0f, spacing);

        // IMPORTANT: keep the mesh alive
        meshFilter.sharedMesh = builder.Mesh;
        
        Debug.Log($"Mesh generated: {builder.Mesh.vertexCount} vertices, {builder.Mesh.triangles.Length / 3} triangles");

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

    /// <summary>
    /// Signed Distance Function for a capsule (cylinder with rounded ends)
    /// </summary>
    private float SDFCapsule(Vector3 p, Vector3 a, Vector3 b, float radius)
    {
        Vector3 pa = p - a;
        Vector3 ba = b - a;
        float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
        return Vector3.Distance(p, a + ba * h) - radius;
    }

    /// <summary>
    /// Smooth minimum function for metaball-style blending at joints
    /// </summary>
    private float SmoothMin(float d1, float d2, float k)
    {
        float h = Mathf.Clamp01(0.5f + 0.5f * (d2 - d1) / k);
        return Mathf.Lerp(d2, d1, h) - k * h * (1.0f - h);
    }
}