using UnityEngine;

/// <summary>
/// Adapter that decides whether a cell should be included in the histogram.
/// Uses NutrientSimulator.Field.IsSolid mask (if you populate it).
/// Attach this to the SAME GameObject that has NutrientSimulator.
/// </summary>
public class ScaffoldMaskAdapter : MonoBehaviour
{
    public enum CellSelection
    {
    AllCells,
    FluidCellsOnly, // !IsSolid
    SolidCellsOnly // IsSolid
    }
    [Header("Reference")]
    public NutrientSimulator simulator; // drag in, or auto-find

    [Header("Selection")]
    public CellSelection selection = CellSelection.FluidCellsOnly;

    private void Awake()
    {
        if (simulator == null) simulator = GetComponent<NutrientSimulator>();
    }

    public bool IsScaffold(int x, int y, int z)
    {
        // NOTE: Name kept as IsScaffold to match exporter API.
        // Here it means: "should this cell be INCLUDED?"
        if (simulator == null || simulator.Field == null) return false;

        bool isSolid = simulator.Field.IsSolid[x, y, z];

        switch (selection)
        {
            case CellSelection.AllCells:
                return true;
            case CellSelection.SolidCellsOnly:
                return isSolid;
            case CellSelection.FluidCellsOnly:
            default:
                return !isSolid;
        }
    }
}