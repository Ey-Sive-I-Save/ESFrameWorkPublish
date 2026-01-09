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

            // 将 Y 置为 0
            Vector3 flat = pos._NoY();

            // 修改分量
            Vector3 changed = pos._WithY(5f)._WithX(2f);

            // 安全按分量相除
            Vector3 divisor = new Vector3(1f, 1f, 1f);
            Vector3 safeDiv = pos._SafeDivideVector3(divisor);

            // 计算水平距离
            float dist = pos._DistanceToHorizontal(Vector3.zero);

            // 近似比较
            bool nearZero = pos._IsApproximatelyZero(0.001f);

            // 用于可视化
            Debug.DrawLine(pos, flat, Color.green);
        }
    }
}
