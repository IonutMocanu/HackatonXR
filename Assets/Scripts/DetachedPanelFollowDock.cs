using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

/// <summary>
/// Follows dock anchor when idle; stops while the shell Grabbable is selected.
/// </summary>
[DisallowMultipleComponent]
public class DetachedPanelFollowDock : MonoBehaviour
{
    [SerializeField] private Transform homeAnchor;
    [SerializeField] private Transform panelsRoot;

    private Grabbable _grabbable;
    private bool _isGrabbed;

    public void Configure(Transform anchor, Transform followRoot, Grabbable grabbable)
    {
        Unsubscribe();

        homeAnchor = anchor;
        panelsRoot = followRoot;
        _grabbable = grabbable != null ? grabbable : GetComponent<Grabbable>();
        _isGrabbed = false;

        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }

        if (panelsRoot != null)
        {
            transform.SetParent(panelsRoot, true);
        }

        SnapToAnchor();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
    }

    private void HandlePointerEvent(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:
                _isGrabbed = true;
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                _isGrabbed = false;
                SnapToAnchor();
                break;
        }
    }

    private void LateUpdate()
    {
        if (homeAnchor == null || IsPanelGrabbed())
        {
            return;
        }

        SnapToAnchor();
    }

    private void SnapToAnchor()
    {
        transform.SetPositionAndRotation(homeAnchor.position, homeAnchor.rotation);
    }

    private bool IsPanelGrabbed()
    {
        if (_isGrabbed)
        {
            return true;
        }

        if (_grabbable != null && _grabbable.SelectingPointsCount > 0)
        {
            return true;
        }

        HandGrabInteractable[] handGrabs = GetComponentsInChildren<HandGrabInteractable>(true);
        for (int i = 0; i < handGrabs.Length; i++)
        {
            if (handGrabs[i] != null && handGrabs[i].State == InteractableState.Select)
            {
                return true;
            }
        }

        return false;
    }
}
