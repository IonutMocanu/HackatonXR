using TMPro;
using UnityEngine;

public class MicroGestureInput : MonoBehaviour
{
    [Header("Hands")]
    public OVRHand OvrHand;

    [Header("Take picture zone")]
    public PasstroughCameraDisplay PhotoComponent;

    [Header("Yolo models")]
    public LeafRecognition LeafRecognition;

    [Header("Text zone")]
    [SerializeField] public TextMeshProUGUI DiseaseNameTextMeshProUGUI;
    [SerializeField] public TextMeshProUGUI SolutionDiseaseTextMeshProUGUI;

    private void Update()
    {
        var microgesture = OvrHand.GetMicrogestureType();

        switch (microgesture)
        {
            case OVRHand.MicrogestureType.ThumbTap:
                PhotoComponent.TakePicture();
                var result = LeafRecognition.RunYoloDiseaseCheck(PhotoComponent.Picture);

                DiseaseNameTextMeshProUGUI.text = "Disease detected: " + result.ToString();
                //SolutionDiseaseTextMeshProUGUI.text = "Solution: " + LeafRecognition.diseaseSolutions[result];
                break;
            case OVRHand.MicrogestureType.Invalid:
                break;
            default:
                break;
        }
    }
}
