using UnityEngine;

/// <summary>
/// Pure data class: a 3D grid of nutrient concentrations aligned to world space.
/// No MonoBehaviour here – it’s owned and used by NutrientSimulator.
/// </summary>
[System.Serializable]
public class NutrientField
{
    public readonly int sizeX;
    public readonly int sizeY;
    public readonly int sizeZ;

    public readonly float cellSize;
    public readonly Vector3 origin; // World position of cell (0,0,0) center.

    // Main buffers
    private float[,,] _current;
    private float[,,] _next;

    /// <summary>Expose read-only access to the current concentration buffer.</summary>
    public float[,,] Concentration => _current;

    /// <summary>Optional occupancy mask (solid scaffold regions) – not used yet, but ready.</summary>
    public bool[,,] IsSolid { get; private set; }

    public NutrientField(Bounds regionBounds, float cellSize, float padding)
    {
        this.cellSize = Mathf.Max(0.0001f, cellSize);

        // Expand bounds by padding on all sides
        Vector3 paddedMin = regionBounds.min - Vector3.one * padding;
        Vector3 paddedMax = regionBounds.max + Vector3.one * padding;
        Vector3 paddedSize = paddedMax - paddedMin;

        // Decide grid size in each dimension
        sizeX = Mathf.Max(1, Mathf.CeilToInt(paddedSize.x / this.cellSize));
        sizeY = Mathf.Max(1, Mathf.CeilToInt(paddedSize.y / this.cellSize));
        sizeZ = Mathf.Max(1, Mathf.CeilToInt(paddedSize.z / this.cellSize));

        // Place origin at the center of cell (0,0,0)
        origin = paddedMin + new Vector3(this.cellSize, this.cellSize, this.cellSize) * 0.5f;

        _current = new float[sizeX, sizeY, sizeZ];
        _next = new float[sizeX, sizeY, sizeZ];
        IsSolid = new bool[sizeX, sizeY, sizeZ];
    }

    /// <summary>
    /// Convert a grid index to world-space position at the center of that cell.
    /// </summary>
    public Vector3 IndexToWorld(int x, int y, int z)
    {
        return origin + new Vector3(
            x * cellSize,
            y * cellSize,
            z * cellSize
        );
    }

    /// <summary>
    /// Convert a world-space position to the corresponding grid index.
    /// Returns false if outside the grid.
    /// </summary>
    public bool WorldToIndex(Vector3 worldPos, out int x, out int y, out int z)
    {
        Vector3 local = worldPos - origin;

        x = Mathf.FloorToInt(local.x / cellSize + 0.5f);
        y = Mathf.FloorToInt(local.y / cellSize + 0.5f);
        z = Mathf.FloorToInt(local.z / cellSize + 0.5f);

        if (x < 0 || x >= sizeX ||
            y < 0 || y >= sizeY ||
            z < 0 || z >= sizeZ)
        {
            return false;
        }

        return true;
    }

    /// <summary>Fill entire field with a constant value.</summary>
    public void Fill(float value)
    {
        for (int x = 0; x < sizeX; x++)
        for (int y = 0; y < sizeY; y++)
        for (int z = 0; z < sizeZ; z++)
        {
            _current[x, y, z] = value;
            _next[x, y, z] = value;
        }
    }

    /// <summary>
    /// Set all boundary cells (outer shell of the 3D grid) to a constant value.
    /// Useful for "bath" boundary conditions.
    /// </summary>
    public void SetBoundary(float value)
    {
        for (int x = 0; x < sizeX; x++)
        for (int y = 0; y < sizeY; y++)
        for (int z = 0; z < sizeZ; z++)
        {
            bool isBoundary = (x == 0 || x == sizeX - 1 ||
                               y == 0 || y == sizeY - 1 ||
                               z == 0 || z == sizeZ - 1);

            if (isBoundary)
            {
                _current[x, y, z] = value;
                _next[x, y, z] = value;
            }
        }
    }

    /// <summary>Mark a cell as solid (scaffold material).</summary>
    public void SetSolid(int x, int y, int z, bool solid = true)
    {
        if (x < 0 || x >= sizeX ||
            y < 0 || y >= sizeY ||
            z < 0 || z >= sizeZ)
            return;

        IsSolid[x, y, z] = solid;
    }

    /// <summary>Swap current and next buffers after a simulation step.</summary>
    public void SwapBuffers()
    {
        var tmp = _current;
        _current = _next;
        _next = tmp;
    }

    /// <summary>Get the next buffer so the simulator can write into it.</summary>
    public float[,,] GetNextBuffer()
    {
        return _next;
    }
}
