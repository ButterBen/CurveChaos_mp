using System.Collections.Generic;
using UnityEngine;

public class DrawLine : MonoBehaviour
{
    private GameObject head = null;
    public List<GameObject> line = null;

    void Start()
    {
        line = new List<GameObject>();
        head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var renderer = head.GetComponent<Renderer>();
        renderer.material.SetColor("_Color", Color.red);
    }
}
