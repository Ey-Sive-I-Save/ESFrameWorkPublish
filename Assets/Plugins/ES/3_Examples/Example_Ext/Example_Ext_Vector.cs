using System;
using UnityEngine;

// 示例：演示 ExtForVector.cs 中常用方法
namespace ES
{
    public class Example_Ext_Vector : MonoBehaviour
    {
        void Update()
        {
            Vector3 pos = transform.position;
            Vector3 other = new Vector3(1, 2, 3);
            Vector3 divisor = new Vector3(1f, 2f, 0f);
            Vector2 v2 = new Vector2(5, 6);

            // 1. 三轴分别相乘
            var mul = pos._MutiVector3(other); // 各分量相乘
            Debug.Log($"_MutiVector3: {mul} // 各分量相乘");

            // 2. 三轴分别安全除法
            var safeDiv = pos._SafeDivideVector3(divisor); // 除数为0自动用1
            Debug.Log($"_SafeDivideVector3: {safeDiv} // 各分量安全除法");

            // 3. 三轴直接除法（需保证除数不为0）
            var div = pos._DivideVector3(other);
            Debug.Log($"_DivideVector3: {div} // 各分量直接除法");

            // 4. 将Y分量设为0
            var flat = pos._NoY();
            Debug.Log($"_NoY: {flat} // Y分量归零");

            // 5. 替换分量
            Debug.Log($"_WithY: {pos._WithY(5f)} // 替换Y分量");
            Debug.Log($"_WithX: {pos._WithX(2f)} // 替换X分量");
            Debug.Log($"_WithZ: {pos._WithZ(8f)} // 替换Z分量");

            // 6. 分量乘系数
            Debug.Log($"_WithYMuti: {pos._WithYMuti(2f)} // Y分量乘2");
            Debug.Log($"_WithXMuti: {pos._WithXMuti(3f)} // X分量乘3");
            Debug.Log($"_WithZMuti: {pos._WithZMuti(4f)} // Z分量乘4");

            // 7. 水平距离
            float dist = pos._DistanceToHorizontal(other);
            Debug.Log($"_DistanceToHorizontal: {dist} // XZ平面距离");

            // 8. 近似为零
            Debug.Log($"_IsApproximatelyZero: {pos._IsApproximatelyZero()} // 是否近似零向量");

            // 9. XZ转Vector2
            Debug.Log($"_ToVector2XZ: {pos._ToVector2XZ()} // XZ转Vector2");

            // 10. Vector2 XZ转Vector3
            Debug.Log($"_FromXZ: {v2._FromXZ(7f)} // Vector2转Vector3, Y=7");

            // 11. XZ平面朝向角
            Debug.Log($"_AngleHorizontal: {pos._AngleHorizontal(other)} // XZ平面朝向角");

            // 12. XZ平面带符号夹角
            Debug.Log($"_SignedAngleXZ: {pos._SignedAngleXZ(other)} // XZ平面带符号夹角");

            // 13. 近似相等
            Debug.Log($"_ApproxEquals: {pos._ApproxEquals(other)} // 近似相等");

            // 14. XZ平面垂直向量
            Debug.Log($"_PerpendicularXZ: {pos._PerpendicularXZ()} // XZ平面垂直向量");

            // 15. 仅XZ平面移动
            Debug.Log($"_MoveTowardsXZ: {pos._MoveTowardsXZ(other, 1f)} // 仅XZ平面移动");

            // 可视化
            Debug.DrawLine(pos, flat, Color.green); // Y归零线
        }
    }
}
