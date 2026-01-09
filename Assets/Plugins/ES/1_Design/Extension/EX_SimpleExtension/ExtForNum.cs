using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Burst;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 针对 float 和 int 的扩展方法集合
    /// </summary>
    public static class ExtForNum
    {
        #region 运算辅助

        /// <summary>
        /// 安全除法：当除数为 0 时将除数视为 1，避免除零异常。
        /// </summary>
        /// <param name="f">被除数。</param>
        /// <param name="b">除数。</param>
        /// <returns>返回 f / b 的结果；当 b 为 0 时返回 f。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _SafeDivide(this float f, float b)
        {
            if (b == 0) b = 1;
            return f / b;
        }

        /// <summary>
        /// 将浮点数限制在 [min, max] 区间内。
        /// </summary>
        /// <param name="value">待限制值。</param>
        /// <param name="min">最小值（包含）。</param>
        /// <param name="max">最大值（包含）。</param>
        /// <returns>返回限制后的值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _Clamp(this float value, float min, float max)
        {
            if (value < min)
            {
                value = min;
            }
            else if (value > max)
            {
                value = max;
            }
            return value;
        }

        /// <summary>
        /// 将整数限制在 [min, max] 区间内。
        /// </summary>
        /// <param name="value">待限制整数。</param>
        /// <param name="min">最小值（包含）。</param>
        /// <param name="max">最大值（包含）。</param>
        /// <returns>返回限制后的整数。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _Clamp(this int value, int min, int max)
        {
            if (value < min)
            {
                value = min;
            }
            else if (value > max)
            {
                value = max;
            }
            return value;
        }

        /// <summary>
        /// 将浮点数限制在 [0,1] 区间内。
        /// </summary>
        /// <param name="value">待限制值。</param>
        /// <returns>返回 0 到 1 之间的数值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _Clamp01(this float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }
            return value;
        }

        /// <summary>
        /// 将角度（度）归一化到 [0, 360) 范围内。
        /// </summary>
        /// <param name="angle">角度，单位为度。</param>
        /// <returns>归一化后的角度，范围 [0, 360)。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _AsNormalizeAngle(this float angle)
        {
            angle %= 360f;
            if (angle < 0) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 将角度（度）归一化到 [-180, 180) 范围内。
        /// </summary>
        /// <param name="angle">角度，单位为度。</param>
        /// <returns>归一化后的角度，范围 [-180, 180)。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _AsNormalizeAngle180(this float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 将数值从一个区间线性映射到另一个区间。
        /// </summary>
        /// <param name="value">输入值。</param>
        /// <param name="fromMin">源区间最小值。</param>
        /// <param name="fromMax">源区间最大值。</param>
        /// <param name="toMin">目标区间最小值，默认为 0。</param>
        /// <param name="toMax">目标区间最大值，默认为 1。</param>
        /// <returns>映射到目标区间的值；若源区间大小接近 0 则返回 toMin。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _Remap(this float value, float fromMin, float fromMax, float toMin = 0, float toMax = 1)
        {
            if (Mathf.Abs(fromMax - fromMin) < Mathf.Epsilon) return toMin; // 避免除0
            return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        }

        /// <summary>
        /// 线性插值（不自动限制 t）。
        /// </summary>
        /// <param name="start">起始值。</param>
        /// <param name="end">结束值。</param>
        /// <param name="t">插值因子，通常在 0 到 1 之间。</param>
        /// <returns>插值结果。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _LerpTo(this float start, float end, float t)
        {
            return start + (end - start) * t;
        }

        /// <summary>
        /// 平滑阻尼插值（类似 Mathf.SmoothDamp），允许自定义时间步长。
        /// </summary>
        /// <param name="current">当前值。</param>
        /// <param name="target">目标值。</param>
        /// <param name="currentVelocity">当前速度引用，函数会修改该引用。</param>
        /// <param name="smoothTime">平滑时间，越小越快。</param>
        /// <param name="maxSpeed">最大速度（可选）。</param>
        /// <param name="deltaTime">时间步长，默认 0.02（可传入 Time.deltaTime）。</param>
        /// <returns>返回新的平滑插值值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _SmoothDamp(this float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = 0.02f)
        {
            // 注意：这里不给 deltaTime 默认 -1，而是一个安全的固定值（0.02秒 ≈ 50fps）
            // 调用时显式传 Time.deltaTime 更清晰
            return Mathf.SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }

        /// <summary>
        /// 反向插值，返回当前值在指定区间内的归一化位置（0 到 1）。
        /// </summary>
        /// <param name="value">输入值。</param>
        /// <param name="from">区间起点。</param>
        /// <param name="to">区间终点。</param>
        /// <returns>归一化位置；当区间宽度为 0 时返回 0。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _InverseLerp(this float value, float from, float to)
        {
            if (Mathf.Abs(to - from) < Mathf.Epsilon) return 0f;
            return Mathf.Clamp01((value - from) / (to - from));
        }

        /// <summary>
        /// 四舍五入为最接近的整数。
        /// </summary>
        /// <param name="pre">输入浮点数。</param>
        /// <returns>四舍五入后的整数。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _RoundInt(this float pre)
        {
            return Mathf.RoundToInt(pre);
        }

        /// <summary>
        /// 将角度（度）转换为弧度。
        /// </summary>
        /// <param name="degrees">角度值（度）。</param>
        /// <returns>对应弧度值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _ToRadians(this float degrees)
        {
            return degrees * MathF.PI / 180f;
        }

        /// <summary>
        /// 将弧度转换为角度（度）。
        /// </summary>
        /// <param name="radians">弧度值。</param>
        /// <returns>对应角度（度）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _ToDegrees(this float radians)
        {
            return radians * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 将浮点数循环映射到 [min, max) 区间内。
        /// </summary>
        /// <param name="value">输入值。</param>
        /// <param name="min">区间最小值。</param>
        /// <param name="max">区间最大值（不包含）。</param>
        /// <returns>映射后的值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _Cycle(this float value, float min, float max)
        {
            float range = max - min;
            if (Mathf.Abs(range) < Mathf.Epsilon) return min;
            return min + ((value - min) % range + range) % range;
        }

        /// <summary>
        /// 将整数循环映射到 [min, max) 区间内。
        /// </summary>
        /// <param name="value">输入整数。</param>
        /// <param name="min">区间最小值。</param>
        /// <param name="max">区间最大值（不包含）。</param>
        /// <returns>映射后的整数值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _Cycle(this int value, int min, int max)
        {
            int range = max - min;
            if (range == 0) return min;
            return min + ((value - min) % range + range) % range;
        }
        #endregion
        #region 判断

        /// <summary>
        /// 判断浮点数是否在指定范围内（包含边界）。
        /// </summary>
        /// <param name="f">要判断的浮点数。</param>
        /// <param name="range">范围，x 为最小值，y 为最大值。</param>
        /// <returns>若在范围内返回 true，否则 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsInRange(this float f, Vector2 range)
        {
            return f >= range.x && f <= range.y;
        }

        /// <summary>
        /// 判断整数是否在指定范围内（包含边界）。
        /// </summary>
        /// <param name="i">要判断的整数。</param>
        /// <param name="range">范围，x 为最小值，y 为最大值。</param>
        /// <returns>若在范围内返回 true，否则 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsInRange(this int i, Vector2Int range)
        {
            return i >= range.x && i <= range.y;
        }

        /// <summary>
        /// 判断浮点数是否接近 0（基于阈值）。
        /// </summary>
        /// <param name="f">输入值。</param>
        /// <param name="threshold">判断阈值，默认 0.001。</param>
        /// <returns>若绝对值小于阈值返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsApproximatelyZero(this float f, float threshold = 0.001f)
        {
            return Mathf.Abs(f) < threshold;
        }

        /// <summary>
        /// 判断两个浮点数是否近似相等（基于阈值）。
        /// </summary>
        /// <param name="a">第一个浮点数。</param>
        /// <param name="b">第二个浮点数。</param>
        /// <param name="threshold">允许的误差范围，默认 0.001。</param>
        /// <returns>若差值小于阈值返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsApproximately(this float a, float b, float threshold = 0.001f)
        {
            return Mathf.Abs(a - b) < threshold;
        }

        /// <summary>
        /// 判断整数是否为偶数。
        /// </summary>
        /// <param name="i">输入整数。</param>
        /// <returns>偶数返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsEven(this int i)
        {
            return i % 2 == 0;
        }

        /// <summary>
        /// 判断整数是否为奇数。
        /// </summary>
        /// <param name="i">输入整数。</param>
        /// <returns>奇数返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsOdd(this int i)
        {
            return i % 2 != 0;
        }

        /// <summary>
        /// 判断整数能否被指定除数整除（除数为 0 时返回 false）。
        /// </summary>
        /// <param name="value">被除整数。</param>
        /// <param name="divisor">除数。</param>
        /// <returns>能整除返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsDivisibleBy(this int value, int divisor)
        {
            if (divisor == 0) return false;
            return value % divisor == 0;
        }

        /// <summary>
        /// 判断浮点数是否为正数。
        /// </summary>
        /// <param name="value">输入浮点数。</param>
        /// <returns>大于 0 返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsPositive(this float value) => value > 0f;

        /// <summary>
        /// 判断整数是否为正数。
        /// </summary>
        /// <param name="value">输入整数。</param>
        /// <returns>大于 0 返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsPositive(this int value) => value > 0;

        /// <summary>
        /// 判断浮点数是否为负数。
        /// </summary>
        /// <param name="value">输入浮点数。</param>
        /// <returns>小于 0 返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsNegative(this float value) => value < 0f;

        /// <summary>
        /// 判断整数是否为负数。
        /// </summary>
        /// <param name="value">输入整数。</param>
        /// <returns>小于 0 返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsNegative(this int value) => value < 0;

        /// <summary>
        /// 获取浮点数的符号，返回 -1、0 或 1。
        /// </summary>
        /// <param name="value">输入浮点数。</param>
        /// <returns>-1、0 或 1。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _Sign(this float value)
        {
            return value > 0 ? 1 : (value < 0 ? -1 : 0);
        }

        /// <summary>
        /// 获取整数的符号，返回 -1、0 或 1。
        /// </summary>
        /// <param name="value">输入整数。</param>
        /// <returns>-1、0 或 1。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _Sign(this int value)
        {
            return value > 0 ? 1 : (value < 0 ? -1 : 0);
        }

        #endregion

        #region 显示格式

        /// <summary>
        /// 将浮点数格式化为指定小数位数的字符串。
        /// </summary>
        /// <param name="num">输入浮点数。</param>
        /// <param name="digits">小数位数。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string _ToString_FormatToDecimalPlaces(this float num, int digits)
        {
            return num.ToString($"F{digits}");
        }

        /// <summary>
        /// 将浮点数转换为百分比字符串，可指定小数位。
        /// </summary>
        /// <param name="num">输入浮点数（例如 0.5 表示 50%）。</param>
        /// <param name="digits">小数位数，默认 0。</param>
        /// <returns>带 % 的百分比字符串。</returns>
        public static string _ToString_Percentage(this float num, int digits = 0)
        {
            return (num * 100).ToString($"F{digits}") + "%";
        }

        /// <summary>
        /// 将整数转换为英语日期序数（如 1st, 2nd, 3rd, 4th）。
        /// </summary>
        /// <param name="num">输入整数（通常为日期）。</param>
        /// <returns>带序数后缀的字符串。</returns>
        public static string _ToString_DateOrdinal(this int num)
        {
            if (num % 100 / 10 == 1) return $"{num}th";
            switch (num % 10)
            {
                case 1: return $"{num}st";
                case 2: return $"{num}nd";
                case 3: return $"{num}rd";
                default: return $"{num}th";
            }
        }

        /// <summary>
        /// 将浮点数格式化为带千分位的字符串。
        /// </summary>
        /// <param name="num">输入浮点数。</param>
        /// <returns>带千分位的字符串。</returns>
        public static string _ToFormattedString_1000(this float num)
        {
            return num.ToString("#,0.##");
        }

        /// <summary>
        /// 将正整数转换为罗马数字表示（I, V, X 等）。
        /// </summary>
        /// <param name="num">输入整数（建议为正数）。</param>
        /// <returns>罗马数字字符串。</returns>
        public static string _ToString_Roman(this int num)
        {
            var romanNumerals = new (int, string)[]
            {
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
                (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
                (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
            };

            var result = new StringBuilder();
            foreach (var (value, numeral) in romanNumerals)
            {
                while (num >= value)
                {
                    result.Append(numeral);
                    num -= value;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// 将整数格式化为当前区域文化的货币字符串表示。
        /// </summary>
        /// <param name="num">整数金额。</param>
        /// <returns>带货币符号的字符串。</returns>
        public static string _ToString_MoneyFormat(this int num)
        {
            return num.ToString("C", CultureInfo.CurrentCulture);
        }
        #endregion

        #region 特殊

        /// <summary>
        /// 生成从 start 到 end（包含）的连续整数序列，适合 foreach 枚举使用。
        /// </summary>
        /// <param name="start">起始整数。</param>
        /// <param name="end">结束整数（包含）。</param>
        /// <returns>返回一个可枚举的整数序列。</returns>
        public static IEnumerable<int> _GetIEnumerable_TargetRangeInts(this int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                yield return i;
            }
        }
        #endregion
    }
}

