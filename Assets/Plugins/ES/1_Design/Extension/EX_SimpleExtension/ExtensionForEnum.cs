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
        // 81. 检查枚举是否包含特定标志
        public static bool HasFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return enumValue.HasFlag(flag);
        }

        // 82. 添加枚举标志
        public static T _AddFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return (T)(object)((int)(object)enumValue | (int)(object)flag);
        }

        // 83. 移除枚举标志
        public static T _RemoveFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return (T)(object)((int)(object)enumValue & ~(int)(object)flag);
        }

        // 84. 切换枚举标志
        public static T _ToggleFlag<T>(this T enumValue, T flag) where T : Enum
        {
            return enumValue.HasFlag(flag) ? enumValue._RemoveFlag(flag) : enumValue._AddFlag(flag);
        }

        // 85. 获取枚举的所有值
        public static IEnumerable<T> _GetValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }

        // 86. 获取枚举的描述(如果有Description特性)
        public static string _GetDescription(this Enum enumValue)
        {
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            return attributes.Length > 0 ? attributes[0].Description : enumValue.ToString();
        }

        // 87. 检查枚举值是否有效
        public static bool _IsDefined<T>(this T enumValue) where T : Enum
        {
            return Enum.IsDefined(typeof(T), enumValue);
        }

        // 88. 获取枚举的下一个值(循环)
        public static T _Next<T>(this T enumValue) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, enumValue) + 1;
            return index >= values.Length ? values[0] : values[index];
        }

        // 89. 获取枚举的上一个值(循环)
        public static T _Previous<T>(this T enumValue) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, enumValue) - 1;
            return index < 0 ? values[values.Length - 1] : values[index];
        }

        // 90. 随机获取枚举值
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

