using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtensionForColor 
    {
        #region 简单更改颜色特征
        // 51. 设置颜色的alpha值
        public static Color _WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
        #endregion


        #region 转化
        // 52. 将颜色转换为16进制字符串
        public static string _AsHexFormat(this Color color)
        {
            return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
        }

        // 53. 从16进制字符串创建颜色
        public static Color _FromHex(this string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return Color.white;

            return new Color(
                int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f
            );
        }
        #endregion
        // 54. 颜色插值
        public static Color _LerpTo(this Color from, Color to, float t)
        {
            return Color.Lerp(from, to, t);
        }

        // 55. 颜色反转
        public static Color _Invert(this Color color)
        {
            return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a);
        }

        // 56. 检查颜色是否近似相等
        public static bool _Approximately(this Color a, Color b, float threshold = 0.001f)
        {
            return Mathf.Abs(a.r - b.r) < threshold &&
                   Mathf.Abs(a.g - b.g) < threshold &&
                   Mathf.Abs(a.b - b.b) < threshold &&
                   Mathf.Abs(a.a - b.a) < threshold;
        }

        // 57. 获取颜色的灰度值
        public static float _GetGrayscale(this Color color)
        {
            return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
        }

        // 58. 创建随机颜色
        public static Color _RandomColor(this Color color, bool randomAlpha = false)
        {
            return new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                randomAlpha ? UnityEngine.Random.value : 1f
            );
        }

        // 59. 将颜色转换为灰度
        public static Color _AsGrayscale(this Color color)
        {
            float gray = color._GetGrayscale();
            return new Color(gray, gray, gray, color.a);
        }

        // 60. 颜色亮度调整
        public static Color _WithRGBMuti(this Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a
            );
        }
    }
}

