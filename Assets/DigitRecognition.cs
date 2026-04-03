using UnityEngine;
using System.Runtime.InteropServices;
using Unity.InferenceEngine;

public class DigitRecognition : MonoBehaviour
{
    [SerializeField] private float threshhold = 0.9f;

    [SerializeField] private ModelAsset m_modelAsset;

    public float[] results;

    private Worker worker;

    public Texture2D testPicture;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Model model = ModelLoader.Load(m_modelAsset);

        FunctionalGraph graph = new FunctionalGraph();
        FunctionalTensor[] inputs = graph.AddInputs(model);
        FunctionalTensor[] outputs = Functional.Forward(model, inputs);

        FunctionalTensor softmax = Functional.Softmax(outputs[0]);

        Model runtimeModel = graph.Compile(softmax);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);

        Debug.Log(RunAI(testPicture));
    }

   public int RunAI(Texture2D picture)
    {
        using Tensor<float> inputTensor = TextureConverter.ToTensor(picture, 28, 28, 1);

        worker.Schedule(inputTensor);

        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

        results = outputTensor.DownloadToArray();
    
        return GetMaxIndex(results);
    }

    private void OnDisable()
    {
        worker.Dispose();
    }

    public int GetMaxIndex(float[] array)
    {
        int maxIndex = 0;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] > array[maxIndex])
            {
                maxIndex = i;
            }
        }

        if (array[maxIndex] > threshhold)
        {
            return maxIndex;
        }
        else
        {
            return -1;
        }
    }
}
