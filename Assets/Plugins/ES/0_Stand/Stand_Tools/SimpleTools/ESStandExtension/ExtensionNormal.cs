
using System;
using System.Collections;
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
    
    internal static partial class ExtensionNormal
    {
        private static readonly Dictionary<Type, string> TypeDisplayNameCache = new Dictionary<Type, string>();

        /// <summary>
        /// 产生一个只有自己的列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> _AsListOnlySelf<T>(this T obj)
        {
            return new List<T> { obj };
        }
        /// <summary>
        /// 产生一个只有自己的数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] _AsArrayOnlySelf<T>(this T obj)
        {
            return new T[] { obj };
        }
        /// <summary>
        /// 交换两个变量值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="b"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _Swap<T>(ref this T a, ref T b) where T : struct
        {
            (a, b) = (b, a);
        }


        
        public static string _GetTypeDisplayName(this Type type)
        {
            if (type == null) return string.Empty;
            if (TypeDisplayNameCache.TryGetValue(type, out var cached)) return cached;

            string displayName;
            #if UNITY_EDITOR
            var tr=type.GetCustomAttribute<TypeRegistryItemAttribute>();

            if(tr!=null)
            {
                displayName = tr.Name;
                TypeDisplayNameCache[type] = displayName;
                return displayName;
            }
            #endif
            var cf=type.GetCustomAttribute<CreateAssetMenuAttribute>();
            if(cf!=null)
            {
                displayName = cf.menuName;
                TypeDisplayNameCache[type] = displayName;
                return displayName;
            }
           var esM=type.GetCustomAttribute<ESMessageAttribute>();
           if(esM!=null)
           {
               displayName = esM.message;
               TypeDisplayNameCache[type] = displayName;
               return displayName;
           }
           
           var createPath=type.GetCustomAttribute<ESCreatePathAttribute>();
          if(createPath!=null)
          {
              displayName = createPath.GroupName+"/"+createPath.MyName;
              TypeDisplayNameCache[type] = displayName;
              return displayName;
          }

           displayName = type.Name;
           TypeDisplayNameCache[type] = displayName;
           return displayName;
        }
    }
}
