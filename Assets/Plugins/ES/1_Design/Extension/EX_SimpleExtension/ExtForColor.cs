using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtForColor
    {
        #region RGB 通道修改
        /// <summary>
        /// 修改红色通道 (返回新颜色)
        /// </summary>
        public static Color _WithR(this Color color, float r)
        {
            color.r = r;
            return color;
        }
        /// <summary>
        /// 修改绿色通道 (返回新颜色)
        /// </summary>
        public static Color _WithG(this Color color, float g)
        {
            color.g = g;
            return color;
        }
        /// <summary>
        /// 修改蓝色通道 (返回新颜色)
        /// </summary>
        public static Color _WithB(this Color color, float b)
        {
            color.b = b;
            return color;
        }
        /// <summary>
        /// 修改RGB通道 (返回新颜色)
        /// </summary>
        public static Color _WithRGB(this Color color, float r, float g, float b)
        {
            return new Color(r, g, b, color.a);
        }
        /// <summary>
        /// 引用方式修改红色通道
        /// </summary>
        public static Color _WithRRef(ref this Color color, float r)
        {
            color.r = r;
            return color;
        }
        /// <summary>
        /// 引用方式修改绿色通道
        /// </summary>
        public static Color _WithGRef(ref this Color color, float g)
        {
            color.g = g;
            return color;
        }
        /// <summary>
        /// 引用方式修改蓝色通道
        /// </summary>
        public static Color _WithBRef(ref this Color color, float b)
        {
            color.b = b;
            return color;
        }
        #endregion
        #region 透明度操作
        /// <summary>
        /// 引用修改颜色透明度
        /// </summary>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        /// <returns></returns>
        public static Color _WithAlphaRef(ref this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
        /// <summary>
        /// 设置透明度(新颜色)
        /// </summary>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        /// <returns></returns>
        public static Color _WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// 透明度乘 (返回新颜色)
        /// </summary>
        public static Color _MultiplyAlpha(this Color color, float alphaMultiplier)
        {
            color.a *= alphaMultiplier;
            return color;
        }

        /// <summary>
        /// 引用方式透明度乘
        /// </summary>
        public static Color _MultiplyAlphaRef(ref this Color color, float alphaMultiplier)
        {
            color.a *= alphaMultiplier;
            return color;
        }

        /// <summary>
        /// 预乘Alpha (RGB分量乘以Alpha值)
        /// </summary>
        public static Color _RGBMutiAlpha(this Color color)
        {
            return new Color(color.r * color.a, color.g * color.a, color.b * color.a, color.a);
        }

        /// <summary>
        /// 引用方式预乘Alpha
        /// </summary>
        public static Color _RGBMutiAlphaRef(ref this Color color)
        {
            color.r *= color.a;
            color.g *= color.a;
            color.b *= color.a;
            return color;
        }

        #endregion



        #region 转化
       /// <summary>
       /// 将颜色转换为16进制字符串
       /// </summary>
       /// <param name="color"></param>
       /// <returns></returns>
        public static string _AsHexFormat(this Color color)
        {
            return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
        }

        /// <summary>
        /// 从16进制字符串创建颜色
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static Color _ColorFromHex(this string hex)
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



        /// <summary>
        /// 颜色反转
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static Color _Invert(this Color color)
        {
            return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a);
        }

        /// <summary>
        /// 检查颜色(4值均要求)是否近似相等
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static bool _ApproximatelyEach(this Color a, Color b, float threshold = 0.001f)
        {
            return Mathf.Abs(a.r - b.r) < threshold &&
                   Mathf.Abs(a.g - b.g) < threshold &&
                   Mathf.Abs(a.b - b.b) < threshold &&
                   Mathf.Abs(a.a - b.a) < threshold;
        }
        /// <summary>
        /// 检查颜色(总体要求)是否近似相等
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static bool _ApproximatelyTogether(this Color a, Color b, float threshold = 0.004f)
        {
            return Mathf.Abs(a.r - b.r)+ Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)+ Mathf.Abs(a.a - b.a) < threshold ;
        }
        /// <summary>
        /// 获取颜色的灰度值
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static float _GetGrayscale(this Color color)
        {
            return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
        }

        /// <summary>
        /// 创建随机颜色(新颜色)
        /// </summary>
        /// <param name="color"></param>
        /// <param name="randomAlpha"></param>
        /// <returns></returns>
        public static Color _RandomColor(this Color color, bool randomAlpha = false)
        {
            return color= new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                randomAlpha ? UnityEngine.Random.value : 1f
            );
        }

        /// <summary>
        /// 创建随机颜色Ref
        /// </summary>
        /// <param name="color"></param>
        /// <param name="randomAlpha"></param>
        /// <returns></returns>
        public static Color _RandomColorRef(ref this Color color, bool randomAlpha = false)
        {
            return color = new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                randomAlpha ? UnityEngine.Random.value : 1f
            );
        }


        /// <summary>
        /// 将颜色转换为灰度
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static Color _AsGrayscale(this Color color)
        {
            float gray = color._GetGrayscale();
            return new Color(gray, gray, gray, color.a);
        }
        /// <summary>
        /// 颜色亮度调整 RGB* （新颜色）
        /// </summary>
        /// <param name="color"></param>
        /// <param name="factor">RGB乘</param>
        /// <returns></returns>
        public static Color _WithRGBMuti( this Color color, float factor)
        {
            color.r *= factor;
            color.g *= factor;
            color.b *= factor;
            return color;
        }
        /// <summary>
        /// 颜色亮度调整 RGB* Ref
        /// </summary>
        /// <param name="color"></param>
        /// <param name="factor">RGB乘</param>
        /// <returns></returns>
        public static Color _WithRGBMutiRef(ref this Color color, float factor)
        {
            color.r *= factor;
            color.g *= factor;
            color.b *= factor;
            return color;
        }
    }
}

