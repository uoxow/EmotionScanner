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
        ResetAllAnimations();

        // --- 【変更点】ここからクロップ処理 ---

        // 1. カメラの映像から、真ん中の正方形を切り抜いた「新しい画像」を作ります
        RenderTexture squareTexture = GetCenterSquare(webCamTexture);

        // 2. その正方形の画像を使ってTensorを作ります
        // (注: NHWC設定はそのまま使います！)
        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, ImageSize, ImageSize, 3));
        TextureConverter.ToTensor(squareTexture, inputTensor, new TextureTransform()
            .SetDimensions(width: ImageSize, height: ImageSize, channels: 3)
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
        RenderTexture.ReleaseTemporary(squareTexture);
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