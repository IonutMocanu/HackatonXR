using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class QwenClient : MonoBehaviour
{
    [Header("Qwen/Llama.cpp Configuration")]
    // UPDATED PORT TO 5006 TO MATCH DOCKER
    [SerializeField] private string qwenUrl = "http://192.168.0.214:5006/v1/chat/completions";

    [Header("UI Display & Connections")]
    [SerializeField] public TMP_Text UITextDisplay;
    [SerializeField] private ChatConversationView chatView;
    [Tooltip("Drag the TTSClient script here from the Inspector")]
    [SerializeField] public TTSClient ttsClient; // Bridge to the voice engine

    private void Awake()
    {
        EnsureChatView();
    }

    public void AskQwen(string userText)
    {
        Debug.Log($"[QwenClient] Request received from Whisper. Text: {userText}");

        ChatConversationView view = EnsureChatView();
        if (view != null && !view.HasPendingAssistantResponse)
        {
            view.AddUserMessage(userText);
            view.AddAssistantThinking("Qwen se gândește...");
        }

        StartCoroutine(SendPromptCoroutine(userText));
    }

    private IEnumerator SendPromptCoroutine(string userText)
    {
        Debug.Log("[QwenClient] Formatting JSON payload...");

        ChatRequest requestData = new ChatRequest
        {
            model = "qwen",
            stream = false,
            temperature = 0.7f,
            messages = new ChatMessage[]
            {
                new ChatMessage { role = "system", content = "You are an advanced Precision Agriculture and Plant Pathology AI integrated into an AR headset. Your goal is to diagnose issues and train the user on treatments. When analyzing a crop issue, first deliver a precise, data-driven diagnostic. After the diagnostic, provide the solution formatted as a structured, step-by-step training module. Keep your formatting tight, using numbered steps optimized for quick reading on an AR heads-up display. Do not use conversational filler." },
                new ChatMessage { role = "user", content = userText }
            }
        };

        string jsonPayload = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

        Debug.Log($"[QwenClient] Sending POST request to: {qwenUrl}...");

        using (UnityWebRequest www = new UnityWebRequest(qwenUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            www.timeout = 60;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[QwenClient] ERROR: {www.error}\nServer Details: {www.downloadHandler.text}");
                ChatConversationView view = EnsureChatView();
                if (view != null)
                {
                    view.AddAssistantMessage($"[Qwen Connection Error: {www.error}]");
                }
                else if (UITextDisplay != null)
                {
                    UITextDisplay.text += $"\n\n[Qwen Connection Error: {www.error}]";
                }
            }
            else
            {
                Debug.Log("[QwenClient] SUCCESS! Received payload.");

                ChatResponse response = JsonUtility.FromJson<ChatResponse>(www.downloadHandler.text);

                if (response != null && response.choices != null && response.choices.Length > 0)
                {
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

                    // THIS IS NEW: Pass the final answer to the TTS Client!
                    if (ttsClient != null)
                    {
                        ttsClient.Speak(aiAnswer);
                    }
                    else
                    {
                        Debug.LogWarning("[QwenClient] TTSClient is not linked in the inspector. Skipping audio.");
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

    [Serializable] public class ChatMessage { public string role; public string content; }
    [Serializable] public class ChatRequest { public string model; public bool stream; public float temperature; public ChatMessage[] messages; }
    [Serializable] public class ChatResponse { public Choice[] choices; }
    [Serializable] public class Choice { public ChatMessage message; }
}