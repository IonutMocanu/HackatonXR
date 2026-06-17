using System.Collections;
using TMPro;
using UnityEngine;

public class MicroGestureInput : MonoBehaviour
{
    [Header("Hands")]
    public OVRHand OvrHand;

    [Header("Take picture zone")]
    public PasstroughCameraDisplay PhotoComponent;

    [Header("Yolo models")]
    public ModelManagement ModelMng;

    [Header("LLM vision (optional)")]
    [SerializeField] private QwenClient qwenClient;
    [SerializeField] private bool sendPictureToLlm = true;
    [TextArea(2, 4)]
    [SerializeField] private string llmImagePrompt = "Analyze this crop image and diagnose any visible plant disease or stress.";

    [Header("Switcher")]
    [SerializeField] public bool IsLLM;

    private OVRHand.MicrogestureType m_lastGesture;
    private bool m_isHandlingTap;

    private void Update()
    {
        var microgesture = OvrHand.GetMicrogestureType();

        if (microgesture == OVRHand.MicrogestureType.ThumbTap
            && m_lastGesture != OVRHand.MicrogestureType.ThumbTap
            && !m_isHandlingTap)
        {
            StartCoroutine(HandleThumbTapCoroutine());
        }

        m_lastGesture = microgesture;
    }

    private IEnumerator HandleThumbTapCoroutine()
    {
        m_isHandlingTap = true;

        yield return PhotoComponent.TakePictureCoroutine();

        if (IsLLM)
        {
            if (sendPictureToLlm && qwenClient != null)
            {
                qwenClient.AskQwenWithImage(PhotoComponent.Picture, llmImagePrompt);
            }
        }
        else if (ModelMng != null)
        {
            yield return null;
            ModelMng.RunYoloDiseaseCheck(PhotoComponent.Picture);
        }

        m_isHandlingTap = false;
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
