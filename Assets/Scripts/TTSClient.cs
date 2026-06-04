using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(AudioSource))]
public class TTSClient : MonoBehaviour
{
    [Header("Orpheus TTS (see github.com/Lex-au/Orpheus-FastAPI)")]
    [SerializeField] private string host = "192.168.0.214";
    [SerializeField] private int port = 5005;

    [Header("Settings")]
    [SerializeField] private string voice = "tara";
    [SerializeField] private string model = "orpheus";
    [SerializeField] private float speed = 1.0f;
    [SerializeField] private int requestTimeoutSeconds = 90;
    [SerializeField] private int maxInputCharacters = 900;

    private AudioSource audioSource;
    private Coroutine speechCoroutine;
    private const float PauseBetweenSegments = 0.12f;

    private string OpenAiSpeechUrl => $"http://{host}:{port}/v1/audio/speech";
    private string LegacySpeakUrl => $"http://{host}:{port}/speak";

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        audioSource.enabled = true;
    }

    public void Speak(string text)
    {
        if (speechCoroutine != null)
        {
            StopCoroutine(speechCoroutine);
        }

        audioSource.Stop();
        speechCoroutine = StartCoroutine(SpeakAllText(text));
    }

    private IEnumerator SpeakAllText(string text)
    {
        string cleanText = PrepareTextForTts(text);
        if (string.IsNullOrWhiteSpace(cleanText))
        {
            Debug.LogWarning("[TTSClient] Skipping TTS because cleaned text is empty.");
            yield break;
        }

        List<string> segments = SplitIntoSegments(cleanText, Mathf.Max(200, maxInputCharacters));
        Debug.Log($"[TTSClient] Speaking {cleanText.Length} chars in {segments.Count} segment(s).");

        for (int i = 0; i < segments.Count; i++)
        {
            yield return GenerateAndPlayAudio(segments[i], i + 1, segments.Count);

            if (PauseBetweenSegments > 0f && i < segments.Count - 1)
            {
                yield return new WaitForSeconds(PauseBetweenSegments);
            }
        }

        speechCoroutine = null;
    }

    private IEnumerator GenerateAndPlayAudio(string cleanText, int segmentIndex, int segmentCount)
    {
        Debug.Log($"[TTSClient] Sending segment {segmentIndex}/{segmentCount} ({cleanText.Length} chars) to Orpheus.");

        var openAiPayload = new TtsRequest
        {
            model = model,
            input = cleanText,
            voice = voice,
            response_format = "wav",
            speed = speed
        };

        bool played = false;
        yield return SendTtsRequest(OpenAiSpeechUrl, "openai", JsonUtility.ToJson(openAiPayload), ok => played = ok);
        if (played)
        {
            yield return WaitForCurrentClip();
            yield break;
        }

        var legacyPayload = new LegacySpeakRequest { text = cleanText, voice = voice };
        yield return SendTtsRequest(LegacySpeakUrl, "speak", JsonUtility.ToJson(legacyPayload), ok => played = ok);
        if (played)
        {
            yield return WaitForCurrentClip();
        }

        if (!played)
        {
            Debug.LogError("[TTSClient] TTS failed on both /v1/audio/speech and /speak.");
        }
    }

    private IEnumerator SendTtsRequest(string endpoint, string schemaName, string payload, Action<bool> onDone)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
        Debug.Log($"[TTSClient] POST {endpoint} ({schemaName}), payload={bodyRaw.Length} bytes, timeout={requestTimeoutSeconds}s");

        using (UnityWebRequest www = new UnityWebRequest(endpoint, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Accept", "audio/wav");
            www.timeout = 0;

            float startedAt = Time.realtimeSinceStartup;
            UnityWebRequestAsyncOperation op = www.SendWebRequest();
            while (!op.isDone)
            {
                if (Time.realtimeSinceStartup - startedAt > requestTimeoutSeconds)
                {
                    www.Abort();
                    Debug.LogError($"[TTSClient] ({schemaName}) timed out after {requestTimeoutSeconds}s.");
                    onDone?.Invoke(false);
                    yield break;
                }
                yield return null;
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[TTSClient] ({schemaName}) failed. Code={(int)www.responseCode}, Error={www.error}, Body={www.downloadHandler.text}");
                onDone?.Invoke(false);
                yield break;
            }

            string contentType = www.GetResponseHeader("Content-Type") ?? "unknown";
            byte[] responseData = www.downloadHandler.data;
            float elapsed = Time.realtimeSinceStartup - startedAt;
            Debug.Log($"[TTSClient] ({schemaName}) OK in {elapsed:F1}s. content-type={contentType}, bytes={responseData?.Length ?? 0}");

            byte[] wavData = ExtractWavBytes(contentType, responseData);
            if (wavData == null || wavData.Length <= 44)
            {
                string bodyPreview = www.downloadHandler.text;
                if (!string.IsNullOrEmpty(bodyPreview) && bodyPreview.Length > 240)
                {
                    bodyPreview = bodyPreview.Substring(0, 240) + "...";
                }
                Debug.LogError($"[TTSClient] ({schemaName}) no WAV in response. Body: {bodyPreview}");
                onDone?.Invoke(false);
                yield break;
            }

            AudioClip clip = WavUtility.ToAudioClip(wavData);
            if (clip == null)
            {
                Debug.LogError($"[TTSClient] ({schemaName}) WAV decode failed.");
                onDone?.Invoke(false);
                yield break;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log($"[TTSClient] Playing clip: {clip.samples} samples @ {clip.frequency}Hz");
            onDone?.Invoke(true);
        }
    }

    private string PrepareTextForTts(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        string text = raw;
        text = Regex.Replace(text, @"\*\*(.*?)\*\*", "$1");
        text = Regex.Replace(text, @"^\s*\d+\.\s*", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"[^\w\s\.,!?-]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    private List<string> SplitIntoSegments(string text, int maxCharacters)
    {
        List<string> segments = new List<string>();
        int start = 0;

        while (start < text.Length)
        {
            int remaining = text.Length - start;
            if (remaining <= maxCharacters)
            {
                segments.Add(text.Substring(start).Trim());
                break;
            }

            int end = start + maxCharacters;
            int split = text.LastIndexOfAny(new[] { '.', '!', '?', ',', ';', ':' }, end - 1, maxCharacters);
            if (split <= start + 80)
            {
                split = text.LastIndexOf(' ', end - 1, maxCharacters);
            }

            if (split <= start)
            {
                split = end;
            }

            segments.Add(text.Substring(start, split - start + 1).Trim());
            start = split + 1;

            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
        }

        return segments;
    }

    private IEnumerator WaitForCurrentClip()
    {
        while (audioSource != null && audioSource.isPlaying)
        {
            yield return null;
        }
    }

    private byte[] ExtractWavBytes(string contentType, byte[] responseBytes)
    {
        if (responseBytes == null || responseBytes.Length == 0)
        {
            return null;
        }

        if (contentType.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
            WavUtility.HasRiffHeader(responseBytes))
        {
            return responseBytes;
        }

        string jsonText = Encoding.UTF8.GetString(responseBytes);
        if (jsonText.IndexOf("\"error\"", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Debug.LogError($"[TTSClient] Server returned error JSON: {jsonText}");
            return null;
        }

        // Some OpenAPI docs expose the response as a plain JSON "string".
        // If it's quoted, try interpreting it first as base64.
        string trimmed = jsonText.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
        {
            string rawString = trimmed.Substring(1, trimmed.Length - 2).Replace("\\n", "").Replace("\\\"", "\"");
            try
            {
                byte[] maybeWav = Convert.FromBase64String(rawString);
                if (HasLikelyAudioPayload(maybeWav))
                {
                    return maybeWav;
                }
            }
            catch
            {
                // Not base64; continue with object parsing below.
            }
        }

        TtsJsonResponse json = JsonUtility.FromJson<TtsJsonResponse>(jsonText);
        string base64 = json != null ? (!string.IsNullOrEmpty(json.audio) ? json.audio : json.data) : null;
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TTSClient] Base64 decode failed: {ex.Message}");
            return null;
        }
    }

    private bool HasLikelyAudioPayload(byte[] bytes)
    {
        return bytes != null && bytes.Length > 44;
    }

    [Serializable]
    private class TtsRequest
    {
        public string model;
        public string input;
        public string voice;
        public string response_format;
        public float speed;
    }

    [Serializable]
    private class LegacySpeakRequest
    {
        public string text;
        public string voice;
    }

    [Serializable]
    private class TtsJsonResponse
    {
        public string audio;
        public string data;
    }
}

public static class WavUtility
{
    public static bool HasRiffHeader(byte[] wavData)
    {
        return wavData != null &&
               wavData.Length > 12 &&
               wavData[0] == 'R' && wavData[1] == 'I' && wavData[2] == 'F' && wavData[3] == 'F' &&
               wavData[8] == 'W' && wavData[9] == 'A' && wavData[10] == 'V' && wavData[11] == 'E';
    }

    public static AudioClip ToAudioClip(byte[] wavData)
    {
        if (!HasRiffHeader(wavData))
        {
            Debug.LogError("[TTSClient] WAV decode failed: missing RIFF/WAVE header.");
            return null;
        }

        int channels = BitConverter.ToInt16(wavData, 22);
        int sampleRate = BitConverter.ToInt32(wavData, 24);
        int bitsPerSample = BitConverter.ToInt16(wavData, 34);
        int dataOffset = FindDataChunkOffset(wavData);

        if (channels <= 0 || sampleRate <= 0 || bitsPerSample != 16 || dataOffset < 0)
        {
            Debug.LogError($"[TTSClient] WAV format invalid. channels={channels}, rate={sampleRate}, bits={bitsPerSample}");
            return null;
        }

        int bytesPerSample = bitsPerSample / 8;
        int sampleCount = (wavData.Length - dataOffset) / bytesPerSample;
        float[] floatData = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(wavData, dataOffset + (i * 2));
            floatData[i] = sample / 32768.0f;
        }

        int frames = sampleCount / channels;
        if (frames <= 0)
        {
            return null;
        }

        AudioClip clip = AudioClip.Create("TTS_Clip", frames, channels, sampleRate, false);
        clip.SetData(floatData, 0);
        return clip;
    }

    private static int FindDataChunkOffset(byte[] wavData)
    {
        int i = 12;
        while (i + 8 <= wavData.Length)
        {
            bool isData =
                wavData[i] == 'd' &&
                wavData[i + 1] == 'a' &&
                wavData[i + 2] == 't' &&
                wavData[i + 3] == 'a';

            int chunkSize = BitConverter.ToInt32(wavData, i + 4);
            int chunkDataStart = i + 8;
            if (isData)
            {
                return chunkDataStart;
            }

            if (chunkSize < 0)
            {
                return -1;
            }

            i = chunkDataStart + chunkSize;
        }

        return -1;
    }
}
