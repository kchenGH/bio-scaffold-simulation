using UnityEngine;
using UnityEngine.UI;

public class NutrientProbe : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera used for raycasting (usually the main camera).")]
    public Camera targetCamera;

    [Tooltip("Nutrient simulator that owns the NutrientField.")]
    public NutrientSimulator simulator;

    [Tooltip("Layer mask for scaffold geometry (or Everything if you haven't set a specific layer).")]
    public LayerMask scaffoldMask = ~0;   // default: everything

    [Header("UI")]
    [Tooltip("Root panel (enable/disable based on whether we're hovering something).")]
    public GameObject infoPanel;

    [Tooltip("Text UI element to show position info.")]
    public Text positionText;

    [Tooltip("Text UI element to show nutrient concentration.")]
    public Text concentrationText;

    [Header("Settings")]
    [Tooltip("Maximum distance for the raycast.")]
    public float maxRayDistance = 1000f;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Start()
    {
        if (simulator == null)
        {
            simulator = FindFirstObjectByType<NutrientSimulator>();
            if (simulator == null)
            {
                Debug.LogError("[NutrientProbe] No NutrientSimulator found in scene.");
                enabled = false;
                return;
            }
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (targetCamera == null || simulator == null || simulator.Field == null)
        {
            if (infoPanel != null) infoPanel.SetActive(false);
            return;
        }

        // Ray from mouse position
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, scaffoldMask))
        {
            Vector3 worldPos = hit.point;

            // Convert to nutrient field index
            var field = simulator.Field;
            if (field.WorldToIndex(worldPos, out int ix, out int iy, out int iz))
            {
                float c = field.Concentration[ix, iy, iz];

                // Show panel + update text
                if (infoPanel != null) infoPanel.SetActive(true);

                if (positionText != null)
                {
                    positionText.text =
                        $"World: {worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2}\n" +
                        $"Grid: ({ix}, {iy}, {iz})";
                }

                if (concentrationText != null)
                {
                    concentrationText.text = $"Nutrient: {c:F3} units";
                }
            }
            else
            {
                // Hit was outside the simulated field
                if (infoPanel != null) infoPanel.SetActive(false);
            }
        }
        else
        {
            // Nothing hit this frame
            if (infoPanel != null) infoPanel.SetActive(false);
        }
    }
}
