using System.Collections;
using Meta.XR;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PasstroughCameraDisplay : MonoBehaviour
{
    [SerializeField] private PassthroughCameraAccess m_cameraAccess;

    public float CropPercent = 0.5f;

    public Texture2D Picture;

    [SerializeField] private Renderer m_quadRendered;
    [SerializeField] private string m_textureName;

    [SerializeField] private int m_yoffset;
    [SerializeField] private int m_xoffset;

    private NativeArray<Color32> m_croppedBuffer;

    public IEnumerator TakePictureCoroutine()
    {
        if (m_cameraAccess == null || !m_cameraAccess.IsPlaying)
        {
            yield break;
        }

        yield return new WaitForEndOfFrame();

        var cameraResolution = m_cameraAccess.CurrentResolution;
        int sourceWidth = cameraResolution.x;
        int sourceHeight = cameraResolution.y;

        int cropWidth = (int)(sourceWidth * CropPercent);
        int startX = Mathf.Clamp((sourceWidth - cropWidth) / 2 + m_xoffset, 0, Mathf.Max(0, sourceWidth - cropWidth));
        int startY = Mathf.Clamp((sourceHeight - cropWidth) / 2 + m_yoffset, 0, Mathf.Max(0, sourceHeight - cropWidth));

        EnsurePictureTexture(cropWidth);

        Texture source = m_cameraAccess.GetTexture();
        if (source == null)
        {
            yield break;
        }

        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(source);
        while (!request.done)
        {
            yield return null;
        }

        if (request.hasError)
        {
            Debug.LogWarning("[PasstroughCameraDisplay] AsyncGPUReadback failed, using GetColors fallback.");
            CropFromGetColors(sourceWidth, sourceHeight, cropWidth, startX, startY);
        }
        else
        {
            using (NativeArray<Color32> pixels = request.GetData<Color32>())
            {
                CropFromNativeArray(pixels, sourceWidth, sourceHeight, cropWidth, startX, startY);
            }
        }

        ApplyPictureToQuad();
    }

    private void EnsurePictureTexture(int cropWidth)
    {
        if (Picture != null && Picture.width == cropWidth && Picture.height == cropWidth)
        {
            return;
        }

        Picture = new Texture2D(cropWidth, cropWidth, TextureFormat.RGBA32, false);
    }

    private void CropFromGetColors(int sourceWidth, int sourceHeight, int cropWidth, int startX, int startY)
    {
        NativeArray<Color32> pixels = m_cameraAccess.GetColors();
        if (!pixels.IsCreated || pixels.Length == 0)
        {
            return;
        }

        CropFromNativeArray(pixels, sourceWidth, sourceHeight, cropWidth, startX, startY);
    }

    private void CropFromNativeArray(
        NativeArray<Color32> pixels,
        int sourceWidth,
        int sourceHeight,
        int cropWidth,
        int startX,
        int startY)
    {
        int expectedLength = sourceWidth * sourceHeight;
        if (pixels.Length < expectedLength)
        {
            Debug.LogWarning($"[PasstroughCameraDisplay] Unexpected pixel buffer length: {pixels.Length}, expected {expectedLength}.");
            return;
        }

        int cropCount = cropWidth * cropWidth;
        if (!m_croppedBuffer.IsCreated || m_croppedBuffer.Length != cropCount)
        {
            if (m_croppedBuffer.IsCreated)
            {
                m_croppedBuffer.Dispose();
            }

            m_croppedBuffer = new NativeArray<Color32>(cropCount, Allocator.Persistent);
        }

        for (int y = 0; y < cropWidth; y++)
        {
            NativeArray<Color32>.Copy(
                pixels,
                (startY + y) * sourceWidth + startX,
                m_croppedBuffer,
                y * cropWidth,
                cropWidth);
        }

        Picture.SetPixelData(m_croppedBuffer, 0);
        Picture.Apply(false, false);
    }

    private void ApplyPictureToQuad()
    {
        if (m_quadRendered != null)
        {
            m_quadRendered.material.SetTexture(m_textureName, Picture);
        }
    }

    private void OnDestroy()
    {
        if (m_croppedBuffer.IsCreated)
        {
            m_croppedBuffer.Dispose();
        }
    }
}
