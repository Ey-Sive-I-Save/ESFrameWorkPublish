
#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{

    public static partial class ExtNormal
    {
        /// <summary>
        /// 将当前对象包装为仅包含自身的 <see cref="List{T}"/>。
        /// 注意：如果 <paramref name="obj"/> 为 null，返回的列表仍会包含一个 null 项（与 C# 集合语义一致）。
        /// </summary>
        /// <typeparam name="T">列表元素类型。</typeparam>
        /// <param name="obj">要包装的对象（可为 null）。</param>
        /// <returns>包含单个元素的列表。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> _AsListOnlySelf<T>(this T? obj)
        {
            return new List<T?> { obj } as List<T> ?? new List<T> { obj! };
        }
        /// <summary>
        /// 将当前对象包装为仅包含自身的数组。
        /// 注意：如果 <paramref name="obj"/> 为 null，返回的数组仍会包含一个 null 项（与 C# 数组语义一致）。
        /// </summary>
        /// <typeparam name="T">数组元素类型。</typeparam>
        /// <param name="obj">要包装的对象（可为 null）。</param>
        /// <returns>包含单个元素的数组。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] _AsArrayOnlySelf<T>(this T? obj)
        {
            return new T[] { obj! };
        }
        /// <summary>
        /// 交换两个变量的值（泛型，支持引用类型和值类型）。
        /// </summary>
        /// <typeparam name="T">要交换的类型。</typeparam>
        /// <param name="a">第一个变量（使用 ref 传入以便修改呼叫方）。</param>
        /// <param name="b">第二个变量（使用 ref 传入以便修改呼叫方）。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _Swap<T>(ref T a, ref T b)
        {
            (a, b) = (b, a);
        }



        private static readonly ConcurrentDictionary<Type, string> s_TypeDisplayNameCache = new ConcurrentDictionary<Type, string>();

        /// <summary>
        /// 获取类型的展示名称（优先使用自定义特性中的显示名）。
        /// 方法做了线程安全的缓存以避免重复反射开销。
        /// </summary>
        /// <param name="type">要查询的类型（不能为空）。</param>
        /// <returns>类型的展示名称，如果没有任何特性则返回 <see cref="Type.Name"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string _GetTypeDisplayName(this Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            // 使用并发字典缓存结果，避免重复反射。GetOrAdd 的 valueFactory 只会在不存在时运行一次（线程安全）。
            return s_TypeDisplayNameCache.GetOrAdd(type, t =>
            {
#if UNITY_EDITOR
                var tr = t.GetCustomAttribute<TypeRegistryItemAttribute>();
                if (tr != null && !string.IsNullOrEmpty(tr.Name)) return tr.Name;
#endif

                var cf = t.GetCustomAttribute<CreateAssetMenuAttribute>();
                if (cf != null && !string.IsNullOrEmpty(cf.menuName)) return cf.menuName;

                var esM = t.GetCustomAttribute<ESMessageAttribute>();
                if (esM != null && !string.IsNullOrEmpty(esM.message)) return esM.message;

                var createPath = t.GetCustomAttribute<ESCreatePathAttribute>();
                if (createPath != null)
                {
                    var group = createPath.GroupName ?? string.Empty;
                    var myName = createPath.MyName ?? string.Empty;
                    return string.IsNullOrEmpty(group) ? myName : (group + "/" + myName);
                }

                return t.Name;
            });
        }
    }
}
