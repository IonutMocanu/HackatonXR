using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public class DiseaseInfo
{
    public string diseaseName;

    [TextArea(3, 5)]
    public string solution;
}

[System.Serializable]
public class CropModelConfig
{
    public string buttonID;
    public ModelAsset modelAsset;
    public DiseaseInfo[] diseases;
    public float confidenceThreshold = 0.25f;
}

public class ModelManagement : MonoBehaviour
{
    [Header("Configuratii Modele AI")]
    public List<CropModelConfig> availableModels;

    [Header("Debug Section")]
    public TextMeshProUGUI ModelNameTextMeshProUGUI;

    [Header("Text zone")]
    [SerializeField] public TextMeshProUGUI DiseaseNameTextMeshProUGUI;
    [SerializeField] public TextMeshProUGUI SolutionDiseaseTextMeshProUGUI;

    private CropModelConfig currentConfig;
    private Worker worker;
    private Model runtimeModel;

    public string lastDetectedDisease { get; private set; }
    public string lastSolution { get; private set; }

    void Start()
    {
        if (availableModels.Count > 0)
        {
            SwitchModel(availableModels[0].buttonID);
        }
    }

    public void SwitchModel(string targetButtonID)
    {
        CropModelConfig newConfig = availableModels.Find(model => model.buttonID == targetButtonID);

        if (newConfig != null)
        {
            if (worker != null) worker.Dispose();

            currentConfig = newConfig;
            runtimeModel = ModelLoader.Load(currentConfig.modelAsset);
            worker = new Worker(runtimeModel, BackendType.GPUCompute);

            Debug.Log($"✅ Model schimbat cu succes! Detectăm pentru: {currentConfig.buttonID}");
            //ModelNameTextMeshProUGUI.text = "Model: " + currentConfig.buttonID;
        }
        else
        {
            Debug.LogError($"❌ Nu s-a găsit modelul pentru: {targetButtonID}");
        }
    }

    public void RunYoloDiseaseCheck(Texture2D picture)
    {
        if (worker == null || currentConfig == null) return;

        using Tensor<float> inputTensor = TextureConverter.ToTensor(picture, 224, 224, 3);
        worker.Schedule(inputTensor);

        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        float[] results = outputTensor.DownloadToArray();

        float bestConfidence = -1f;
        int bestClassId = -1;

        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] > bestConfidence)
            {
                bestConfidence = results[i];
                bestClassId = i;
            }
        }

        if (bestConfidence > currentConfig.confidenceThreshold && bestClassId >= 0 && bestClassId < currentConfig.diseases.Length)
        {
            DiseaseInfo detectedInfo = currentConfig.diseases[bestClassId];

            lastDetectedDisease = detectedInfo.diseaseName;
            lastSolution = detectedInfo.solution;

            ModelNameTextMeshProUGUI.text = "Model: " + currentConfig.buttonID + $" clasificare: {lastDetectedDisease} | incredere: {(bestConfidence * 100).ToString("F2")}%";

            DiseaseNameTextMeshProUGUI.text = "Disease " + lastDetectedDisease;
            SolutionDiseaseTextMeshProUGUI.text = lastSolution;

            return;
        }

        lastDetectedDisease = "Undetected";
        lastSolution = "Unclear! Try again.";
        Debug.Log($"❌ Nedetectat. Scor maxim obținut: {(bestConfidence * 100).ToString("F2")}% pentru clasa {bestClassId}");

        DiseaseNameTextMeshProUGUI.text = lastDetectedDisease;
        SolutionDiseaseTextMeshProUGUI.text = lastSolution;

        return;
    }

    private void OnDisable()
    {
        if (worker != null) worker.Dispose();
    }
}