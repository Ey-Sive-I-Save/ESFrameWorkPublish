using System;
using UnityEngine;

namespace ES
{
    // 示例：演示 ExtForNum.cs 中常用数值扩展
    public class Example_Ext_Num : MonoBehaviour
    {
        void Start()
        {
            float a = 10f;
            float b = 0f;

            // 安全除法
            float safe = a._SafeDivide(b);
            Debug.Log($"Safe divide: {safe}");

            // 限制范围
            float clamped = a._Clamp(0f, 5f);
            Debug.Log($"Clamped: {clamped}");

            // 角度归一化
            float ang = 450f._AsNormalizeAngle();
            Debug.Log($"Normalized angle: {ang}");

            // 映射
            float mapped = 5f._Remap(0f, 10f, 0f, 1f);
            Debug.Log($"Mapped: {mapped}");
        }
    }
}
