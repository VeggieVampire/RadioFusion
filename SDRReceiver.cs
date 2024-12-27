using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;
using System;

public class SDRReceiver : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private Thread sdrThread;
    public string host = "192.168.0.154";  // SDR IP
    public int port = 1234;

    private byte[] buffer = new byte[16384];  // SDR buffer
    public LineRenderer lineRenderer;
    private ConcurrentQueue<float[]> dataQueue = new ConcurrentQueue<float[]>();

    private MemoryStream audioBuffer = new MemoryStream();
    private byte[] audioTempBuffer = new byte[8192];
    private float[] latestAmplitudes;  // Store latest amplitudes

    // Frequency queue
    private ConcurrentQueue<float> frequencyQueue = new ConcurrentQueue<float>();
    private float currentFrequency = 99500000;  // Default 99.5 MHz

    void Start()
    {
        sdrThread = new Thread(new ThreadStart(ConnectToSDR));
        sdrThread.IsBackground = true;
        sdrThread.Start();
        lineRenderer.positionCount = buffer.Length / 2;
    }

    void ConnectToSDR()
    {
        try
        {
            client = new TcpClient(host, port);
            stream = client.GetStream();
            Debug.Log("Connected to SDR!");

            // Start streaming at default frequency
            SendFrequencyCommand(currentFrequency);

            while (true)
            {
                int bytesRead = stream.Read(audioTempBuffer, 0, audioTempBuffer.Length);
                if (bytesRead > 0)
                {
                    // Split data for visualization and audio
                    ProcessAmplitudeData(audioTempBuffer, bytesRead);
                    StoreAudioData(audioTempBuffer, bytesRead);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("SDR Connection Error: " + e.Message);
        }
    }

    // Process amplitude for visualization
    void ProcessAmplitudeData(byte[] data, int length)
    {
        float[] amplitudes = new float[length / 2];
        for (int i = 0; i < length / 2; i++)
        {
            amplitudes[i] = data[i] / 255.0f;
        }
        dataQueue.Enqueue(amplitudes);
        latestAmplitudes = amplitudes;  // Store for scanner
    }

    // Store audio data for playback
    void StoreAudioData(byte[] data, int length)
    {
        lock (audioBuffer)
        {
            audioBuffer.Write(data, 0, length);
            if (audioBuffer.Length > 48000 * 2 * 5)  // Keep 5 seconds of audio max
            {
                audioBuffer.SetLength(48000 * 2 * 5);
                audioBuffer.Position = 0;
            }
        }
    }

    public float GetCurrentFrequency()
    {
        return currentFrequency;
    }

    // Expose audio data for SDRAudioReceiver
    public byte[] GetAudioBuffer()
    {
        lock (audioBuffer)
        {
            byte[] audioData = audioBuffer.ToArray();
            audioBuffer.SetLength(0);  // Clear after reading
            return audioData;
        }
    }

    // Expose latest amplitude data for FrequencyScanner
    public float[] GetLatestAmplitudes()
    {
        return latestAmplitudes ?? new float[0];  // Return empty array if no data
    }

    void Update()
    {
        while (frequencyQueue.TryDequeue(out float frequency))
        {
            SendFrequencyCommand(frequency);
        }

        if (dataQueue.TryDequeue(out float[] amplitudes))
        {
            ProcessSDRData(amplitudes);
        }
    }

    void ProcessSDRData(float[] amplitudes)
    {
        int pointCount = amplitudes.Length;
        if (lineRenderer.positionCount != pointCount)
        {
            lineRenderer.positionCount = pointCount;
        }

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 position = new Vector3(i * 0.1f, amplitudes[i] * 40, 0);
            lineRenderer.SetPosition(i, position);
        }
    }

    public void QueueFrequencyChange(float frequency)
    {
        frequencyQueue.Enqueue(frequency);
        Debug.Log("Queued Frequency Change: " + (frequency / 1e6f) + " MHz");
    }

    private void SendFrequencyCommand(float frequency)
    {
        try
        {
            byte[] command = System.Text.Encoding.ASCII.GetBytes($"FREQ {frequency}\n");
            stream.Write(command, 0, command.Length);
            Debug.Log("Streaming at Frequency: " + (frequency / 1e6f) + " MHz");
            currentFrequency = frequency;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to send frequency command: " + e.Message);
        }
    }

    void OnApplicationQuit()
    {
        sdrThread.Abort();
        stream.Close();
        client.Close();
    }
}
