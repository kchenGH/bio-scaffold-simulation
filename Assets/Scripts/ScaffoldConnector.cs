using System.Collections.Generic;
using UnityEngine;

public class ScaffoldConnector : MonoBehaviour
{
    [Header("References")]
    public Transform scaffoldRoot; // Parent for cylinders
    public float spacing = 1.0f; // Set by ScaffoldGenerator
    public float spacingMultiplier = 1.6f; // Max distance to connect nodes
    public float radiusMultiplier = 0.45f; // Thickness of struts
    [Header("Gap Fix / Local Connectivity")]
    [Tooltip("Try to detect under-connected nodes and add extra struts to nearby neighbors.")]
    public bool fixLongGaps = true;

    [Tooltip("Minimum number of connections each node should try to have after gap fixing.")]
    public int minConnectionsPerNode = 2;

    [Tooltip("Maximum number of extra connections to add per node during gap fixing.")]
    public int maxNewConnectionsPerNode = 3;

    [Tooltip("If a node's closest connection is further than (maxDist * gapMultiplier), it is considered under-connected.")]
    public float gapMultiplier = 1.2f;

    [Tooltip("Factor to determine how far we search for neighbors when fixing gaps (searchRadius = maxDist * searchRadiusMultiplier).")]
    public float searchRadiusMultiplier = 2.5f;

    [Header("Island Detection / Repair")]
    [Tooltip("Detect disconnected clusters (islands) and connect each to the main network.")]
    public bool connectIslands = true;

    [Tooltip("Maximum factor (relative to normal maxDist) used when searching for closest points between islands and main cluster.")]
    public float islandSearchRadiusMultiplier = 6.0f;

    [Tooltip("How many separate bridge connections to create from each island into the main network.")]
    public int islandBridgeCount = 2;

    [Header("Loop Closing / C-shape Fix")]
    [Tooltip("Detect nodes that are close in space but far in the graph, and connect them to close C-shaped gaps.")]
    public bool closeLoops = true;

    [Tooltip("Radius factor (relative to maxDist) to search for potential loop-closing neighbors.")]
    public float loopSearchRadiusMultiplier = 2.0f;

    [Tooltip("Maximum number of loop-closing connections to add per node.")]
    public int maxLoopConnectionsPerNode = 1;

    [Tooltip("Maximum BFS depth when estimating graph distance between two nodes.")]
    public int maxLoopGraphHops = 6;

    [Tooltip("If graph distance between two nearby nodes is >= this many hops (or unreachable within maxLoopGraphHops), we add a loop-closing edge.")]
    public int minGraphHopsToBridge = 3;

    // Internal list of scaffold nodes (sphere transforms)
    private readonly List<Transform> nodes = new List<Transform>();

    // ---------------------------------------
    // Called by ScaffoldGenerator
    // ---------------------------------------
    public void AddNode(Transform t)
    {
        if (t != null)
            nodes.Add(t);
    }

    public void ClearNodes()
    {
        nodes.Clear();
    }

    // -------------------------------------------------------------
    // MAIN CALL: Build all struts connecting nearby scaffold nodes
    // -------------------------------------------------------------
    public void BuildStruts()
    {
        int count = nodes.Count;
        if (count < 2) return;

        float maxDist   = spacing * spacingMultiplier;
        float cylRadius = spacing * radiusMultiplier;

        // Adjacency list: which node indices are connected to which
        List<int>[] neighbors = new List<int>[count];
        for (int i = 0; i < count; i++)
        {
            neighbors[i] = new List<int>();
        }

        // -------------------------------------------------
        // PASS 1: Basic distance-based connections
        // -------------------------------------------------
        for (int i = 0; i < count; i++)
        {
            Vector3 a = nodes[i].position;

            for (int j = i + 1; j < count; j++)
            {
                Vector3 b = nodes[j].position;
                float dist = Vector3.Distance(a, b);

                if (dist <= maxDist)
                {
                    neighbors[i].Add(j);
                    neighbors[j].Add(i);
                }
            }
        }

        // -------------------------------------------------
        // PASS 2: Optional gap-fix / nearest-neighbor repair
        // -------------------------------------------------
        if (fixLongGaps)
        {
            FixLongGaps(neighbors, maxDist);
        }

        // -------------------------------------------------
        // PASS 3: Detect islands and connect them to main network
        //        (with multi-bridge connections per island)
        // -------------------------------------------------
        if (connectIslands)
        {
            ConnectIslands(neighbors, maxDist);
        }

        // -------------------------------------------------
        // PASS 4: Close local loops / C-shaped gaps
        // -------------------------------------------------
        if (closeLoops)
        {
            CloseLocalLoops(neighbors, maxDist);
        }

        // -------------------------------------------------
        // PASS 5: Actually create cylinders for each edge
        // -------------------------------------------------
        for (int i = 0; i < count; i++)
        {
            Vector3 a = nodes[i].position;

            foreach (int j in neighbors[i])
            {
                // Only build each edge once (i < j)
                if (j <= i) continue;

                Vector3 b = nodes[j].position;
                CreateCylinderBetween(a, b, cylRadius);
            }
        }
    }

    // -------------------------------------------------------------
    // GAP FIX: Detect under-connected nodes and connect them
    // -------------------------------------------------------------
    private void FixLongGaps(List<int>[] neighbors, float maxDist)
    {
        int count = nodes.Count;
        float maxDesiredDist = maxDist * gapMultiplier;
        float searchRadius   = maxDist * searchRadiusMultiplier;

        for (int i = 0; i < count; i++)
        {
            Vector3   nodePos   = nodes[i].position;
            List<int> neighList = neighbors[i];

            // 1) Find the current closest connected neighbor distance
            float closestConnected = float.MaxValue;
            for (int n = 0; n < neighList.Count; n++)
            {
                int   j = neighList[n];
                float d = Vector3.Distance(nodePos, nodes[j].position);
                if (d < closestConnected) closestConnected = d;
            }

            bool hasNoConnections     = neighList.Count == 0;
            bool hasTooLongConnection = closestConnected > maxDesiredDist;

            // Node is already reasonably connected → skip
            if (!hasNoConnections && !hasTooLongConnection && neighList.Count >= minConnectionsPerNode)
                continue;

            // 2) Gather candidate neighbors in a search radius that aren't already connected
            List<(int idx, float dist)> candidates = new List<(int idx, float dist)>();

            for (int j = 0; j < count; j++)
            {
                if (j == i) continue;
                if (neighList.Contains(j)) continue;

                float dist = Vector3.Distance(nodePos, nodes[j].position);
                if (dist > searchRadius) continue;

                candidates.Add((j, dist));
            }

            if (candidates.Count == 0)
                continue;

            // 3) Sort by distance so we connect to the closest ones first
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            int newConnections = 0;

            foreach (var cand in candidates)
            {
                if (newConnections >= maxNewConnectionsPerNode) break;

                // If we weren't completely isolated and we've now hit the minimum, we can stop
                if (!hasNoConnections && neighList.Count >= minConnectionsPerNode) break;

                int j = cand.idx;

                // Symmetric connection
                neighList.Add(j);
                neighbors[j].Add(i);

                newConnections++;
            }
        }
    }

    // -------------------------------------------------------------
    // ISLAND DETECTION: find disconnected clusters & connect them
    // -------------------------------------------------------------
    private void ConnectIslands(List<int>[] neighbors, float maxDist)
    {
        int count = nodes.Count;
        if (count == 0) return;

        // 1) Find connected components using BFS
        bool[]          visited    = new bool[count];
        List<List<int>> components = new List<List<int>>();

        for (int i = 0; i < count; i++)
        {
            if (visited[i]) continue;

            List<int> component = new List<int>();
            Queue<int> queue   = new Queue<int>();

            visited[i] = true;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                component.Add(current);

                List<int> neighList = neighbors[current];
                for (int k = 0; k < neighList.Count; k++)
                {
                    int neighborIndex = neighList[k];
                    if (visited[neighborIndex]) continue;

                    visited[neighborIndex] = true;
                    queue.Enqueue(neighborIndex);
                }
            }

            components.Add(component);
        }

        // If everything is already one big component, nothing to do
        if (components.Count <= 1) return;

        // 2) Identify the "main" component: the one with the largest node count
        int mainComponentIndex = 0;
        int maxSize            = components[0].Count;

        for (int i = 1; i < components.Count; i++)
        {
            int size = components[i].Count;
            if (size > maxSize)
            {
                maxSize            = size;
                mainComponentIndex = i;
            }
        }

        HashSet<int> mainSet = new HashSet<int>(components[mainComponentIndex]);

        // 3) For each other component (island), connect it to the main component
        float islandSearchRadius    = maxDist * islandSearchRadiusMultiplier;
        float islandSearchRadiusSqr = islandSearchRadius * islandSearchRadius;

        for (int c = 0; c < components.Count; c++)
        {
            if (c == mainComponentIndex) continue;

            List<int> island = components[c];
            if (island.Count == 0) continue;

            // Collect candidate bridge edges between this island and the main component
            List<(int islandIdx, int mainIdx, float distSqr)> candidates =
                new List<(int islandIdx, int mainIdx, float distSqr)>();

            for (int ii = 0; ii < island.Count; ii++)
            {
                int islandIndex = island[ii];
                Vector3 islandPos = nodes[islandIndex].position;

                foreach (int mainIndex in mainSet)
                {
                    Vector3 mainPos = nodes[mainIndex].position;
                    float distSqr   = (islandPos - mainPos).sqrMagnitude;

                    if (distSqr > islandSearchRadiusSqr)
                        continue;

                    candidates.Add((islandIndex, mainIndex, distSqr));
                }
            }

            if (candidates.Count == 0)
            {
                // If nothing was within the radius, relax: allow the single closest pair regardless of distance.
                float bestDistSqr   = float.MaxValue;
                int   bestIslandIdx = -1;
                int   bestMainIdx   = -1;

                for (int ii = 0; ii < island.Count; ii++)
                {
                    int islandIndex = island[ii];
                    Vector3 islandPos = nodes[islandIndex].position;

                    foreach (int mainIndex in mainSet)
                    {
                        Vector3 mainPos = nodes[mainIndex].position;
                        float distSqr   = (islandPos - mainPos).sqrMagnitude;

                        if (distSqr < bestDistSqr)
                        {
                            bestDistSqr   = distSqr;
                            bestIslandIdx = islandIndex;
                            bestMainIdx   = mainIndex;
                        }
                    }
                }

                if (bestIslandIdx != -1 && bestMainIdx != -1)
                {
                    if (!neighbors[bestIslandIdx].Contains(bestMainIdx))
                        neighbors[bestIslandIdx].Add(bestMainIdx);
                    if (!neighbors[bestMainIdx].Contains(bestIslandIdx))
                        neighbors[bestMainIdx].Add(bestIslandIdx);
                }

                continue;
            }

            // Sort by distance so we create the shortest bridges first
            candidates.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));

            int bridgesCreated = 0;

            foreach (var cand in candidates)
            {
                if (bridgesCreated >= islandBridgeCount)
                    break;

                int islandIdx = cand.islandIdx;
                int mainIdx   = cand.mainIdx;

                if (neighbors[islandIdx].Contains(mainIdx))
                    continue;

                neighbors[islandIdx].Add(mainIdx);
                neighbors[mainIdx].Add(islandIdx);

                bridgesCreated++;
            }
        }
    }

    // -------------------------------------------------------------
    // LOOP CLOSING: connect nodes close in space but far in graph
    // -------------------------------------------------------------
    private void CloseLocalLoops(List<int>[] neighbors, float maxDist)
    {
        int count = nodes.Count;
        if (count == 0) return;

        float loopRadius    = maxDist * loopSearchRadiusMultiplier;
        float loopRadiusSqr = loopRadius * loopRadius;

        for (int i = 0; i < count; i++)
        {
            Vector3 posI = nodes[i].position;
            List<int> neighI = neighbors[i];

            int added = 0;

            // Gather candidates that are close in space but not neighbors
            List<(int idx, float distSqr)> candidates = new List<(int idx, float distSqr)>();

            for (int j = 0; j < count; j++)
            {
                if (j == i) continue;
                if (neighI.Contains(j)) continue;

                float distSqr = (nodes[j].position - posI).sqrMagnitude;
                if (distSqr > loopRadiusSqr) continue;

                candidates.Add((j, distSqr));
            }

            if (candidates.Count == 0)
                continue;

            // Sort by spatial distance
            candidates.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));

            foreach (var cand in candidates)
            {
                if (added >= maxLoopConnectionsPerNode)
                    break;

                int j = cand.idx;

                // Estimate graph distance between i and j
                int hops = EstimateGraphDistance(i, j, neighbors, maxLoopGraphHops);

                // If unreachable within depth, or too many hops, we close the loop
                if (hops == -1 || hops >= minGraphHopsToBridge)
                {
                    neighI.Add(j);
                    neighbors[j].Add(i);
                    added++;
                }
            }
        }
    }

    private int EstimateGraphDistance(int start, int target, List<int>[] neighbors, int maxDepth)
    {
        if (start == target) return 0;

        int n = neighbors.Length;
        bool[] visited = new bool[n];
        Queue<(int node, int depth)> queue = new Queue<(int node, int depth)>();

        visited[start] = true;
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int node  = current.node;
            int depth = current.depth;

            if (depth >= maxDepth)
                continue;

            List<int> neigh = neighbors[node];
            for (int i = 0; i < neigh.Count; i++)
            {
                int next = neigh[i];
                if (visited[next]) continue;

                if (next == target)
                    return depth + 1;

                visited[next] = true;
                queue.Enqueue((next, depth + 1));
            }
        }

        // Not found within maxDepth
        return -1;
    }

    // -------------------------------------------------------------
    // Create cylinder connecting two spheres
    // -------------------------------------------------------------
    private void CreateCylinderBetween(Vector3 a, Vector3 b, float radius)
    {
        Vector3 mid    = (a + b) * 0.5f;
        Vector3 dir    = (b - a);
        float   length = dir.magnitude;

        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.transform.SetParent(scaffoldRoot, worldPositionStays: false);

        cyl.transform.position = mid;
        cyl.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);

        cyl.transform.localScale = new Vector3(radius, length * 0.5f, radius);

        // Optional: remove collider for performance
        Collider col = cyl.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
    }

    // -------------------------------------------------------------
    // Remove spherical nodes after struts are built
    // -------------------------------------------------------------
    public void DeleteNodes()
    {
        foreach (Transform t in nodes)
        {
            if (t != null)
                Destroy(t.gameObject);
        }

        nodes.Clear();
    }
}