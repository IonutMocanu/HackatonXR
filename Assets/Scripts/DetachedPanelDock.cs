using System;
using System.Collections.Generic;
using System.Reflection;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Zone placeholders around the main panel for detached tabs (left, right, top).
/// Panels parented under <see cref="DetachedPanelsRoot"/> move with the main panel.
/// </summary>
[DisallowMultipleComponent]
public class DetachedPanelDock : MonoBehaviour
{
    public enum ZoneId
    {
        Left,
        Right,
        Top
    }

    private static readonly ZoneId[] ReservationOrder = { ZoneId.Right, ZoneId.Left, ZoneId.Top };

    /// <summary>
    /// Toggle index 0 = stanga, 1 = dreapta, 2 = sus (ordinea iconitelor din pager).
    /// </summary>
    private static readonly ZoneId[] ToggleIndexToZone = { ZoneId.Left, ZoneId.Right, ZoneId.Top };

    [Serializable]
    public class ZoneSlot
    {
        public ZoneId Zone;
        public Transform Anchor;
        public SnapInteractable SnapInteractable;
        [NonSerialized] public GameObject Occupant;
    }

    [Header("Hierarchy (auto-created if empty)")]
    [SerializeField] private Transform mainPanelRoot;
    [SerializeField] private Transform detachedPanelsRoot;
    [SerializeField] private Transform dockZonesRoot;

    [Header("Snap detection (Meta ISDK)")]
    [SerializeField] private Rigidbody snapDetectionRigidbody;
    [SerializeField] private BoxCollider snapDetectionCollider;
    [SerializeField] private Vector3 snapDetectionCenter = Vector3.zero;
    [SerializeField] private Vector3 snapDetectionSize = new(2.5f, 2.5f, 2.5f);

    [Header("Default local poses (relative to main panel)")]
    [SerializeField] private Vector3 rightLocalPosition = new(0.5f, 0f, -0.17f);
    [SerializeField] private Vector3 leftLocalPosition = new(-0.5f, 0f, -0.17f);
    [SerializeField] private Vector3 topLocalPosition = new(0f, 0.55f, -0.17f);
    [SerializeField] private float sideYawDegrees = 35f;

    [SerializeField] private ZoneSlot[] zones = Array.Empty<ZoneSlot>();

    [Tooltip("Legacy spawner transforms from InteractablePanelManager (first = Right).")]
    [SerializeField] private Transform[] legacySpawners = Array.Empty<Transform>();

    public Transform DetachedPanelsRoot => detachedPanelsRoot;
    public Rigidbody SnapDetectionRigidbody => snapDetectionRigidbody;

    public void Initialize(Transform mainPanel, Transform[] spawners)
    {
        mainPanelRoot = mainPanel != null ? mainPanel : mainPanelRoot;
        if (spawners != null && spawners.Length > 0)
        {
            legacySpawners = spawners;
        }

        EnsureHierarchy();
        EnsureSnapDetectionZone();
        EnsureZones();
        BindZoneRigidbodies();
    }

    public bool HasFreeZone()
    {
        for (int i = 0; i < ReservationOrder.Length; i++)
        {
            ZoneSlot candidate = FindZone(ReservationOrder[i]);
            if (candidate != null && candidate.Occupant == null)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsZoneAvailableForPage(int toggleIndex)
    {
        ZoneSlot slot = GetZoneForPage(toggleIndex);
        if (slot != null && slot.Occupant == null)
        {
            return true;
        }

        return HasFreeZone();
    }

    public bool TryReserveZoneForPage(int toggleIndex, out ZoneSlot slot)
    {
        slot = GetZoneForPage(toggleIndex);
        if (slot != null && slot.Occupant == null)
        {
            return true;
        }

        return TryReserveZone(out slot);
    }

    public bool TryReserveZone(out ZoneSlot slot)
    {
        slot = null;
        for (int i = 0; i < ReservationOrder.Length; i++)
        {
            ZoneSlot candidate = FindZone(ReservationOrder[i]);
            if (candidate != null && candidate.Occupant == null)
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private static ZoneId GetZoneIdForToggleIndex(int toggleIndex)
    {
        toggleIndex = Mathf.Max(0, toggleIndex);
        if (toggleIndex < ToggleIndexToZone.Length)
        {
            return ToggleIndexToZone[toggleIndex];
        }

        return ReservationOrder[toggleIndex % ReservationOrder.Length];
    }

    private ZoneSlot GetZoneForPage(int toggleIndex)
    {
        return FindZone(GetZoneIdForToggleIndex(toggleIndex));
    }

    public void DockPanelAtZone(GameObject panel, ZoneSlot slot)
    {
        if (panel == null || slot == null || detachedPanelsRoot == null)
        {
            return;
        }

        slot.Occupant = panel;

        GameObject mainPanel = mainPanelRoot != null ? mainPanelRoot.gameObject : null;
        DetachedPanelSnapSetup.ConfigureGrab(panel, mainPanel);

        if (detachedPanelsRoot != null)
        {
            panel.transform.SetParent(detachedPanelsRoot, true);
        }

        Grabbable shellGrabbable = DetachedPanelSnapSetup.GetOrCreateShellGrabbable(panel);

        DetachedPanelFollowDock followDock = panel.GetComponent<DetachedPanelFollowDock>();
        if (followDock == null)
        {
            followDock = panel.AddComponent<DetachedPanelFollowDock>();
        }

        followDock.Configure(slot.Anchor, detachedPanelsRoot, shellGrabbable);
    }

    public void ReleaseZone(ZoneSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        slot.Occupant = null;
    }

    private void EnsureHierarchy()
    {
        if (mainPanelRoot == null)
        {
            return;
        }

        if (detachedPanelsRoot == null)
        {
            detachedPanelsRoot = FindOrCreateChild(mainPanelRoot, "DetachedPanelsRoot");
        }

        if (dockZonesRoot == null)
        {
            dockZonesRoot = FindOrCreateChild(mainPanelRoot, "DockZones");
        }
    }

    private void EnsureSnapDetectionZone()
    {
        if (mainPanelRoot == null)
        {
            return;
        }

        Transform detectionRoot = FindOrCreateChild(mainPanelRoot, "SnapDetectionZone");
        detectionRoot.localPosition = snapDetectionCenter;

        if (snapDetectionRigidbody == null)
        {
            snapDetectionRigidbody = detectionRoot.GetComponent<Rigidbody>();
            if (snapDetectionRigidbody == null)
            {
                snapDetectionRigidbody = detectionRoot.gameObject.AddComponent<Rigidbody>();
            }

            snapDetectionRigidbody.useGravity = false;
            snapDetectionRigidbody.isKinematic = true;
        }

        if (snapDetectionCollider == null)
        {
            snapDetectionCollider = detectionRoot.GetComponent<BoxCollider>();
            if (snapDetectionCollider == null)
            {
                snapDetectionCollider = detectionRoot.gameObject.AddComponent<BoxCollider>();
            }

            snapDetectionCollider.isTrigger = true;
            snapDetectionCollider.size = snapDetectionSize;
            snapDetectionCollider.center = Vector3.zero;
        }
    }

    private void EnsureZones()
    {
        if (dockZonesRoot == null)
        {
            return;
        }

        if (zones == null || zones.Length == 0)
        {
            zones = new ZoneSlot[3];
        }

        EnsureZoneSlot(ZoneId.Right, rightLocalPosition, Quaternion.Euler(0f, sideYawDegrees, 0f), 0);
        EnsureZoneSlot(ZoneId.Left, leftLocalPosition, Quaternion.Euler(0f, -sideYawDegrees, 0f), 1);
        EnsureZoneSlot(ZoneId.Top, topLocalPosition, Quaternion.Euler(-sideYawDegrees, 0f, 0f), 2);
        ApplyLegacyRightSpawner();
    }

    private void ApplyLegacyRightSpawner()
    {
        if (legacySpawners == null || legacySpawners.Length == 0 || legacySpawners[0] == null)
        {
            return;
        }

        Transform legacy = legacySpawners[0];
        if (legacy.parent == mainPanelRoot || legacy.IsChildOf(mainPanelRoot))
        {
        ZoneSlot right = FindZone(ZoneId.Right);
        if (right == null)
        {
            return;
        }

        right.Anchor = legacy;
        right.SnapInteractable = legacy.GetComponent<SnapInteractable>();
        if (right.SnapInteractable == null)
        {
            right.SnapInteractable = legacy.gameObject.AddComponent<SnapInteractable>();
        }
        }
    }

    private void EnsureZoneSlot(ZoneId zoneId, Vector3 localPosition, Quaternion localRotation, int index)
    {
        if (zones.Length <= index)
        {
            return;
        }

        ZoneSlot slot = zones[index] ?? new ZoneSlot();
        slot.Zone = zoneId;

        if (zoneId == ZoneId.Right
            && legacySpawners != null
            && legacySpawners.Length > 0
            && legacySpawners[0] != null)
        {
            slot.Anchor = legacySpawners[0];
            slot.SnapInteractable = legacySpawners[0].GetComponent<SnapInteractable>();
            if (slot.SnapInteractable == null)
            {
                slot.SnapInteractable = legacySpawners[0].gameObject.AddComponent<SnapInteractable>();
            }

            zones[index] = slot;
            return;
        }

        if (slot.Anchor == null)
        {
            string objectName = $"DockZone_{zoneId}";
            Transform existing = dockZonesRoot.Find(objectName);
            if (existing != null)
            {
                slot.Anchor = existing;
            }
            else
            {
                GameObject anchorObject = new GameObject(objectName);
                slot.Anchor = anchorObject.transform;
                slot.Anchor.SetParent(dockZonesRoot, false);
                slot.Anchor.localPosition = localPosition;
                slot.Anchor.localRotation = localRotation;
                slot.Anchor.localScale = Vector3.one;
            }
        }

        if (slot.SnapInteractable == null && slot.Anchor != null)
        {
            slot.SnapInteractable = slot.Anchor.GetComponent<SnapInteractable>();
            if (slot.SnapInteractable == null)
            {
                slot.SnapInteractable = slot.Anchor.gameObject.AddComponent<SnapInteractable>();
            }
        }

        zones[index] = slot;
    }

    private void BindZoneRigidbodies()
    {
        if (snapDetectionRigidbody == null || zones == null)
        {
            return;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            ZoneSlot slot = zones[i];
            if (slot?.SnapInteractable == null)
            {
                continue;
            }

            DetachedPanelSnapSetup.AssignRigidbody(slot.SnapInteractable, snapDetectionRigidbody);
        }
    }

    private ZoneSlot FindZone(ZoneId zoneId)
    {
        if (zones == null)
        {
            return null;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].Zone == zoneId)
            {
                return zones[i];
            }
        }

        return null;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(childName);
        Transform childTransform = child.transform;
        childTransform.SetParent(parent, false);
        childTransform.localPosition = Vector3.zero;
        childTransform.localRotation = Quaternion.identity;
        childTransform.localScale = Vector3.one;
        return childTransform;
    }
}
