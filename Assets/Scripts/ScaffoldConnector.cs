using System.Collections.Generic;
using UnityEngine;

public class ScaffoldConnector : MonoBehaviour
{
    [Header("References")]
    public Transform scaffoldRoot; // parent for cylinders
    public float spacing = 1.0f; // set by ScaffoldGenerator
    public float radiusMultiplier = 0.45f; // thickness of struts

    private List<Transform> nodes = new List<Transform>();

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
                    CreateCylinderBetween(a, b, cylRadius);
                }
            }
        }
    }

    // -------------------------------------------------------------
    // Create cylinder connecting two spheres
    // -------------------------------------------------------------
    private void CreateCylinderBetween(Vector3 a, Vector3 b, float radius)
    {
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