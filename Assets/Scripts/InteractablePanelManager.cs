using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractablePanelManager : MonoBehaviour
{
    public GameObject InteractableUIPanel;
    public Transform FatherTransform;

    [Tooltip("Locul unde sa se spwaneze paginiile noi generate")]
    public Transform[] spawners;

    public GameObject[] Pages;

    [Tooltip("GameObject - Taticul la iconitele din bara principala || Script - Indexare si sursa")]
    public PageScroll PageScrollObject;

    [Serializable]
    public class RectTransformState
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector3 LocalScale;

        public static RectTransformState Capture(RectTransform rect)
        {
            return new RectTransformState
            {
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                LocalScale = rect.localScale
            };
        }

        public void Apply(RectTransform rect)
        {
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = SizeDelta;
            rect.localScale = LocalScale;
        }
    }

    [Serializable]
    public class Page
    {
        public Toggle Toggle;
        public RectTransform Container;
        public CanvasGroup CanvasGroup;
        public int OriginalIndex;
        public int PagesArrayIndex;
        public GameObject ChildPanel;
        public GameObject PageContent;
        public object StoredPageEntry;
        public Transform OriginalParent;
        public int OriginalSiblingIndex;
        public RectTransformState SavedRectState;
        public ScrollContentState SavedScrollContentState;
        public GameObject CenterWrapper;
        public List<Behaviour> DisabledNavigationBehaviours = new();
    }

    [Serializable]
    public class ScrollContentState
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public bool Horizontal;
        public bool Vertical;
        public ScrollRect.MovementType MovementType;
        public bool HadHorizontalLayoutGroup;
        public int HorizontalLayoutChildAlignment;
        public bool HadGridLayoutGroup;
        public int GridLayoutChildAlignment;
        public bool HadContentSizeFitter;
        public bool ContentSizeFitterEnabled;

        public static ScrollContentState Capture(ScrollRect scroll)
        {
            RectTransform content = scroll.content;
            ScrollContentState state = new ScrollContentState
            {
                AnchorMin = content.anchorMin,
                AnchorMax = content.anchorMax,
                Pivot = content.pivot,
                AnchoredPosition = content.anchoredPosition,
                SizeDelta = content.sizeDelta,
                Horizontal = scroll.horizontal,
                Vertical = scroll.vertical,
                MovementType = scroll.movementType
            };

            HorizontalLayoutGroup horizontalLayout = content.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout != null)
            {
                state.HadHorizontalLayoutGroup = true;
                state.HorizontalLayoutChildAlignment = (int)horizontalLayout.childAlignment;
            }

            GridLayoutGroup gridLayout = content.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                state.HadGridLayoutGroup = true;
                state.GridLayoutChildAlignment = (int)gridLayout.childAlignment;
            }

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                state.HadContentSizeFitter = true;
                state.ContentSizeFitterEnabled = fitter.enabled;
            }

            return state;
        }

        public void Apply(ScrollRect scroll)
        {
            if (scroll == null || scroll.content == null)
            {
                return;
            }

            RectTransform content = scroll.content;
            content.anchorMin = AnchorMin;
            content.anchorMax = AnchorMax;
            content.pivot = Pivot;
            content.anchoredPosition = AnchoredPosition;
            content.sizeDelta = SizeDelta;
            scroll.horizontal = Horizontal;
            scroll.vertical = Vertical;
            scroll.movementType = MovementType;

            if (HadHorizontalLayoutGroup)
            {
                HorizontalLayoutGroup horizontalLayout = content.GetComponent<HorizontalLayoutGroup>();
                if (horizontalLayout != null)
                {
                    horizontalLayout.childAlignment = (TextAnchor)HorizontalLayoutChildAlignment;
                }
            }

            if (HadGridLayoutGroup)
            {
                GridLayoutGroup gridLayout = content.GetComponent<GridLayoutGroup>();
                if (gridLayout != null)
                {
                    gridLayout.childAlignment = (TextAnchor)GridLayoutChildAlignment;
                }
            }

            if (HadContentSizeFitter)
            {
                ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    fitter.enabled = ContentSizeFitterEnabled;
                }
            }
        }
    }

    [SerializeField]
    public List<Page> OutPages;

    [Range(-1f, 1f)]
    public float VizibilitateThreshold = 0.5f;

    private int _lastSyncedPageIndex = -1;

    private void OnEnable()
    {
        Canvas.willRenderCanvases += OnWillRenderCanvases;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
    }

    private readonly List<(GameObject panel, RectTransform page)> _pendingDetachedLayout = new();

    private void OnWillRenderCanvases()
    {
        if (_pendingDetachedLayout.Count == 0)
        {
            return;
        }

        var pending = new List<(GameObject panel, RectTransform page)>(_pendingDetachedLayout);
        _pendingDetachedLayout.Clear();

        foreach ((GameObject panel, RectTransform page) in pending)
        {
            if (panel == null || page == null || !IsPageStillDetached(page))
            {
                continue;
            }

            FitDetachedPageLayout(panel, page);
        }
    }

    private bool IsPageStillDetached(RectTransform page)
    {
        if (page == null)
        {
            return false;
        }

        for (int i = 0; i < OutPages.Count; i++)
        {
            Page detached = OutPages[i];
            if (detached.PageContent != null
                && detached.PageContent.GetComponent<RectTransform>() == page)
            {
                return true;
            }
        }

        return false;
    }

    private void RemovePendingDetachedLayout(GameObject childPanel, RectTransform page)
    {
        for (int i = _pendingDetachedLayout.Count - 1; i >= 0; i--)
        {
            (GameObject panel, RectTransform pendingPage) = _pendingDetachedLayout[i];
            if (panel == childPanel || pendingPage == page)
            {
                _pendingDetachedLayout.RemoveAt(i);
            }
        }
    }

    private void Update()
    {
        Vector3 directieCatreCamera = (Camera.main.transform.position - transform.position).normalized;
        Vector3 normalaPalmei = -transform.up;
        float dotProduct = Vector3.Dot(normalaPalmei, directieCatreCamera);
        bool estePalmaSpreCamera = dotProduct > VizibilitateThreshold;

        GameObject buton = gameObject.transform.GetChild(0).gameObject;
        if (buton.activeSelf != estePalmaSpreCamera)
        {
            buton.SetActive(estePalmaSpreCamera);
        }

        SyncMainPanelPageIndexIfChanged();
    }

    public void OnOffPanel()
    {
        if (InteractableUIPanel.activeInHierarchy)
        {
            InteractableUIPanel.SetActive(false);
        }
        else
        {
            InteractableUIPanel.SetActive(true);
            InteractableUIPanel.transform.position = FatherTransform.position;
            InteractableUIPanel.transform.rotation = new Quaternion(
                FatherTransform.rotation.x,
                FatherTransform.rotation.y,
                0f,
                FatherTransform.rotation.w);
        }
    }

    public void SpawnTab()
    {
        if (PageScrollObject == null || PageScrollObject.Pages == null || PageScrollObject.Pages.Count == 0)
        {
            return;
        }

        int spawnIndex = PageScrollObject.PageIndex;
        if (spawnIndex < 0 || spawnIndex >= PageScrollObject.Pages.Count)
        {
            return;
        }

        object storedEntry = PageScrollObject.Pages[spawnIndex];
        Toggle pageToggle = PageScrollObject.Pages[spawnIndex].toggle;
        RectTransform pageContainer = PageScrollObject.Pages[spawnIndex].container;
        CanvasGroup pageCanvasGroup = PageScrollObject.Pages[spawnIndex].canvasGroup;

        if (pageContainer == null)
        {
            return;
        }

        Vector3 spawnPosition = GetDetachedPanelSpawnPosition();
        Quaternion spawnRotation = spawners[0].transform.rotation;

        GameObject ChildPanel = Instantiate(InteractableUIPanel, spawnPosition, spawnRotation);

        DisableChildPanelPageSystems(ChildPanel);

        GameObject topBar = ChildPanel.transform
            .Find("PanelInteractable").GetChild(1).GetChild(1).gameObject;
        topBar.transform.GetChild(1).gameObject.SetActive(false);

        Button mergeButton = topBar.transform.GetChild(2).GetComponent<Button>();
        mergeButton.onClick.RemoveAllListeners();
        mergeButton.onClick.AddListener(() => MergeTabBack(ChildPanel));

        Transform detachedHost = GetMenuContentMargin(ChildPanel.transform);
        ClearDetachedHost(detachedHost);

        List<Behaviour> disabledNavigation = new();
        GameObject pageContent = pageContainer.gameObject;
        int pagesArrayIndex = FindPagesArrayIndex(pageContent);
        Transform originalParent = pageContent.transform.parent;
        int originalSiblingIndex = pageContent.transform.GetSiblingIndex();
        RectTransform pageRect = pageContent.GetComponent<RectTransform>();
        RectTransformState savedRect = RectTransformState.Capture(pageRect);
        ScrollContentState savedScrollContent = CaptureScrollContentState(pageRect);

        GameObject centerWrapper = CreateDetachedCenterWrapper(detachedHost);
        pageContent.transform.SetParent(centerWrapper.transform, false);
        ApplyPageRectCenteredInWrapper(pageRect, savedRect, detachedHost as RectTransform);

        IsolateDetachedPageNavigation(pageContent, disabledNavigation);

        OutPages.Add(new Page
        {
            Toggle = pageToggle,
            Container = pageContainer,
            CanvasGroup = pageCanvasGroup,
            OriginalIndex = spawnIndex,
            PagesArrayIndex = pagesArrayIndex,
            ChildPanel = ChildPanel,
            PageContent = pageContent,
            StoredPageEntry = storedEntry,
            OriginalParent = originalParent,
            OriginalSiblingIndex = originalSiblingIndex,
            SavedRectState = savedRect,
            SavedScrollContentState = savedScrollContent,
            CenterWrapper = centerWrapper,
            DisabledNavigationBehaviours = disabledNavigation
        });

        FitDetachedPageLayout(ChildPanel, pageRect);
        _pendingDetachedLayout.Add((ChildPanel, pageRect));
        StartCoroutine(FitDetachedPageLayoutDelayed(ChildPanel, pageRect));
        SetPageVisible(pageContent, pageCanvasGroup, true);

        SetPagerToggleVisible(pageToggle, false);
        PageScrollObject.Pages.RemoveAt(spawnIndex);
        RefreshMainPanelAfterDetach(spawnIndex);

        GameObject bottomBar = ChildPanel.transform
            .Find("PanelInteractable").GetChild(1).GetChild(3).gameObject;
        if (bottomBar.transform.childCount > 0)
        {
            Destroy(bottomBar.transform.GetChild(0).gameObject);
        }
    }

    public void MergeTabBack(GameObject childPanel)
    {
        Page detached = OutPages.Find(p => p.ChildPanel == childPanel);
        if (detached == null)
        {
            return;
        }

        Transform restoreParent = detached.OriginalParent != null
            ? detached.OriginalParent
            : GetPageContentContainer(InteractableUIPanel.transform);

        RectTransform pageRect = detached.PageContent.GetComponent<RectTransform>();
        RemovePendingDetachedLayout(childPanel, pageRect);

        if (detached.CenterWrapper != null)
        {
            pageRect.SetParent(restoreParent, false);
            Destroy(detached.CenterWrapper);
            detached.CenterWrapper = null;
        }
        else
        {
            pageRect.SetParent(restoreParent, false);
        }

        if (detached.SavedRectState != null)
        {
            detached.SavedRectState.Apply(pageRect);
        }

        int siblingIndex = Mathf.Clamp(detached.OriginalSiblingIndex, 0, restoreParent.childCount);
        pageRect.SetSiblingIndex(siblingIndex);

        InsertPageEntry(detached.OriginalIndex, detached.StoredPageEntry);
        SetPagerToggleVisible(detached.Toggle, true);

        RestoreScrollLayout(pageRect, detached.SavedScrollContentState);
        RestoreDetachedPageNavigation(detached);
        EnablePageNavigation(detached.PageContent);

        ApplyPageScrollState(detached.OriginalIndex);
        RefreshMainPanelNavigation();
        _lastSyncedPageIndex = PageScrollObject != null ? PageScrollObject.PageIndex : -1;

        if (PageScrollObject != null)
        {
            PageScrollObject.enabled = true;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(pageRect);
        if (pageRect.parent is RectTransform parentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        Canvas.ForceUpdateCanvases();

        OutPages.Remove(detached);
        Destroy(childPanel);
    }

    public void CloseNewTab(GameObject panel, GameObject topBar)
    {
        MergeTabBack(panel);
    }

    private static Transform GetMenuContentMargin(Transform panelRoot)
    {
        return panelRoot.Find("PanelInteractable").GetChild(1).GetChild(2).GetChild(0);
    }

    private static Transform GetPageContentContainer(Transform panelRoot)
    {
        Transform menuContent = GetMenuContentMargin(panelRoot);
        return menuContent != null && menuContent.childCount > 0
            ? menuContent.GetChild(0)
            : menuContent;
    }

    private static void ClearDetachedHost(Transform host)
    {
        if (host == null)
        {
            return;
        }

        for (int i = host.childCount - 1; i >= 0; i--)
        {
            Destroy(host.GetChild(i).gameObject);
        }
    }

    private IEnumerator FitDetachedPageLayoutDelayed(GameObject childPanel, RectTransform pageRect)
    {
        yield return null;
        yield return null;

        if (childPanel == null || pageRect == null || !IsPageStillDetached(pageRect))
        {
            yield break;
        }

        FitDetachedPageLayout(childPanel, pageRect);
    }

    private void FitDetachedPageLayout(GameObject childPanel, RectTransform pageRect)
    {
        Page detached = FindDetachedPageByRect(pageRect);
        RectTransform host = GetMenuContentMargin(childPanel.transform) as RectTransform;
        RectTransform panelCanvas = childPanel.transform.Find("PanelInteractable").GetChild(1) as RectTransform;

        if (panelCanvas != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelCanvas);
        }

        if (host != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(host);
        }

        Canvas.ForceUpdateCanvases();

        if (detached?.CenterWrapper != null)
        {
            StretchRectToParent(detached.CenterWrapper.GetComponent<RectTransform>());
        }

        if (detached?.SavedRectState != null)
        {
            ApplyPageRectCenteredInWrapper(pageRect, detached.SavedRectState, host);
        }

        FitScrollAreaInDetachedPage(pageRect);

        LayoutRebuilder.ForceRebuildLayoutImmediate(pageRect);
        Canvas.ForceUpdateCanvases();
    }

    private Page FindDetachedPageByRect(RectTransform pageRect)
    {
        for (int i = 0; i < OutPages.Count; i++)
        {
            Page detached = OutPages[i];
            if (detached.PageContent != null
                && detached.PageContent.GetComponent<RectTransform>() == pageRect)
            {
                return detached;
            }
        }

        return null;
    }

    private static GameObject CreateDetachedCenterWrapper(Transform host)
    {
        GameObject wrapperObject = new GameObject("DetachedPageCenter", typeof(RectTransform));
        RectTransform wrapperRect = wrapperObject.GetComponent<RectTransform>();
        wrapperRect.SetParent(host, false);
        StretchRectToParent(wrapperRect);
        return wrapperObject;
    }

    private static void ApplyPageRectCenteredInWrapper(
        RectTransform pageRect,
        RectTransformState savedRect,
        RectTransform host)
    {
        if (pageRect == null)
        {
            return;
        }

        pageRect.anchorMin = new Vector2(0.5f, 0.5f);
        pageRect.anchorMax = new Vector2(0.5f, 0.5f);
        pageRect.pivot = new Vector2(0.5f, 0.5f);
        pageRect.anchoredPosition = Vector2.zero;

        if (savedRect == null)
        {
            return;
        }

        pageRect.sizeDelta = savedRect.SizeDelta;
        Vector3 scale = savedRect.LocalScale;

        if (host != null && savedRect.SizeDelta.x > 0f && savedRect.SizeDelta.y > 0f)
        {
            float scaleX = host.rect.width / savedRect.SizeDelta.x;
            float scaleY = host.rect.height / savedRect.SizeDelta.y;
            float fitScale = Mathf.Min(1f, scaleX, scaleY);
            scale *= fitScale;
        }

        pageRect.localScale = scale;
    }

    private static void StretchRectToParent(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static ScrollContentState CaptureScrollContentState(RectTransform pageRect)
    {
        ScrollRect scroll = pageRect.GetComponent<ScrollRect>();
        return scroll != null ? ScrollContentState.Capture(scroll) : null;
    }

    private static void FitScrollAreaInDetachedPage(RectTransform pageRect)
    {
        ScrollRect scroll = pageRect.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            return;
        }

        if (scroll.viewport != null)
        {
            StretchRectToParent(scroll.viewport);
        }

        if (scroll.content == null)
        {
            return;
        }

        ApplyDetachedContentLayoutAlignment(scroll.content);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        Canvas.ForceUpdateCanvases();
    }

    private static void ApplyDetachedContentLayoutAlignment(RectTransform content)
    {
        HorizontalLayoutGroup horizontalLayout = content.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout != null)
        {
            horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
        }

        GridLayoutGroup gridLayout = content.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
        }

        VerticalLayoutGroup verticalLayout = content.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null)
        {
            verticalLayout.childAlignment = TextAnchor.MiddleCenter;
        }
    }

    private static void RestoreScrollLayout(RectTransform pageRect, ScrollContentState savedState)
    {
        ScrollRect scroll = pageRect.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            return;
        }

        if (savedState != null)
        {
            savedState.Apply(scroll);
        }
        else
        {
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            RectTransform content = scroll.content;
            if (content != null)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.anchoredPosition = Vector2.zero;
            }
        }

        if (scroll.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        }
    }

    private static void IsolateDetachedPageNavigation(GameObject pageRoot, List<Behaviour> disabledBehaviours)
    {
        foreach (MonoBehaviour behaviour in pageRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || !behaviour.enabled)
            {
                continue;
            }

            if (!ShouldDisableForDetachedPanel(behaviour))
            {
                continue;
            }

            behaviour.enabled = false;
            disabledBehaviours?.Add(behaviour);
        }
    }

    private static void RestoreDetachedPageNavigation(Page detached)
    {
        if (detached.DisabledNavigationBehaviours == null)
        {
            return;
        }

        for (int i = detached.DisabledNavigationBehaviours.Count - 1; i >= 0; i--)
        {
            Behaviour behaviour = detached.DisabledNavigationBehaviours[i];
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        detached.DisabledNavigationBehaviours.Clear();
    }

    private static void EnablePageNavigation(GameObject pageRoot)
    {
        if (pageRoot == null)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in pageRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || !ShouldDisableForDetachedPanel(behaviour))
            {
                continue;
            }

            behaviour.enabled = true;
        }
    }

    private static bool ShouldDisableForDetachedPanel(MonoBehaviour behaviour)
    {
        if (behaviour is ScrollRect)
        {
            return false;
        }

        Type type = behaviour.GetType();

        return type.GetField("swipeExecuted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
    }

    private static void SetPageVisible(GameObject page, CanvasGroup canvasGroup, bool visible)
    {
        if (page != null)
        {
            page.SetActive(true);
        }

        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void DisableChildPanelPageSystems(GameObject childPanel)
    {
        PageScroll childScroll = childPanel.GetComponentInChildren<PageScroll>(true);
        if (childScroll != null)
        {
            childScroll.enabled = false;
        }

        InteractablePanelManager childManager = childPanel.GetComponentInChildren<InteractablePanelManager>(true);
        if (childManager != null && childManager != this)
        {
            childManager.enabled = false;
        }

        IsolateDetachedPageNavigation(childPanel, null);
    }

    private void RefreshMainPanelAfterDetach(int removedIndex)
    {
        if (PageScrollObject == null || PageScrollObject.Pages == null || PageScrollObject.Pages.Count == 0)
        {
            return;
        }

        PageScrollObject.enabled = true;

        int visibleIndex = removedIndex;
        if (visibleIndex >= PageScrollObject.Pages.Count)
        {
            visibleIndex = PageScrollObject.Pages.Count - 1;
        }

        visibleIndex = Mathf.Max(0, visibleIndex);
        ApplyPageScrollState(visibleIndex);
        RefreshMainPanelNavigation();
        _lastSyncedPageIndex = PageScrollObject.PageIndex;
    }

    private void SyncMainPanelPageIndexIfChanged()
    {
        if (PageScrollObject == null || PageScrollObject.Pages == null || PageScrollObject.Pages.Count == 0)
        {
            return;
        }

        int currentIndex = Mathf.Clamp(PageScrollObject.PageIndex, 0, PageScrollObject.Pages.Count - 1);
        if (currentIndex == _lastSyncedPageIndex)
        {
            return;
        }

        ApplyPageScrollState(currentIndex);
        _lastSyncedPageIndex = currentIndex;
    }

    private void RefreshMainPanelNavigation()
    {
        if (PageScrollObject == null || PageScrollObject.Pages == null)
        {
            return;
        }

        for (int i = 0; i < PageScrollObject.Pages.Count; i++)
        {
            RectTransform container = PageScrollObject.Pages[i].container;
            if (container == null)
            {
                continue;
            }

            container.gameObject.SetActive(true);
            EnablePageNavigation(container.gameObject);
        }
    }

    private void ApplyPageScrollState(int pageIndex)
    {
        if (PageScrollObject == null || PageScrollObject.Pages == null || PageScrollObject.Pages.Count == 0)
        {
            return;
        }

        pageIndex = Mathf.Clamp(pageIndex, 0, PageScrollObject.Pages.Count - 1);
        PageScrollObject.PageIndex = pageIndex;

        for (int i = 0; i < PageScrollObject.Pages.Count; i++)
        {
            bool isActive = i == pageIndex;
            ApplyMainPanelPageState(
                PageScrollObject.Pages[i].container,
                PageScrollObject.Pages[i].canvasGroup,
                isActive);
        }

        SyncMainPanelContentContainer(pageIndex);

        Toggle toggle = PageScrollObject.Pages[pageIndex].toggle;
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(true);
        }
    }

    private static void ApplyMainPanelPageState(RectTransform container, CanvasGroup canvasGroup, bool isActive)
    {
        if (container != null)
        {
            container.gameObject.SetActive(true);
            SetGraphicsRaycastTarget(container.gameObject, isActive);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = isActive ? 1f : 0f;
            canvasGroup.interactable = isActive;
            canvasGroup.blocksRaycasts = isActive;
        }
    }

    private static void SetGraphicsRaycastTarget(GameObject root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = enabled;
        }
    }

    private void SyncMainPanelContentContainer(int activePageIndex)
    {
        Transform contentContainer = GetPageContentContainer(InteractableUIPanel.transform);
        if (contentContainer == null || PageScrollObject == null)
        {
            return;
        }

        for (int i = 0; i < contentContainer.childCount; i++)
        {
            RectTransform child = contentContainer.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            int pageIndex = FindPageIndexByContainer(child);
            if (pageIndex < 0)
            {
                SetGraphicsRaycastTarget(child.gameObject, false);
                continue;
            }

            bool isActive = pageIndex == activePageIndex;
            ApplyMainPanelPageState(
                PageScrollObject.Pages[pageIndex].container,
                PageScrollObject.Pages[pageIndex].canvasGroup,
                isActive);
        }
    }

    private int FindPageIndexByContainer(RectTransform container)
    {
        if (container == null || PageScrollObject == null || PageScrollObject.Pages == null)
        {
            return -1;
        }

        for (int i = 0; i < PageScrollObject.Pages.Count; i++)
        {
            if (PageScrollObject.Pages[i].container == container)
            {
                return i;
            }
        }

        return -1;
    }

    private Vector3 GetDetachedPanelSpawnPosition()
    {
        Transform spawner = spawners[0];
        Vector3 position = spawner.position;
        int slot = OutPages.Count;

        if (slot > 0)
        {
            position += spawner.right * 0.4f * slot;
        }

        return position;
    }

    private int FindPagesArrayIndex(GameObject pageContent)
    {
        if (pageContent == null || Pages == null)
        {
            return -1;
        }

        for (int i = 0; i < Pages.Length; i++)
        {
            if (Pages[i] == pageContent)
            {
                return i;
            }
        }

        return -1;
    }

    private static void SetPagerToggleVisible(Toggle toggle, bool visible)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.gameObject.SetActive(visible);
    }

    private void InsertPageEntry(int index, object entry)
    {
        if (entry == null || PageScrollObject == null)
        {
            return;
        }

        object pages = PageScrollObject.Pages;
        if (pages == null)
        {
            return;
        }

        if (pages is IList list)
        {
            index = Mathf.Clamp(index, 0, list.Count);
            list.Insert(index, entry);
            return;
        }

        MethodInfo insertMethod = pages.GetType().GetMethod("Insert", new[] { typeof(int), entry.GetType() });
        if (insertMethod != null)
        {
            index = Mathf.Clamp(index, 0, (int)pages.GetType().GetProperty("Count").GetValue(pages));
            insertMethod.Invoke(pages, new[] { index, entry });
        }
    }
}
