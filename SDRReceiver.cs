using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;
using System;
using System.Collections.Generic;

public class SDRReceiver : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private Thread sdrThread;

    public string host = "192.168.0.154";
    public int port = 1234;

    private byte[] buffer = new byte[16384];
    public LineRenderer lineRenderer;
    private ConcurrentQueue<float[]> dataQueue = new ConcurrentQueue<float[]>();

    private MemoryStream audioBuffer = new MemoryStream();
    private byte[] audioTempBuffer = new byte[8192];
    private float[] latestAmplitudes;

    private ConcurrentQueue<float> frequencyQueue = new ConcurrentQueue<float>();
    private float currentFrequency = 99500000;

    public uint centerFrequency = 100000000;
    public uint stationFrequency = 99500000;

    private uint tunerGainIndex = 20;
    private uint minGainIndex = 0;
    private uint maxGainIndex = 200;

    private Dictionary<KeyCode, (byte command, bool enabled)> sdrCommandToggles = new Dictionary<KeyCode, (byte, bool)>
    {
        { KeyCode.Z, (0x05, false) },
        { KeyCode.X, (0x09, false) },
        { KeyCode.C, (0x08, false) },
        { KeyCode.V, (0x0E, false) },
        { KeyCode.B, (0x0A, true) },
        { KeyCode.N, (0x03, true) },
    };

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

            SetupSDR();
            TuneToStation(stationFrequency);

            while (true)
            {
                int bytesRead = stream.Read(audioTempBuffer, 0, audioTempBuffer.Length);
                if (bytesRead > 0)
                {
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

    void SetupSDR()
    {
        SendSDRCommand(0x02, 3200000);
        foreach (var cmd in sdrCommandToggles)
        {
            SendSDRCommand(cmd.Value.command, cmd.Value.enabled ? 1u : 0u);
        }
        SendSDRCommand(0x0D, tunerGainIndex);
    }

    public void TuneToStation(uint frequency)
    {
        stationFrequency = frequency;
        uint offset = 1000000;

        if (Math.Abs(stationFrequency - centerFrequency) > offset)
        {
            centerFrequency = stationFrequency;
            SendSDRCommand(0x01, centerFrequency);
        }

        uint tuneOffset = stationFrequency - centerFrequency;
        SendFrequencyCommand(tuneOffset);
    }

    private void SendFrequencyCommand(uint offset)
    {
        SendSDRCommand(0x01, centerFrequency + offset);
    }

    private void SendSDRCommand(byte commandType, uint value)
    {
        byte[] command = new byte[5];
        command[0] = commandType;

        byte[] valueBytes = BitConverter.GetBytes(value);
        Array.Reverse(valueBytes);
        Array.Copy(valueBytes, 0, command, 1, 4);

        stream.Write(command, 0, command.Length);
        Debug.Log($"[SDR] Command 0x{commandType:X2} set to {value}");
    }

    void ProcessAmplitudeData(byte[] data, int length)
    {
        float[] amplitudes = new float[length / 2];
        for (int i = 0; i < length / 2; i++)
        {
            amplitudes[i] = data[i] / 255.0f;
        }
        dataQueue.Enqueue(amplitudes);
        latestAmplitudes = amplitudes;
    }

    public void ProcessSDRData(float[] amplitudes)
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
        Debug.Log($"Queued frequency change to: {frequency / 1e6f} MHz");
    }

    void StoreAudioData(byte[] data, int length)
    {
        lock (audioBuffer)
        {
            audioBuffer.Write(data, 0, length);
            if (audioBuffer.Length > 48000 * 2 * 5)
            {
                audioBuffer.SetLength(48000 * 2 * 5);
                audioBuffer.Position = 0;
            }
        }
    }

    public byte[] GetAudioBuffer()
    {
        lock (audioBuffer)
        {
            byte[] audioData = audioBuffer.ToArray();
            audioBuffer.SetLength(0);
            return audioData;
        }
    }

    public float[] GetLatestAmplitudes()
    {
        return latestAmplitudes ?? new float[0];
    }

    void Update()
    {
        while (frequencyQueue.TryDequeue(out float frequency))
        {
            TuneToStation((uint)frequency);
        }

        if (dataQueue.TryDequeue(out float[] amplitudes))
        {
            ProcessSDRData(amplitudes);
        }

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            AdjustTunerGain(-1);
        }
        if (Input.GetKeyDown(KeyCode.Period))
        {
            AdjustTunerGain(1);
        }

        foreach (var cmd in sdrCommandToggles.Keys)
        {
            if (Input.GetKeyDown(cmd))
            {
                ToggleSDRCommand(cmd);
            }
        }
    }

    void AdjustTunerGain(int direction)
    {
        tunerGainIndex = (uint)Mathf.Clamp(tunerGainIndex + direction, minGainIndex, maxGainIndex);
        SendSDRCommand(0x0D, tunerGainIndex);
        Debug.Log($"[SDR] Tuner Gain adjusted to index {tunerGainIndex}");
    }

    void ToggleSDRCommand(KeyCode key)
    {
        if (sdrCommandToggles.ContainsKey(key))
        {
            var cmd = sdrCommandToggles[key];
            cmd.enabled = !cmd.enabled;
            sdrCommandToggles[key] = cmd;

            SendSDRCommand(cmd.command, cmd.enabled ? 1u : 0u);
            Debug.Log($"[SDR] {key} - 0x{cmd.command:X2} toggled {(cmd.enabled ? "ON" : "OFF")}");
        }
    }

    void OnApplicationQuit()
    {
        sdrThread.Abort();
        stream.Close();
        client.Close();
    }
}

