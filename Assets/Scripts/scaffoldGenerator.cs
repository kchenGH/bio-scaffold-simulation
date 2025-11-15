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

    void Awake()
    {
        nodeRadius = nodePrefab.GetComponent<SphereCollider>().radius * nodePrefab.transform.localScale.x;
    }
    void Start()
    {
        GenerateBfsLattice();
    }

    // --- BFS Lattice Generation ---
    private void GenerateBfsLattice()
    {
        // BFS structures
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        // Start at grid origin (0,0,0)
        Vector3Int originCell = Vector3Int.zero;
        queue.Enqueue(originCell);
        visited.Add(originCell);

        Vector3 worldOrigin = transform.position;

        // 6-neighbour directions (±x, ±y, ±z)
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

            // Bound check: cube bound in grid space
            if (Mathf.Max(Mathf.Abs(cell.x), Mathf.Abs(cell.y), Mathf.Abs(cell.z)) > maxDistance)
                continue;

            // Convert grid cell to world position
            Vector3 worldPos = worldOrigin + new Vector3(cell.x, cell.y, cell.z) * spacing;

            // If this cell is inside an obstacle, skip it and DO NOT expand neighbors from here
            if (Physics.CheckSphere(worldPos, nodeRadius, obstacleMask))
            {
                // This effectively makes walls “solid”: BFS doesn't pass through them
                continue;
            }

            // Place node
            Instantiate(nodePrefab, worldPos, Quaternion.identity, scaffoldRoot);

            // Enqueue neighbors
            foreach (Vector3Int dir in directions)
            {
                Vector3Int next = cell + dir;

                if (visited.Contains(next))
                    continue;

                // Convert BFS grid coordinates to world
                Vector3 prevPos = worldOrigin + new Vector3(cell.x, cell.y, cell.z) * spacing;
                Vector3 nextPos = worldOrigin + new Vector3(next.x, next.y, next.z) * spacing;

                // --- NEW: SphereCast between nodes to prevent tunneling ---
                Vector3 castDirection = (nextPos - prevPos).normalized;

                if (Physics.SphereCast(
                        prevPos,
                        nodeRadius * 0.95f,
                        castDirection,
                        out RaycastHit hit,
                        spacing,
                        obstacleMask))
                {
                    // Wall in-between → do NOT expand into this cell
                    continue;
                }

                // --- OLD: CheckSphere remains for endpoint safety ---
                if (Physics.CheckSphere(nextPos, nodeRadius, obstacleMask))
                {
                    continue;
                }

                // Accept cell
                visited.Add(next);
                queue.Enqueue(next);
            }
        }
    }
}
