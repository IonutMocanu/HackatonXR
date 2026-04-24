using TMPro;
using UnityEngine;

public class InteractablePanelManager : MonoBehaviour
{
    public GameObject InteractableUIPanel;
    public Transform FatherTransform;
    //public TextMeshProUGUI NextTextMeshProUGUI;

    [Header("Setări Vizibilitate")]
    [Tooltip("1 = Perfect aliniat, 0 = Din profil. 0.5 înseamnă o toleranță de aprox 60 de grade.")]
    [Range(-1f, 1f)]
    public float VizibilitateThreshold = 0.5f;


    private void Update()
    {
        //NextTextMeshProUGUI.text = gameObject.transform.rotation.ToString();

        // 1. Calculăm direcția de la palmă către cameră (Vector normalizat)
        Vector3 directieCatreCamera = (Camera.main.transform.position - transform.position).normalized;

        // 2. Determinăm vectorul "feței" palmei.
        // NOTĂ: În funcție de cum este orientat modelul/prefab-ul tău, 
        // fața palmei ar putea fi transform.forward sau -transform.forward.
        Vector3 normalaPalmei = -transform.up;

        // 3. Calculăm produsul scalar (Dot Product)
        float dotProduct = Vector3.Dot(normalaPalmei, directieCatreCamera);

        // 4. Dacă produsul scalar depășește pragul stabilit, palma e îndreptată spre noi
        bool estePalmaSpreCamera = dotProduct > VizibilitateThreshold;

        // Optimizare: Aplicăm SetActive doar dacă starea trebuie schimbată
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
}
