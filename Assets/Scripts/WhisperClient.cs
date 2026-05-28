using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android; 
#endif

public class WhisperClient : MonoBehaviour
{
    [Header("Whisper API Configuration")]
    [SerializeField] private string whisperUrl = "http://192.168.0.214:8080/v1/audio/transcriptions";

    [Header("Conexiuni UI & Qwen")]
    [SerializeField] public TMP_Text UITextDisplay;
    [Tooltip("Trage scriptul QwenClient aici din Inspector")]
    [SerializeField] public QwenClient qwenClient; // Puntea către al doilea script

    [Header("Audio Settings")]
    [SerializeField] private float microphoneGain = 5.0f;

    private AudioClip recordingClip;
    private string deviceName;
    private int nativeHardwareSampleRate = 48000;
    private const int TargetWhisperRate = 16000;

    private bool m_isRecording;

    void Start()
    {
        m_isRecording = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
#endif

        if (Microphone.devices.Length > 0)
        {
            deviceName = Microphone.devices[0];
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
            if (maxFreq > 0) nativeHardwareSampleRate = maxFreq;
            Debug.Log($"Selected Mic: {deviceName} operating natively at {nativeHardwareSampleRate}Hz");
        }
        else
        {
            Debug.LogError("No microphone detected!");
        }
    }

    public void Recording()
    {
        if (m_isRecording)
        {
            StopRecording();
            m_isRecording = false;
        }
        else
        {
            StartRecording();
            m_isRecording = true;
        }
    }

    public void StartRecording()
    {
        if (deviceName == null) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone)) return;
#endif

        if (UITextDisplay != null) UITextDisplay.text = "Te ascult...";
        recordingClip = Microphone.Start(deviceName, false, 30, nativeHardwareSampleRate);
    }

    public void StopRecording()
    {
        if (deviceName == null || !Microphone.IsRecording(deviceName)) return;

        int lastPosition = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        if (lastPosition < (nativeHardwareSampleRate / 2))
        {
            if (UITextDisplay != null) UITextDisplay.text = "Înregistrare prea scurtă.";
            return;
        }

        if (UITextDisplay != null) UITextDisplay.text = "Whisper procesează...";

        byte[] wavData = ConvertToWavResampled(recordingClip, lastPosition, TargetWhisperRate);
        StartCoroutine(UploadAudio(wavData));
    }

    private IEnumerator UploadAudio(byte[] wavBytes)
    {
        var formData = new System.Collections.Generic.List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", wavBytes, "audio.wav", "audio/wav"),
            new MultipartFormDataSection("model", "ggml-base.bin")
        };

        using (UnityWebRequest www = UnityWebRequest.Post(whisperUrl, formData))
        {
            www.timeout = 60;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Whisper Error: {www.error}");
                if (UITextDisplay != null) UITextDisplay.text = $"Whisper Error: {www.error}";
            }
            else
            {
                WhisperResponse result = JsonUtility.FromJson<WhisperResponse>(www.downloadHandler.text);

                if (string.IsNullOrWhiteSpace(result.text))
                {
                    if (UITextDisplay != null) UITextDisplay.text = "[Nu am înțeles nimic / Liniște]";
                }
                else
                {
                    // Whisper a reușit! Trimitem textul mai departe către Qwen.
                    if (UITextDisplay != null) UITextDisplay.text = $"Tu: {result.text}\n\nQwen se gândește...";

                    if (qwenClient != null)
                    {
                        qwenClient.AskQwen(result.text); // Apelează scriptul 2
                    }
                    else
                    {
                        Debug.LogWarning("Ai uitat să legi QwenClient în Inspector!");
                    }
                }
            }
        }
    }

    private byte[] ConvertToWavResampled(AudioClip clip, int targetLength, int targetSampleRate)
    {
        float[] samples = new float[targetLength * clip.channels];
        clip.GetData(samples, 0);

        float sampleRatio = (float)clip.frequency / targetSampleRate;
        int newLength = Mathf.FloorToInt(samples.Length / clip.channels / sampleRatio);

        short[] intData = new short[newLength];

        for (int i = 0; i < newLength; i++)
        {
            float sourceIndex = i * sampleRatio;
            int frameIndex = Mathf.FloorToInt(sourceIndex);
            int arrayIndex = frameIndex * clip.channels;

            float sample = (arrayIndex < samples.Length) ? samples[arrayIndex] : 0f;
            float dither = UnityEngine.Random.Range(-0.0001f, 0.0001f);
            sample = Mathf.Clamp((sample + dither) * microphoneGain, -1f, 1f);

            intData[i] = (short)(sample * 32767);
        }

        using (MemoryStream memStream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(memStream))
        {
            writer.Write(new byte[44]);
            foreach (short s in intData) writer.Write(s);

            int dataLength = (int)memStream.Length - 44;
            int fileSize = dataLength + 36;

            memStream.Seek(0, SeekOrigin.Begin);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(targetSampleRate);
            writer.Write(targetSampleRate * 1 * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(dataLength);

            return memStream.ToArray();
        }
    }

    [Serializable]
    public class WhisperResponse { public string text; }
}