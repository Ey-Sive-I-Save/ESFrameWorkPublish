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
    public static class ExtensionForFloatAndInt
    {
        #region 运算辅助

        /// <summary>
        /// 安全除法，避免除以零。当除数为 0 时，强制改为 1。
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _SafeDivide(this float f, float b)
        {
            if (b == 0) b = 1;
            return f / b;
        }
        /// <summary>
        /// 限制数值在 [min, max] 范围内
        /// </summary>
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
        /// 限制数值在 [min, max] 范围内
        /// </summary>
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
        /// 限制数值在 [0,1] 范围内
        /// </summary>
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
        /// 将角度归一化到 [0, 360)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _AsNormalizeAngle(this float angle)
        {
            angle %= 360f;
            if (angle < 0) angle += 360f;
            return angle;
        }
        /// <summary>
        /// 将角度归一化到 [-180, 180)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _AsNormalizeAngle180(this float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }
        /// <summary>
        /// 将数值映射到新的范围
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _Remap(this float value, float fromMin, float fromMax, float toMin = 0, float toMax = 1)
        {
            if (Mathf.Abs(fromMax - fromMin) < Mathf.Epsilon) return toMin; // 避免除0
            return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        }
        /// <summary>
        /// 线性插值（Clamp 在 0-1 内）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _LerpTo(this float start, float end, float t)
        {
            return start + (end - start) * t;
        }
        /// <summary>
        /// 平滑阻尼插值（类似 Mathf.SmoothDamp，但不强制依赖 Time.deltaTime）
        /// </summary>
        /// <param name="current">当前值</param>
        /// <param name="target">目标值</param>
        /// <param name="currentVelocity">当前速度（ref 参数，内部会更新）</param>
        /// <param name="smoothTime">平滑时间，越小越快</param>
        /// <param name="maxSpeed">最大速度（可选）</param>
        /// <param name="deltaTime">
        /// 时间步长，通常为 <see cref="Time.deltaTime"/>，也可以自定义（如固定步长模拟）
        /// </param>
        /// <returns>插值结果</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _SmoothDamp(this float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = 0.02f)
        {
            // 注意：这里不给 deltaTime 默认 -1，而是一个安全的固定值（0.02秒 ≈ 50fps）
            // 调用时显式传 Time.deltaTime 更清晰
            return Mathf.SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }
        /// <summary>
        /// 反插值，返回当前值在 [from, to] 范围内的归一化位置 (0-1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _InverseLerp(this float value, float from, float to)
        {
            if (Mathf.Abs(to - from) < Mathf.Epsilon) return 0f;
            return Mathf.Clamp01((value - from) / (to - from));
        }
        /// <summary>
        /// 四舍五入到最接近的整数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _RoundInt(this float pre)
        {
            return Mathf.RoundToInt(pre);
        }
        /// <summary>
        /// 将角度（度）转为弧度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _ToRadians(this float degrees)
        {
            return degrees * MathF.PI / 180f;
        }
        /// <summary>
        /// 将弧度转为角度（度）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _ToDegrees(this float radians)
        {
            return radians * 57.29578f;
        }
        /// <summary>
        /// 将数值循环限制在 [min, max) 范围内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _Cycle(this float value, float min, float max)
        {
            float range = max - min;
            if (Math.Abs(range) < Mathf.Epsilon) return min;
            return min + ((value - min) % range + range) % range;
        }
        /// <summary>
        /// 将数值循环限制在 [min, max) 范围内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _Cycle(this int value, int min, int max)
        {
            int range = max - min;
            if (Math.Abs(range) < Mathf.Epsilon) return min;
            return min + ((value - min) % range + range) % range;
        }
        #endregion
        #region 判断

        /// <summary>
        /// 检查 float 是否在给定范围内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsInRange(this float f, Vector2 range)
        {
            return f >= range.x && f <= range.y;
        }
        /// <summary>
        /// 检查 int 是否在给定范围内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsInRange(this int i, Vector2Int range)
        {
            return i >= range.x && i <= range.y;
        }
        /// <summary>
        /// 检查 float 是否接近 0
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsApproximatelyZero(this float f, float threshold = 0.001f)
        {
            return Mathf.Abs(f) < threshold;
        }
        /// <summary>
        /// 检查两个 float 是否近似相等
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsApproximately(this float a, float b, float threshold = 0.001f)
        {
            return Mathf.Abs(a - b) < threshold;
        }
        /// <summary>
        /// 检查 int 是否为偶数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsEven(this int i)
        {
            return i % 2 == 0;
        }
        /// <summary>
        /// 检查 int 是否为奇数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsOdd(this int i)
        {
            return i % 2 != 0;
        }
        /// <summary>
        /// 检查 int 是否能被指定数整除
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsDivisibleBy(this int value, int divisor)
        {
            if (divisor == 0) return false;
            return value % divisor == 0;
        }
        /// <summary>
        /// 是否为正数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsPositive(this float value) => value > 0f;
        /// <summary>
        /// 是否为正数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsPositive(this int value) => value > 0;
        /// <summary>
        /// 是否为负数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsNegative(this float value) => value < 0f;
        /// <summary>
        /// 是否为负数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsNegative(this int value) => value < 0;
        /// <summary>
        /// 获取数值符号（-1, 0, 1）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _Sign(this float value)
        {
            return value > 0 ? 1 : (value < 0 ? -1 : 0);
        }
        /// <summary>
        /// 获取数值符号（-1, 0, 1）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int _Sign(this int value)
        {
            return value > 0 ? 1 : (value < 0 ? -1 : 0);
        }

        #endregion

        #region 显示格式
        /// <summary>
        /// 格式化为固定小数位
        /// </summary>
        public static string _ToString_FormatToDecimalPlaces(this float num, int digits)
        {
            return num.ToString($"F{digits}");
        }
        /// <summary>
        /// 转换为百分比%字符串，可指定小数位
        /// </summary>
        public static string _ToString_Percentage(this float num, int digits = 0)
        {
            return (num * 100).ToString($"F{digits}") + "%";
        }
        /// <summary>
        /// 转换为日期序数（如 1st, 2nd, 3rd, 4th...）
        /// </summary>
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
        /// 转换为带千分位的格式化字符串
        /// </summary>
        public static string _ToFormattedString_1000(this float num)
        {
            return num.ToString("#,0.##");
        }
        /// <summary>
        /// 转换为罗马数字I,V,X
        /// </summary>
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
        /// 转换为货币格式，使用当前区域文化
        /// </summary>
        public static string _ToString_MoneyFormat(this int num)
        {
            return num.ToString("C", CultureInfo.CurrentCulture);
        }
        #endregion

        #region 特殊

        /// <summary>
        /// 获得 [start, end] 的连续整数序列
        /// </summary>
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

