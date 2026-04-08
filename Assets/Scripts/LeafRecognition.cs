using UnityEngine;
using Unity.InferenceEngine;

public class LeafRecognition : MonoBehaviour
{
    [SerializeField] private float confidenceThreshold = 0.25f;
    [SerializeField] private ModelAsset m_modelAsset;

    public string[] diseaseNames = {
        "ALS", "Angular Leafspot", "Anthracnose Fruit Rot", "Bean Rust",
        "Blossom Blight", "Gray Mold", "Leaf Spot", "Powdery Mildew Fruit",
        "Powdery Mildew Leaf", "disease", "leaf mold", "spider mites"
    };

    public Texture2D testPicture;

    private Worker worker;
    private Model runtimeModel;

    void Start()
    {
        runtimeModel = ModelLoader.Load(m_modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);

        if (testPicture != null)
        {
            string rezultat = RunYoloDiseaseCheck(testPicture);
            Debug.Log("Rezultat test Start: " + rezultat);
        }
    }

    public string RunYoloDiseaseCheck(Texture2D picture)
    {
        // 1. Pregătim imaginea
        using Tensor<float> inputTensor = TextureConverter.ToTensor(picture, 640, 640, 3);

        // 2. Rulăm inferența
        worker.Schedule(inputTensor);

        // 3. Extragem rezultatele [1, 300, 6]
        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        float[] results = outputTensor.DownloadToArray();

        int numDetections = 300;
        int elementsPerDetection = 6;

        float bestConfidence = 0f;
        int bestClassId = -1;

        // 4. Căutăm detecția cu cea mai mare probabilitate
        for (int i = 0; i < numDetections; i++)
        {
            int baseIndex = i * elementsPerDetection;

            // Extragem doar Încrederea (index 4) și ID-ul clasei (index 5)
            float confidence = results[baseIndex + 4];
            int classId = Mathf.RoundToInt(results[baseIndex + 5]);

            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestClassId = classId;
            }
        }

        // 5. Interpretăm rezultatul
        if (bestConfidence > confidenceThreshold && bestClassId >= 0 && bestClassId < diseaseNames.Length)
        {
            string detectedDisease = diseaseNames[bestClassId];
            Debug.Log($"✅ BOALĂ DETECTATĂ: {(bestConfidence * 100).ToString("0.0")}% pentru clasa {bestClassId} ({detectedDisease})");
            return detectedDisease;
        }

        Debug.Log("Nicio boală detectată clar peste pragul setat.");
        return "Nedetectat";
    }

    private void OnDisable()
    {
        if (worker != null) worker.Dispose();
    }
}