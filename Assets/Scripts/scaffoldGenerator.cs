using UnityEngine;

public class ScaffoldGenerator : MonoBehaviour
{
    public GameObject nodePrefab;
    public Transform scaffoldRoot;
    [Header("Volume Settings")]
    public int size = 25;           // +/- range on each axis
    public float spacing = 1.0f;

    [Header("Porosity Noise")]
    public float noiseScale = 0.1f;
    [Range(0f, 1f)]
    public float poreThreshold = 0.45f;

    void Start()
    {
        GenerateFoamScaffold();
    }

    void GenerateFoamScaffold()
    {
        Vector3 origin = transform.position;

        for (int x = -size; x <= size; x++)
        for (int y = -size; y <= size; y++)
        for (int z = -size; z <= size; z++)
        {
            Vector3 worldPos = origin + new Vector3(x, y, z) * spacing;

            float n = Worley3D(worldPos * noiseScale);

            if (n > poreThreshold)
            {
                Instantiate(nodePrefab, worldPos, Quaternion.identity, scaffoldRoot);
            }
        }
    }

    // --- 3D Worley Noise (Cellular Noise) ---
    float Worley3D(Vector3 p)
    {
        int xi = Mathf.FloorToInt(p.x);
        int yi = Mathf.FloorToInt(p.y);
        int zi = Mathf.FloorToInt(p.z);

        float minDist = 9999f;

        for (int xo = -1; xo <= 1; xo++)
        for (int yo = -1; yo <= 1; yo++)
        for (int zo = -1; zo <= 1; zo++)
        {
            Vector3 cell = new Vector3(xi + xo, yi + yo, zi + zo);
            Vector3 featurePoint = cell + RandomFloat3(cell);
            float d = Vector3.Distance(p, featurePoint);
            if (d < minDist) minDist = d;
        }

        return Mathf.Clamp01(minDist);
    }

    Vector3 RandomFloat3(Vector3 seed)
    {
        float x = Mathf.Abs(Mathf.Sin(Vector3.Dot(seed, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f);
        float y = Mathf.Abs(Mathf.Sin(Vector3.Dot(seed, new Vector3(93.9898f, 67.345f, 12.345f))) * 24634.6345f);
        float z = Mathf.Abs(Mathf.Sin(Vector3.Dot(seed, new Vector3(56.123f, 98.765f, 43.543f))) * 35734.7345f);

        return new Vector3(x % 1f, y % 1f, z % 1f);
    }
}