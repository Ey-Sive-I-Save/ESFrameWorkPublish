
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
            //操作两个FLoat
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float FunctionForHandleTwoFloat(float f1, float f2, EnumCollect.HandleTwoNumber twoFloatFunction)
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

            //比较两个Float
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool FunctionForCompareTwoFloat(float left, float right, EnumCollect.CompareTwoNumber useFunction)
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
                    case EnumCollect.CompareTwoNumber.Recipprocal: return 0.01f>Mathf.Abs(left * right - 1);
                    case EnumCollect.CompareTwoNumber.NotRecipprocal: return 0.01f < Mathf.Abs(left * right - 1);
                    case EnumCollect.CompareTwoNumber.Mask_And_NotZero: return ((int)left & (int)right) != 0;
                    case EnumCollect.CompareTwoNumber.Mask_ANd_Zero: return ((int)left & (int)right) == 0;

                }
                return false;
            }

            //操作两个Int
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int FunctionForHandleTwoInt(int i1, int i2, EnumCollect.HandleTwoNumber twoIntFunction)
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool FunctionForHandleTwoBool(bool b1, bool b2, EnumCollect.HandleTwoBool twoBoolFunction)
            {
                switch (twoBoolFunction)
                {
                    case EnumCollect.HandleTwoBool.Set: return b2;
                    case EnumCollect.HandleTwoBool.And: return b1 && b2;
                    case EnumCollect.HandleTwoBool.Or: return b1 || b2;
                    case EnumCollect.HandleTwoBool.SetNot: return b2?false:true;
                    case EnumCollect.HandleTwoBool.On_If: return b2 ? true : b1;
                    case EnumCollect.HandleTwoBool.Off_If: return b2 ? false : b1;
                    case EnumCollect.HandleTwoBool.Switch_If: return b2?b1^true:b1;
                    default: return b2;
                }
            }
            //操作两个bool

            #endregion

            #region 字符串
            public static string FunctionForStringAsIndentNameCase(string input, HandleIndentStringName handleType)
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
            #endregion

            #region 容器
            //取出一个
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
            //取出部分
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
                                    int num = Mathf.Min(ps.Count, lastIndex);
                                    List<T> ps2 = ps.OrderBy(n => UnityEngine.Random.value).Take(num).ToList();
                                    ps = ps2;
                                    break;
                                case EnumCollect.SelectSome.Selector: break;
                                case EnumCollect.SelectSome.TrySort: break;
                            }
                            return ps;
                        }
                    }

                }
                return default;
            }
            #endregion

            #region 集成Dotween
            //dotween集成
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
                            //use_.OnWaypointChange(action);
                            break;
                    }
                }
                return;
            }
            //Function_OperationValue_InLine
         /*   [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
            public static float OpearationFloat_Inline(float value, float Value, OperationOptionsForFloat settleType)
            {
                switch (settleType)
                {
                    case OperationOptionsForFloat.Add: return value + Value;
                    case OperationOptionsForFloat.Sub: return value - Value;
                    case OperationOptionsForFloat.PerUp: return value * (1 + Value);
                    case OperationOptionsForFloat.Max: return Mathf.Clamp(value, value, Value);
                    case OperationOptionsForFloat.Min: return Mathf.Clamp(value, Value, value);
                    case OperationOptionsForFloat.Wave: return value + UnityEngine.Random.Range(-Value, Value);
                    default: return value;
                }
            }
            [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
            public static float OpearationFloat_Cancel_Inline(float value, float Value, OperationOptionsForFloat settleType)
            {
                switch (settleType)
                {
                    case OperationOptionsForFloat.Add: return value - Value;
                    case OperationOptionsForFloat.Sub: return value + Value;
                    case OperationOptionsForFloat.PerUp: return value._SafeDivide(1 + Value);
                    case OperationOptionsForFloat.Max: return Mathf.Clamp(value, value, Value);
                    case OperationOptionsForFloat.Min: return Mathf.Clamp(value, Value, value);
                    case OperationOptionsForFloat.Wave: return value + UnityEngine.Random.Range(-Value, Value);
                    default: return value;
                }
            }*/
            #endregion
        }
    }
}

