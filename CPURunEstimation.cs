using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class CPURunEstimation : MonoBehaviour
{
    public ComputeShader cs;
    public RenderTexture LDRtex;
    GameObject[] LIghts;
    GameObject[] Cubes;
    public GameObject light;
    public GameObject Cube;
    public Transform parent;

    public GameObject HDRPlane;
    public GameObject LabelPlane;
    public GameObject IrradiancePlane;
    public GameObject ResultPositionPlane;


    int width, height;
    Texture2D ResultPositiontexture;

    public class EstiVales
    {
        public Vector2[] polar;
        public Vector3[] ELs;

        public EstiVales(Vector2[] polar, Vector3[] ELs)
        {
            this.ELs = ELs;
            this.polar = polar;
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        //------------- Estimation-----------------
        EstiVales estivalue = Estimation();

        LIghts = new GameObject[estivalue.polar.Length];
        Cubes = new GameObject[estivalue.polar.Length];


        Vector4[] EstiPosition = new Vector4[width * height];
        float lr = 0.3f, lg = 0.59f, lb = 0.11f;

        for (int i = 1; i < estivalue.polar.Length; i++)
        {
            var p = estivalue.polar[i];

            Vector3 position = PolarToCartesian(p.x, p.y);
            Color color = new Color(estivalue.ELs[i].x, estivalue.ELs[i].y, estivalue.ELs[i].z, 1);
            float intensity = Vector3.Dot(estivalue.ELs[i], new Vector3(lr, lg, lb));
            LIghts[i] = CreateLight(position, color, light, intensity, parent);
            Cubes[i] = CreateCube(position, color, Cube, parent);

        }
        for (int i = 1; i < estivalue.polar.Length; i++)
        {

            float intensity = Vector3.Dot(estivalue.ELs[i], new Vector3(lr, lg, lb));
            Vector2 XYposition = Polar2XY(estivalue.polar[i].x, estivalue.polar[i].y, width, height);
            EstiPosition[(int)XYposition.x + (int)XYposition.y * width] = new Vector4(intensity, 0, 0, 1);
        }

        ResultPositiontexture = new Texture2D(width, height, TextureFormat.RGB24, false);


    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < LIghts.Length; i++)
        {
            if (LIghts[i] != null)
            {
                Destroy(LIghts[i]);
                Destroy(Cubes[i]);
            }
        }
        EstiVales estivalue = Estimation();

        LIghts = new GameObject[estivalue.polar.Length];
        Cubes = new GameObject[estivalue.polar.Length];

        Vector4[] EstiPosition = new Vector4[width * height];
        float lr = 0.3f, lg = 0.59f, lb = 0.11f;

        for (int i = 1; i < estivalue.polar.Length; i++)
        {
            
            
            float intensity = Vector3.Dot(estivalue.ELs[i], new Vector3(lr, lg, lb));
            
            
            if (intensity > 0.5f)
            {
                var p = estivalue.polar[i];

                Vector3 position = PolarToCartesian(p.x, p.y);
                Color color = new Color(estivalue.ELs[i].x, estivalue.ELs[i].y, estivalue.ELs[i].z, 1);
                color.a = 1;
                Color Debugcolor = color;
                LIghts[i] = CreateLight(position, color, light, intensity, parent);
                Debug.Log("light: " + i + " position: " + LIghts[i].transform + " intensity :" + intensity);
                Debugcolor= Color.red;
                Cubes[i] = CreateCube(position, Debugcolor, Cube, parent);
            }
            
            
            
        }
        for (int i = 1; i < estivalue.polar.Length; i++)
        {

            float intensity = Vector3.Dot(estivalue.ELs[i], new Vector3(lr, lg, lb));
            Vector2 XYposition = Polar2XY(estivalue.polar[i].x, estivalue.polar[i].y, width, height);
            EstiPosition[(int)XYposition.x + (int)XYposition.y * width] = new Vector4(intensity, 0, 0, 1);
        }



        ApplyVector4ArrayToTexture(EstiPosition, ResultPositiontexture);
        ResultPositionPlane.GetComponent<Renderer>().material.mainTexture = ResultPositiontexture;

    }

    GameObject CreateCube(Vector3 position, Color color, GameObject cube, Transform parent)
    {
        GameObject cubeInstance = Instantiate(cube, position, Quaternion.identity, parent);
        cubeInstance.GetComponent<Renderer>().material.color = color;

        return cubeInstance;
    }

    GameObject CreateLight(Vector3 position, Color color, GameObject lightPrefab, float intensity, Transform parent)
    {
        GameObject lightInstance = Instantiate(lightPrefab, position, Quaternion.identity, parent);
        Light directionalLight = lightInstance.GetComponent<Light>();
        if (directionalLight != null)
        {
            directionalLight.type = LightType.Directional;  // ���C�g�^�C�v���f�B���N�V���i���ɐݒ�
            directionalLight.color = color;                 // ���C�g�̐F��ݒ�
            directionalLight.intensity = intensity;         // �v�Z�������x���g�p
            directionalLight.transform.LookAt(Vector3.zero); // ���_�Ɍ�����
            directionalLight.shadows = LightShadows.Soft;
        }
        return lightInstance;
    }
    public static Vector3 PolarToCartesian(float phi, float theta, float radius = 1.0f)
    {
        // x = r * sin(��) * cos(��)
        float x = radius * Mathf.Sin(theta) * Mathf.Cos(phi);

        // y = r * cos(��)
        float y = radius * Mathf.Cos(theta);

        // z = r * sin(��) * sin(��)
        float z = radius * Mathf.Sin(theta) * Mathf.Sin(phi);

        return new Vector3(x, y, z);
    }
    EstiVales Estimation()
    {
        width = LDRtex.width; height = LDRtex.height;



        ////--------Inverse Tone Mapping---------
        Vector4[] HDRtex;
        HDRtex = InverseToneMapping(LDRtex);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        ApplyVector4ArrayToTexture(HDRtex, texture);
        HDRPlane.GetComponent<Renderer>().material.mainTexture = texture;
        //--------Thresholding approach--------
        float[] Luminances = new float[width * height];
        float mean = 0, mean2 = 0;
        float lr = 0.3f, lg = 0.59f, lb = 0.11f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;
                float Yp = HDRtex[idx].x * lr + HDRtex[idx].y * lg + HDRtex[idx].z * lb;
                Luminances[idx] = Yp;
                mean += Yp;
                mean2 += Yp * Yp;

            }
        }
        mean /= width * height; mean2 /= width * height;
        float sigma = Mathf.Sqrt(mean2 - (mean * mean));

        float Yt = mean + 2 * sigma;

        ////---------Breath first Search---------
        int[] labels = new int[width * height];
        int LightCount = LabelBreadthFirstSerch(Luminances, labels, width, height, Yt);
        Texture2D Labeltexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        ApplyIntArrayToTexture(labels, Labeltexture);
        LabelPlane.GetComponent<Renderer>().material.mainTexture = Labeltexture;
        ////--------Detected Lights Properties----------
        ////-------Pixel Irradiance,Lights irradiance--------------
        Vector4[] Irradiances = new Vector4[width * height];
        Vector4[] DebugIra = new Vector4[width * height];
        Vector3[] Els = new Vector3[LightCount + 1];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;
                if (x == 0 && y == 0)
                {
                    DebugIra[idx] = new Vector4(1, 0, 0, 1);
                }
                if (labels[idx] != 0)
                {
                    float OmegaP = GetPixelSolidAngle(x, y);
                    Vector3 HDR = new Vector3(HDRtex[idx].x, HDRtex[idx].y, HDRtex[idx].z);
                    Vector3 Ep = OmegaP * (HDR - HDR * Mathf.Min(1, Yt / Luminances[idx]));
                    Irradiances[idx] = new Vector4(Ep.x, Ep.y, Ep.z, labels[idx]);
                    DebugIra[idx] = new Vector4(Ep.x * 10, Ep.y * 10, Ep.z * 10, 1);
                    Els[labels[idx]] += Ep;
                }
            }
        }


        Texture2D Irradiancetexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        ApplyVector4ArrayToTexture(DebugIra, Irradiancetexture);
        IrradiancePlane.GetComponent<Renderer>().material.mainTexture = Irradiancetexture;

        //---------Light's position--------------------

        Vector2[] PolarPosion = new Vector2[LightCount + 1];// Polar 0:None, 1-LightCount: Light
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;


                if (labels[idx] != 0)
                {
                    float YEp = Irradiances[idx].x * lr + Irradiances[idx].y * lg + Irradiances[idx].z * lb;
                    Vector2 PixelPolar = XY2Polar(x, y, width, height);

                    PolarPosion[labels[idx]] += YEp * PixelPolar;

                }
            }
        }

        for (int i = 1; i <= LightCount; i++)
        {
            float YEl = Els[i].x * lr + Els[i].y * lg + Els[i].z * lb;
            PolarPosion[i] /= YEl;
        }

        EstiVales estivlaues = new EstiVales(PolarPosion, Els);

        return estivlaues;
    }







    public float CalculateSolidAngle(float theta1, float theta2, float dPhi)
    {
        return (Mathf.Cos(theta1) - Mathf.Cos(theta2)) * dPhi;
    }

    public (float theta1, float theta2, float dPhi) PixelToSpherical(int x, int y)
    {
        // Convert pixel x, y to phi (longitude) and theta (latitude) in radians
        float dPhi = Mathf.Deg2Rad * (360f / width);
        float thetaMid = Mathf.Deg2Rad * (180f - 180f * (y / (float)height));
        float dTheta = Mathf.Deg2Rad * (180f / height);

        float theta1 = thetaMid - dTheta / 2;
        float theta2 = thetaMid + dTheta / 2;

        return (theta1, theta2, dPhi);
    }
    public float GetPixelSolidAngle(int x, int y)
    {
        // Convert pixel (x, y) to spherical coordinates (theta1, theta2, dPhi)
        var (theta1, theta2, dPhi) = PixelToSpherical(x, y);

        // Calculate and return the solid angle
        return CalculateSolidAngle(theta1, theta2, dPhi);
    }
    void ApplyVector4ArrayToTexture(Vector4[] vectors, Texture2D tex)
    {
        Color[] colors = new Color[vectors.Length];

        // Vector3��Color�ɕϊ� (x, y, z �� RGB �Ƀ}�b�s���O)
        for (int i = 0; i < vectors.Length; i++)
        {
            Vector4 v = vectors[i];
            colors[i] = new Color(v.x, v.y, v.z, 1);
        }

        // �e�N�X�`���ɐF�f�[�^��K�p
        tex.SetPixels(colors);
        tex.Apply();  // Apply() ���Ă΂Ȃ��ƕύX�����f����Ȃ�
    }
    void ApplyIntArrayToTexture(int[] vectors, Texture2D tex)
    {
        Color[] colors = new Color[vectors.Length];

        // Vector3��Color�ɕϊ� (x, y, z �� RGB �Ƀ}�b�s���O)
        for (int i = 0; i < vectors.Length; i++)
        {
            int v = vectors[i];
            colors[i] = new Color(v, v, v, 1);
        }

        // �e�N�X�`���ɐF�f�[�^��K�p
        tex.SetPixels(colors);
        tex.Apply();  // Apply() ���Ă΂Ȃ��ƕύX�����f����Ȃ�
    }

    Vector4[] InverseToneMapping(RenderTexture LDR)
    {
        int width = LDRtex.width, height = LDRtex.height;
        Vector4[] HDRtex = new Vector4[width * height];
        ComputeBuffer HDRtexBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector4>());
        int id = cs.FindKernel("LDR2HDR");
        uint threadSizeX, threadSizeY, threadSizeZ;
        cs.GetKernelThreadGroupSizes(id, out threadSizeX, out threadSizeY, out threadSizeZ);
        int groupcountX = width / (int)threadSizeX;
        int groupcountY = height / (int)threadSizeY;
        int groupcountZ = 1;


        float lr = 0.3f, lg = 0.59f, lb = 0.11f;
        cs.SetInt("width", width); cs.SetInt("height", height);
        cs.SetFloat("lr", lr); cs.SetFloat("lg", lg); cs.SetFloat("lb", lb);
        cs.SetTexture(id, "LDR2HDR_LDR", LDR);
        cs.SetBuffer(id, "HDRtexBuffer", HDRtexBuffer);

        cs.Dispatch(id, groupcountX, groupcountY, groupcountZ);
        HDRtexBuffer.GetData(HDRtex);
        HDRtexBuffer.Release();

        return HDRtex;
    }
    int LabelBreadthFirstSerch(float[] InputTex, int[] labels, int width, int height, float LuminanceThreshold)
    {

        int componentCount = 0;


        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {

                if (labels[x + y * width] == 0 && IsAboveThreshold(InputTex[x + y * width], LuminanceThreshold))
                {
                    componentCount++;

                    int count = BFS(x, y, componentCount, InputTex, labels, width, height, LuminanceThreshold);

                }
            }
        }


        return componentCount;
    }

    bool IsAboveThreshold(float luminance, float LuminanceThreshold)
    {
        return luminance >= LuminanceThreshold;
    }

    int BFS(int startX, int startY, int label, float[] pixels, int[] labels, int width, int height, float LuminanceThreshold)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        labels[startX + startY * width] = label;
        int count = 1;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int x = current.x;
            int y = current.y;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];


                if (nx >= 0 && nx < width && ny >= 0 && ny < height && labels[nx + ny * width] == 0 && IsAboveThreshold(pixels[nx + ny * width], LuminanceThreshold))
                {
                    queue.Enqueue(new Vector2Int(nx, ny));
                    labels[nx + ny * width] = label;
                    count++;
                }
            }
        }
        return count;
    }

    Vector2 XY2Polar(int x, int y, int width, int height)
    {
        Vector2 polar;

        polar = new Vector2((1 - x / (float)width) * 2 * Mathf.PI - Mathf.PI, Mathf.PI - y / (float)height * Mathf.PI);

        return polar;
    }
    Vector2 Polar2XY(float phi, float theta, int width, int height)
    {
        Vector2 xy;
        float x = width * (1 - ((phi + Mathf.PI) / (2 * Mathf.PI)));
        float y = height * (Mathf.PI - theta) / Mathf.PI;
        xy = new Vector2(x, y);

        return xy;
    }

}