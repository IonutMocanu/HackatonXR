using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class ChangeUI : MonoBehaviour
{
    [Header("UI Panels")]
    public List<GameObject> UIPanels = new();

    [Header("Controls")]
    public GameObject BackButton;
    public GameObject NextButton;

    public TextMeshProUGUI BackTextMeshProUGUI;
    public TextMeshProUGUI NextTextMeshProUGUI;

    [Header("Media Setup")]
    public RawImage Display;
    public VideoPlayer MyVideoPlayer;
    public VideoClip IntroVideoClip;
    public List<VideoClip> VideoClips = new();

    [Header("Layout Settings")]
    public RectTransform ButtonContainer;
    public GameObject InteractableFather;

    private int m_indexList = 0;
    private Button m_back_button;
    private Button m_next_button;

    private bool m_isNavigating = false;
    private float m_navigationCooldown = 0.3f;

    private void Start()
    {
        m_indexList = 0;
        m_back_button = BackButton.GetComponent<Button>();
        m_next_button = NextButton.GetComponent<Button>();
        RefreshUI();
    }

    private void RefreshUI()
    {
        for (int i = 0; i < UIPanels.Count; i++)
        {
            if (UIPanels[i] != null)
            {
                UIPanels[i].SetActive(i == m_indexList);
            }
        }

        if (m_indexList < 3)
        {
            if (MyVideoPlayer != null && IntroVideoClip != null)
            {
                if (MyVideoPlayer.clip != IntroVideoClip)
                {
                    MyVideoPlayer.clip = IntroVideoClip;
                    MyVideoPlayer.Play();
                }
                else if (!MyVideoPlayer.isPlaying)
                {
                    MyVideoPlayer.Play();
                }
            }
        }
        else
        {
            int videoIndex = m_indexList - 3;

            if (MyVideoPlayer != null && videoIndex < VideoClips.Count)
            {
                if (MyVideoPlayer.clip != VideoClips[videoIndex])
                {
                    MyVideoPlayer.clip = VideoClips[videoIndex];
                    MyVideoPlayer.Play();
                }
                else if (!MyVideoPlayer.isPlaying)
                {
                    MyVideoPlayer.Play();
                }
            }
        }

        m_back_button.onClick.RemoveAllListeners();
        m_next_button.onClick.RemoveAllListeners();

        if (m_indexList == 2)
        {
            BackTextMeshProUGUI.text = "No";
            NextTextMeshProUGUI.text = "Yes";

            m_back_button.onClick.AddListener(NextPanel);
            m_next_button.onClick.AddListener(OnYesClicked);

            ToggleButtonState(BackButton, true);
            ToggleButtonState(NextButton, true);
        }
        else if (m_indexList == UIPanels.Count - 1)
        {
            BackTextMeshProUGUI.text = "Back";
            NextTextMeshProUGUI.text = "Finish";

            m_back_button.onClick.AddListener(PreviousPanel);
            m_next_button.onClick.AddListener(OnFinishClicked);

            ToggleButtonState(BackButton, true);
            ToggleButtonState(NextButton, true);
        }
        else
        {
            BackTextMeshProUGUI.text = "Back";
            NextTextMeshProUGUI.text = "Next";

            m_back_button.onClick.AddListener(PreviousPanel);
            m_next_button.onClick.AddListener(NextPanel);

            ToggleButtonState(BackButton, m_indexList > 0);
            ToggleButtonState(NextButton, true);
        }

        if (ButtonContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(ButtonContainer);
        }
    }

    public void NextPanel()
    {
        if (m_isNavigating) return;

        if (m_indexList < UIPanels.Count - 1)
        {
            StartCoroutine(NavigationCooldownRoutine());
            m_indexList++;
            RefreshUI();
        }
    }

    public void PreviousPanel()
    {
        if (m_isNavigating) return;

        if (m_indexList > 0)
        {
            StartCoroutine(NavigationCooldownRoutine());
            m_indexList--;
            RefreshUI();
        }
    }

    private IEnumerator NavigationCooldownRoutine()
    {
        m_isNavigating = true;
        yield return new WaitForSeconds(m_navigationCooldown);
        m_isNavigating = false;
    }

    private void OnYesClicked()
    {
        if (InteractableFather != null) InteractableFather.SetActive(false);
    }

    private void OnFinishClicked()
    {
        if (InteractableFather != null) InteractableFather.SetActive(false);
    }

    private void ToggleButtonState(GameObject btnObj, bool show)
    {
        if (btnObj == null) return;
        btnObj.SetActive(show);
        Graphic[] graphics = btnObj.GetComponentsInChildren<Graphic>();
        foreach (Graphic g in graphics) g.enabled = show;
        Selectable sel = btnObj.GetComponent<Selectable>();
        if (sel != null) sel.interactable = show;
        Collider col = btnObj.GetComponent<Collider>();
        if (col != null) col.enabled = show;
    }
}