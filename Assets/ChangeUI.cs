using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video; // Trebuie să adaugi asta

public class ChangeUI : MonoBehaviour
{
    [Header("UI Panels")]
    public List<GameObject> UIPanels = new List<GameObject>();

    [Header("Video Setup")]
    public VideoPlayer MainVideoPlayer; // Trage Video Player-ul aici
    public List<VideoClip> VideoClips = new List<VideoClip>(); // Pune clipurile în ordine aici
    public RenderTexture SharedTexture; // Trage Render Texture-ul (DialogVideos) aici

    [Header("Controls")]
    public GameObject BackButton;
    public GameObject NextButton;

    private int m_indexList = 0;

    private void Start()
    {
        m_indexList = 0;
        RefreshUI();
    }

    public void NextPanel()
    {
        if (m_indexList < UIPanels.Count - 1)
        {
            m_indexList++;
            RefreshUI();
        }
    }

    public void PreviousPanel()
    {
        if (m_indexList > 0)
        {
            m_indexList--;
            RefreshUI();
        }
    }

    private void RefreshUI()
    {
        for (int i = 0; i < UIPanels.Count; i++)
        {
            if (UIPanels[i] != null)
                UIPanels[i].SetActive(i == m_indexList);
        }

        // --- LOGICA VIDEO ---
        if (MainVideoPlayer != null && VideoClips.Count > m_indexList)
        {
            MainVideoPlayer.Stop(); // Oprim clipul actual
            ClearRenderTexture(SharedTexture); // Curățăm imaginea veche (neagră)

            MainVideoPlayer.clip = VideoClips[m_indexList]; // Schimbăm clipul
            MainVideoPlayer.Play(); // Pornim clipul nou
        }

        ToggleButtonState(BackButton, m_indexList > 0);
        ToggleButtonState(NextButton, m_indexList < UIPanels.Count - 1);
    }

    // Funcție care „spală” textura să nu mai rămână cadrul blocat
    private void ClearRenderTexture(RenderTexture rt)
    {
        if (rt == null) return;
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = active;
    }

    private void ToggleButtonState(GameObject btnObj, bool show)
    {
        if (btnObj == null) return;
        Graphic[] graphics = btnObj.GetComponentsInChildren<Graphic>();
        foreach (Graphic g in graphics) g.enabled = show;

        Selectable sel = btnObj.GetComponent<Selectable>();
        if (sel != null) sel.interactable = show;

        Collider col = btnObj.GetComponent<Collider>();
        if (col != null) col.enabled = show;

        btnObj.SetActive(show);
    }
}