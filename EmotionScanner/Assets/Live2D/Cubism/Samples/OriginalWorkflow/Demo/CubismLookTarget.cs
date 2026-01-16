using UnityEngine;
using Live2D.Cubism.Framework.LookAt;

public class CubismLookTarget : MonoBehaviour, ICubismLookTarget
{
    public Vector3 GetPosition()
    {
        //位置を取得
        var targetPosition = Input.mousePosition;

        targetPosition.z = 10.0f;

        //ワールド座標変換
        var worldPos = Camera.main.ScreenToWorldPoint(targetPosition);

        return worldPos;
    }

    public bool IsActive()
    {
        return true;
    }
}