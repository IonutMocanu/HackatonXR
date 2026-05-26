using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

/// <summary>
/// Grab/move setup for detached panels. Snap-to-zone uses <see cref="DetachedPanelMovement"/> instead of locking SnapInteractor.
/// </summary>
public static class DetachedPanelSnapSetup
{
    public static void DisableSnapOnPanel(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        SnapInteractor[] interactors = panelRoot.GetComponentsInChildren<SnapInteractor>(true);
        for (int i = 0; i < interactors.Length; i++)
        {
            if (interactors[i] != null)
            {
                interactors[i].enabled = false;
            }
        }
    }

    public static void ConfigureMovement(GameObject panelRoot, Transform homeAnchor)
    {
        if (panelRoot == null)
        {
            return;
        }

        DisableSnapOnPanel(panelRoot);
        DisablePanelChromeGrabs(panelRoot);

        Grabbable rootGrabbable = EnsureRootGrabbable(panelRoot);
        if (rootGrabbable == null)
        {
            return;
        }

        WireHandGrabsToGrabbable(panelRoot, rootGrabbable);
        DisableExtraGrabbables(panelRoot, rootGrabbable);

        PointableElement grabPointable = FindHandGrabPointable(panelRoot);

        DetachedPanelMovement movement = panelRoot.GetComponent<DetachedPanelMovement>();
        if (movement == null)
        {
            movement = panelRoot.AddComponent<DetachedPanelMovement>();
        }

        movement.Configure(homeAnchor, rootGrabbable, grabPointable);
    }

    public static void AssignRigidbody(SnapInteractable interactable, Rigidbody rigidbody)
    {
        if (interactable == null || rigidbody == null)
        {
            return;
        }

        SetField(interactable, "_rigidbody", rigidbody);
    }

    private static PointableElement FindHandGrabPointable(GameObject panelRoot)
    {
        Transform panelInteractable = panelRoot.transform.Find("PanelInteractable");
        if (panelInteractable == null)
        {
            return null;
        }

        HandGrabInteractable[] handGrabs = panelInteractable.GetComponentsInChildren<HandGrabInteractable>(true);
        for (int i = 0; i < handGrabs.Length; i++)
        {
            object pointable = GetField(handGrabs[i], "_pointableElement");
            if (pointable is PointableElement element)
            {
                return element;
            }
        }

        return null;
    }

    private static void WireHandGrabsToGrabbable(GameObject panelRoot, Grabbable rootGrabbable)
    {
        Transform panelInteractable = panelRoot.transform.Find("PanelInteractable");
        if (panelInteractable == null)
        {
            return;
        }

        HandGrabInteractable[] handGrabs = panelInteractable.GetComponentsInChildren<HandGrabInteractable>(true);
        for (int i = 0; i < handGrabs.Length; i++)
        {
            HandGrabInteractable handGrab = handGrabs[i];
            if (handGrab == null)
            {
                continue;
            }

            SetField(handGrab, "_pointableElement", rootGrabbable);
            handGrab.enabled = true;
        }
    }

    private static void DisableExtraGrabbables(GameObject panelRoot, Grabbable rootGrabbable)
    {
        Grabbable[] grabbables = panelRoot.GetComponentsInChildren<Grabbable>(true);
        for (int i = 0; i < grabbables.Length; i++)
        {
            Grabbable grabbable = grabbables[i];
            if (grabbable == null || grabbable == rootGrabbable)
            {
                continue;
            }

            grabbable.enabled = false;
        }
    }

    private static Grabbable EnsureRootGrabbable(GameObject panelRoot)
    {
        Grabbable grabbable = panelRoot.GetComponent<Grabbable>();
        if (grabbable == null)
        {
            grabbable = panelRoot.AddComponent<Grabbable>();
        }

        SetField(grabbable, "_targetTransform", panelRoot.transform);

        Rigidbody panelRigidbody = FindPanelRigidbody(panelRoot);
        if (panelRigidbody != null)
        {
            SetField(grabbable, "_rigidbody", panelRigidbody);
        }

        grabbable.enabled = true;
        return grabbable;
    }

    private static void DisablePanelChromeGrabs(GameObject panelRoot)
    {
        HandGrabInteractable[] handGrabs = panelRoot.GetComponentsInChildren<HandGrabInteractable>(true);
        Transform panelInteractable = panelRoot.transform.Find("PanelInteractable");

        for (int i = 0; i < handGrabs.Length; i++)
        {
            HandGrabInteractable handGrab = handGrabs[i];
            if (handGrab == null)
            {
                continue;
            }

            if (panelInteractable != null && handGrab.transform.IsChildOf(panelInteractable))
            {
                continue;
            }

            handGrab.enabled = false;
        }
    }

    private static Rigidbody FindPanelRigidbody(GameObject panelRoot)
    {
        Transform panelInteractable = panelRoot.transform.Find("PanelInteractable");
        if (panelInteractable != null)
        {
            Rigidbody rb = panelInteractable.GetComponent<Rigidbody>();
            if (rb != null)
            {
                return rb;
            }
        }

        return panelRoot.GetComponentInChildren<Rigidbody>(true);
    }

    private static object GetField(Component component, string fieldName)
    {
        if (component == null)
        {
            return null;
        }

        FieldInfo field = component.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field?.GetValue(component);
    }

    private static void SetField(Component component, string fieldName, object value)
    {
        if (component == null)
        {
            return;
        }

        FieldInfo field = component.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(component, value);
    }
}
