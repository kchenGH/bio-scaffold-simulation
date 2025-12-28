using System;
using System.IO;
using System.Text;
using UnityEngine;

public class NutrientHistogramExporter : MonoBehaviour
{
    [Header("Histogram Settings")]
    [Range(5, 200)]
    public int binCount = 50;
    [Tooltip("Clamp values into this range before binning.")]
    public float minValue = 0f;

    public float maxValue = 1f;

    [Tooltip("If true, use scaffoldMask to decide which cells are included.")]
    public bool useMask = true;

    [Header("Output")]
    public string fileNamePrefix = "nutrient_histogram";
    public bool logSummaryToConsole = true;

    [Header("References")]
    public NutrientFieldAdapter nutrientField;
    public ScaffoldMaskAdapter scaffoldMask;

    [ContextMenu("Export Histogram Now")]
    public void ExportHistogramNow()
    {
        if (nutrientField == null)
        {
            Debug.LogError("[NutrientHistogramExporter] nutrientField is not assigned.");
            return;
        }

        if (!nutrientField.IsReady)
        {
            Debug.LogWarning("[NutrientHistogramExporter] Nutrient field not ready yet (simulator.Field == null). " +
                            "Enter Play mode and wait for initialization, then export again.");
            return;
        }

        int sx = nutrientField.SizeX;
        int sy = nutrientField.SizeY;
        int sz = nutrientField.SizeZ;

        if (sx <= 0 || sy <= 0 || sz <= 0)
        {
            Debug.LogError("[NutrientHistogramExporter] Nutrient field sizes are invalid.");
            return;
        }

        if (useMask && scaffoldMask == null)
        {
            Debug.LogWarning("[NutrientHistogramExporter] useMask=true but scaffoldMask is not assigned. Including ALL cells.");
        }

        float range = maxValue - minValue;
        if (range <= 0f)
        {
            Debug.LogError("[NutrientHistogramExporter] maxValue must be > minValue.");
            return;
        }

        int[] bins = new int[binCount];
        int included = 0;
        int skipped = 0;

        for (int z = 0; z < sz; z++)
        for (int y = 0; y < sy; y++)
        for (int x = 0; x < sx; x++)
        {
            if (useMask && scaffoldMask != null)
            {
                if (!scaffoldMask.IsScaffold(x, y, z))
                {
                    skipped++;
                    continue;
                }
            }

            float v = nutrientField.GetNutrient(x, y, z);

            // Clamp
            if (v < minValue) v = minValue;
            if (v > maxValue) v = maxValue;

            float t = (v - minValue) / range; // 0..1
            int bin = (t >= 1f) ? (binCount - 1) : Mathf.FloorToInt(t * binCount);
            bin = Mathf.Clamp(bin, 0, binCount - 1);

            bins[bin]++;
            included++;
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{fileNamePrefix}_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        WriteHistogramCsv(path, bins, minValue, maxValue);

        if (logSummaryToConsole)
        {
            Debug.Log($"[NutrientHistogramExporter] Exported histogram CSV:\n{path}");
            Debug.Log($"[NutrientHistogramExporter] Included={included}, Skipped={skipped}, Bins={binCount}, Range=[{minValue}, {maxValue}]");
        }
    }

    private void WriteHistogramCsv(string path, int[] bins, float minV, float maxV)
    {
        float range = maxV - minV;
        float binWidth = range / bins.Length;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("bin_min,bin_max,count");

        for (int i = 0; i < bins.Length; i++)
        {
            float bmin = minV + i * binWidth;
            float bmax = (i == bins.Length - 1) ? maxV : (minV + (i + 1) * binWidth);
            sb.AppendLine($"{bmin:F6},{bmax:F6},{bins[i]}");
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}