using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A ScrollRect that only handles drags along its own scrolling axis and
/// forwards the orthogonal drags to a parent ScrollRect. This lets the chat
/// scroll vertically while horizontal swipes still page between panels.
/// </summary>
public class DirectionalScrollRect : ScrollRect
{
    private ScrollRect parentScrollRect;
    private bool routeToParent;

    protected override void Awake()
    {
        base.Awake();
        ResolveParent();
    }

    private void ResolveParent()
    {
        if (parentScrollRect != null)
        {
            return;
        }

        if (transform.parent != null)
        {
            parentScrollRect = transform.parent.GetComponentInParent<ScrollRect>();
        }
    }

    public override void OnInitializePotentialDrag(PointerEventData eventData)
    {
        ResolveParent();
        if (parentScrollRect != null)
        {
            parentScrollRect.OnInitializePotentialDrag(eventData);
        }
        base.OnInitializePotentialDrag(eventData);
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        ResolveParent();

        // Decide once, at the start of the gesture, whether this is a
        // horizontal swipe (page) or a vertical scroll (chat).
        Vector2 dragDelta = eventData.position - eventData.pressPosition;
        bool horizontalDominant = Mathf.Abs(dragDelta.x) > Mathf.Abs(dragDelta.y);

        // Route to the parent when the gesture is horizontal (and we can't
        // scroll horizontally ourselves) so panel swiping keeps working.
        routeToParent = horizontalDominant && !horizontal && parentScrollRect != null;

        if (routeToParent)
        {
            parentScrollRect.OnBeginDrag(eventData);
        }
        else
        {
            base.OnBeginDrag(eventData);
        }
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (routeToParent && parentScrollRect != null)
        {
            parentScrollRect.OnDrag(eventData);
        }
        else
        {
            base.OnDrag(eventData);
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (routeToParent && parentScrollRect != null)
        {
            parentScrollRect.OnEndDrag(eventData);
        }
        else
        {
            base.OnEndDrag(eventData);
        }
        routeToParent = false;
    }
}
