using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;

public class LeafRecognition : MonoBehaviour
{
    [SerializeField] private float confidenceThreshold = 0.25f;
    [SerializeField] private ModelAsset m_modelAsset;

    public string[] diseaseNames = {
        "ALS", "Angular Leafspot", "Anthracnose Fruit Rot", "Bean Rust",
        "Blossom Blight", "Gray Mold", "Leaf Spot", "Powdery Mildew Fruit",
        "Powdery Mildew Leaf", "disease", "leaf mold", "spider mites"
    };

    public Dictionary<string, string> diseaseSolutions = new()
    {
        { "ALS", "Apply copper-based fungicides and practice crop rotation." },
        { "Angular Leafspot", "Use copper-based treatments, avoid overhead watering (irrigate at the base), and destroy infected plant debris." },
        { "Anthracnose Fruit Rot", "Remove and destroy infected fruits. Apply preventive fungicides (e.g., copper-based or chlorothalonil)." },
        { "Bean Rust", "Plant resistant varieties, ensure good air circulation, and apply rust-specific fungicides at the first sign of infection." },
        { "Blossom Blight", "Prune and burn affected branches/flowers. Apply preventive fungicide treatments during the bud and blooming stages." },
        { "Gray Mold", "Improve ventilation, reduce humidity (especially in greenhouses), and use Botrytis-specific fungicides." },
        { "Leaf Spot", "Remove diseased leaves, ensure proper spacing between plants, and apply broad-spectrum fungicides." },
        { "Powdery Mildew Fruit", "Apply sulfur-based treatments, neem oil, or potassium bicarbonate to protect fruits from mildew." },
        { "Powdery Mildew Leaf", "Spray leaves with mildew-specific fungicides (sulfur-based). Avoid excessive nitrogen fertilization." },
        { "disease", "Generic identification: Requires a more detailed visual inspection to determine the correct treatment. Maintain good plant hygiene." },
        { "leaf mold", "Decrease air humidity, increase ventilation, and apply preventive fungicides (common in greenhouse tomatoes)." },
        { "spider mites", "This is a pest, not a disease. Treat with neem oil, insecticidal soap, or specific miticides. Regular water misting can deter their spread." },
        { "Undetected", "Undetected, try again!" }
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
        return "Undetected";
    }

    private void OnDisable()
    {
        if (worker != null) worker.Dispose();
    }
}