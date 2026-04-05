using UnityEngine;
using Unity.InferenceEngine;

public class LeafRecognition : MonoBehaviour
{
    [SerializeField] private float confidenceThreshold = 0.4f;
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
            string detectedDisease = RunYoloDiseaseCheck(testPicture);
            Debug.Log("Rezultat detecție: " + detectedDisease);
        }
    }

    public string RunYoloDiseaseCheck(Texture2D picture)
    {
        using Tensor<float> inputTensor = TextureConverter.ToTensor(picture, 640, 640, 3);
        worker.Schedule(inputTensor);

        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        TensorShape shape = outputTensor.shape;

        float[] results = outputTensor.DownloadToArray();

        int diseaseIndex = GetHighestConfidenceClass(results, shape);

        if (diseaseIndex != -1)
        {
            if (diseaseIndex < diseaseNames.Length)
            {
                return diseaseNames[diseaseIndex];
            }
            else
            {
                return $"Eroare logică: Clasa detectată are ID-ul {diseaseIndex}, dar nu ar trebui să depășească 11!";
            }
        }

        return "Nicio boală detectată clar (încredere sub pragul setat).";
    }

    private int GetHighestConfidenceClass(float[] array, TensorShape shape)
    {
        int dim1 = shape[1];
        int dim2 = shape[2];

        int channels, numAnchors;
        bool isTransposed;

        if (dim1 > dim2)
        {
            numAnchors = dim1; // 8400
            channels = dim2;   // 16
            isTransposed = true; // Modelul este [1, 8400, 16]
        }
        else
        {
            channels = dim1;   // 16
            numAnchors = dim2; // 8400
            isTransposed = false; // Modelul este [1, 16, 8400]
        }

        int actualNumClasses = channels - 4; // 16 - 4 coordonate = 12 clase

        int bestClassIndex = -1;
        float maxConfidence = 0f;

        // Parcurgem toate ancorele și toate clasele
        for (int anchor = 0; anchor < numAnchors; anchor++)
        {
            for (int classIndex = 0; classIndex < actualNumClasses; classIndex++)
            {
                float confidence;

                if (isTransposed)
                {
                    // Citire pentru forma [1, 8400, 16]
                    confidence = array[anchor * channels + 4 + classIndex];
                }
                else
                {
                    // Citire pentru forma [1, 16, 8400]
                    confidence = array[(4 + classIndex) * numAnchors + anchor];
                }

                if (confidence > maxConfidence)
                {
                    maxConfidence = confidence;
                    bestClassIndex = classIndex;
                }
            }
        }

        if (maxConfidence > confidenceThreshold)
        {
            Debug.Log($"Încredere maximă: {(maxConfidence * 100).ToString("0.0")}% pentru clasa {bestClassIndex} ({diseaseNames[bestClassIndex]})");
            return bestClassIndex;
        }

        return -1;
    }

    private void OnDisable()
    {
        if (worker != null)
        {
            worker.Dispose();
        }
    }
}