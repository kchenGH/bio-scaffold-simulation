using System.Collections.Generic;
using UnityEngine;

public class ScaffoldConnector : MonoBehaviour
{
    [Header("References")]
    public Transform scaffoldRoot; // parent for cylinders
    public float spacing = 1.0f; // set by ScaffoldGenerator
    public float radiusMultiplier = 0.45f; // thickness of struts

    private List<Transform> nodes = new List<Transform>();
    private List<(Transform node1, Transform node2, float radius)> connections = new List<(Transform, Transform, float)>();

    // ---------------------------------------
    // Called by ScaffoldGenerator
    // ---------------------------------------
    public void AddNode(Transform t)
    {
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
        connections.Clear(); // Clear old connections
        
        float maxDist = spacing * 1.6f;
        float cylRadius = spacing * radiusMultiplier;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 a = nodes[i].position;

            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector3 b = nodes[j].position;
                float dist = Vector3.Distance(a, b);

                if (dist <= maxDist)
                {
                    // DISABLE visual cylinder creation - mesh will handle it
                    // CreateCylinderBetween(a, b, cylRadius);
                    
                    // Store connection data for mesh generation
                    connections.Add((nodes[i], nodes[j], cylRadius));
                }
            }
        }
        
        Debug.Log($"Stored {connections.Count} cylinder connections for mesh generation");
    }

    // -------------------------------------------------------------
    // Create cylinder connecting two spheres (DISABLED FOR MESH MODE)
    // -------------------------------------------------------------
    private void CreateCylinderBetween(Vector3 a, Vector3 b, float radius)
    {
        // COMMENTED OUT - Using marching cubes mesh instead
        /*
        Vector3 mid = (a + b) * 0.5f;
        Vector3 dir = (b - a);
        float length = dir.magnitude;

        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.transform.SetParent(scaffoldRoot);

        cyl.transform.position = mid;
        cyl.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);

        cyl.transform.localScale = new Vector3(radius, length * 0.5f, radius);

        // Optional: remove collider for performance
        Destroy(cyl.GetComponent<Collider>());
        */
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

    /// <summary>
    /// Export all cylinder strut data for mesh generation
    /// </summary>
    public List<(Vector3 start, Vector3 end, float radius)> GetCylinderData()
    {
        List<(Vector3 start, Vector3 end, float radius)> cylinders = new List<(Vector3 start, Vector3 end, float radius)>();
        
        foreach (var connection in connections)
        {
            if (connection.node1 != null && connection.node2 != null)
            {
                Vector3 start = connection.node1.position;
                Vector3 end = connection.node2.position;
                float radius = connection.radius;
                cylinders.Add((start, end, radius));
            }
        }
        
        return cylinders;
    }
}