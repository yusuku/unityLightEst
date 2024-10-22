using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
public class Runtimedebugtes : MonoBehaviour
{
    //-----Debug------------
    public GameObject LuminanceDebug;
    public GameObject HDRDebug;
    public GameObject BFDebug;
    public GameObject ECPlane;
    public GameObject IrradiancePlane;
    public GameObject LabelPlane;
    public GameObject WeightedPlane;
    //------Lights----------
    public Transform parent;
    public GameObject Light;

    public GameObject[] LIghts;

    //------Shader---------
    public ComputeShader cs; //compute shader esti4.compute
    public ComputeShader cs2;
    uint threadSizeX, threadSizeY, threadSizeZ;
    int groupcountX, groupcountY, groupcountZ;
    int id;
    int width, height;
    int groupCount;


    public RenderTexture LDRtex;
    float LuminanceThreshold;

    public class EstiVales
    {
        public Vector3[] positions;
        public float[] LuminanceValue;
        public Vector4[] IrradianceValue;
        public int LightCount;
        public EstiVales(Vector3[] positions, float[] LuminanceValue, Vector4[] IrradianceValue, int LightCount)
        {
            this.positions = positions;
            this.LuminanceValue = LuminanceValue;
            this.IrradianceValue = IrradianceValue;
            this.LightCount = LightCount;

        }
    }
 

    
    // Start is called before the first frame update
    void Start()
    {
        EstiVales estivalues = Estimate3DPosition();
        LIghts = new GameObject[estivalues.LightCount];
        for (int i = 0; i < estivalues.LightCount; i++)
        {
            Vector3 position = estivalues.positions[i];
            float intensity = estivalues.LuminanceValue[i] / estivalues.IrradianceValue[i].w*10;
            Color color = estivalues.IrradianceValue[i] / estivalues.IrradianceValue[i].w;
            color = new Color(color.r * 10, color.g * 10, color.b * 10, 1);
            LIghts[i]= CreateLight(position, color, Light, intensity, parent);
            Debug.Log("label: " + i + " position :" + position + " intensity : " + intensity + " color: " + color);
        }
    }
    private void Update()
    {
        for (int i = 0; i < LIghts.Length; i++)
        {
            if (LIghts[i] != null)
            {
                Destroy(LIghts[i]);
            }
        }

        EstiVales estivalues = Estimate3DPosition();
        LIghts = new GameObject[estivalues.LightCount];
        for (int i = 0; i < estivalues.LightCount; i++)
        {
            Vector3 position = estivalues.positions[i];
            float intensity = estivalues.LuminanceValue[i] / estivalues.IrradianceValue[i].w * 10;
            Color color = estivalues.IrradianceValue[i] / estivalues.IrradianceValue[i].w * 10;
            color = new Color(color.r * 10, color.g * 10, color.b * 10, 1);
            LIghts[i] = CreateLight(position, color, Light, intensity, parent);
            Debug.Log("label: " + i + " position :" + position + " intensity : " + intensity + " color: " + color);
        }
    }


    EstiVales Estimate3DPosition()
    {
        int[] labels;
        width = LDRtex.width; height = LDRtex.height;
        labels = new int[width * height];

        //------LDR2Luminunce---------
        float[] Luminances;
        ComputeBuffer LuminancesBuffer;

        id = cs.FindKernel("LDR2Luminunce");
        cs.GetKernelThreadGroupSizes(id, out threadSizeX, out threadSizeY, out threadSizeZ);
        groupcountX = width / (int)threadSizeX;
        groupcountY = height / (int)threadSizeY;
        groupcountZ = 1;
        groupCount = groupcountX * groupcountY;

        RenderTexture DebugLuminance = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        DebugLuminance.enableRandomWrite = true;
        DebugLuminance.Create();
        Luminances = new float[width * height];
        LuminancesBuffer = new ComputeBuffer(Luminances.Length, Marshal.SizeOf<float>());
        cs.SetTexture(id, "LDR2L_InputTex", LDRtex);
        cs.SetTexture(id, "DebugLuminance", DebugLuminance);
        cs.SetBuffer(id, "LDR2L_ResultLuminunce", LuminancesBuffer);
        cs.SetInt("width", width); cs.SetInt("height", height);
        cs.SetFloat("lr", 0.3f); cs.SetFloat("lg", 0.59f); cs.SetFloat("lb", 0.11f);
        cs.Dispatch(id, groupcountX, groupcountY, groupcountZ);
        LuminanceDebug.GetComponent<Renderer>().material.mainTexture = DebugLuminance;


        LuminancesBuffer.GetData(Luminances);
        //-------LDR2HDR--------------------

        ComputeBuffer HDRtexBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector4>());

        RenderTexture HDRtex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        HDRtex.enableRandomWrite = true;
        HDRtex.Create();
        id = cs.FindKernel("LDR2HDR");
        cs.SetBuffer(id, "LDR2HDR_Luminances", LuminancesBuffer);
        cs.SetTexture(id, "LDR2HDR_LDR", LDRtex);
        cs.SetTexture(id, "LDR2HDR_HDR", HDRtex);
        cs.SetBuffer(id, "HDRtexBuffer", HDRtexBuffer);
        cs.Dispatch(id, groupcountX, groupcountY, groupcountZ);
        HDRDebug.GetComponent<Renderer>().material.mainTexture = HDRtex;

        //------Luminunce Mean------------------

        float LuminanceMean = CalMean(Luminances);


        //------Luminunce Squared Mean------------------
        float[] SquaredLuminances = SquaredArray(Luminances);
        float SquaredLuminanceMean = CalMean(SquaredLuminances);

        //-------Luminance Valiance---------------------
        float LuminanceVariance = SquaredLuminanceMean - Mathf.Pow(LuminanceMean, 2);

        //--------LuminanceThresholding
        LuminanceThreshold = LuminanceMean + 2 * LuminanceVariance;

        //--------Breadth-first search onCPU 
        int LightsCount = LabelBreadthFirstSerch(Luminances, labels);

        RenderTexture DebugDBF = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        DebugDBF.enableRandomWrite = true;
        DebugDBF.Create();
        ComputeBuffer DBf_Labels = new ComputeBuffer(labels.Length, Marshal.SizeOf<int>());
        DBf_Labels.SetData(labels);
        id = cs.FindKernel("DebugBreathFirst");
        cs.SetInt("LightsCount", LightsCount);
        cs.SetBuffer(id, "DBf_Labels", DBf_Labels);
        cs.SetTexture(id, "DebugDBF", DebugDBF);
        cs.Dispatch(id, groupcountX, groupcountY, groupcountZ);
        BFDebug.GetComponent<Renderer>().material.mainTexture = DebugDBF;


        //--------EnergyConservation------------------
        RenderTexture EC_HDRTex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        EC_HDRTex.enableRandomWrite = true;
        EC_HDRTex.Create();
        ComputeBuffer EC_HDRTexBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector4>());


        id = cs.FindKernel("EnergyConservation");

        cs.SetBuffer(id, "EC_HDRtexBuffer", HDRtexBuffer);
        cs.SetTexture(id, "EC_HDROutput", EC_HDRTex);
        cs.SetBuffer(id, "EC_HDRTexBuffer", EC_HDRTexBuffer);
        cs.SetFloat("LuminanceThreshold", LuminanceThreshold);
        cs.Dispatch(id, groupcountX, groupcountY, groupcountZ);

        ECPlane.GetComponent<Renderer>().material.mainTexture = EC_HDRTex;


        //--------Irradiance------------------------- 100*cos-100*cos
        RenderTexture IR_Irradiance = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        IR_Irradiance.enableRandomWrite = true;
        IR_Irradiance.Create();

        ComputeBuffer IR_IrradianceBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector4>());
        Vector4[] IR_IrradianceValues = new Vector4[width * height];
        id = cs2.FindKernel("Irradiance");
        cs2.SetInt("width", width); cs2.SetInt("height", height);
        cs2.SetFloat("IR_LuminanceThresholding", LuminanceThreshold);
        cs2.SetBuffer(id, "IR_HDRtexBuffer", HDRtexBuffer);
        cs2.SetTexture(id, "IR_ResultIrradiance", IR_Irradiance);
        cs2.SetBuffer(id, "IR_LuminunceInput", LuminancesBuffer);
        cs2.SetBuffer(id, "IR_IrradianceBuffer", IR_IrradianceBuffer);

        cs2.Dispatch(id, groupcountX, groupcountY, groupcountZ);
        IrradiancePlane.GetComponent<Renderer>().material.mainTexture = IR_Irradiance;


        //---------Polar Cooridanetes-----
        Vector2[] PolarCoordinates;
        PolarCoordinates = new Vector2[LightsCount];
        Vector4[] IrradianceValue = new Vector4[LightsCount];
        float[] LuminanceValue = new float[LightsCount];
        for (int i = 1; i <= LightsCount; i++)
        {
            //-----------LabelsMask--------------
            id = cs2.FindKernel("LabelsMask");
            ComputeBuffer _labels = new ComputeBuffer(width * height, Marshal.SizeOf<int>());
            _labels.SetData(labels);
            ComputeBuffer LM_LightsMaskBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector4>());

            cs2.SetBuffer(id, "labels", _labels);
            cs2.SetBuffer(id, "LM_IrradianceInput", IR_IrradianceBuffer);
            RenderTexture ResultLightsMask = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            ResultLightsMask.enableRandomWrite = true;
            ResultLightsMask.Create();
            cs2.SetTexture(id, "LM_ResultLightsMask", ResultLightsMask);
            cs2.SetBuffer(id, "LM_ResultLightsMaskBuffer", LM_LightsMaskBuffer);
            cs2.SetInt("label", i);//----------------Label------------------
            cs2.Dispatch(id, groupcountX, groupcountY, groupcountZ);

            LabelPlane.GetComponent<Renderer>().material.mainTexture = ResultLightsMask;

            //-----------LightsWeightCoordinates--------------
            RenderTexture ResultWeighted = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            ResultWeighted.enableRandomWrite = true;
            ResultWeighted.Create();
            ComputeBuffer WeightedPolarBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector2>());

            id = cs2.FindKernel("LightsWeightCoordinates");
            cs2.SetBuffer(id, "LWC_LabelIrradianceInput", LM_LightsMaskBuffer);
            cs2.SetTexture(id, "ResultWeighted", ResultWeighted);
            cs2.SetBuffer(id, "WeightedPolarBuffer", WeightedPolarBuffer);
            cs2.Dispatch(id, groupcountX, groupcountY, groupcountZ);

            WeightedPlane.GetComponent<Renderer>().material.mainTexture = ResultWeighted;
            //-----------Lights EL, YEL------------------------ 
            Vector4[] IrradianceLightMaskValue = new Vector4[width * height];
            LM_LightsMaskBuffer.GetData(IrradianceLightMaskValue);
            Vector4 EL = Cal4Sum(IrradianceLightMaskValue);
            
            Vector4 LuminanceWeight = new Vector4(0.3f, 0.59f, 0.11f, 0f);
            float YEl = Vector4.Dot(EL, LuminanceWeight);
            
            //-----------Lights Polar Cooridanate-----
            Vector2[] WeightedPolarValues = new Vector2[width * height];
            WeightedPolarBuffer.GetData(WeightedPolarValues);
            Vector2 LightPolarCoordinates = Cal2Sum(WeightedPolarValues);
           
            Debug.Log("label: " + i + " Polar :" + LightPolarCoordinates / YEl / Mathf.PI * 180f+" EL : "+EL+" YEL: "+YEl);

            //0------------Result-----------
            PolarCoordinates[i - 1] = LightPolarCoordinates / YEl;
            IrradianceValue[i - 1] = EL;
            LuminanceValue[i - 1] = YEl;
        }
        Vector3[] position = new Vector3[LightsCount];
        for (int i = 0; i < LightsCount; i++)
        {
            position[i] = Polar2Position(PolarCoordinates[i].x, PolarCoordinates[i].y, 1);
        }


        EstiVales estivalues = new EstiVales(position, LuminanceValue, IrradianceValue, LightsCount);

        return estivalues;
    }

    Vector3 Polar2Position(float phi,float theta,float radius)
    {
        Vector3 Position;
        float px = Mathf.Sin(theta) * Mathf.Cos(phi);
        float pz = Mathf.Sin(theta) * Mathf.Sin(phi);
        float py = Mathf.Cos(theta);
        Position = new Vector3(px, py, pz);
        Position *= radius;
        return Position;
    }

    GameObject CreateLight(Vector3 position, Color color, GameObject lightPrefab, float intensity , Transform parent)
    {
        GameObject lightInstance = Instantiate(lightPrefab, position, Quaternion.identity, parent);
        Light directionalLight = lightInstance.GetComponent<Light>();
        if (directionalLight != null)
        {
            directionalLight.type = LightType.Directional;  // ライトタイプをディレクショナルに設定
            directionalLight.color = color;                 // ライトの色を設定
            directionalLight.intensity = intensity;         // 計算した強度を使用
            directionalLight.transform.LookAt(Vector3.zero); // 原点に向ける
            directionalLight.shadows = LightShadows.Soft;
        }
        return lightInstance;
    }


    int LabelBreadthFirstSerch(float[] InputTex, int[] labels)
    {
        // labels配列の初期化
        //labels = new int[width*height];
        int componentCount = 0;

        // 幅優先探索による連結成分の番号付け
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // ラベルが0で、かつしきい値を超える場合に幅優先探索を開始
                if (labels[x+y*width] == 0 && IsAboveThreshold(InputTex[x + y * width]))
                {
                    componentCount++;
                    // labels配列もBFSに渡す
                    int count=BFS(x, y, componentCount, InputTex, labels);
                    Debug.Log("componentCount:  "+ componentCount+"  : "+count);
                }
            }
        }

      
        return componentCount;
    }

    bool IsAboveThreshold(float luminance)
    {
        return luminance >= LuminanceThreshold;
    }

    // labelsを引数に追加
    int  BFS(int startX, int startY, int label, float[] pixels, int[] labels)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        labels[startX+startY*width] = label;
        int count = 1;
        // 4方向に隣接するピクセルを探索
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

                // 境界チェックとしきい値のチェック
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && labels[nx+ny*width] == 0 && IsAboveThreshold(pixels[nx + ny * width]))
                {
                    queue.Enqueue(new Vector2Int(nx, ny));
                    labels[nx + ny * width] = label;
                    count++;
                }
            }
        }
        return count;
    }


    float[] SquaredArray(float[] array)
    {
        float[] Squared = new float[array.Length];
        for(int i = 0; i < array.Length; i++)
        {
            Squared[i] = array[i] * array[i];
        }
        return Squared;
    }

    float CalMean(float[] array2D)
    {
        float sum = 0;
        foreach (var lum in array2D)
        {
            sum += lum;
        }
        return sum / array2D.Length;

    }
    Vector4 Cal4Sum(Vector4[] array2D)
    {
        Vector4 sum =Vector4.zero;
        foreach (var lum in array2D)
        {
            sum += lum;
        }
        return sum ;

    }
    Vector2 Cal2Sum(Vector2[] array2D)
    {
        Vector2 sum = Vector2.zero;
        foreach (var lum in array2D)
        {
            sum += lum;
        }
        return sum;

    }
}
