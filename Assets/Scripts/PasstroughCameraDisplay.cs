using UnityEngine;
using Meta.XR;
using TMPro;

public class PasstroughCameraDisplay : MonoBehaviour
{
    [SerializeField] private PassthroughCameraAccess m_cameraAccess;

    public float CropPercent = 0.5f;

    private Texture2D m_fullTexture;
    private Texture2D m_picture;

    [SerializeField] private Renderer m_quadRendered;
    [SerializeField] private string m_textureName;

    public LeafRecognition LeafRecognition;


    [SerializeField] private TextMeshProUGUI m_textMeshProUGUI;

    [SerializeField] private int m_yoffset;
    [SerializeField] private int m_xoffset;


    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            TakePicture();

            var result = LeafRecognition.RunYoloDiseaseCheck(m_picture);

            m_textMeshProUGUI.text = "Prediction: " + result.ToString();
        }
    }

    public void TakePicture()
    {
        var cameraResolution = m_cameraAccess.CurrentResolution;

        int sourceWidth = cameraResolution[0];
        int sourceHeight = cameraResolution[1];

        int cropWidth = (int)(sourceWidth * CropPercent);

        int startX = (sourceWidth - cropWidth) / 2 + m_xoffset;
        int startY = (sourceHeight - cropWidth) / 2 + m_yoffset;

        if (m_fullTexture == null || m_fullTexture.width != sourceWidth || m_fullTexture.height != sourceHeight)
        {
            m_fullTexture = new Texture2D(sourceWidth, sourceHeight);
        }

        if (m_picture == null || m_picture.width != cropWidth || m_picture.height != cropWidth)
        {
            m_picture = new Texture2D(cropWidth, cropWidth);
        }

        var pixels = m_cameraAccess.GetColors();
        m_fullTexture.SetPixelData(pixels, 0);
        m_fullTexture.Apply();

        Color[] croppedPixels = m_fullTexture.GetPixels(startX, startY, cropWidth, cropWidth);

        m_picture.SetPixels(croppedPixels);
        m_picture.Apply();

        m_quadRendered.material.SetTexture(m_textureName, m_picture);
    }
}