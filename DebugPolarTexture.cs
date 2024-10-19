using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugPolarTexture : MonoBehaviour
{
    public Texture2D LDRTex;
    public GameObject Sphere;
    public Transform parent;
    // Start is called before the first frame update
    void Start()
    {
        PolarDebug(LDRTex);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void texUVDebug(Texture2D LDRTex)
    {
        Color[] pixels = LDRTex.GetPixels();
        int width = LDRTex.width, height = LDRTex.height;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height / 2; y++)
            {
                Color pic = pixels[x + y * width];
                Vector3 position = new Vector3(x, y, 0);
                CreateObjects(position, pic, Sphere, parent);
            }
        }
    }
    void PolarDebug(Texture2D LDRTex)
    {
        Color[] pixels = LDRTex.GetPixels();
        int width = LDRTex.width, height = LDRTex.height;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color pic = pixels[x + y * width];
                Vector3 position = UV23DPosition(x, y, width, height, 10);
                CreateObjects(position, pic, Sphere, parent);
            }
        }
    }
    Vector3 UV23DPosition(int x, int y,int width,int height,float radius) 
    {
        Vector3 Position;
        float u = x / (float)width;
        float v = y / (float)height;
        float phi =-2*Mathf.PI*u+Mathf.PI;
        float theta =- Mathf.PI*v+Mathf.PI;// v*  Mathf.PI - Mathf.PI/2;

        float px = Mathf.Sin(theta) * Mathf.Cos(phi);
        float pz = Mathf.Sin(theta) * Mathf.Sin(phi);
        float py = Mathf.Cos(theta);
        Position = new Vector3(px, py,pz);
        Position *= radius;
        return Position;
    }
    GameObject CreateObjects(Vector3 position, Color color, GameObject obj,Transform parent)
    {
        GameObject objInstance = Instantiate(obj, position, Quaternion.identity, parent);
        objInstance.GetComponent<Renderer>().material.color = color;
        return objInstance;
    }
}
