using UnityEngine;

public class SDRAudioReceiver : MonoBehaviour
{
    public SDRReceiver sdrReceiver;
    public AudioSource audioSource;

    void Update()
    {
        // Fetch audio buffer from SDRReceiver
        byte[] audioData = sdrReceiver.GetAudioBuffer();
        if (audioData.Length > 0)
        {
            AudioClip clip = AudioClip.Create("SDRStream", audioData.Length / 2, 1, 48000, false);
            clip.SetData(BytesToFloatArray(audioData), 0);

            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    float[] BytesToFloatArray(byte[] data)
    {
        int len = data.Length / 2;
        float[] floatData = new float[len];

        for (int i = 0; i < len; i++)
        {
            short sample = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
            floatData[i] = sample / 32768.0f;
        }

        return floatData;
    }
}
