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

    public Transform HandTransform;

    public UnityEvent<int> OnPartSelected;

    public OVRInput.Button SpawnButton;

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
    }

    public void HideAndTiggerSelected()
    {
        OnPartSelected.Invoke(m_currentSelectedPart);
        RadialPartCanvas.gameObject.SetActive(false);
    }

    public void GetSelectedRadialPart()
    {
        Vector3 centerToHand = HandTransform.position - RadialPartCanvas.position;
        Vector3 centerToHandProjected = Vector3.ProjectOnPlane(centerToHand,RadialPartCanvas.forward);

        float angle = Vector3.SignedAngle(RadialPartCanvas.up, centerToHandProjected, RadialPartCanvas.forward);

        if (angle < 0) angle += 360;
        Debug.Log("Angle: " + angle.ToString());

        m_currentSelectedPart = (int)angle * NumberOfRadialPart / 360;

        Debug.Log("Index: " + m_currentSelectedPart.ToString());

        for (int i = 0; i < m_spawnedParts.Count; i++)
        {
            if(i == m_currentSelectedPart)
            {
                m_spawnedParts[i].GetComponent<Image>().color = Color.yellow;
                m_spawnedParts[i].transform.localScale = 1.1f * Vector3.one;
            }
            else
            {
                m_spawnedParts[i].GetComponent<Image>().color = Color.white;
                m_spawnedParts[i].transform.localScale = Vector3.one;
            }
        }
    }

    private void CreateRadialUI()
    {
        RadialPartCanvas.gameObject.SetActive(true);
        RadialPartCanvas.position = HandTransform.position;
        RadialPartCanvas.rotation = HandTransform.rotation;

        foreach (var item in m_spawnedParts)
        {
            Destroy(item);
        }

        m_spawnedParts.Clear();

        for (int i = 0; i < NumberOfRadialPart; i++)
        {
            var angle = (i * 360 / NumberOfRadialPart - AngleBetweenPart / 2) + 90;

            var radialPartEulerAngle = new Vector3(0, 0, angle);

            var spwanedRadialPart = Instantiate(RadialPartPrefab, RadialPartCanvas);

            spwanedRadialPart.transform.position = RadialPartCanvas.position;

            spwanedRadialPart.transform.localEulerAngles = radialPartEulerAngle;

            spwanedRadialPart.GetComponent<Image>().fillAmount = (1f / NumberOfRadialPart) - (AngleBetweenPart / 360f);

            m_spawnedParts.Add(spwanedRadialPart);
        }
    }
}
