using UnityEngine;

/// <summary>
/// Samples the NutrientField at each scaffold renderer's position
/// and colors the scaffold based on local nutrient concentration.
/// Attach this to a separate GameObject or to ScaffoldRoot.
/// </summary>
public class ScaffoldNutrientVisualizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Nutrient simulator that owns the NutrientField.")]
    public NutrientSimulator simulator;

    [Tooltip("Root transform containing all scaffold renderers.")]
    public Transform scaffoldRoot;

    [Header("Color Mapping")]
    [Tooltip("Concentration value mapped to the 'low' color.")]
    public float minConcentration = 0f;

    [Tooltip("Concentration value mapped to the 'high' color.")]
    public float maxConcentration = 1f;

    [Tooltip("Color at low concentration.")]
    public Color lowColor = Color.blue;

    [Tooltip("Midpoint color (for t = 0.5).")]
    public Color midColor = Color.green;

    [Tooltip("Color at high concentration.")]
    public Color highColor = Color.red;

    [Header("Update Settings")]
    [Tooltip("How often to update colors (seconds).")]
    public float updateInterval = 0.1f;

    private Renderer[] _renderers;
    private float _timer;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        // If not set, assume this object is the scaffold root
        if (scaffoldRoot == null)
        {
            scaffoldRoot = transform;
        }

        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (simulator == null)
        {
            simulator = FindFirstObjectByType<NutrientSimulator>();
            if (simulator == null)
            {
                Debug.LogError("[ScaffoldNutrientVisualizer] No NutrientSimulator found in scene.");
            }
        }

        if (scaffoldRoot == null)
        {
            Debug.LogError("[ScaffoldNutrientVisualizer] No scaffoldRoot assigned.");
            return;
        }

        RefreshRenderers();
    }

    private void RefreshRenderers()
    {
        if (scaffoldRoot == null) return;

        // true = include inactive children too
        _renderers = scaffoldRoot.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"[ScaffoldNutrientVisualizer] Refreshed, found {_renderers.Length} scaffold renderers.");
    }

    private void Update()
    {
        if (simulator == null || simulator.Field == null)
            return;

        // If scaffold was generated after Start(), refresh once we detect nothing
        if ((_renderers == null || _renderers.Length == 0) && scaffoldRoot != null)
        {
            RefreshRenderers();
            if (_renderers == null || _renderers.Length == 0)
            {
                // Still nothing; nothing to color this frame.
                return;
            }
        }

        _timer += Time.deltaTime;
        if (_timer < updateInterval) return;
        _timer = 0f;

        var field = simulator.Field;

        foreach (var rend in _renderers)
        {
            if (rend == null) continue;

            // Sample at renderer's bounds center
            Vector3 samplePos = rend.bounds.center;

            if (!field.WorldToIndex(samplePos, out int ix, out int iy, out int iz))
                continue; // outside field

            float c = field.Concentration[ix, iy, iz];

            // Normalize concentration into 0-1 range
            float t = 0f;
            if (maxConcentration > minConcentration)
            {
                t = Mathf.InverseLerp(minConcentration, maxConcentration, c);
            }
            t = Mathf.Clamp01(t);

            Color col = EvaluateColor(t);

            // Use MaterialPropertyBlock to avoid duplicating materials
            rend.GetPropertyBlock(_mpb);

            // URP Lit typically uses _BaseColor; legacy uses _Color
            _mpb.SetColor("_BaseColor", col);
            _mpb.SetColor("_Color", col);

            rend.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// Simple 3-point gradient: low -> mid -> high.
    /// t in [0,1].
    /// </summary>
    private Color EvaluateColor(float t)
    {
        if (t <= 0.5f)
        {
            float u = t / 0.5f; // 0..1
            return Color.Lerp(lowColor, midColor, u);
        }
        else
        {
            float u = (t - 0.5f) / 0.5f; // 0..1
            return Color.Lerp(midColor, highColor, u);
        }
    }
}
