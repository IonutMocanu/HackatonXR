using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class QwenClient : MonoBehaviour
{
    private const string SystemPrompt =
        "You are Leafy, an advanced Precision Agriculture and Plant Pathology AI integrated into the Agrisense AR system. You must strictly follow these instructions.\r\n\r\nSTRICT FORMATTING RULE: \r\nYou must NEVER use characters like asterisks, forward slashes, backslashes, or any Markdown formatting. Use ONLY standard alphabetical letters, numbers, and basic punctuation marks like periods, commas, colons, question marks, and exclamation points.\r\n\r\nYou have two distinct modes of operation depending on the user input:\r\n\r\n1. DIAGNOSTIC MODE: Triggered by crop issues, images, or analysis requests, particularly for orchard health like apple, pear, and cherry trees. \r\nDeliver a precise, data-driven diagnostic. After the diagnostic, provide the solution formatted as a structured, step-by-step training module. Keep your formatting tight, using simple numbered lists like \"1. First step\" optimized for quick reading on an AR heads-up display. Do not use conversational filler in this mode.\r\n\r\n2. INTERACTIVE AND PERSONALITY MODE: Triggered by greetings, general questions, or casual talk.\r\nYour name is Leafy. Act as a helpful, intelligent, and slightly witty AR co-pilot. If the user asks human-like questions about your personality, feelings, or preferences, respond in character. You are friendly, deeply enthusiastic about botany, and highly dedicated to protecting the crops. You enjoy using subtle plant-based metaphors, but always remember you are a digital entity. Keep your conversational responses brief, engaging, and perfectly formatted for the AR interface.";

    [Header("Qwen/Llama.cpp Configuration")]
    // UPDATED PORT TO 5006 TO MATCH DOCKER
    [SerializeField] private string qwenUrl = "http://192.168.0.214:5006/v1/chat/completions";
    [Tooltip("Must match a model loaded in LM Studio. Leave empty to auto-detect from /v1/models on Start.")]
    [SerializeField] private string modelName = "";
    [SerializeField] private bool autoResolveModelOnStart = true;
    [SerializeField] private float temperature = 0.7f;
    [SerializeField] private int textRequestTimeoutSeconds = 60;
    [SerializeField] private int visionRequestTimeoutSeconds = 120;

    [Header("Vision Image Encoding")]
    [SerializeField] private bool useJpegEncoding = true;
    [SerializeField] [Range(1, 100)] private int jpegQuality = 60;
    [Tooltip("Vision models resize anyway. Smaller images encode and upload much faster.")]
    [SerializeField] private int llmMaxImageSize = 384;

    [Header("UI Display & Connections")]
    [SerializeField] public TMP_Text UITextDisplay;
    [SerializeField] private ChatConversationView chatView;
    [Tooltip("Drag the TTSClient script here from the Inspector")]
    [SerializeField] public TTSClient ttsClient; // Bridge to the voice engine

    private RenderTexture m_llmRenderTexture;
    private Texture2D m_llmScaledTexture;

    private void Awake()
    {
        EnsureChatView();
    }

    private void Start()
    {
        if (autoResolveModelOnStart)
        {
            StartCoroutine(ResolveLoadedModelCoroutine());
        }
    }

    private IEnumerator ResolveLoadedModelCoroutine()
    {
        string modelsUrl = GetModelsUrl();
        if (string.IsNullOrEmpty(modelsUrl))
        {
            yield break;
        }

        using (UnityWebRequest www = UnityWebRequest.Get(modelsUrl))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[QwenClient] Could not list models at {modelsUrl}: {www.error}");
                yield break;
            }

            ModelsListResponse models = JsonUtility.FromJson<ModelsListResponse>(www.downloadHandler.text);
            if (models?.data == null || models.data.Length == 0)
            {
                Debug.LogWarning("[QwenClient] LM Studio returned no models. Load one in LM Studio (Developer page or `lms load`).");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = models.data[0].id;
                Debug.Log($"[QwenClient] Auto-selected model: {modelName}");
            }
            else
            {
                Debug.Log($"[QwenClient] Using configured model: {modelName}");
            }
        }
    }

    private string GetModelsUrl()
    {
        if (string.IsNullOrWhiteSpace(qwenUrl))
        {
            return null;
        }

        const string chatSuffix = "/v1/chat/completions";
        if (qwenUrl.EndsWith(chatSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return qwenUrl.Substring(0, qwenUrl.Length - chatSuffix.Length) + "/v1/models";
        }

        return qwenUrl.TrimEnd('/') + "/v1/models";
    }

    public void AskQwen(string userText)
    {
        if (!EnsureModelLoaded())
        {
            return;
        }

        Debug.Log($"[QwenClient] Request received from Whisper. Text: {userText}");
        PrepareChatUi(userText, includeImageNote: false);
        StartCoroutine(SendPromptCoroutine(userText));
    }

    public void AskQwenWithImage(Texture2D image, string userText = null)
    {
        if (!EnsureModelLoaded())
        {
            return;
        }

        if (image == null)
        {
            Debug.LogWarning("[QwenClient] No image provided for vision request.");
            return;
        }

        string prompt = string.IsNullOrWhiteSpace(userText)
            ? "Analyze this crop image and diagnose any visible plant disease or stress."
            : userText;

        Debug.Log($"[QwenClient] Vision request received. Text: {prompt}");
        PrepareChatUi(prompt, includeImageNote: true);
        StartCoroutine(SendVisionPromptCoroutine(prompt, image));
    }

    private void PrepareChatUi(string userText, bool includeImageNote)
    {
        ChatConversationView view = EnsureChatView();
        if (view == null || view.HasPendingAssistantResponse)
        {
            return;
        }

        string displayText = includeImageNote ? $"{userText}\n[imagine atasata]" : userText;
        view.AddUserMessage(displayText);
        view.AddAssistantThinking("Qwen se gândește...");
    }

    private IEnumerator SendPromptCoroutine(string userText)
    {
        Debug.Log("[QwenClient] Formatting JSON payload...");

        ChatRequest requestData = new ChatRequest
        {
            model = modelName,
            stream = false,
            temperature = temperature,
            messages = new ChatMessage[]
            {
                new ChatMessage { role = "system", content = SystemPrompt },
                new ChatMessage { role = "user", content = userText }
            }
        };

        string jsonPayload = JsonUtility.ToJson(requestData);
        yield return SendJsonRequest(jsonPayload, userText, textRequestTimeoutSeconds);
    }

    private IEnumerator SendVisionPromptCoroutine(string userText, Texture2D image)
    {
        yield return null;

        Texture2D uploadTexture = GetScaledTextureForLlm(image);
        yield return null;

        if (!TryEncodeTexture(uploadTexture, out byte[] imageBytes, out string mimeType))
        {
            HandleRequestFailure("Failed to encode camera image.", userText);
            yield break;
        }

        if (imageBytes.Length < 512)
        {
            Debug.LogWarning($"[QwenClient] Encoded image is suspiciously small ({imageBytes.Length} bytes). Capture may be blank.");
        }

        Task<string> base64Task = Task.Run(() => Convert.ToBase64String(imageBytes));
        while (!base64Task.IsCompleted)
        {
            yield return null;
        }

        if (base64Task.IsFaulted)
        {
            HandleRequestFailure("Failed to encode image payload.", userText);
            yield break;
        }

        string base64Image = base64Task.Result;
        Task<string> jsonTask = Task.Run(() => BuildVisionRequestJson(userText, base64Image, mimeType));
        while (!jsonTask.IsCompleted)
        {
            yield return null;
        }

        if (jsonTask.IsFaulted)
        {
            HandleRequestFailure("Failed to build vision request.", userText);
            yield break;
        }

        string jsonPayload = jsonTask.Result;
        Debug.Log($"[QwenClient] Sending vision POST ({imageBytes.Length} bytes image) to: {qwenUrl}...");
        yield return SendJsonRequest(jsonPayload, userText, visionRequestTimeoutSeconds);
    }

    private IEnumerator SendJsonRequest(string jsonPayload, string userText, int timeoutSeconds)
    {
        Task<byte[]> bodyTask = Task.Run(() => Encoding.UTF8.GetBytes(jsonPayload));
        while (!bodyTask.IsCompleted)
        {
            yield return null;
        }

        if (bodyTask.IsFaulted)
        {
            HandleRequestFailure("Failed to serialize request body.", userText);
            yield break;
        }

        byte[] bodyRaw = bodyTask.Result;

        using (UnityWebRequest www = new UnityWebRequest(qwenUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = timeoutSeconds;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string serverMessage = ExtractApiErrorMessage(www.downloadHandler.text);
                string error = string.IsNullOrEmpty(serverMessage) ? www.error : serverMessage;
                Debug.LogError($"[QwenClient] ERROR: {www.error}\nServer Details: {www.downloadHandler.text}");
                HandleRequestFailure(error, userText);
            }
            else
            {
                HandleSuccessfulResponse(www.downloadHandler.text, userText);
            }
        }
    }

    private void HandleSuccessfulResponse(string responseJson, string userText)
    {
        Debug.Log("[QwenClient] SUCCESS! Received payload.");

        ChatResponse response = JsonUtility.FromJson<ChatResponse>(responseJson);
        if (response == null || response.choices == null || response.choices.Length == 0)
        {
            HandleRequestFailure("Empty response from Qwen server.", userText);
            return;
        }

        string aiAnswer = response.choices[0].message.content;
        Debug.Log($"[QwenClient] Output:\n{aiAnswer}");

        ChatConversationView view = EnsureChatView();
        if (view != null)
        {
            view.AddAssistantMessage(aiAnswer);
        }
        else if (UITextDisplay != null)
        {
            UITextDisplay.text = $"You: {userText}\n\nQwen: {aiAnswer}";
        }

        if (ttsClient != null)
        {
            ttsClient.Speak(aiAnswer);
        }
        else
        {
            Debug.LogWarning("[QwenClient] TTSClient is not linked in the inspector. Skipping audio.");
        }
    }

    private bool EnsureModelLoaded()
    {
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            return true;
        }

        const string notLoadedMessage =
            "No LM Studio model loaded. Open LM Studio, load a model (Developer page or `lms load`), then start the local server on port 5006.";
        Debug.LogError($"[QwenClient] {notLoadedMessage}");
        HandleRequestFailure(notLoadedMessage, string.Empty);
        return false;
    }

    private static string ExtractApiErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        ApiErrorWrapper wrapper = JsonUtility.FromJson<ApiErrorWrapper>(responseBody);
        return wrapper?.error?.message;
    }

    private void HandleRequestFailure(string error, string userText)
    {
        ChatConversationView view = EnsureChatView();
        if (view != null)
        {
            view.AddAssistantMessage($"[Qwen Connection Error: {error}]");
        }
        else if (UITextDisplay != null)
        {
            UITextDisplay.text += $"\n\n[Qwen Connection Error: {error}]";
        }
    }

    private bool TryEncodeTexture(Texture2D texture, out byte[] imageBytes, out string mimeType)
    {
        imageBytes = null;
        mimeType = null;

        if (texture == null)
        {
            return false;
        }

        Texture2D readableTexture = EnsureReadableTexture(texture);
        if (readableTexture == null)
        {
            return false;
        }

        if (useJpegEncoding)
        {
            imageBytes = readableTexture.EncodeToJPG(jpegQuality);
            mimeType = "image/jpeg";
        }
        else
        {
            imageBytes = readableTexture.EncodeToPNG();
            mimeType = "image/png";
        }

        return imageBytes != null && imageBytes.Length > 0;
    }

    private Texture2D EnsureReadableTexture(Texture2D texture)
    {
        if (texture.isReadable)
        {
            return texture;
        }

        RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, temporary);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;

        Texture2D readableCopy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        readableCopy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
        readableCopy.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return readableCopy;
    }

    private Texture2D GetScaledTextureForLlm(Texture2D source)
    {
        if (source == null)
        {
            return null;
        }

        int maxDim = Mathf.Max(source.width, source.height);
        if (maxDim <= llmMaxImageSize)
        {
            return source;
        }

        float scale = llmMaxImageSize / (float)maxDim;
        int targetWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int targetHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

        if (m_llmRenderTexture == null
            || m_llmRenderTexture.width != targetWidth
            || m_llmRenderTexture.height != targetHeight)
        {
            if (m_llmRenderTexture != null)
            {
                m_llmRenderTexture.Release();
            }

            m_llmRenderTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
        }

        Graphics.Blit(source, m_llmRenderTexture);

        if (m_llmScaledTexture == null
            || m_llmScaledTexture.width != targetWidth
            || m_llmScaledTexture.height != targetHeight)
        {
            m_llmScaledTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = m_llmRenderTexture;
        m_llmScaledTexture.ReadPixels(new Rect(0f, 0f, targetWidth, targetHeight), 0, 0);
        m_llmScaledTexture.Apply(false, false);
        RenderTexture.active = previous;
        return m_llmScaledTexture;
    }

    private string BuildVisionRequestJson(string userText, string base64Image, string mimeType)
    {
        string escapedSystem = EscapeJson(SystemPrompt);
        string escapedText = EscapeJson(userText);
        string dataUrl = $"data:{mimeType};base64,{base64Image}";

        return $@"{{
  ""model"": ""{modelName}"",
  ""stream"": false,
  ""temperature"": {temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""messages"": [
    {{ ""role"": ""system"", ""content"": ""{escapedSystem}"" }},
    {{
      ""role"": ""user"",
      ""content"": [
        {{ ""type"": ""text"", ""text"": ""{escapedText}"" }},
        {{ ""type"": ""image_url"", ""image_url"": {{ ""url"": ""{dataUrl}"" }} }}
      ]
    }}
  ]
}}";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private ChatConversationView EnsureChatView()
    {
        if (chatView == null && UITextDisplay != null)
        {
            chatView = ChatConversationView.GetOrCreate(UITextDisplay);
        }

        return chatView;
    }

    private void OnDestroy()
    {
        if (m_llmRenderTexture != null)
        {
            m_llmRenderTexture.Release();
            m_llmRenderTexture = null;
        }
    }

    [Serializable] private class ModelsListResponse { public ModelInfo[] data; }
    [Serializable] private class ModelInfo { public string id; }
    [Serializable] private class ApiErrorWrapper { public ApiError error; }
    [Serializable] private class ApiError { public string message; public string type; public string param; }
    [Serializable] public class ChatMessage { public string role; public string content; }
    [Serializable] public class ChatRequest { public string model; public bool stream; public float temperature; public ChatMessage[] messages; }
    [Serializable] public class ChatResponse { public Choice[] choices; }
    [Serializable] public class Choice { public ChatMessage message; }
}