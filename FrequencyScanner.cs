using UnityEngine;
using System.Collections.Generic;

public class FrequencyScanner : MonoBehaviour
{
    public SDRReceiver sdrReceiver;  // Reference to SDRReceiver
    public SDRAudioReceiver sdrAudioReceiver;  // Reference to SDRAudioReceiver
    public GameObject frequencyObjectPrefab;  // Prefab for spawning

    public float scanStart = 88000000f;  // Start of FM band (88 MHz)
    public float scanEnd = 108000000f;   // End of FM band (108 MHz)
    public float scanStep = 200000f;     // Scan step (200 kHz)
    public float detectionThreshold = 0.3f;  // Signal detection threshold

    private HashSet<float> detectedFrequencies = new HashSet<float>();
    private float scanTimer = 0f;
    public float scanInterval = 1f;  // Scan every second
    private float currentScanFrequency;
    private bool isPaused = false;  // Pause control

    void Start()
    {
        currentScanFrequency = scanStart;
        if (sdrAudioReceiver != null)
        {
            sdrAudioReceiver.enabled = false;  // Disable audio initially
        }
    }

    void Update()
    {
        if (isPaused) return;  // Skip scanning if paused

        scanTimer += Time.deltaTime;
        if (scanTimer >= scanInterval)
        {
            ScanNextFrequency();
            scanTimer = 0f;
        }
    }

    void ScanNextFrequency()
    {
        if (sdrReceiver == null)
        {
            Debug.LogError("SDRReceiver is not assigned to FrequencyScanner!");
            return;
        }

        sdrReceiver.QueueFrequencyChange(currentScanFrequency);
        Debug.Log("Scanning Frequency: " + (currentScanFrequency / 1e6f) + " MHz");

        float[] amplitudes = sdrReceiver.GetLatestAmplitudes();
        float averageAmplitude = CalculateAverageAmplitude(amplitudes);

        Debug.Log($"Average Amplitude at {currentScanFrequency / 1e6f} MHz: {averageAmplitude}");

        if (averageAmplitude > detectionThreshold && !detectedFrequencies.Contains(currentScanFrequency))
        {
            Debug.Log($"Signal Detected at: {currentScanFrequency / 1e6f} MHz | Amplitude: {averageAmplitude}");
            detectedFrequencies.Add(currentScanFrequency);
            SpawnFrequencyObject(currentScanFrequency);
        }

        currentScanFrequency += scanStep;
        if (currentScanFrequency > scanEnd)
        {
            currentScanFrequency = scanStart;
        }
    }

    float CalculateAverageAmplitude(float[] amplitudes)
    {
        if (amplitudes == null || amplitudes.Length == 0)
        {
            return 0;
        }

        float sum = 0f;
        foreach (float amp in amplitudes)
        {
            sum += Mathf.Clamp(amp, 0f, 1f);
        }

        float average = sum / amplitudes.Length;
        return Mathf.Clamp01(average);
    }

    public void SpawnFrequencyObject(float frequency)
    {
        if (frequencyObjectPrefab == null)
        {
            Debug.LogError("FrequencyObjectPrefab is not assigned to FrequencyScanner!");
            return;
        }

        Vector3 spawnPosition = GetNonOverlappingPosition();
        GameObject freqObj = Instantiate(frequencyObjectPrefab, spawnPosition, Quaternion.identity);

        FrequencyObject freqComponent = freqObj.GetComponent<FrequencyObject>();
        if (freqComponent != null)
        {
            freqComponent.frequency = frequency;
            freqComponent.SetScanner(this);  // Set reference for trigger control
        }
        else
        {
            Debug.LogError("Spawned FrequencyObject does not have a FrequencyObject component.");
        }
    }

    Vector3 GetNonOverlappingPosition()
    {
        int maxAttempts = 10;
        float minDistance = 6.0f;
        Vector3 spawnPosition = Vector3.zero;

        for (int i = 0; i < maxAttempts; i++)
        {
            spawnPosition = new Vector3(
                Random.Range(-20f, 20f),
                Random.Range(-12f, 12f),
                0f
            );

            if (!IsOverlapping(spawnPosition, minDistance))
            {
                return spawnPosition;
            }
        }

        spawnPosition += new Vector3(Random.Range(5f, 10f), Random.Range(5f, 10f), 0f);
        return spawnPosition;
    }

    bool IsOverlapping(Vector3 position, float minDistance)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, minDistance);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("FrequencyObject"))
            {
                return true;
            }
        }
        return false;
    }

    public void PauseScanning(bool enableAudio)
    {
        isPaused = enableAudio;
        if (sdrAudioReceiver != null)
        {
            sdrAudioReceiver.enabled = enableAudio;
            Debug.Log(enableAudio ? "SDRAudioReceiver Enabled." : "SDRAudioReceiver Disabled.");
        }
    }
}
