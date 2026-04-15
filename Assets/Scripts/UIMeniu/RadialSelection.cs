using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RangeAttribute = UnityEngine.RangeAttribute;
using UnityEngine.Events;

public class RadialSelection : MonoBehaviour
{
    [Range(2, 10)]
    public int NumberOfRadialPart;

    public GameObject RadialPartPrefab;

    public Transform RadialPartCanvas;

    public float AngleBetweenPart;

    private List<GameObject> m_spawnedParts = new();
    private List<GameObject> m_spawnedIcons = new();

    public Transform HandTransform;

    public OVRInput.Button SpawnButton;

    [Header("Icons")]
    public List<Sprite> RadialIcons;
    public float IconDistance;

    public float IconHoverDistance;

    [Header("Events")]
    public UnityEvent<int> OnPartSelected;

    private int m_currentSelectedPart = -1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {

    }

    // Update is called once per frame
    private void Update()
    {
        if (OVRInput.GetDown(SpawnButton))
        {
            CreateRadialUI();
        }

        if (OVRInput.Get(SpawnButton))
        {
            GetSelectedRadialPart();
        }

        if (OVRInput.GetUp(SpawnButton))
        {
            HideAndTiggerSelected();
        }
        //CreateRadialUI();
        //GetSelectedRadialPart();
    }

    public void HideAndTiggerSelected()
    {
        OnPartSelected.Invoke(m_currentSelectedPart);
        RadialPartCanvas.gameObject.SetActive(false);
    }

    public void GetSelectedRadialPart()
    {
        Vector3 centerToHand = HandTransform.position - RadialPartCanvas.position;
        Vector3 centerToHandProjected = Vector3.ProjectOnPlane(centerToHand, RadialPartCanvas.forward);

        float angle = Vector3.SignedAngle(RadialPartCanvas.up, centerToHandProjected, RadialPartCanvas.forward);

        if (angle < 0) angle += 360;

        m_currentSelectedPart = (int)(angle * NumberOfRadialPart / 360f);

        float sliceAngle = 360f / NumberOfRadialPart;
        float halfSliceAngle = sliceAngle / 2f;

        for (int i = 0; i < m_spawnedParts.Count; i++)
        {
            Transform iconTransform = m_spawnedIcons[i].transform;
            RectTransform iconRect = iconTransform.GetComponent<RectTransform>();

            float iconGlobalAngle = (i * sliceAngle) + 90f - halfSliceAngle;
            Vector3 direction2D = Quaternion.Euler(0, 0, iconGlobalAngle) * Vector3.up;

            Vector3 defaultPos = direction2D * IconDistance;
            defaultPos.z = -2f;

            Vector3 hoverPos = direction2D * (IconDistance + IconHoverDistance);
            hoverPos.z = -2f;

            if (i == m_currentSelectedPart)
            {
                m_spawnedParts[i].GetComponent<Image>().color = Color.yellow;
                m_spawnedParts[i].transform.localScale = 1.1f * Vector3.one;

                iconRect.localScale = 1.2f * Vector3.one;
                iconRect.localPosition = hoverPos;
            }
            else
            {
                m_spawnedParts[i].GetComponent<Image>().color = Color.white;
                m_spawnedParts[i].transform.localScale = Vector3.one;

                iconRect.localScale = Vector3.one;
                iconRect.localPosition = defaultPos;
            }
        }
    }

    private void CreateRadialUI()
    {
        RadialPartCanvas.gameObject.SetActive(true);
        RadialPartCanvas.position = HandTransform.position;
        RadialPartCanvas.rotation = HandTransform.rotation;

        foreach (var item in m_spawnedParts) { Destroy(item); }
        foreach (var item in m_spawnedIcons) { Destroy(item); }

        m_spawnedParts.Clear();
        m_spawnedIcons.Clear();

        float sliceAngle = 360f / NumberOfRadialPart;
        float halfSliceAngle = sliceAngle / 2f;

        for (int i = 0; i < NumberOfRadialPart; i++)
        {
            float sliceStartAngle = (i * sliceAngle) + 90f - (AngleBetweenPart / 2f);
            var radialPartEulerAngle = new Vector3(0, 0, sliceStartAngle);

            var spwanedRadialPart = Instantiate(RadialPartPrefab, RadialPartCanvas);
            spwanedRadialPart.transform.position = RadialPartCanvas.position;
            spwanedRadialPart.transform.localEulerAngles = radialPartEulerAngle;
            spwanedRadialPart.GetComponent<Image>().fillAmount = (1f / NumberOfRadialPart) - (AngleBetweenPart / 360f);

            m_spawnedParts.Add(spwanedRadialPart);

            if (RadialIcons != null && i < RadialIcons.Count && RadialIcons[i] != null)
            {
                var icon = new GameObject(RadialIcons[i].name);
                icon.transform.SetParent(RadialPartCanvas, false);
                icon.transform.SetAsLastSibling();

                icon.AddComponent<Image>().sprite = RadialIcons[i];
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.sizeDelta = 31f * Vector2.one;

                float iconGlobalAngle = (i * sliceAngle) + 90f - halfSliceAngle;
                Vector3 iconLocalPos = Quaternion.Euler(0, 0, iconGlobalAngle) * Vector3.up * IconDistance;

                iconLocalPos.z = -2f;
                iconRect.localPosition = iconLocalPos;
                iconRect.localRotation = Quaternion.identity;

                m_spawnedIcons.Add(icon);
            }
            else
            {
                var emptyIcon = new GameObject("EmptyIcon");
                emptyIcon.transform.SetParent(RadialPartCanvas, false);
                m_spawnedIcons.Add(emptyIcon);
            }
        }
    }
}