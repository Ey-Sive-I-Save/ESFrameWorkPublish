using ES;
using System;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ES
{

    public static class ExtForEnum 
    {
        #region 常规

        /// <summary>
        /// 添加枚举标志（按位或）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">要操作的枚举值。</param>
        /// <param name="flag">要添加的标志位。</param>
        /// <returns>包含指定标志的新枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _AddFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return (T)(object)((int)(object)enumValue | (int)(object)flag);
        }

        /// <summary>
        /// 移除枚举标志（按位与取反）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">要操作的枚举值。</param>
        /// <param name="flag">要移除的标志位。</param>
        /// <returns>移除指定标志后的枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _RemoveFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return (T)(object)((int)(object)enumValue & ~(int)(object)flag);
        }

        /// <summary>
        /// 切换枚举标志（存在则移除，否则添加）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">要操作的枚举值。</param>
        /// <param name="flag">要切换的标志位。</param>
        /// <returns>切换指定标志后的枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _ToggleFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return enumValue.HasFlag(flag) ? enumValue._RemoveFlag(flag) : enumValue._AddFlag(flag);
        }

        /// <summary>
        /// 切换枚举标志（同 _ToggleFlag 的别名）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">要操作的枚举值。</param>
        /// <param name="flag">要切换的标志位。</param>
        /// <returns>切换指定标志后的枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _SwitchFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return enumValue.HasFlag(flag) ? enumValue._RemoveFlag(flag) : enumValue._AddFlag(flag);
        }

        /// <summary>
        /// 检查枚举值是否包含所有指定的标志位。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _HasAllFlags<T>(this T enumValue, params T[] flags) where T : Enum
        {
            int Value = (int)(object)enumValue;
            foreach (T flag in flags)
            {
                int Flag = (int)(object)(flag);
                if ((Value & Flag) != Flag)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 检查枚举值是否包含任何指定的标志位。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _HasAnyFlags<T>(this T enumValue, params T[] flags) where T : Enum
        {
            int Value = (int)(object)enumValue;
            foreach (T flag in flags)
            {
                int Flag = (int)(object)(flag);
                if ((Value & Flag) == Flag)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 返回指定枚举类型的所有定义值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <returns>枚举值序列。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> _GetEnumValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }

        /// <summary>
        /// 获取枚举字段的 DescriptionAttribute 描述字符串（若存在），否则返回字段名。
        /// </summary>
        /// <param name="enumValue">枚举实例。</param>
        /// <returns>描述文本或枚举名称。</returns>
        public static string _GetDescription(this Enum enumValue)
        {
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            return attributes.Length > 0 ? attributes[0].Description : enumValue.ToString();
        }

        /// <summary>
        /// 判断枚举值是否为枚举类型的已定义常量。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <returns>若已定义则返回 true，否则 false。</returns>
        public static bool _IsDefined<T>(this T enumValue) where T : Enum
        {
            return Enum.IsDefined(typeof(T), enumValue);
        }

        /// <summary>
        /// 返回枚举定义中的下一个值，若已到末尾则循环到第一个值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">当前枚举值。</param>
        /// <returns>下一个枚举值（循环）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _Next<T>(this T enumValue) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, enumValue) + 1;
            return index >= values.Length ? values[0] : values[index];
        }

        /// <summary>
        /// 返回枚举定义中的上一个值，若已到开头则循环到最后一个值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">当前枚举值。</param>
        /// <returns>上一个枚举值（循环）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _Previous<T>(this T enumValue) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, enumValue) - 1;
            return index < 0 ? values[values.Length - 1] : values[index];
        }

        /// <summary>
        /// 随机返回枚举中一个已定义的值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <returns>随机选取的枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _Random<T>() where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            return values[UnityEngine.Random.Range(0, values.Length)];
        }
        #endregion

        #region ES扩展

        /// <summary>
        /// 获取枚举字段在 Unity Inspector 中显示的名称（来自 InspectorNameAttribute）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <returns>Inspector 显示名；当未找到属性时，可能抛出 NullReferenceException。</returns>
        public static string _GetInspectorName<T>(this T enumValue) where T : Enum
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            if (field == null) return enumValue.ToString();
            var att = field.GetCustomAttribute<InspectorNameAttribute>();
            return att?.displayName ?? enumValue.ToString();
        }


        /// <summary>
        /// 获取枚举字段上自定义的 ESMessageAttribute（若存在）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <returns>找到的 ESMessageAttribute 实例，未找到则返回 null。</returns>
        public static ESMessageAttribute _Get_ATT_ESMessage<T>(this T enumValue) where T : Enum
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            if (field == null) return null;
            var att = field.GetCustomAttribute<ESMessageAttribute>();
            return att;
        }

        /// <summary>
        /// 获取枚举字段上 ESMessageAttribute 中的 message 字段字符串，若不存在则返回默认值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <param name="defaultValue">当未找到 message 时返回的默认值。</param>
        /// <returns>message 字符串或默认值。</returns>
        public static string _Get_ATT_ESStringMessage<T>(this T enumValue,string defaultValue="") where T : Enum
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            var att = field.GetCustomAttribute<ESMessageAttribute>();
            return att?.message ?? defaultValue;
        }
        #endregion

        #region 64位安全扩展（新增，保留原方法不变）

        /// <summary>
        /// 将枚举值转换为 64 位整数表示。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <returns>等价的 64 位整数值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long _ToInt64Value<T>(this T enumValue) where T : Enum
        {
            return Convert.ToInt64(enumValue);
        }

        /// <summary>
        /// 以 64 位安全方式添加枚举标志（支持任何底层类型的枚举）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">要操作的枚举值。</param>
        /// <param name="flag">要添加的标志位。</param>
        /// <returns>包含指定标志的新枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _AddFlag64<T>(this T enumValue, T flag) where T : Enum
        {
            long v = Convert.ToInt64(enumValue);
            long f = Convert.ToInt64(flag);
            long r = v | f;
            return (T)Enum.ToObject(typeof(T), r);
        }

        /// <summary>
        /// 以 64 位安全方式移除枚举标志（支持任何底层类型的枚举）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">要操作的枚举值。</param>
        /// <param name="flag">要移除的标志位。</param>
        /// <returns>移除指定标志后的枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _RemoveFlag64<T>(this T enumValue, T flag) where T : Enum
        {
            long v = Convert.ToInt64(enumValue);
            long f = Convert.ToInt64(flag);
            long r = v & ~f;
            return (T)Enum.ToObject(typeof(T), r);
        }

        /// <summary>
        /// 以 64 位安全方式切换枚举标志（存在则移除，否则添加）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">要操作的枚举值。</param>
        /// <param name="flag">要切换的标志位。</param>
        /// <returns>切换指定标志后的枚举值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _ToggleFlag64<T>(this T enumValue, T flag) where T : Enum
        {
            long v = Convert.ToInt64(enumValue);
            long f = Convert.ToInt64(flag);
            bool hasAll = (v & f) == f;
            long r = hasAll ? (v & ~f) : (v | f);
            return (T)Enum.ToObject(typeof(T), r);
        }

        /// <summary>
        /// 以 64 位安全方式检查枚举值是否包含所有指定标志位。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <param name="flags">要检查的标志位列表。</param>
        /// <returns>若包含所有标志返回 true，否则 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _HasAllFlags64<T>(this T enumValue, params T[] flags) where T : Enum
        {
            long v = Convert.ToInt64(enumValue);
            foreach (T flag in flags)
            {
                long f = Convert.ToInt64(flag);
                if ((v & f) != f)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 以 64 位安全方式检查枚举值是否包含任意一个指定标志位。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <param name="flags">要检查的标志位列表。</param>
        /// <returns>若包含任一标志返回 true，否则 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _HasAnyFlags64<T>(this T enumValue, params T[] flags) where T : Enum
        {
            long v = Convert.ToInt64(enumValue);
            foreach (T flag in flags)
            {
                long f = Convert.ToInt64(flag);
                if ((v & f) == f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取枚举字段的 Inspector 显示名称（安全版本，带空检查）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举实例。</param>
        /// <returns>Inspector 显示名或枚举名称（当未找到字段或属性时）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string _GetInspectorNameSafe<T>(this T enumValue) where T : Enum
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            if (field == null) return enumValue.ToString();
            var att = field.GetCustomAttribute<InspectorNameAttribute>();
            return att?.displayName ?? enumValue.ToString();
        }

        #endregion
    }
}

