using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

/// <summary>
/// Grab setup for detached shells — same movement provider as main panel, moves the shell root (not only PanelInteractable rigidbody).
/// </summary>
public static class DetachedPanelSnapSetup
{
    public static void ConfigureGrab(GameObject shellRoot, GameObject mainPanel)
    {
        if (shellRoot == null)
        {
            return;
        }

        DisableSnapOnPanel(shellRoot);
        DisableGrabInteractables(shellRoot);
        DisablePanelChromeGrabs(shellRoot);

        Grabbable shellGrabbable = GetOrCreateShellGrabbable(shellRoot);
        ClearGrabTransformersOnHierarchy(shellRoot);
        SetField(shellGrabbable, "_targetTransform", shellRoot.transform);
        SetField(shellGrabbable, "_rigidbody", null);
        shellGrabbable.enabled = true;

        object movementProvider = GetSharedMovementProvider(mainPanel);
        RewireExternalPointables(shellRoot, shellGrabbable);
        WirePanelHandGrabs(shellRoot, shellGrabbable, movementProvider);
        DisableChildGrabbables(shellRoot, shellGrabbable);
    }

    public static Grabbable GetOrCreateShellGrabbable(GameObject shellRoot)
    {
        Grabbable[] rootGrabbables = shellRoot.GetComponents<Grabbable>();
        Grabbable primary = rootGrabbables.Length > 0 ? rootGrabbables[0] : shellRoot.AddComponent<Grabbable>();

        for (int i = 1; i < rootGrabbables.Length; i++)
        {
            if (rootGrabbables[i] != null)
            {
                rootGrabbables[i].enabled = false;
            }
        }

        ClearGrabTransformers(primary);
        return primary;
    }

    public static void DisableSnapOnPanel(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        SnapInteractor[] interactors = panelRoot.GetComponentsInChildren<SnapInteractor>(true);
        for (int i = 0; i < interactors.Length; i++)
        {
            SnapInteractor interactor = interactors[i];
            if (interactor == null)
            {
                continue;
            }

            SetField(interactor, "_defaultInteractable", null);
            SetField(interactor, "_timeOutInteractable", null);
            interactor.enabled = false;
        }
    }

    public static void AssignRigidbody(SnapInteractable interactable, Rigidbody rigidbody)
    {
        if (interactable == null || rigidbody == null)
        {
            return;
        }

        SetField(interactable, "_rigidbody", rigidbody);
    }

    private static void DisableGrabInteractables(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == "GrabInteractable")
            {
                behaviour.enabled = false;
            }
        }
    }

    private static void ClearGrabTransformersOnHierarchy(GameObject root)
    {
        Grabbable[] grabbables = root.GetComponentsInChildren<Grabbable>(true);
        for (int i = 0; i < grabbables.Length; i++)
        {
            ClearGrabTransformers(grabbables[i]);
        }
    }

    private static void ClearGrabTransformers(Grabbable grabbable)
    {
        if (grabbable == null)
        {
            return;
        }

        SetField(grabbable, "_oneGrabTransformer", null);
        SetField(grabbable, "_twoGrabTransformer", null);
    }

    private static void RewireExternalPointables(GameObject shellRoot, Grabbable shellGrabbable)
    {
        MonoBehaviour[] behaviours = shellRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            FieldInfo pointableField = behaviour.GetType().GetField(
                "_pointableElement",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (pointableField == null || !typeof(PointableElement).IsAssignableFrom(pointableField.FieldType))
            {
                continue;
            }

            if (pointableField.GetValue(behaviour) is Component current
                && !current.transform.IsChildOf(shellRoot.transform))
            {
                pointableField.SetValue(behaviour, shellGrabbable);
            }
        }
    }

    private static void WirePanelHandGrabs(
        GameObject shellRoot,
        Grabbable shellGrabbable,
        object movementProvider)
    {
        Transform panelInteractable = DetachedPanelHierarchy.GetPanelInteractable(shellRoot.transform);
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

            SetField(handGrab, "_pointableElement", shellGrabbable);
            SetField(handGrab, "_rigidbody", null);
            if (movementProvider != null)
            {
                SetField(handGrab, "_movementProvider", movementProvider);
            }

            handGrab.enabled = true;
        }
    }

    private static void DisableChildGrabbables(GameObject shellRoot, Grabbable shellGrabbable)
    {
        Grabbable[] grabbables = shellRoot.GetComponentsInChildren<Grabbable>(true);
        for (int i = 0; i < grabbables.Length; i++)
        {
            Grabbable grabbable = grabbables[i];
            if (grabbable == null || grabbable == shellGrabbable)
            {
                continue;
            }

            grabbable.enabled = false;
        }
    }

    private static void DisablePanelChromeGrabs(GameObject panelRoot)
    {
        HandGrabInteractable[] handGrabs = panelRoot.GetComponentsInChildren<HandGrabInteractable>(true);
        Transform panelInteractable = DetachedPanelHierarchy.GetPanelInteractable(panelRoot.transform);

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

    private static object GetSharedMovementProvider(GameObject mainPanel)
    {
        if (mainPanel == null)
        {
            return null;
        }

        Transform mainPanelInteractable = mainPanel.transform.Find("PanelInteractable");
        if (mainPanelInteractable == null)
        {
            return null;
        }

        HandGrabInteractable mainHandGrab = mainPanelInteractable.GetComponentInChildren<HandGrabInteractable>(true);
        return mainHandGrab != null ? GetField(mainHandGrab, "_movementProvider") : null;
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
