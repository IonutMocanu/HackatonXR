using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

public class InteractablePanelManager : MonoBehaviour
{
    public GameObject InteractableUIPanel;
    public Transform FatherTransform;
    public Transform[] spawners;
    public GameObject[] Pages;
    public PageScroll PageScrollObject;
    //public TextMeshProUGUI NextTextMeshProUGUI;

    [Range(-1f, 1f)]
    public float VizibilitateThreshold = 0.5f;

    private void Update()
    {
        //NextTextMeshProUGUI.text = gameObject.transform.rotation.ToString();
        Vector3 directieCatreCamera = (Camera.main.transform.position - transform.position).normalized;

        Vector3 normalaPalmei = -transform.up;

        float dotProduct = Vector3.Dot(normalaPalmei, directieCatreCamera);
        bool estePalmaSpreCamera = dotProduct > VizibilitateThreshold;

        GameObject buton = gameObject.transform.GetChild(0).gameObject;
        if (buton.activeSelf != estePalmaSpreCamera)
        {
            buton.SetActive(estePalmaSpreCamera);
        }
    }

    public void OnOffPanel()
    {
        if (InteractableUIPanel.activeInHierarchy)
        {
            InteractableUIPanel.SetActive(false);
        }
        else
        {
            InteractableUIPanel.SetActive(true);
            InteractableUIPanel.transform.position = FatherTransform.position;
            InteractableUIPanel.transform.rotation = new Quaternion(FatherTransform.rotation.x, FatherTransform.rotation.y, 0f, FatherTransform.transform.rotation.w);
        }
    }

    public void SpawnTab()
    {
        GameObject ChildPanel = Instantiate(InteractableUIPanel, spawners[0].transform.position, spawners[0].transform.rotation);

        GameObject topBar;
        GameObject bottomBar;
        GameObject mainMenu;
        //int k = 0;


        //dezactivare butoane top bar
        topBar = ChildPanel.transform.Find("PanelInteractable").transform.GetChild(1).transform.GetChild(1).gameObject;
        topBar.transform.GetChild(1).gameObject.SetActive(false);
        topBar.transform.GetChild(2).gameObject.SetActive(false);
        topBar.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>().text = PageScrollObject.PageIndex.ToString(); //debug

        //alegere panel-uri
        mainMenu = ChildPanel.transform.Find("PanelInteractable").transform.GetChild(1).transform.GetChild(2).transform.GetChild(0).transform.GetChild(0).gameObject;

        for (var i = 0; i < Pages.Length; i++)
        {
            if (i == PageScrollObject.PageIndex)
            {
                Pages[i].transform.SetParent(mainMenu.transform, false);
            }
        }

        //foreach(Transform child in mainMenu.transform)
        //{
        //    //if(k != PageScrollObject.PageIndex) Destroy(child);

        //    if(k == PageScrollObject.PageIndex)
        //    {

        //    }


        //    k++;
        //}

        //alegere butoane meniul orizontal ? il anulam ??? -- de stabilit
        bottomBar = ChildPanel.transform.Find("PanelInteractable").transform.GetChild(1).transform.GetChild(3).gameObject;
        bottomBar.transform.GetChild(0).gameObject.SetActive(false);
    }
}
