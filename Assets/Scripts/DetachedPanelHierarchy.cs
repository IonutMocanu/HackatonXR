using UnityEngine;

/// <summary>
/// Helpers for detached shells (root may be a wrapper with PanelInteractable as child).
/// </summary>
public static class DetachedPanelHierarchy
{
    public static Transform GetPanelInteractable(Transform panelRoot)
    {
        if (panelRoot == null)
        {
            return null;
        }

        if (panelRoot.name == "PanelInteractable")
        {
            return panelRoot;
        }

        Transform panelInteractable = panelRoot.Find("PanelInteractable");
        return panelInteractable != null ? panelInteractable : panelRoot;
    }
}
