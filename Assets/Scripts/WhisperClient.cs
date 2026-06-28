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
    [SerializeField] private string whisperUrl = "https://voice.xrici.online/inference";

    [Header("Conexiuni UI & Qwen")]
    [SerializeField] public TMP_Text UITextDisplay;
    [SerializeField] private ChatConversationView chatView;
    [Tooltip("Trage scriptul QwenClient aici din Inspector")]
    [SerializeField] public QwenClient qwenClient;

    [Header("Audio Settings")]
    [SerializeField] private float microphoneGain = 5.0f;

    private AudioClip recordingClip;
    private string deviceName;
    private int nativeHardwareSampleRate = 48000;
    private const int TargetWhisperRate = 16000;

    private bool m_isRecording;
    private float recordStartTime; // Adăugat pentru fallback-ul de timp

    private void Awake()
    {
        EnsureChatView();
    }

    void Start()
    {
        m_isRecording = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
#endif
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
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            ChatConversationView viewUI = EnsureChatView();
            if (viewUI != null) viewUI.ShowStatus("Aprobă permisiunea și apasă din nou.");
            return;
        }
#endif

        if (deviceName == null && Microphone.devices.Length > 0)
        {
            deviceName = Microphone.devices[0];
            Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);
            if (maxFreq > 0) nativeHardwareSampleRate = maxFreq;
            Debug.Log($"[WhisperClient] Microfon setat: {deviceName} la {nativeHardwareSampleRate}Hz");
        }

        if (deviceName == null)
        {
            Debug.LogError("[WhisperClient] Nu găsesc niciun microfon activ!");
            return;
        }

        Debug.Log("[WhisperClient] ---> ÎNREGISTRAREA A ÎNCEPUT <---");

        ChatConversationView view = EnsureChatView();
        if (view != null) view.ShowStatus("Te ascult...");
        else if (UITextDisplay != null) UITextDisplay.text = "Te ascult...";

        recordStartTime = Time.time;
        recordingClip = Microphone.Start(deviceName, false, 30, nativeHardwareSampleRate);
    }

    public void StopRecording()
    {
        if (deviceName == null || !Microphone.IsRecording(deviceName)) return;

        Debug.Log("[WhisperClient] ---> OPRIM MICROFONUL <---");
        int lastPosition = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        float elapsed = Time.time - recordStartTime;
        Debug.Log($"[WhisperClient] Timp fizic scurs: {elapsed:F2}s, Pozitie raportată de Unity: {lastPosition}");

        // REZOLVARE BUG QUEST: Unity raportează uneori 0 position pe Android chiar dacă ai vorbit
        if (lastPosition == 0 && elapsed > 0.5f)
        {
            lastPosition = (int)(elapsed * nativeHardwareSampleRate);
            Debug.Log($"[WhisperClient] Am detectat bug-ul Unity (pozitie 0). Am estimat poziția corectă la: {lastPosition}");
        }

        if (lastPosition < (nativeHardwareSampleRate / 2))
        {
            Debug.LogWarning("[WhisperClient] Anulat: Înregistrare sub 0.5 secunde!");
            ChatConversationView view = EnsureChatView();
            if (view != null) view.ShowStatus("Înregistrare prea scurtă.");
            else if (UITextDisplay != null) UITextDisplay.text = "Înregistrare prea scurtă.";
            return;
        }

        ChatConversationView processingView = EnsureChatView();
        if (processingView != null) processingView.ShowStatus("Whisper procesează...");
        else if (UITextDisplay != null) UITextDisplay.text = "Whisper procesează...";

        Debug.Log("[WhisperClient] Convertim clipul în WAV...");
        byte[] wavData = ConvertToWavResampled(recordingClip, lastPosition, TargetWhisperRate);
        Debug.Log($"[WhisperClient] Pachet creat ({wavData.Length} bytes). Lansăm cererea către cloud...");

        StartCoroutine(UploadAudio(wavData));
    }

    private IEnumerator UploadAudio(byte[] wavBytes)
    {
        float uploadStart = Time.time;
        Debug.Log($"[WhisperClient] POST către: {whisperUrl}");

        var formData = new System.Collections.Generic.List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", wavBytes, "audio.wav", "audio/wav"),
            new MultipartFormDataSection("model", "ggml-base.en.bin")
        };

        using (UnityWebRequest www = UnityWebRequest.Post(whisperUrl, formData))
        {
            www.timeout = 60;
            yield return www.SendWebRequest();

            float requestDuration = Time.time - uploadStart;
            Debug.Log($"[WhisperClient] Request terminat în {requestDuration:F2}s. Status: {www.result}");

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[WhisperClient] EROARE SERVER: {www.error} | Detalii: {www.downloadHandler.text}");
                ChatConversationView view = EnsureChatView();
                if (view != null) view.ShowStatus($"Eroare rețea: {www.error}");
                else if (UITextDisplay != null) UITextDisplay.text = $"Eroare rețea: {www.error}";
            }
            else
            {
                Debug.Log($"[WhisperClient] RĂSPUNS: {www.downloadHandler.text}");
                WhisperResponse result = JsonUtility.FromJson<WhisperResponse>(www.downloadHandler.text);

                if (string.IsNullOrWhiteSpace(result.text))
                {
                    ChatConversationView view = EnsureChatView();
                    if (view != null) view.ShowStatus("[Nu am înțeles nimic / Liniște]");
                    else if (UITextDisplay != null) UITextDisplay.text = "[Nu am înțeles nimic / Liniște]";
                }
                else
                {
                    Debug.Log($"[WhisperClient] Text recunoscut cu succes. Îl trimit la Qwen...");
                    ChatConversationView view = EnsureChatView();
                    if (view != null)
                    {
                        view.AddUserMessage(result.text);
                        view.AddAssistantThinking("Qwen se gândește...");
                    }
                    else if (UITextDisplay != null)
                    {
                        UITextDisplay.text = $"Tu: {result.text}\n\nQwen se gândește...";
                    }

                    if (qwenClient != null)
                    {
                        qwenClient.AskQwen(result.text);
                    }
                    else
                    {
                        Debug.LogWarning("[WhisperClient] Ai uitat să legi QwenClient în Inspector!");
                    }
                }
            }
        }
    }

    private ChatConversationView EnsureChatView()
    {
        if (chatView == null && UITextDisplay != null)
        {
            chatView = ChatConversationView.GetOrCreate(UITextDisplay);
        }

        return chatView;
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