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
            int ia = 7;
            
            float f1 = -2.5f;
            float f2 = 1.23456f;

            float f4 = 180f;
            float f5 = 3.14159f;

            int i3 = 21;
            int i4 = 2024;
            float velocity = 0f;

            // 安全除法
            Debug.Log($"Safe divide: {a} / {b} = {a._SafeDivide(b)}");

            // 限制范围
            Debug.Log($"Clamp float: {a._Clamp(0f, 5f)}");
            Debug.Log($"Clamp int: {ia._Clamp(0, 5)}");
            Debug.Log($"Clamp01: {f2._Clamp01()}");

            // 角度归一化
            Debug.Log($"Normalized angle [0,360): {450f._AsNormalizeAngle()}");
            Debug.Log($"Normalized angle [-180,180): {270f._AsNormalizeAngle180()}");

            // 区间映射
            Debug.Log($"Remap 5 from [0,10] to [0,1]: {5f._Remap(0f, 10f, 0f, 1f)}");

            // 线性插值
            Debug.Log($"LerpTo 0->10, t=0.3: {0f._LerpTo(10f, 0.3f)}");

            // 平滑阻尼插值
            float smooth = 0f._SmoothDamp(10f, ref velocity, 0.5f, 20f, 0.02f);
            Debug.Log($"SmoothDamp: {smooth}, velocity: {velocity}");

            // 反向插值
            Debug.Log($"InverseLerp 5 in [0,10]: {5f._InverseLerp(0f, 10f)}");

            // 四舍五入
            Debug.Log($"RoundInt 1.7: {1.7f._RoundInt()}");

            // 角度/弧度转换
            Debug.Log($"ToRadians 180: {f4._ToRadians()}");
            Debug.Log($"ToDegrees pi: {f5._ToDegrees()}");

            // 循环映射
            Debug.Log($"Cycle float 13 in [0,5): {13f._Cycle(0f, 5f)}");
            Debug.Log($"Cycle int 13 in [0,5): {13._Cycle(0, 5)}");

            // 判断区间
            Debug.Log($"IsInRange float 0.5 in [0,1]: {0.5f._IsInRange(new Vector2(0f, 1f))}");
            Debug.Log($"IsInRange int 3 in [1,5]: {3._IsInRange(new Vector2Int(1, 5))}");

            // 判断接近0/近似
            Debug.Log($"IsApproximatelyZero 0.0005: {0.0005f._IsApproximatelyZero()}");
            Debug.Log($"IsApproximately 1.0001~1.0002: {1.0001f._IsApproximately(1.0002f)}");

            // 偶数/奇数/整除
            Debug.Log($"IsEven 4: {4._IsEven()}");
            Debug.Log($"IsOdd 5: {5._IsOdd()}");
            Debug.Log($"IsDivisibleBy 15 by 5: {15._IsDivisibleBy(5)}");

            // 正负号
            Debug.Log($"IsPositive float 1.2: {1.2f._IsPositive()}");
            Debug.Log($"IsPositive int -3: {(-3)._IsPositive()}");
            Debug.Log($"IsNegative float -1.2: {(-1.2f)._IsNegative()}");
            Debug.Log($"IsNegative int -3: {(-3)._IsNegative()}");
            Debug.Log($"Sign float -2.5: {f1._Sign()}");
            Debug.Log($"Sign int 7: {ia._Sign()}");

            // 显示格式
            Debug.Log($"ToString_FormatToDecimalPlaces 1.23456, 2: {f2._ToString_FormatToDecimalPlaces(2)}");
            Debug.Log($"ToString_Percentage 0.876, 1: {0.876f._ToString_Percentage(1)}");
            Debug.Log($"ToString_DateOrdinal 21: {i3._ToString_DateOrdinal()}");
            Debug.Log($"ToFormattedString_1000 12345.67: {12345.67f._ToFormattedString_1000()}");
            Debug.Log($"ToString_Roman 2024: {i4._ToString_Roman()}");
            Debug.Log($"ToString_MoneyFormat 123456: {123456._ToString_MoneyFormat()}");

            // 特殊：生成区间整数序列
            var rangeInts = 1._GetIEnumerable_TargetRangeInts(5);
            Debug.Log("RangeInts 1-5: " + string.Join(",", rangeInts));
        }
    }
}
