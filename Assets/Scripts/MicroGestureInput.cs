using TMPro;
using UnityEngine;

public class MicroGestureInput : MonoBehaviour
{
    [Header("Hands")]
    public OVRHand OvrHand;

    [Header("Take picture zone")]
    public PasstroughCameraDisplay PhotoComponent;

    [Header("Yolo models")]
    //public LeafRecognition LeafRecognition;
    public ModelManagement ModelMng;

    [Header("LLM vision (optional)")]
    [SerializeField] private QwenClient qwenClient;
    [SerializeField] private bool sendPictureToLlm = true;
    [TextArea(2, 4)]
    [SerializeField] private string llmImagePrompt = "Analyze this crop image and diagnose any visible plant disease or stress.";

    [Header("Switcher")]
    [SerializeField] public bool IsLLM;

    private void Update()
    {
        var microgesture = OvrHand.GetMicrogestureType();

        switch (microgesture)
        {
            case OVRHand.MicrogestureType.ThumbTap:
                PhotoComponent.TakePicture();
                //var result = LeafRecognition.RunYoloDiseaseCheck(PhotoComponent.Picture);
                ModelMng.RunYoloDiseaseCheck(PhotoComponent.Picture);

                if (sendPictureToLlm && qwenClient != null)
                {
                    qwenClient.AskQwenWithImage(PhotoComponent.Picture, llmImagePrompt);
                }

                //DiseaseNameTextMeshProUGUI.text = "Disease detected: " + result.ToString();
                //SolutionDiseaseTextMeshProUGUI.text = "Solution: " + LeafRecognition.diseaseSolutions[result];
                break;
            case OVRHand.MicrogestureType.Invalid:
                break;
            default:
                break;
        }
    }


    public void SwitchLLM()
    {
        IsLLM = true;
    }

    public void SwitchYolo()
    {
        IsLLM = false;
    }
}
