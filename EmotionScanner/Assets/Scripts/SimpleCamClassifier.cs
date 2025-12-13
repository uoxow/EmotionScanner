using UnityEngine;
using UnityEngine.UI;
using Unity.Sentis;
using TMPro;

public class SimpleCamClassifier : MonoBehaviour
{
    [Header("必要なファイル")]
    public ModelAsset modelAsset;
    public TextAsset labelsAsset;

    [Header("画面のパーツ")]
    public RawImage previewUI;
    public TextMeshProUGUI resultText;

    [Header("アニメーションさせるモデル")]
    public Animator anim; // ★変更1：publicにして、Inspectorからモデルをセットできるようにした！

    private Worker worker;
    private WebCamTexture webCamTexture;
    private string[] labels;
    
    private const string InputName = "sequential_45_input";
    private const string OutputName = "sequential_47";
    private const int ImageSize = 224;

    // ★変更3：高速切り替え防止用。前回のアニメーション番号を覚えておく
    private int lastIndex = -1; 

    // 全てのアニメーションフラグをオフにする便利関数
    void ResetAllAnimations()
    {
        if (anim == null) return;
        Debug.Log("表情リセット");
        anim.SetBool("sorrow", false);
        anim.SetBool("astonish", false);
        anim.SetBool("Runrun", false);
        anim.SetBool("happiness", false);
    }
 // 正方形に切り抜きつつ、左右反転（鏡）にする関数
    void CropToSquare(Texture source, RenderTexture destination)
    {
        float scaleHeight = 1.0f;
        float scaleWidth = 1.0f;

        if (source.width > source.height)
        {
            scaleWidth = (float)source.height / source.width;
        }
        else
        {
            scaleHeight = (float)source.width / source.height;
        }

        float offsetX = (1.0f - scaleWidth) / 2.0f;
        float offsetY = (1.0f - scaleHeight) / 2.0f;

        // --- ここが変更点（鏡にする魔法） ---
        // scaleWidth にマイナスをつけると、画像がクルッと反転します
        // その分、表示位置がずれるので offset の計算も少し変えます
        Vector2 scale = new Vector2(-scaleWidth, scaleHeight);
        Vector2 offset = new Vector2(offsetX + scaleWidth, offsetY); // 反転した分ずらす

        Graphics.Blit(source, destination, scale, offset);
    }

    int flg =0;
    void Start()
    {
        if (labelsAsset != null)
        {
            labels = labelsAsset.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        Model model = ModelLoader.Load(modelAsset);
        // ★CPUのままでOKです（安定動作のため）
        worker = new Worker(model, BackendType.CPU);
        
        // ★削除：ここでGetComponentすると、GameManager自身のAnimatorを探してしまうので削除しました。
        // anim = GetComponent<Animator>(); 

        webCamTexture = new WebCamTexture();
        previewUI.texture = webCamTexture;
        webCamTexture.Play();
        Debug.Log("プログラムを開始");
    }

    void Update()
    {
        if (webCamTexture == null || !webCamTexture.didUpdateThisFrame) return;
        //ResetAllAnimations();

        // 1. 切り抜き用のテクスチャ（RenderTexture）を準備
        // ※ Start()で作っておくのが理想ですが、まずはここで説明します
        RenderTexture cropRT = RenderTexture.GetTemporary(ImageSize, ImageSize, 0);

        // 2. さっき作った関数で、カメラ映像(webCamTexture)の真ん中を cropRT にコピー！
        // これで「つぶれていない正方形の画像」が cropRT に入ります。
        CropToSquare(webCamTexture, cropRT);

        // 3. 画面のプレビューにも、切り抜いた正方形を表示（確認用）
        previewUI.texture = cropRT;

        // 4. Tensorの作成
        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, ImageSize, ImageSize, 3));
    
        // 5. 切り抜いた画像(cropRT)をSentisに渡す
        TextureConverter.ToTensor(cropRT, inputTensor, new TextureTransform()
            .SetDimensions(width: ImageSize, height: ImageSize, channels: 3) // もう正方形なので変形しない
            .SetTensorLayout(TensorLayout.NHWC));
        // 3. 推論実行
        worker.Schedule(inputTensor);
        
        Debug.Log("推論を実行");

        // --- (ここから下は今までと同じ) ---

        using Tensor<float> outputTensor = worker.PeekOutput(OutputName) as Tensor<float>;
        float[] probabilities = outputTensor.DownloadToArray();

        int maxIndex = 0;
        float maxVal = 0;
        for (int i = 0; i < probabilities.Length; i++)
        {
            if (probabilities[i] > maxVal)
            {
                maxVal = probabilities[i];
                maxIndex = i;
            }
        }
        // アニメーション制御
        if (maxIndex != lastIndex)
        {
            ResetAllAnimations();
            if (maxIndex == 1) {
                anim.SetBool("sorrow", true);
                Debug.Log("悲しみを検知");
            }
            else if (maxIndex == 2) {
                anim.SetBool("astonish", true);
                Debug.Log("驚きを検知");
            }
            else if (maxIndex == 3) {
                if(flg==0){
                    anim.SetBool("happiness", true);
                    Debug.Log("幸せを検知");
                    flg = 1;
                }else{
                    anim.SetBool("Runrun", true);
                    Debug.Log("楽しいを検知");
                    flg = 0;
                }
            }
            lastIndex = maxIndex;
        }

        // 結果表示
        if (labels != null && maxIndex < labels.Length)
        {
            resultText.text = $"{labels[maxIndex]}\n({maxVal * 100:F1}%)";
            Debug.Log("推論の結果はディスプレイ");
        }
        else
        {
            resultText.text = $"Index: {maxIndex}\n({maxVal * 100:F1}%)";
            Debug.Log("結果はディスプレイ2");
        }
        
        // 【重要】作ったRenderTextureはお掃除（解放）しないとメモリがあふれます
        RenderTexture.ReleaseTemporary(cropRT);
    }

    // --- 【新機能】カメラの真ん中を切り抜く魔法の関数 ---
    RenderTexture GetCenterSquare(WebCamTexture original)
    {
        // 1. 縦と横、どっちが短いか調べる
        int size = Mathf.Min(original.width, original.height);
        
        // 2. 切り抜き用の正方形テクスチャを作る
        RenderTexture rt = RenderTexture.GetTemporary(ImageSize, ImageSize);
        
        // 3. 画像を加工してコピーする準備
        // (ここでアスペクト比を調整して、真ん中だけ映るようにします)
        float scaleHeight = (float)size / original.height;
        float scaleWidth = (float)size / original.width;
        
        // 4. Graphics.Blitを使って、縮小・切り抜きを同時に行う
        // scale: 拡大縮小率, offset: ずらす量
        Vector2 scale = new Vector2(scaleWidth, scaleHeight);
        Vector2 offset = new Vector2((1 - scaleWidth) / 2f, (1 - scaleHeight) / 2f);
        
        Graphics.Blit(original, rt, new Vector2(scale.x, scale.y), new Vector2(offset.x, offset.y));
        
        return rt;
    }

    void OnDestroy()
    {
        worker?.Dispose();
        if (webCamTexture != null) webCamTexture.Stop();
    }
}