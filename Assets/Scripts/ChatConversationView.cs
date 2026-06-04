using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatConversationView : MonoBehaviour
{
    private static ChatConversationView activeInstance;

    [Header("Runtime UI")]
    [SerializeField] private TMP_Text legacyText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;

    [Header("Chat Area")]
    [Tooltip("Optional. If set, the chat fills this rect. Otherwise the script climbs up to the page root.")]
    [SerializeField] private RectTransform chatAreaOverride;
    [Tooltip("Name prefix of the page root the chat should fill (keeps the panel's original edges).")]
    [SerializeField] private string pageRootNamePrefix = "Library Page";
    [Tooltip("Inset from the page edges so the original border stays visible.")]
    [SerializeField] private float edgeMargin = 16f;
    [Tooltip("Horizontal shift of the whole chat. Negative moves it to the left.")]
    [SerializeField] private float chatHorizontalShift = 0f;
    [Tooltip("Corner radius (px) of the chat bubbles.")]
    [SerializeField] private int bubbleCornerRadius = 10;
    [Tooltip("How far to bring the chat toward the viewer (local -Z). Larger = more in front.")]
    [SerializeField] private float chatForwardOffset = 0.25f;

    [Header("Bubble Layout")]
    [SerializeField] private float maxBubbleWidth = 560f;
    [SerializeField] private float horizontalPadding = 24f;
    [SerializeField] private float verticalPadding = 16f;
    [SerializeField] private float messageSpacing = 12f;
    [SerializeField] private float fontSize = 24f;
    [SerializeField] private Color userBubbleColor = new Color(0.0f, 0.48f, 0.95f, 0.92f);
    [SerializeField] private Color assistantBubbleColor = new Color(0.12f, 0.15f, 0.18f, 0.92f);
    [SerializeField] private Color statusColor = new Color(0.72f, 0.78f, 0.85f, 1f);

    private TMP_FontAsset fontAsset;
    private Material fontMaterial;
    private TMP_Text pendingAssistantText;
    private bool initialized;

    public bool HasPendingAssistantResponse => pendingAssistantText != null;

    public static ChatConversationView GetOrCreate(TMP_Text anchorText)
    {
        // Hide any placeholder ("New Text") on the anchor, even if the chat
        // already exists and is hosted on a different text object.
        if (anchorText != null)
        {
            anchorText.text = string.Empty;
            anchorText.enabled = false;
        }

        if (activeInstance != null)
        {
            return activeInstance;
        }

        if (anchorText == null)
        {
            return null;
        }

        ChatConversationView view = anchorText.GetComponent<ChatConversationView>();
        if (view == null)
        {
            view = anchorText.gameObject.AddComponent<ChatConversationView>();
        }

        view.legacyText = anchorText;
        activeInstance = view;
        return view;
    }

    private void Awake()
    {
        if (legacyText == null)
        {
            legacyText = GetComponent<TMP_Text>();
        }

        if (activeInstance == null)
        {
            activeInstance = this;
        }

        Initialize();
    }

    public void ShowStatus(string status)
    {
        Initialize();
        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        CreateMessage(status, false, true);
        RequestScrollToBottom();
    }

    public void AddUserMessage(string message)
    {
        Initialize();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        pendingAssistantText = null;
        CreateMessage(message, true, false);
        RequestScrollToBottom();
    }

    public void AddAssistantThinking(string status)
    {
        Initialize();
        pendingAssistantText = CreateMessage(status, false, false);
        RequestScrollToBottom();
    }

    public void AddAssistantMessage(string message)
    {
        Initialize();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (pendingAssistantText != null)
        {
            pendingAssistantText.text = Sanitize(message);
            pendingAssistantText = null;
        }
        else
        {
            CreateMessage(message, false, false);
        }

        if (contentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
        RequestScrollToBottom();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (legacyText == null)
        {
            legacyText = GetComponent<TMP_Text>();
        }

        if (legacyText != null)
        {
            fontAsset = legacyText.font;
            fontMaterial = legacyText.fontSharedMaterial;
            // Hide the original single-line text WITHOUT deactivating the
            // GameObject, otherwise this component (which lives on the same
            // object) becomes inactive and can no longer start coroutines.
            legacyText.text = string.Empty;
            legacyText.enabled = false;
        }

        scrollRect = scrollRect != null ? scrollRect : CreateScrollRect();
        if (scrollRect == null)
        {
            Debug.LogError("[ChatConversationView] Could not create chat ScrollRect.");
            return;
        }

        contentRoot = contentRoot != null ? contentRoot : scrollRect.content;
        ConfigureContentRoot();
        DestroyExistingChildren(contentRoot);

        initialized = true;
    }

    private ScrollRect CreateScrollRect()
    {
        RectTransform host = ResolveChatArea();
        if (host == null)
        {
            host = transform as RectTransform;
        }

        // RectMask2D clips the scroll without needing a visible (colored) graphic.
        GameObject scrollObject = new GameObject("LLM Chat Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform scrollTransform = scrollObject.GetComponent<RectTransform>();
        scrollTransform.SetParent(host, false);
        // Fill the whole page but keep an inset so the panel's original edges stay visible.
        scrollTransform.anchorMin = Vector2.zero;
        scrollTransform.anchorMax = Vector2.one;
        scrollTransform.offsetMin = new Vector2(edgeMargin + chatHorizontalShift, edgeMargin);
        scrollTransform.offsetMax = new Vector2(-edgeMargin + chatHorizontalShift, -edgeMargin);
        scrollTransform.localScale = Vector3.one;
        scrollTransform.localRotation = Quaternion.identity;
        // Bring the chat toward the viewer (local -Z) so it sits in front of the page.
        scrollTransform.localPosition = new Vector3(scrollTransform.localPosition.x, scrollTransform.localPosition.y, -Mathf.Abs(chatForwardOffset));
        scrollTransform.SetAsLastSibling();

        Image background = scrollObject.GetComponent<Image>();
        // Fully transparent: no black block, but still a raycast target so the
        // user can drag/scroll over empty areas.
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = true;

        GameObject contentObject = new GameObject("LLM Chat Content", typeof(RectTransform));
        RectTransform contentTransform = contentObject.GetComponent<RectTransform>();
        contentTransform.SetParent(scrollTransform, false);
        contentTransform.anchorMin = new Vector2(0f, 1f);
        contentTransform.anchorMax = new Vector2(1f, 1f);
        contentTransform.pivot = new Vector2(0.5f, 1f);
        contentTransform.offsetMin = Vector2.zero;
        contentTransform.offsetMax = Vector2.zero;

        ScrollRect createdScrollRect = scrollObject.GetComponent<ScrollRect>();
        createdScrollRect.content = contentTransform;
        createdScrollRect.viewport = scrollTransform;
        createdScrollRect.horizontal = false;
        createdScrollRect.vertical = true;
        createdScrollRect.movementType = ScrollRect.MovementType.Clamped;
        createdScrollRect.scrollSensitivity = 28f;
        return createdScrollRect;
    }

    private RectTransform ResolveChatArea()
    {
        if (chatAreaOverride != null)
        {
            return chatAreaOverride;
        }

        Transform start = legacyText != null ? legacyText.transform : transform;
        RectTransform fallback = start.parent as RectTransform;
        float largestArea = 0f;

        for (Transform t = start.parent; t != null; t = t.parent)
        {
            RectTransform rt = t as RectTransform;
            if (rt == null)
            {
                break; // left the UI (Canvas) hierarchy
            }

            if (!string.IsNullOrEmpty(pageRootNamePrefix)
                && t.name.StartsWith(pageRootNamePrefix, StringComparison.Ordinal))
            {
                return rt; // the page root: fills the whole page, keeps panel edges
            }

            float area = Mathf.Abs(rt.rect.width * rt.rect.height);
            if (area > largestArea)
            {
                largestArea = area;
                fallback = rt;
            }
        }

        return fallback;
    }

    private void ConfigureContentRoot()
    {
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = messageSpacing;
        layout.padding = new RectOffset(16, 16, 16, 16);

        ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private TMP_Text CreateMessage(string message, bool fromUser, bool status)
    {
        GameObject row = new GameObject(status ? "Status Row" : fromUser ? "User Message Row" : "LLM Message Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        RectTransform rowTransform = row.GetComponent<RectTransform>();
        rowTransform.SetParent(contentRoot, false);
        rowTransform.anchorMin = new Vector2(0f, 1f);
        rowTransform.anchorMax = new Vector2(1f, 1f);

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childAlignment = status ? TextAnchor.MiddleCenter : fromUser ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

        LayoutElement rowLayoutElement = row.GetComponent<LayoutElement>();
        rowLayoutElement.flexibleWidth = 1f;

        GameObject bubble = new GameObject(status ? "Status" : fromUser ? "You Bubble" : "Qwen Bubble", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        bubble.transform.SetParent(rowTransform, false);

        Image bubbleImage = bubble.GetComponent<Image>();
        bubbleImage.color = status ? new Color(0f, 0f, 0f, 0f) : fromUser ? userBubbleColor : assistantBubbleColor;
        if (!status)
        {
            bubbleImage.sprite = GetRoundedSprite(bubbleCornerRadius);
            bubbleImage.type = Image.Type.Sliced;
            bubbleImage.pixelsPerUnitMultiplier = 1f;
        }

        VerticalLayoutGroup bubbleLayout = bubble.GetComponent<VerticalLayoutGroup>();
        bubbleLayout.childControlWidth = true;
        bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = false;
        bubbleLayout.childForceExpandHeight = false;
        bubbleLayout.padding = new RectOffset((int)horizontalPadding, (int)horizontalPadding, (int)verticalPadding, (int)verticalPadding);

        ContentSizeFitter bubbleFitter = bubble.GetComponent<ContentSizeFitter>();
        bubbleFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bubbleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement bubbleLayoutElement = bubble.GetComponent<LayoutElement>();
        bubbleLayoutElement.preferredWidth = status ? -1f : maxBubbleWidth;
        bubbleLayoutElement.flexibleWidth = 0f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(bubble.transform, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = Sanitize(message);
        text.font = fontAsset;
        text.fontSharedMaterial = fontMaterial;
        text.fontSize = status ? fontSize * 0.85f : fontSize;
        text.color = status ? statusColor : Color.white;
        text.alignment = status ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
        textLayout.preferredWidth = status ? 520f : maxBubbleWidth - (horizontalPadding * 2f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        return text;
    }

    private static Sprite roundedSprite;
    private static int roundedSpriteRadius = -1;

    private static Sprite GetRoundedSprite(int radius)
    {
        radius = Mathf.Max(1, radius);
        if (roundedSprite != null && roundedSpriteRadius == radius)
        {
            return roundedSprite;
        }

        int size = radius * 2 + 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        roundedSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        roundedSpriteRadius = radius;
        return roundedSprite;
    }

    private static string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        // Normalize common typographic punctuation to ASCII so we don't glue
        // words/numbers together when stripping unsupported glyphs.
        string clean = message
            .Replace('\u2013', '-').Replace('\u2014', '-')
            .Replace('\u2018', '\'').Replace('\u2019', '\'')
            .Replace('\u201C', '"').Replace('\u201D', '"')
            .Replace("\u2026", "...");

        // Strip emoji / symbols / arrows that the bundled font cannot render
        // (e.g. ✅ ❗ → ™). They spam TMP warnings and trigger costly layout
        // recalculations. Latin + Romanian diacritics are preserved.
        clean = Regex.Replace(
            clean,
            @"[\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2100-\u27BF\u2B00-\u2BFF\uFE0F\u200D]",
            string.Empty);
        clean = Regex.Replace(clean, @"[ \t]{2,}", " ");
        return clean.Trim();
    }

    private void RequestScrollToBottom()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(ScrollToBottomNextFrame());
        }
        else
        {
            ScrollToBottomImmediate();
        }
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        ScrollToBottomImmediate();
    }

    private void ScrollToBottomImmediate()
    {
        if (contentRoot == null || scrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void DestroyExistingChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }
}
