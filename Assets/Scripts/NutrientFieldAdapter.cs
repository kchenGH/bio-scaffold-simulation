using UnityEngine;

/// <summary>
/// Adapter that exposes NutrientSimulator.Field (NutrientField data class)
/// to the histogram exporter.
/// Attach this to the SAME GameObject that has NutrientSimulator.
/// </summary>
public class NutrientFieldAdapter : MonoBehaviour
{
    [Header("Reference")]
    public NutrientSimulator simulator; // drag in, or auto-find on same GO
    public bool IsReady => simulator != null && simulator.Field != null;

    public int SizeX => IsReady ? simulator.Field.sizeX : 0;
    public int SizeY => IsReady ? simulator.Field.sizeY : 0;
    public int SizeZ => IsReady ? simulator.Field.sizeZ : 0;

    private void Awake()
    {
        if (simulator == null) simulator = GetComponent<NutrientSimulator>();
    }

    public float GetNutrient(int x, int y, int z)
    {
        if (!IsReady) return 0f;

        // Read directly from the current concentration buffer
        return simulator.Field.Concentration[x, y, z];
    }
}