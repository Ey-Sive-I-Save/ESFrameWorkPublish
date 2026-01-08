using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{

    /// <summary>
    /// 一组针对 <see cref="UnityEngine.Color"/> 的实用扩展方法。
    /// 提供返回新 Color 的不可变风格方法，以及若干就地（in-place）修改方法。
    /// 注意：就地方法使用 ref/ ref return，调用方需显式使用 ref 语义。
    /// </summary>
    public static class ExtForColor
    {
        #region RGB 通道修改
        /// <summary>
        /// 修改红色通道并返回新的 <see cref="Color"/> 实例。
        /// </summary>
        /// <param name="color">原始颜色（值传递）。</param>
        /// <param name="r">新的红色分量，范围通常为 0 到 1。</param>
        /// <returns>包含更新后红色分量的新 <see cref="Color"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _WithR(this Color color, float r)
        {
            color.r = r;
            return color;
        }
        /// <summary>
        /// 修改绿色通道并返回新的 <see cref="Color"/> 实例。
        /// </summary>
        /// <param name="color">原始颜色（值传递）。</param>
        /// <param name="g">新的绿色分量，范围通常为 0 到 1。</param>
        /// <returns>包含更新后绿色分量的新 <see cref="Color"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _WithG(this Color color, float g)
        {
            color.g = g;
            return color;
        }
        /// <summary>
        /// 修改蓝色通道并返回新的 <see cref="Color"/> 实例。
        /// </summary>
        /// <param name="color">原始颜色（值传递）。</param>
        /// <param name="b">新的蓝色分量，范围通常为 0 到 1。</param>
        /// <returns>包含更新后蓝色分量的新 <see cref="Color"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _WithB(this Color color, float b)
        {
            color.b = b;
            return color;
        }
        /// <summary>
        /// 同时设置 RGB 三个通道并返回新的 <see cref="Color"/>，保留原有 alpha 值。
        /// </summary>
        /// <param name="color">原始颜色（仅用于保留 alpha）。</param>
        /// <param name="r">新的红色分量。</param>
        /// <param name="g">新的绿色分量。</param>
        /// <param name="b">新的蓝色分量。</param>
        /// <returns>具有新 RGB 分量且 alpha 不变的 <see cref="Color"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _WithRGB(this Color color, float r, float g, float b)
        {
            return new Color(r, g, b, color.a);
        }
        /// <summary>
        /// 就地修改红色通道（按引用传入 <see cref="Color"/>）。
        /// </summary>
        /// <param name="color">要就地修改的颜色（按引用传递）。</param>
        /// <param name="r">新的红色分量。</param>
        /// <returns>修改后的颜色引用。</returns>
        /// <summary>
        /// 就地（in-place）设置红色分量并返回对原始颜色的引用（ref return），方便链式就地修改。
        /// </summary>
        /// <param name="color">要修改的颜色（按引用传入）。</param>
        /// <param name="r">新的红色分量。</param>
        /// <returns>对传入颜色的引用（ref）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Color _WithRRef(ref Color color, float r)
        {
            color.r = r;
            return ref color;
        }
        /// <summary>
        /// 就地修改绿色通道（按引用传入）。
        /// </summary>
        /// <param name="color">要就地修改的颜色（按引用传递）。</param>
        /// <param name="g">新的绿色分量。</param>
        /// <returns>修改后的颜色引用。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Color _WithGRef(ref Color color, float g)
        {
            color.g = g;
            return ref color;
        }
        /// <summary>
        /// 就地修改蓝色通道（按引用传入）。
        /// </summary>
        /// <param name="color">要就地修改的颜色（按引用传递）。</param>
        /// <param name="b">新的蓝色分量。</param>
        /// <returns>修改后的颜色引用。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Color _WithBRef(ref Color color, float b)
        {
            color.b = b;
            return ref color;
        }
        #endregion
        #region 透明度操作
        /// <summary>
        /// 就地修改透明度（按引用传入）。
        /// </summary>
        /// <param name="color">要就地修改的颜色（按引用传递）。</param>
        /// <param name="alpha">新的透明度值（0-1）。</param>
        /// <returns>修改后的颜色引用。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Color _WithAlphaRef(ref Color color, float alpha)
        {
            color.a = alpha;
            return ref color;
        }
        /// <summary>
        /// 设置透明度(新颜色)
        /// </summary>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// 将透明度乘以给定的乘数并返回新颜色（非就地）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _AlphaMultiply(this Color color, float alphaMultiplier)
        {
            color.a *= alphaMultiplier;
            return color;
        }

        /// <summary>
        /// 就地将透明度乘以给定乘数（按引用传入）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Color _AlphaMultiplyRef(ref Color color, float alphaMultiplier)
        {
            color.a *= alphaMultiplier;
            return ref color;
        }

        // 老旧拼写兼容已移除：使用 _AlphaMultiply / _AlphaMultiplyRef

        /// <summary>
        /// 预乘 Alpha（返回新颜色）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _RGBMultiAlpha(this Color color)
        {
            return new Color(color.r * color.a, color.g * color.a, color.b * color.a, color.a);
        }

        /// <summary>
        /// 就地预乘 Alpha（按引用传入）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Color _RGBMultiAlphaRef(ref Color color)
        {
            color.r *= color.a;
            color.g *= color.a;
            color.b *= color.a;
            return ref color;
        }

        // 老旧拼写兼容已移除：使用 _RGBMultiAlpha / _RGBMultiAlphaRef

        #endregion



        #region 转化
        /// <summary>
        /// 将颜色转换为16进制字符串（例如 #RRGGBB 或 #RRGGBBAA）。
        /// 新名称更直观，明确返回 16 进制格式字符串。
        /// </summary>
        /// <param name="c">要转换的颜色。</param>
        /// <param name="includeAlpha">是否包含 alpha 通道（默认为 false）。</param>
        /// <returns>返回以 '#' 开头的 16 进制颜色字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string _ToHex16String(this Color c, bool includeAlpha = false)
        {
            int r = (int)(Mathf.Clamp01(c.r) * 255f + 0.5f);
            int g = (int)(Mathf.Clamp01(c.g) * 255f + 0.5f);
            int b = (int)(Mathf.Clamp01(c.b) * 255f + 0.5f);
            if (!includeAlpha) return $"#{r:X2}{g:X2}{b:X2}";
            int a = (int)(Mathf.Clamp01(c.a) * 255f + 0.5f);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        /// <summary>
        /// 兼容旧名：_AsHexFormat -> _ToHex16String（已弃用）。
        /// 旧方法将被转发到新的、更直观的名称以保持向后兼容。
        /// </summary>
        [Obsolete("使用 _ToHex16String ，这个名字不好认 ")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string _AsHexFormat(this Color c, bool includeAlpha = false) => _ToHex16String(c, includeAlpha);

        /// <summary>
        /// 从16进制字符串创建颜色
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static Color _ToColorFromHex(this string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            hex = hex.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);

            try
            {
                if (hex.Length == 3)
                {
                    string r = new string(hex[0], 2);
                    string g = new string(hex[1], 2);
                    string b = new string(hex[2], 2);
                    return new Color(
                        int.Parse(r, System.Globalization.NumberStyles.HexNumber) / 255f,
                        int.Parse(g, System.Globalization.NumberStyles.HexNumber) / 255f,
                        int.Parse(b, System.Globalization.NumberStyles.HexNumber) / 255f
                    );
                }
                if (hex.Length == 4)
                {
                    string r = new string(hex[0], 2);
                    string g = new string(hex[1], 2);
                    string b = new string(hex[2], 2);
                    string a = new string(hex[3], 2);
                    return new Color(
                        int.Parse(r, System.Globalization.NumberStyles.HexNumber) / 255f,
                        int.Parse(g, System.Globalization.NumberStyles.HexNumber) / 255f,
                        int.Parse(b, System.Globalization.NumberStyles.HexNumber) / 255f,
                        int.Parse(a, System.Globalization.NumberStyles.HexNumber) / 255f
                    );
                }
                if (hex.Length == 6 || hex.Length == 8)
                {
                    int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    if (hex.Length == 6)
                        return new Color(r / 255f, g / 255f, b / 255f);
                    int a = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                    return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                }
            }
            catch
            {
                // ignore and return white
            }
            return Color.white;
        }

        // 更稳健的 Try 版本（建议添加）
        public static bool _TryToColorFromHex(this string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            hex = hex.Trim();

            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);

            try
            {
                if (hex.Length == 3 || hex.Length == 4)
                {
                    // expand short form: e.g. "f0a" -> "ff00aa"
                    int r = Convert.ToInt32(new string(hex[0], 2), 16);
                    int g = Convert.ToInt32(new string(hex[1], 2), 16);
                    int b = Convert.ToInt32(new string(hex[2], 2), 16);
                    if (hex.Length == 3)
                    {
                        color = new Color(r / 255f, g / 255f, b / 255f, 1f);
                        return true;
                    }
                    int a = Convert.ToInt32(new string(hex[3], 2), 16);
                    color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                    return true;
                }
                if (hex.Length == 6 || hex.Length == 8)
                {
                    if (!int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r)) return false;
                    if (!int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g)) return false;
                    if (!int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b)) return false;
                    if (hex.Length == 6)
                    {
                        color = new Color(r / 255f, g / 255f, b / 255f, 1f);
                        return true;
                    }
                    if (!int.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out int a)) return false;
                    color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                    return true;
                }
            }
            catch
            {
                // 保守起见：若发生不可预料错误，返回 false
            }
            return false;
        }
        #endregion



        /// <summary>
        /// 颜色反转（返回新颜色）。
        /// </summary>
        /// <param name="color">输入颜色。</param>
        /// <returns>反转后的颜色（RGB 取反，alpha 保持不变）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// <summary>
        /// 按分量逐个比较是否近似相等（每个分量差值都必须小于阈值）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsApproximatelyEachRGBA(this Color a, Color b, float threshold = 0.001f)
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
        /// <summary>
        /// 按总和比较是否近似相等（所有分量差值绝对值之和小于阈值）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsApproximatelyTogether(this Color a, Color b, float threshold = 0.004f)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < threshold;
        }


        /// <summary>
        /// 获取颜色的灰度值
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        /// <summary>
        /// 计算亮度（近似灰度值），采用 Rec.601 近似系数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _GetGrayscale(this Color color)
        {
            return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
        }

        /// <summary>
        /// 创建随机颜色Ref
        /// </summary>
        /// <param name="color"></param>
        /// <param name="randomAlpha"></param>
        /// <returns></returns>
        /// <summary>
        /// 就地生成随机颜色（按引用传入），并返回对该颜色的引用。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Color _RandomColorRef(ref Color color, bool randomAlpha = false)
        {
            color = new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                randomAlpha ? UnityEngine.Random.value : 1f
            );
            return ref color;
        }


        /// <summary>
        /// 将颜色转换为灰度
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        /// <summary>
        /// 将颜色转换为灰度色（返回新颜色）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// <summary>
        /// 颜色亮度调整（返回新颜色），分量乘以 factor。
        /// </summary>
        /// <summary>
        /// 返回一个 RGB 分量乘以给定系数的新颜色（不就地）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color _WithRGBMulti(this Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        /// <summary>
        /// 就地颜色亮度调整（按引用传入）。
        /// </summary>
        /// <summary>
        /// 就地将 RGB 分量乘以给定系数（按引用传入）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetRGBMulti(ref Color color, float factor)
        {
            color.r *= factor;
            color.g *= factor;
            color.b *= factor;
        }
        /// <summary>
        /// 兼容旧名的就地方法（已弃用）：_WithRGBMutiRef -> _WithRGBMultiInplace
        /// </summary>
        [Obsolete("Use _SetRGBMulti instead")]
        public static void _WithRGBMutiRef(ref Color color, float factor) => _SetRGBMulti(ref color, factor);
    }
}

