using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class CPURunEstimation : MonoBehaviour
{
    public ComputeShader cs;
    public Texture2D LDRtex;
    // Start is called before the first frame update
    void Start()
    {
        int width = LDRtex.width,height = LDRtex.height;
        //--------Inverse Tone Mapping---------
        Vector3[] HDRtex ;
        HDRtex=InverseToneMapping(LDRtex);


        //--------Thresholding approach--------
        float[] Luminances = new float[width * height];
        float mean = 0,mean2=0;
        float lr = 0.3f, lg = 0.59f, lb = 0.11f;
        for (int x = 0; x < width; x++)
        {
            for(int y = 0; y < height; y++)
            {
                int idx = x + y * width;
                float Yp= HDRtex[idx].x * lr + HDRtex[idx].y * lg + HDRtex[idx].z * lb;
                Luminances[idx] = Yp;
                mean += Yp;
                mean2 += Yp * Yp;

            }
        }
        mean /= width * height;mean2 /= width * height;
        float sigma = Mathf.Sqrt(mean2 - (mean * mean));

        float Yt = mean + 2 * sigma;

        //---------Breath first Search---------
        int[] labels = new int[width * height];
        int LightCount=LabelBreadthFirstSerch(Luminances, labels, width, height, Yt);

     

        //--------Detected Lights Properties----------
        //-------Pixel Irradiance,Lights irradiance--------------
        Vector4[] Irradiances = new Vector4[width * height];
        Vector3[] Els = new Vector3[LightCount + 1];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;
                if (labels[idx] != 0)
                {
                   
                    float the = y / height * Mathf.PI, the1 = (y + 1) / height * Mathf.PI;
                    float OmegaP = 2 * Mathf.PI / width * (Mathf.Cos(the1) - Mathf.Cos(the));
                    Vector3 Ep = OmegaP * (HDRtex[idx] - HDRtex[idx] * Mathf.Min(1, Yt / Luminances[idx]));
                    Irradiances[idx] = new Vector4(Ep.x, Ep.y, Ep.z, labels[idx]);
                    Els[labels[idx]] += Ep;
                }
            }
        }

        //---------Light's position--------------------
        Vector2[] PolarPosion = new Vector2[LightCount+1];
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
        
        for(int i = 1; i <=LightCount; i++)
        {
            float YEl=Els[i].x * lr + Els[i].y * lg + Els[i].z * lb;
            PolarPosion[i] /= YEl;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    Vector3[] InverseToneMapping(Texture2D LDR)
    {
        int width = LDRtex.width, height = LDRtex.height;
        Vector3[] HDRtex = new Vector3[width * height];
        ComputeBuffer HDRtexBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector3>());
        int id = cs.FindKernel("LDR2HDR");
        uint threadSizeX, threadSizeY, threadSizeZ;
        cs.GetKernelThreadGroupSizes(id, out threadSizeX, out threadSizeY, out threadSizeZ);
        int groupcountX = width / (int)threadSizeX;
        int groupcountY = height / (int)threadSizeY;
        int groupcountZ = 1;
    

        float lr= 0.3f, lg= 0.59f, lb= 0.11f;
        cs.SetInt("width", width); cs.SetInt("height", height);
        cs.SetFloat("lr", lr); cs.SetFloat("lg", lg); cs.SetFloat("lb", lb); 
        cs.SetTexture(id, "LDR2HDR_LDR", LDR);
        cs.SetBuffer(id, "HDRtexBuffer", HDRtexBuffer);

        cs.Dispatch(id, groupcountX, groupcountY, groupcountZ);

        return HDRtex;
    }
    int LabelBreadthFirstSerch(float[] InputTex, int[] labels,int width,int height,float LuminanceThreshold)
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
                if (labels[x + y * width] == 0 && IsAboveThreshold(InputTex[x + y * width],LuminanceThreshold))
                {
                    componentCount++;
                    // labels配列もBFSに渡す
                    int count = BFS(x, y, componentCount, InputTex, labels,width,height, LuminanceThreshold);
                    Debug.Log("componentCount:  " + componentCount + "  : " + count);
                }
            }
        }


        return componentCount;
    }

    bool IsAboveThreshold(float luminance,float LuminanceThreshold)
    {
        return luminance >= LuminanceThreshold;
    }

    // labelsを引数に追加
    int BFS(int startX, int startY, int label, float[] pixels, int[] labels,int width,int height,float LuminanceThreshold)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        labels[startX + startY * width] = label;
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
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && labels[nx + ny * width] == 0 && IsAboveThreshold(pixels[nx + ny * width],LuminanceThreshold))
                {
                    queue.Enqueue(new Vector2Int(nx, ny));
                    labels[nx + ny * width] = label;
                    count++;
                }
            }
        }
        return count;
    }

    Vector2 XY2Polar(int x, int y,int width,int height)
    {
        Vector2 polar;

        polar = new Vector2((1 - x / width) * 2 * Mathf.PI - Mathf.PI, y / height * Mathf.PI);

        return polar;
    }
    

}
