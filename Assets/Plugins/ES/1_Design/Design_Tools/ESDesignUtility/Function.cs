
using DG.Tweening;
using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using static ES.EnumCollect;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //函数器
        public static class Function
        {
            #region 数学
            /// <summary>
            /// 对两个 <see cref="float"/> 值执行指定的算术或位运算操作，并返回结果。
            /// 方法支持常见的加/减/乘/除、取模以及按位掩码运算。除法/取模在除数为 0 时会将被除数替换为 1 以避免异常。
            /// </summary>
            /// <param name="f1">左操作数。</param>
            /// <param name="f2">右操作数。</param>
            /// <param name="twoFloatFunction">指定要执行的运算类型，来自 <see cref="EnumCollect.HandleTwoNumber"/>。</param>
            /// <returns>运算结果（<see cref="float"/>）。</returns>
            /// <remarks>
            /// - 位运算（Mask_*）会将浮点数强制转换为 <see cref="int"/> 后执行位操作并再返回为 <see cref="float"/>（可能丢失精度）。
            /// - 若希望严格的算术语义（尤其对整数位运算），请使用整型版本 <see cref="HandleTwoInt(int,int,EnumCollect.HandleTwoNumber)"/>。
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float HandleTwoFloat(float f1, float f2, EnumCollect.HandleTwoNumber twoFloatFunction)
            {
                switch (twoFloatFunction)
                {
                    case EnumCollect.HandleTwoNumber.Set: return f2;
                    case EnumCollect.HandleTwoNumber.Add: return f1 + f2;
                    case EnumCollect.HandleTwoNumber.Sub: return f1 - f2;
                    case EnumCollect.HandleTwoNumber.Muti: return f1 * f2;
                    case EnumCollect.HandleTwoNumber.Divide: if (f2 == 0) f2 = 1; return f1 / f2;
                    case EnumCollect.HandleTwoNumber.Model: if (f2 == 0) f2 = 1; return f1 % f2;
                    case EnumCollect.HandleTwoNumber.Mask_And: return (int)f1 & (int)f2;
                    case EnumCollect.HandleTwoNumber.Mask_Or: return (int)f1 | (int)f2;
                    case EnumCollect.HandleTwoNumber.Mask_Xor: return (int)f1 ^ (int)f2;
                    case EnumCollect.HandleTwoNumber.Mask_And_Not: return (int)f1 & ~(int)f2;
                    default: return f2;
                }
            }
            /// <summary>
            /// 比较两个 <see cref="float"/> 值并返回布尔结果。比较方式由 <see cref="EnumCollect.CompareTwoNumber"/> 指定。
            /// </summary>
            /// <param name="left">左操作数。</param>
            /// <param name="right">右操作数。</param>
            /// <param name="useFunction">比较的类型/规则。</param>
            /// <returns>比较结果（<c>true</c> 或 <c>false</c>）。</returns>
            /// <remarks>
            /// - 注意浮点数比较的精度问题（例如相等比较通常不建议直接用 ==）。
            /// - 部分比较（如 Reciprocal）使用了阈值 0.01f 来判断近似相等，这是折衷设计，视场景可调整。
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool CompareTwoFloat(float left, float right, EnumCollect.CompareTwoNumber useFunction)
            {
                switch (useFunction)
                {
                    case EnumCollect.CompareTwoNumber.Equal: return left == right;
                    case EnumCollect.CompareTwoNumber.NotEqual: return left != right;
                    case EnumCollect.CompareTwoNumber.Less: return left < right;
                    case EnumCollect.CompareTwoNumber.LessEqual: return left <= right;
                    case EnumCollect.CompareTwoNumber.Greater: return left > right;
                    case EnumCollect.CompareTwoNumber.GreaterEqual: return left >= right;

                    case EnumCollect.CompareTwoNumber.Never: return false;
                    case EnumCollect.CompareTwoNumber.Always: return true;
                    case EnumCollect.CompareTwoNumber.SameDirect: return left * right > 0;
                    case EnumCollect.CompareTwoNumber.NotSameDirect: return left * right < 0;
                    case EnumCollect.CompareTwoNumber.HasZero: return left * right == 0;
                    case EnumCollect.CompareTwoNumber.NoZero: return (left * right) != 0;

                    case EnumCollect.CompareTwoNumber.ModelMatch:
                        if (right == 0) return false;
                        if (left / right == (int)(left / right)) return true;
                        else return false;
                    case EnumCollect.CompareTwoNumber.NotModelMatch:
                        if (right == 0) return false;
                        if (left / right == (int)(left / right)) return false;
                        else return true;
                    case EnumCollect.CompareTwoNumber.Recipprocal: return 0.01f > Mathf.Abs(left * right - 1);
                    case EnumCollect.CompareTwoNumber.NotRecipprocal: return 0.01f < Mathf.Abs(left * right - 1);
                    case EnumCollect.CompareTwoNumber.Mask_And_NotZero: return ((int)left & (int)right) != 0;
                    case EnumCollect.CompareTwoNumber.Mask_ANd_Zero: return ((int)left & (int)right) == 0;

                }
                return false;
            }
            /// <summary>
            /// 对两个 <see cref="int"/> 值执行指定的算术或位运算，并返回结果。
            /// </summary>
            /// <param name="i1">左操作数。</param>
            /// <param name="i2">右操作数。</param>
            /// <param name="twoIntFunction">操作类型，来自 <see cref="EnumCollect.HandleTwoNumber"/>。</param>
            /// <returns>运算结果（<see cref="int"/>）。</returns>
            /// <remarks>
            /// - 除法/取模在右操作数为 0 时会将其替换为 1 以避免除以 0 的异常。
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int HandleTwoInt(int i1, int i2, EnumCollect.HandleTwoNumber twoIntFunction)
            {
                switch (twoIntFunction)
                {
                    case EnumCollect.HandleTwoNumber.Set: return i2;
                    case EnumCollect.HandleTwoNumber.Add: return i1 + i2;
                    case EnumCollect.HandleTwoNumber.Sub: return i1 - i2;
                    case EnumCollect.HandleTwoNumber.Muti: return i1 * i2;
                    case EnumCollect.HandleTwoNumber.Divide: if (i2 == 0) i2 = 1; return i1 / i2;
                    case EnumCollect.HandleTwoNumber.Model: if (i2 == 0) i2 = 1; return i1 % i2;
                    case EnumCollect.HandleTwoNumber.Mask_And: return (int)i1 & (int)i2;
                    case EnumCollect.HandleTwoNumber.Mask_Or: return (int)i1 | (int)i2;
                    case EnumCollect.HandleTwoNumber.Mask_Xor: return (int)i1 ^ (int)i2;
                    case EnumCollect.HandleTwoNumber.Mask_And_Not: return (int)i1 & ~(int)i2;
                    default: return i2;
                }
            }
            /// <summary>
            /// 对两个布尔值执行逻辑操作并返回结果。
            /// </summary>
            /// <param name="b1">左布尔值。</param>
            /// <param name="b2">右布尔值。</param>
            /// <param name="twoBoolFunction">操作类型，来自 <see cref="EnumCollect.HandleTwoBool"/>。</param>
            /// <returns>操作结果（<see cref="bool"/>）。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool HandleTwoBool(bool b1, bool b2, EnumCollect.HandleTwoBool twoBoolFunction)
            {
                switch (twoBoolFunction)
                {
                    case EnumCollect.HandleTwoBool.Set: return b2;
                    case EnumCollect.HandleTwoBool.And: return b1 && b2;
                    case EnumCollect.HandleTwoBool.Or: return b1 || b2;
                    case EnumCollect.HandleTwoBool.SetNot: return b2 ? false : true;
                    case EnumCollect.HandleTwoBool.On_If: return b2 ? true : b1;
                    case EnumCollect.HandleTwoBool.Off_If: return b2 ? false : b1;
                    case EnumCollect.HandleTwoBool.Switch_If: return b2 ? b1 ^ true : b1;
                    default: return b2;
                }
            }





        
            #endregion

            #region 字符串
            /// <summary>
            /// 根据指定规则规范化字符串的大小写（首字母大写/首字母小写/全部大写/全部小写）。
            /// 使用不变区域性（InvariantCulture）以保证跨平台一致行为。
            /// </summary>
            /// <param name="input">待转换的字符串；若为 <c>null</c> 或空则原样返回。</param>
            /// <param name="handleType">指定的转换类型，来自 <see cref="HandleIndentStringName"/>。</param>
            /// <returns>转换后的字符串。</returns>
            /// <remarks>
            /// - 使用扩展方法 `_FirstUpper` / `_FirstLower`（假定在项目中实现）处理首字母的大小写。
            /// - 对于需要文化敏感的字符串操作，请改用具体文化信息。</remarks>
            public static string HandleStringToIndentName(string input, HandleIndentStringName handleType)
            {
                if (string.IsNullOrEmpty(input))
                    return input;

                // 使用不变文化设置确保跨平台一致性
                CultureInfo culture = CultureInfo.InvariantCulture;

                return handleType switch
                {
                    HandleIndentStringName.StartToUpper => input._FirstUpper(culture),
                    HandleIndentStringName.StartToLower => input._FirstLower(culture),
                    HandleIndentStringName.AllUpper => input.ToUpper(culture),
                    HandleIndentStringName.AllLower => input.ToLower(culture),
                    _ => input // 默认返回原字符串
                };
            }

            /// <summary>
            /// 规范化字符串，移除前导和尾随空白字符，并根据指定规则调整大小写。
            /// </summary>
            /// <param name="input">待处理的字符串。</param>
            /// <param name="trim">是否移除空白字符。</param>
            /// <param name="handleType">大小写转换类型。</param>
            /// <returns>处理后的字符串。</returns>
            public static string NormalizeString(string input, bool trim = true, HandleIndentStringName handleType = HandleIndentStringName.StartToUpper)
            {
                if (string.IsNullOrEmpty(input))
                    return input;

                string result = trim ? input.Trim() : input;
                return HandleStringToIndentName(result, handleType);
            }

            #endregion

            #region 容器
            /// <summary>
            /// 从列表中依据指定策略选择并返回一个元素。
            /// 方法不会抛出异常；当列表为空或为 <c>null</c> 时返回类型默认值（例如引用类型返回 <c>null</c>）。
            /// </summary>
            /// <typeparam name="T">列表元素类型。</typeparam>
            /// <param name="values">源列表，可为 <c>null</c>。</param>
            /// <param name="selectOneType">选择策略，来自 <see cref="EnumCollect.SelectOne"/>。</param>
            /// <param name="lastIndex">用于保存/传递上一次选择的索引，方法会在内部更新该值以便下一次调用使用。</param>
            /// <returns>被选中的元素；找不到时返回 <c>default(T)</c>。</returns>
            /// <remarks>
            /// - `NotNullFirst` 会返回列表中第一个非空元素并把索引写回到 <paramref name="lastIndex"/>。
            /// - `RandomOnly` 使用 UnityEngine.Random 进行随机选择。
            /// - 方法内部对 <paramref name="lastIndex"/> 做边界保护以避免越界访问。
            /// </remarks>
            public static T GetOne<T>(List<T> values, EnumCollect.SelectOne selectOneType, ref int lastIndex)
            {
                if (values != null)
                {
                    if (values.Count > 0)
                    {
                        int lastP = lastIndex;
                        lastIndex = 0;
                        if (values.Count > 1)
                        {
                            switch (selectOneType)
                            {
                                case EnumCollect.SelectOne.NotNullFirst:
                                    for (int i = 0; i < values.Count; i++)
                                    {
                                        if (values[i] != null)
                                        {
                                            lastIndex = i;
                                            break;
                                        }
                                    }
                                    break;
                                case EnumCollect.SelectOne.RandomOnly:
                                    lastIndex = UnityEngine.Random.Range(0, values.Count);
                                    break;
                                case EnumCollect.SelectOne.Next:
                                    lastIndex = lastP + 1;
                                    lastIndex %= values.Count;
                                    break;
                                case EnumCollect.SelectOne.Last:
                                    lastIndex = lastP + values.Count - 1;
                                    lastIndex %= values.Count;
                                    break;
                                case EnumCollect.SelectOne.TrySort:
                                    //Do NothingNow

                                    break;
                                default: break;
                            }
                        }
                    }
                    if (lastIndex >= 0) return values[lastIndex];
                }
                return default;
            }
            /// <summary>
            /// 根据给定策略从列表中选择若干元素并返回新列表。
            /// 方法对源列表做非破坏性处理（会创建并返回新列表），并保证在任何情况下不返回 <c>null</c>（空结果返回空列表）。
            /// </summary>
            /// <typeparam name="T">元素类型。</typeparam>
            /// <param name="values">源列表，可为 <c>null</c>。</param>
            /// <param name="selectSomeType">选择策略，来自 <see cref="EnumCollect.SelectSome"/>。</param>
            /// <param name="lastIndex">用于传递数量或索引语义（策略相关），方法内部会使用并可能修改此值以供后续调用。</param>
            /// <returns>包含所选元素的新列表；当没有元素可选时返回空列表（而非 <c>null</c>）。</returns>
            /// <remarks>
            /// - `RandomSome` 会随机打乱非空元素集合后取前 N（N 由 <paramref name="lastIndex"/> 控制，但方法会做安全截断）。
            /// - `StartSome` / `EndSome` 的语义依赖于 <paramref name="lastIndex"/>：请在使用前参考实现或将接口替换为更直观的数量参数。
            /// </remarks>
            public static List<T> GetSome<T>(List<T> values, EnumCollect.SelectSome selectSomeType, ref int lastIndex)
            {
                if (values != null)
                {
                    if (values.Count > 0)
                    {

                        if (values.Count > 1)
                        {
                            List<T> ps = values.Where(n => n != null).ToList();

                            switch (selectSomeType)
                            {
                                case EnumCollect.SelectSome.AllNotNull:

                                    break;
                                case EnumCollect.SelectSome.StartSome:
                                    int removeTimes = ps.Count - lastIndex;
                                    for (int i = 0; i < removeTimes; i++)
                                    {
                                        if (lastIndex < ps.Count) ps.RemoveAt(lastIndex);
                                    }
                                    break;
                                case EnumCollect.SelectSome.EndSome:
                                    int removeTimes2 = ps.Count - lastIndex;
                                    for (int i = 0; i < removeTimes2; i++)
                                    {
                                        if (ps.Count > 1) ps.RemoveAt(0);
                                    }
                                    break;
                                case EnumCollect.SelectSome.RandomSome:
                                    int num = Mathf.Clamp(lastIndex, 0, ps.Count);
                                    List<T> ps2 = ps.OrderBy(n => UnityEngine.Random.value).Take(num).ToList();
                                    ps = ps2;
                                    break;
                                case EnumCollect.SelectSome.Selector: break;
                                case EnumCollect.SelectSome.TrySort: break;
                            }
                            return ps ?? new List<T>();
                        }
                    }

                }
                return new List<T>();
            }

            /// <summary>
            /// 从数组中依据指定策略选择并返回一个元素。
            /// </summary>
            /// <typeparam name="T">元素类型。</typeparam>
            /// <param name="values">源数组。</param>
            /// <param name="selectOneType">选择策略。</param>
            /// <param name="lastIndex">索引。</param>
            /// <returns>选中的元素。</returns>
            public static T GetOne<T>(T[] values, EnumCollect.SelectOne selectOneType, ref int lastIndex)
            {
                if (values != null && values.Length > 0)
                {
                    // 类似List版本的实现
                    int lastP = lastIndex;
                    lastIndex = 0;
                    if (values.Length > 1)
                    {
                        switch (selectOneType)
                        {
                            case EnumCollect.SelectOne.NotNullFirst:
                                for (int i = 0; i < values.Length; i++)
                                {
                                    if (values[i] != null)
                                    {
                                        lastIndex = i;
                                        break;
                                    }
                                }
                                break;
                            case EnumCollect.SelectOne.RandomOnly:
                                lastIndex = UnityEngine.Random.Range(0, values.Length);
                                break;
                            case EnumCollect.SelectOne.Next:
                                lastIndex = lastP + 1;
                                lastIndex %= values.Length;
                                break;
                            case EnumCollect.SelectOne.Last:
                                lastIndex = lastP + values.Length - 1;
                                lastIndex %= values.Length;
                                break;
                            default: break;
                        }
                    }
                    if (lastIndex >= 0 && lastIndex < values.Length) return values[lastIndex];
                }
                return default;
            }

            #endregion

            #region 集成Dotween
            /// <summary>
            /// 获取Tween对象的指定回调委托。
            /// </summary>
            /// <param name="use">Tween对象。</param>
            /// <param name="callBackType">回调类型。</param>
            /// <returns>回调委托；若Tween为null则返回null。</returns>
            public static Delegate GetCallBackFromTween(Tween use, EnumCollect.CallBackType callBackType)
            {
                if (use != null)
                {
                    switch (callBackType)
                    {
                        case EnumCollect.CallBackType.OnComplete:
                            return use.onComplete;
                        case EnumCollect.CallBackType.OnKill:
                            return use.onKill;
                        case EnumCollect.CallBackType.OnUpdate:
                            return use.onUpdate;
                        case EnumCollect.CallBackType.OnPlay:
                            return use.onPlay;
                        case EnumCollect.CallBackType.OnPause:
                            return use.onPause;
                        case EnumCollect.CallBackType.OnRewind:
                            return use.onRewind;
                        case EnumCollect.CallBackType.OnStepComplete:
                            return use.onStepComplete;
                        case EnumCollect.CallBackType.OnWayPointChange:
                            return use.onWaypointChange;
                    }
                }
                return default;
            }
            /// <summary>
            /// 为Tween对象设置指定类型的回调。
            /// </summary>
            /// <param name="use">Tween对象。</param>
            /// <param name="callBackType">回调类型。</param>
            /// <param name="action">要设置的回调动作。</param>
            public static void SetCallBackFromTween(Tween use, EnumCollect.CallBackType callBackType, TweenCallback action)
            {
                if (use != null)
                {
                    switch (callBackType)
                    {
                        case EnumCollect.CallBackType.OnComplete:
                            use.OnComplete(action);
                            break;
                        case EnumCollect.CallBackType.OnKill:
                            use.OnKill(action);
                            break;
                        case EnumCollect.CallBackType.OnUpdate:
                            use.OnUpdate(action);
                            break;
                        case EnumCollect.CallBackType.OnPlay:
                            use.OnPlay(action);
                            break;
                        case EnumCollect.CallBackType.OnPause:
                            use.OnPause(action);
                            break;
                        case EnumCollect.CallBackType.OnRewind:
                            use.OnRewind(action);
                            break;
                        case EnumCollect.CallBackType.OnStepComplete:
                            use.OnStepComplete(action);
                            break;
                        case EnumCollect.CallBackType.OnWayPointChange:
                            // Note: OnWaypointChange requires TweenCallback<int>, not TweenCallback
                            // use.OnWaypointChange(action); // Uncomment if action is TweenCallback<int>
                            break;
                    }
                }
                return;
            }
            #endregion
        }
    }
}

