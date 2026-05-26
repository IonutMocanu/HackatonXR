using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Detached panel follows its dock zone until grabbed, then moves freely with the hand.
/// </summary>
[DisallowMultipleComponent]
public class DetachedPanelMovement : MonoBehaviour
{
    [SerializeField] private Transform homeAnchor;

    private Grabbable _grabbable;
    private PointableElement _grabPointable;

    public void Configure(Transform anchor, Grabbable grabbable, PointableElement grabPointable)
    {
        homeAnchor = anchor;
        _grabbable = grabbable != null ? grabbable : GetComponent<Grabbable>();
        _grabPointable = grabPointable != null ? grabPointable : _grabbable;

        if (homeAnchor != null)
        {
            transform.SetPositionAndRotation(homeAnchor.position, homeAnchor.rotation);
        }
    }

    private void Awake()
    {
        if (_grabbable == null)
        {
            _grabbable = GetComponent<Grabbable>();
        }

        if (_grabPointable == null)
        {
            _grabPointable = _grabbable;
        }
    }

    private void LateUpdate()
    {
        if (homeAnchor == null)
        {
            return;
        }

        if (IsGrabbed())
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            return;
        }

        transform.SetPositionAndRotation(homeAnchor.position, homeAnchor.rotation);
    }

    private bool IsGrabbed()
    {
        if (_grabPointable != null && _grabPointable.SelectingPointsCount > 0)
        {
            return true;
        }

        return _grabbable != null && _grabbable.SelectingPointsCount > 0;
    }
}
