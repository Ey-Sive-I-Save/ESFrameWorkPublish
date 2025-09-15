using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ES
{

    public static class ExtensionForEnum 
    {
        #region 常规

        /// <summary>
        /// 添加枚举标志
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValue"></param>
        /// <param name="flag">标志Flag</param>
        /// <returns></returns>
        public static T _AddFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return (T)(object)((int)(object)enumValue | (int)(object)flag);
        }

        /// <summary>
        /// 移除枚举标志
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValue"></param>
        /// <param name="flag">标志Flag</param>
        /// <returns></returns>
        public static T _RemoveFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return (T)(object)((int)(object)enumValue & ~(int)(object)flag);
        }

        /// <summary>
        /// 切换枚举标志
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValue"></param>
        /// <param name="flag">标志Flag</param>
        /// <returns></returns>
        public static T _ToggleFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return enumValue.HasFlag(flag) ? enumValue._RemoveFlag(flag) : enumValue._AddFlag(flag);
        }

        /// <summary>
        /// 切换枚举标志
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValue"></param>
        /// <param name="flag">标志Flag</param>
        /// <returns></returns>
        public static T _SwitchFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return enumValue.HasFlag(flag) ? enumValue._RemoveFlag(flag) : enumValue._AddFlag(flag);
        }

        /// <summary>
        /// 检查是否包含所有指定标志
        /// </summary>
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
        /// 检查是否包含任何指定标志
        /// </summary>
        public static bool _HasAnyFlags<T>(this T enumValue, params T[] flags) where T : Enum
        {
            int Value = (int)(object)enumValue;
            foreach (T flag in flags)
            {
                int Flag = (int)(object)(flag);
                if ((Value & Flag) != Flag)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取枚举的所有值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> _GetEnumValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }

        /// <summary>
        /// 获取枚举的描述(如果有Description特性)
        /// </summary>
        /// <param name="enumValue"></param>
        /// <returns></returns>
        public static string _GetDescription(this Enum enumValue)
        {
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            return attributes.Length > 0 ? attributes[0].Description : enumValue.ToString();
        }

        /// <summary>
        /// 检查枚举值是否被显式定义
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValue"></param>
        /// <returns></returns>
        public static bool _IsDefined<T>(this T enumValue) where T : Enum
        {
            return Enum.IsDefined(typeof(T), enumValue);
        }

        /// <summary>
        /// 获取枚举定义的下一个值(循环)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValue"></param>
        /// <returns></returns>
        public static T _Next<T>(this T enumValue) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, enumValue) + 1;
            return index >= values.Length ? values[0] : values[index];
        }

        /// <summary>
        /// 获取枚举定义的上一个值(循环)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValue"></param>
        /// <returns></returns>
        public static T _Previous<T>(this T enumValue) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, enumValue) - 1;
            return index < 0 ? values[values.Length - 1] : values[index];
        }

        /// <summary>
        /// 随机获取定义枚举值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T _Random<T>() where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            return values[UnityEngine.Random.Range(0, values.Length)];
        }
        #endregion

        #region ES扩展
        public static ESMessageAttribute _Get_ATT_ESMessage<T>(this T enumValue) where T : Enum
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            var att = field.GetCustomAttribute<ESMessageAttribute>();
            return att;
        }

        public static string _Get_ATT_ESStringMessage<T>(this T enumValue,string defaultValue="") where T : Enum
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            var att = field.GetCustomAttribute<ESMessageAttribute>();
            return att?.message ?? defaultValue;
        }
        #endregion
    }
}

