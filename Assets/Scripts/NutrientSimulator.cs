using UnityEngine;

/// <summary>
/// MonoBehaviour that owns a NutrientField and updates it over time
/// using a simple diffusion + decay model with fixed boundary conditions.
/// </summary>
public class NutrientSimulator : MonoBehaviour
{
    [Header("Region / Grid Setup")]
    [Tooltip("Root object whose children define the scaffold volume (used for bounds).")]
    public Transform scaffoldRoot;

    [Tooltip("Size of each nutrient grid cell in world units (ideally ~scaffold spacing).")]
    public float cellSize = 1.0f;

    [Tooltip("Extra margin around scaffold bounds for the nutrient field.")]
    public float padding = 2.0f;

    [Header("Physics Parameters")]
    [Tooltip("Diffusion coefficient D (larger = faster spreading).")]
    public float diffusionCoefficient = 1.0f;

    [Tooltip("First-order decay rate (0 = no decay).")]
    public float decayRate = 0.0f;

    [Tooltip("Fixed time step for simulation (seconds).")]
    public float simTimeStep = 0.02f;

    [Header("Boundary Conditions")]
    [Tooltip("Concentration value enforced at the outer boundary cells.")]
    public float boundaryValue = 1.0f;

    [Header("Debug")]
    [Tooltip("Draw gizmo wireframe of the nutrient field bounds.")]
    public bool drawFieldBounds = true;

        [Header("Debug Sampling")]
    [Tooltip("Log a sample concentration periodically to verify the sim is running.")]
    public bool logSample = true;

    [Tooltip("How many simulation steps between log messages.")]
    public int logEveryNSteps = 50;

    private int _stepCount = 0;

    public NutrientField Field { get; private set; }

    private float _accumulator;

    private void Start()
    {
        if (scaffoldRoot == null)
        {
            Debug.LogWarning("[NutrientSimulator] No scaffoldRoot assigned. Using a default region around (0,0,0).");
            Bounds defaultBounds = new Bounds(Vector3.zero, Vector3.one * 10f);
            InitializeField(defaultBounds);
        }
        else
        {
            Bounds scaffoldBounds = ComputeBoundsFromRoot(scaffoldRoot);
            InitializeField(scaffoldBounds);
        }
    }

    private void Update()
    {
        if (Field == null) return;

        _accumulator += Time.deltaTime;

        // Fixed-step simulation
        while (_accumulator >= simTimeStep)
        {
            SimulateStep(simTimeStep);
            _accumulator -= simTimeStep;
        }
    }

    /// <summary>
    /// Compute an axis-aligned bounding box from all Renderers under a root transform.
    /// </summary>
    private Bounds ComputeBoundsFromRoot(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            // Fallback: small cube around root if no renderers are found
            return new Bounds(root.position, Vector3.one * 10f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    /// <summary>
    /// Create NutrientField using scaffold bounds + padding.
    /// </summary>
    private void InitializeField(Bounds regionBounds)
    {
        Field = new NutrientField(regionBounds, cellSize, padding);

        // Initial condition: everything zero
        Field.Fill(0f);

        // Outer boundary is a nutrient-rich bath
        Field.SetBoundary(boundaryValue);

        Debug.Log($"[NutrientSimulator] Initialized field: " +
                  $"{Field.sizeX} x {Field.sizeY} x {Field.sizeZ} cells, cellSize={Field.cellSize}");
    }

    /// <summary>
    /// Single simulation step: diffusion + decay + re-enforce boundary.
    /// </summary>
    private void SimulateStep(float dt)
    {
        if (Field == null) return;

        float[,,] c = Field.Concentration;
        float[,,] next = Field.GetNextBuffer();

        int nx = Field.sizeX;
        int ny = Field.sizeY;
        int nz = Field.sizeZ;

        float h = Field.cellSize;
        float invH2 = 1.0f / (h * h);
        float D = diffusionCoefficient;
        float k = decayRate;

        for (int x = 0; x < nx; x++)
        for (int y = 0; y < ny; y++)
        for (int z = 0; z < nz; z++)
        {
            // Keep boundary cells fixed to boundaryValue
            bool isBoundary = (x == 0 || x == nx - 1 ||
                               y == 0 || y == ny - 1 ||
                               z == 0 || z == nz - 1);

            if (isBoundary)
            {
                next[x, y, z] = boundaryValue;
                continue;
            }

            // Skip solid cells (for now treat them as no-flux)
            if (Field.IsSolid[x, y, z])
            {
                next[x, y, z] = c[x, y, z];
                continue;
            }

            float center = c[x, y, z];

            // 6-neighbor diffusion (simple finite-difference Laplacian)
            float xm = c[x - 1, y, z];
            float xp = c[x + 1, y, z];
            float ym = c[x, y - 1, z];
            float yp = c[x, y + 1, z];
            float zm = c[x, y, z - 1];
            float zp = c[x, y, z + 1];

            float laplacian = (xm + xp + ym + yp + zm + zp - 6f * center) * invH2;

            float diffusionTerm = D * laplacian;
            float decayTerm = -k * center;

            // Explicit Euler update
            float newValue = center + (diffusionTerm + decayTerm) * dt;

            // Clamp to non-negative (nutrients can't go below 0)
            if (newValue < 0f) newValue = 0f;

            next[x, y, z] = newValue;
        }

        // Swap buffers for next frame
        Field.SwapBuffers();

        // --- Debug: log a sample cell to see if things change over time ---
        _stepCount++;
        if (logSample && logEveryNSteps > 0 && (_stepCount % logEveryNSteps == 0))
        {
            int cx = Field.sizeX / 2;
            int cy = Field.sizeY / 2;
            int cz = Field.sizeZ / 2;

            float center = Field.Concentration[cx, cy, cz];
            Debug.Log($"[NutrientSimulator] Step {_stepCount}, center concentration = {center:F4}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawFieldBounds || Field == null) return;

        // Approximate bounds for debug visualization
        int nx = Field.sizeX;
        int ny = Field.sizeY;
        int nz = Field.sizeZ;
        float h = Field.cellSize;

        Vector3 min = Field.origin - new Vector3(h, h, h) * 0.5f;
        Vector3 size = new Vector3(nx * h, ny * h, nz * h);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(min + size * 0.5f, size);
    }
}
