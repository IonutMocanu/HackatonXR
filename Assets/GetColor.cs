using System.Collections.Generic;
using UnityEngine;

public class GetColor : MonoBehaviour
{
    public List<Color> Colors;

    public void SetColor(int i)
    {
        GetComponent<Renderer>().material.color = Colors[i];
    }
}
