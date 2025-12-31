using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Records center concentration vs simulated time at a fixed sampling interval,
/// then exports CSV for an Excel line chart.
/// </summary>
public class NutrientTimeSeriesExporter : MonoBehaviour
{
    [Header("Reference")]
    public NutrientSimulator simulator; // drag your NutrientSimulator here
    [Header("Sampling")]
    [Tooltip("Seconds between samples on the x-axis (e.g., 0.5 or 1.0).")]
    public float sampleIntervalSeconds = 1.0f;

    [Tooltip("Start recording automatically when Play begins and Field is ready.")]
    public bool autoStart = true;

    [Tooltip("Optional: stop recording automatically after this many simulated seconds (0 = never).")]
    public float maxSimulatedDuration = 0f;

    [Header("Export")]
    public string fileNamePrefix = "nutrient_center_timeseries";

    [Tooltip("Press this key during Play mode to export immediately.")]
    public KeyCode exportHotkey = KeyCode.T;

    [Tooltip("Press this key during Play mode to toggle recording on/off.")]
    public KeyCode toggleRecordHotkey = KeyCode.R;

    private readonly List<float> _times = new List<float>(2048);
    private readonly List<float> _values = new List<float>(2048);

    private bool _recording;
    private float _nextSampleTime;

    private void Awake()
    {
        if (simulator == null) simulator = FindFirstObjectByType<NutrientSimulator>();
    }

    private void Start()
    {
        if (autoStart) TryStartRecording();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleRecordHotkey))
        {
            if (_recording) StopRecording();
            else TryStartRecording();
        }

        if (Input.GetKeyDown(exportHotkey))
        {
            ExportNow();
        }

        if (!_recording) return;
        if (simulator == null || !simulator.FieldReady) return;

        float simTime = simulator.SimulatedTime;

        // Optional auto-stop
        if (maxSimulatedDuration > 0f && simTime >= maxSimulatedDuration)
        {
            StopRecording();
            ExportNow();
            return;
        }

        // Record at EXACT sample times: 0.0, 1.0, 2.0... (or 0.5 increments)
        // Use a while loop in case we jump over multiple intervals in one frame.
        while (simTime >= _nextSampleTime)
        {
            float center = simulator.GetCenterConcentration();
            _times.Add(_nextSampleTime);
            _values.Add(center);

            _nextSampleTime += Mathf.Max(0.0001f, sampleIntervalSeconds);
        }
    }

    public void TryStartRecording()
    {
        if (simulator == null)
        {
            Debug.LogError("[NutrientTimeSeriesExporter] simulator reference is missing.");
            return;
        }

        _times.Clear();
        _values.Clear();

        _nextSampleTime = 0f;
        _recording = true;

        Debug.Log("[NutrientTimeSeriesExporter] Recording started.");
    }

    public void StopRecording()
    {
        _recording = false;
        Debug.Log("[NutrientTimeSeriesExporter] Recording stopped.");
    }

    [ContextMenu("Export Time Series Now")]
    public void ExportNow()
    {
        if (_times.Count == 0)
        {
            Debug.LogWarning("[NutrientTimeSeriesExporter] No samples recorded yet. Record first, then export.");
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{fileNamePrefix}_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        WriteCsv(path);

        Debug.Log($"[NutrientTimeSeriesExporter] Exported time series CSV:\n{path}");
        Debug.Log($"[NutrientTimeSeriesExporter] Samples: {_times.Count}, Interval: {sampleIntervalSeconds}s");
    }

    private void WriteCsv(string path)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("time_sec,center_concentration");

        for (int i = 0; i < _times.Count; i++)
        {
            sb.AppendLine($"{_times[i]:F3},{_values[i]:F6}");
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}