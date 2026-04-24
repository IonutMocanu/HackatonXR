using UnityEngine;
using Meta.XR;
using TMPro;

public class PasstroughCameraDisplay : MonoBehaviour
{
    [SerializeField] private PassthroughCameraAccess m_cameraAccess;

    public float CropPercent = 0.5f;

    private Texture2D m_fullTexture;
    public Texture2D Picture;

    [SerializeField] private Renderer m_quadRendered;
    [SerializeField] private string m_textureName;

    //public LeafRecognition LeafRecognition;

    [SerializeField] private int m_yoffset;
    [SerializeField] private int m_xoffset;


    //private void Update()
    //{
    //    if (OVRInput.GetDown(OVRInput.Button.One))
    //    {
    //        TakePicture();

    //        var result = LeafRecognition.RunYoloDiseaseCheck(Picture);

    //        TextMeshProUGUI.text = "Prediction: " + result.ToString();
    //    }
    //}

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

        if (Picture == null || Picture.width != cropWidth || Picture.height != cropWidth)
        {
            Picture = new Texture2D(cropWidth, cropWidth);
        }

        var pixels = m_cameraAccess.GetColors();
        m_fullTexture.SetPixelData(pixels, 0);
        m_fullTexture.Apply();

        Color[] croppedPixels = m_fullTexture.GetPixels(startX, startY, cropWidth, cropWidth);

        Picture.SetPixels(croppedPixels);
        Picture.Apply();

        m_quadRendered.material.SetTexture(m_textureName, Picture);
    }
}