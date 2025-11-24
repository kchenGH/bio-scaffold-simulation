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

    [Tooltip("Size of each nutrient grid cell in world units (ideally ~scaffold spacing). This is the MINIMUM cell size; it may be increased automatically for performance.")]
    public float cellSize = 1.0f;

    [Tooltip("Extra margin around scaffold bounds for the nutrient field.")]
    public float padding = 2.0f;

    [Header("Initialization")]
    [Tooltip("Wait until scaffoldRoot has renderers before creating the field.")]
    public bool autoInitFromScaffold = true;

    [Tooltip("If no renderers are found within this time, fall back to a default box.")]
    public float maxWaitBeforeFallback = 1.0f;

    [Tooltip("Size of the fallback box (only used if no scaffold renderers are found).")]
    public Vector3 fallbackSize = new Vector3(10f, 10f, 10f);

    [Header("Physics Parameters")]
    [Tooltip("Diffusion coefficient D (larger = faster spreading).")]
    public float diffusionCoefficient = 1.0f;

    [Range(0f, 0.1f)]
    [Tooltip("First-order decay rate (0 = no decay, 0.1 = very fast decay).")]
    public float decayRate = 0.0f;

    [Tooltip("Fixed time step for simulation (seconds).")]
    public float simTimeStep = 0.02f;

    [Header("Performance")]
    [Tooltip("Maximum total number of grid cells allowed (sizeX * sizeY * sizeZ). Higher values = slower simulation.")]
    public int maxTotalCells = 250_000;

    [Tooltip("Maximum simulation steps allowed per rendered frame.")]
    public int maxStepsPerFrame = 2;

    [Tooltip("Automatically increase cell size until total cells <= maxTotalCells.")]
    public bool autoAdjustCellSize = true;

    [Header("Boundary Conditions")]
    [Tooltip("Concentration value enforced at the outer boundary cells of the field.")]
    public float boundaryValue = 1.0f;

    [Header("Debug")]
    [Tooltip("Draw gizmo wireframe of the nutrient field bounds.")]
    public bool drawFieldBounds = true;

    [Tooltip("Log a sample concentration periodically to verify the sim is running.")]
    public bool logSample = false;

    [Tooltip("How many simulation steps between log messages.")]
    public int logEveryNSteps = 50;

    public NutrientField Field { get; private set; }

    private float _accumulator;
    private float _timeSinceStart;
    private bool _fieldInitialized;
    private bool _usedFallbackBounds;
    private int _stepCount;

    private void Start()
    {
        _timeSinceStart = 0f;
    }

    private void Update()
    {
        _timeSinceStart += Time.deltaTime;

        // 1) Make sure the field exists and is sized correctly
        if (!_fieldInitialized)
        {
            TryInitializeField();
            if (!_fieldInitialized)
            {
                // Still waiting for scaffold / fallback; do not simulate yet.
                return;
            }
        }

        // 2) Run fixed-step simulation with an upper bound on steps per frame
        if (Field == null) return;

        _accumulator += Time.deltaTime;

        int stepsThisFrame = 0;
        while (_accumulator >= simTimeStep && stepsThisFrame < maxStepsPerFrame)
        {
            SimulateStep(simTimeStep);
            _accumulator -= simTimeStep;
            stepsThisFrame++;
        }

        // If we had more accumulated time than we could process, just drop the excess
        // to prevent runaway catch-up loops.
        if (stepsThisFrame == maxStepsPerFrame && _accumulator > simTimeStep * 4f)
        {
            _accumulator = 0f;
        }
    }

    /// <summary>
    /// Attempt to initialize the field from scaffold renderers.
    /// If none are found and enough time has passed, fall back to a default box.
    /// </summary>
    private void TryInitializeField()
    {
        if (scaffoldRoot != null)
        {
            var renderers = scaffoldRoot.GetComponentsInChildren<Renderer>();

            if (renderers.Length > 0)
            {
                Bounds scaffoldBounds = ComputeBoundsFromRenderers(renderers);
                InitializeField(scaffoldBounds);
                _fieldInitialized = true;
                _usedFallbackBounds = false;

                Debug.Log($"[NutrientSimulator] Initialized field from scaffold bounds at t={_timeSinceStart:F2}s.");
                return;
            }
        }

        // If we reach here, there are no renderers yet.
        if (_timeSinceStart >= maxWaitBeforeFallback)
        {
            Vector3 center = scaffoldRoot != null ? scaffoldRoot.position : Vector3.zero;
            Bounds defaultBounds = new Bounds(center, fallbackSize);
            InitializeField(defaultBounds);
            _fieldInitialized = true;
            _usedFallbackBounds = true;

            Debug.LogWarning("[NutrientSimulator] No scaffold renderers found in time. " +
                             "Using fallback bounds instead.");
        }
    }

    /// <summary>
    /// Compute an axis-aligned bounding box from a set of renderers.
    /// </summary>
    private Bounds ComputeBoundsFromRenderers(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    /// <summary>
    /// Create NutrientField using scaffold bounds + padding.
    /// Automatically increases cell size if grid would exceed maxTotalCells.
    /// Outer boundary cells are set to boundaryValue so diffusion starts from there.
    /// </summary>
    private void InitializeField(Bounds regionBounds)
    {
        float effectiveCellSize = Mathf.Max(0.0001f, cellSize);
        NutrientField candidate = null;

        // Try a few times, increasing cell size if grid is too large
        for (int attempt = 0; attempt < 8; attempt++)
        {
            candidate = new NutrientField(regionBounds, effectiveCellSize, padding);
            long total = (long)candidate.sizeX * candidate.sizeY * candidate.sizeZ;

            if (!autoAdjustCellSize || total <= maxTotalCells)
            {
                break;
            }

            // Increase cell size to reduce resolution and total cell count
            effectiveCellSize *= 1.5f;
        }

        Field = candidate;
        cellSize = Field.cellSize; // Reflect the actual value in inspector

        Field.Fill(0f);
        Field.SetBoundary(boundaryValue);

        long cellCount = (long)Field.sizeX * Field.sizeY * Field.sizeZ;

        Debug.Log($"[NutrientSimulator] Field size: {Field.sizeX} x {Field.sizeY} x {Field.sizeZ} " +
                  $"(total {cellCount} cells), cellSize={Field.cellSize:F3}, padding={padding}, " +
                  $"usedFallback={_usedFallbackBounds}");
    }

    /// <summary>
    /// Single simulation step: diffusion + decay + re-enforce boundary.
    /// Nutrients start from the outer boundary cells and diffuse inward.
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

            float xm = c[x - 1, y, z];
            float xp = c[x + 1, y, z];
            float ym = c[x, y - 1, z];
            float yp = c[x, y + 1, z];
            float zm = c[x, y, z - 1];
            float zp = c[x, y, z + 1];

            float laplacian = (xm + xp + ym + yp + zm + zp - 6f * center) * invH2;

            float diffusionTerm = D * laplacian;
            float decayTerm = -k * center;

            float newValue = center + (diffusionTerm + decayTerm) * dt;
            if (newValue < 0f) newValue = 0f;

            next[x, y, z] = newValue;
        }

        Field.SwapBuffers();

        // Optional debug logging of a sample cell
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

    /// <summary>
    /// Rebuild the nutrient field using the current scaffold geometry.
    /// Call this AFTER you regenerate the scaffold.
    /// </summary>
    public void RegenerateFieldFromScaffold()
        {
        if (scaffoldRoot == null)
        {
        Debug.LogWarning("[NutrientSimulator] Cannot regenerate: scaffoldRoot is null.");
        return;
        }
        var renderers = scaffoldRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[NutrientSimulator] Cannot regenerate: no renderers found under scaffoldRoot.");
            return;
        }

        // Compute fresh bounds from the newly generated scaffold
        Bounds scaffoldBounds = ComputeBoundsFromRenderers(renderers);

        // Reset internal state
        _timeSinceStart = 0f;
        _accumulator = 0f;
        _stepCount = 0;
        _usedFallbackBounds = false;
        _fieldInitialized = false;

        // Build a new field around the new scaffold
        InitializeField(scaffoldBounds);
        _fieldInitialized = true;

        Debug.Log("[NutrientSimulator] Regenerated nutrient field from current scaffold.");
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawFieldBounds || Field == null) return;

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