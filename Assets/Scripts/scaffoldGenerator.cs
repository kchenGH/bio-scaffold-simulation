using System.Collections.Generic;
using UnityEngine;

public class ScaffoldGenerator : MonoBehaviour
{
    [Header("Prefabs & Hierarchy")]
    public GameObject nodePrefab;
    public Transform scaffoldRoot;
    [Header("Lattice Settings")]
    public float spacing = 1f;           // distance between nodes
    public int maxDistance = 10;         // max |x| / |y| / |z| in grid cells

    [Header("Collision")]
    public LayerMask obstacleMask;       // set this to your wall/obstacle layer in Inspector

    private float nodeRadius;
    private Vector3 worldOrigin;

    // ----------------------------
    // ADVANCED POROSITY SETTINGS
    // ----------------------------
    [Header("Porosity: Voronoi")]
    public int voronoiSeedCount = 15;
    [Range(0f, 1f)]
    public float voronoiPoreSize = 0.35f;

    [Header("Porosity: Fiber Alignment")]
    public Vector3 fiberDirection = new Vector3(1, 0, 0);   // X-direction fibers
    [Range(0f, 1f)]
    public float fiberThreshold = 0.35f;                    // lower = more strict alignment

    [Header("Porosity: Random Mixing")]
    [Range(0f, 1f)]
    public float randomPoreChance = 0.05f;

    private List<Vector3> voronoiCenters;

    // ----------------------------

    void Awake()
    {
        nodeRadius = nodePrefab.GetComponent<SphereCollider>().radius * nodePrefab.transform.localScale.x;
    }

    void Start()
    {
        worldOrigin = transform.position;
        GenerateVoronoiSeeds();
        GenerateBfsLattice();
    }

    // --- Generate Voronoi Seeds ---
    private void GenerateVoronoiSeeds()
    {
        voronoiCenters = new List<Vector3>();

        for (int i = 0; i < voronoiSeedCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-maxDistance, maxDistance),
                Random.Range(-maxDistance, maxDistance),
                Random.Range(-maxDistance, maxDistance)
            );

            voronoiCenters.Add(worldOrigin + offset * spacing);
        }
    }

    // --- Porosity helpers ---
    private bool IsVoronoiPore(Vector3 worldPos)
    {
        float minDist = float.MaxValue;

        foreach (var seed in voronoiCenters)
        {
            float d = Vector3.Distance(worldPos, seed);
            if (d < minDist) minDist = d;
        }

        float normalized = minDist / (spacing * maxDistance);
        return normalized < voronoiPoreSize;
    }

    private bool IsFiberPore(Vector3 worldPos)
    {
        Vector3 dirFromOrigin = (worldPos - worldOrigin).normalized;
        float alignment = Mathf.Abs(Vector3.Dot(dirFromOrigin, fiberDirection.normalized));  // 1 = aligned
        return alignment < fiberThreshold;
    }

    private bool ShouldRemoveNode(Vector3 worldPos)
    {
        if (Random.value < randomPoreChance)
            return true;

        if (IsVoronoiPore(worldPos))
            return true;

        if (IsFiberPore(worldPos))
            return true;

        return false;
    }

    // --- BFS Lattice Generation ---
    private void GenerateBfsLattice()
    {
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        Vector3Int originCell = Vector3Int.zero;
        queue.Enqueue(originCell);
        visited.Add(originCell);

        Vector3Int[] directions =
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.forward,
            Vector3Int.back
        };

        while (queue.Count > 0)
        {
            Vector3Int cell = queue.Dequeue();

            // Limit BFS extent
            if (Mathf.Max(Mathf.Abs(cell.x), Mathf.Abs(cell.y), Mathf.Abs(cell.z)) > maxDistance)
                continue;

            Vector3 worldPos = worldOrigin + new Vector3(cell.x, cell.y, cell.z) * spacing;

            // If this cell is inside an obstacle, skip it and do NOT expand neighbors
            if (Physics.CheckSphere(worldPos, nodeRadius, obstacleMask))
                continue;

            // Decide if this cell is a pore (but NEVER pore the origin)
            bool isOrigin = (cell == originCell);
            bool isPore = false;
            if (!isOrigin)
            {
                isPore = ShouldRemoveNode(worldPos);
            }

            // Place node only if not a pore
            if (!isPore)
            {
                Instantiate(nodePrefab, worldPos, Quaternion.identity, scaffoldRoot);
            }

            // Enqueue neighbors regardless of pore status:
            // pores are empty space but should still allow growth through them
            foreach (Vector3Int dir in directions)
            {
                Vector3Int next = cell + dir;

                if (visited.Contains(next))
                    continue;

                Vector3 prevPos = worldOrigin + new Vector3(cell.x, cell.y, cell.z) * spacing;
                Vector3 nextPos = worldOrigin + new Vector3(next.x, next.y, next.z) * spacing;

                Vector3 castDirection = (nextPos - prevPos).normalized;

                // Prevent tunneling through thin walls
                if (Physics.SphereCast(
                        prevPos,
                        nodeRadius * 0.95f,
                        castDirection,
                        out RaycastHit hit,
                        spacing,
                        obstacleMask))
                {
                    continue;
                }

                // Extra safety: don't move into an occupied cell
                if (Physics.CheckSphere(nextPos, nodeRadius, obstacleMask))
                    continue;

                visited.Add(next);
                queue.Enqueue(next);
            }
        }
    }
}