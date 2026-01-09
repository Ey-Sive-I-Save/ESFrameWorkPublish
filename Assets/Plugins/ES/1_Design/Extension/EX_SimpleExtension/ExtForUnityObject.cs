using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    
    public static class ExtForUnityObject
    {
        /// <summary>
        /// 安全使用对象：当对象为 null（或已被 Unity 销毁）时返回 null，便于链式调用。
        /// 用法示例：var x = someObject._TryUse();
        /// </summary>
        /// <typeparam name="T">UnityEngine.Object 或其子类型。</typeparam>
        /// <param name="ob">目标对象。</param>
        /// <returns>若对象可用则返回对象本身，否则返回 null。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _TryUse<T>(this T ob) where T : UnityEngine.Object
        {
            if (ob == null) return null;
            return ob;
        }

        /// <summary>
        /// 如果对象存在则执行指定动作（安全调用），适用于将 null 检查与回调合并。
        /// </summary>
        /// <typeparam name="T">UnityEngine.Object 或其子类型。</typeparam>
        /// <param name="ob">目标对象。</param>
        /// <param name="action">在对象可用时执行的回调。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _TryUse<T>(this T ob, Action<T> action) where T : UnityEngine.Object
        {
            if (ob == null || action == null) return;
            action(ob);
        }

        /// <summary>
        /// 获取对象的资产 GUID（仅在编辑器中有效，非资产时返回空字符串）。
        /// </summary>
        /// <param name="ob">目标对象，允许为 null。</param>
        /// <returns>资产 GUID 字符串；在运行时或非资产对象返回空字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string _GetGUID(this UnityEngine.Object ob)
        {
#if UNITY_EDITOR
            try
            {
                return ESDesignUtility.SafeEditor.GetAssetGUID(ob) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// 判断对象是否为项目资产（仅在编辑器中可正确判断）。
        /// </summary>
        /// <param name="ob">目标对象。</param>
        /// <returns>若为资产返回 true；运行时返回 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsAsset(this UnityEngine.Object ob)
        {
#if UNITY_EDITOR
            return EditorUtility.IsPersistent(ob);
#else
            return false;
#endif
        }

        /// <summary>
        /// 判断对象是否为 null 或已被 Unity 销毁（兼容 Unity 的伪 null 机制）。
        /// </summary>
        /// <param name="ob">目标对象。</param>
        /// <returns>若对象为 null 或已被销毁返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsNullOrDestroyed(this UnityEngine.Object ob)
        {
            return ob == null;
        }

        /// <summary>
        /// 获取对象所在 Scene 名称及在层级中的路径，例如 "SampleScene:Root/Child/This"。
        /// 对于非 GameObject/Component，返回对象名称。
        /// </summary>
        /// <param name="ob">目标对象。</param>
        /// <returns>Scene 名称与层级路径字符串，空对象返回空字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string _GetScenePath(this UnityEngine.Object ob)
        {
            if (ob == null) return string.Empty;
            if (ob is GameObject go)
            {
                var scene = go.scene;
                var sceneName = scene.IsValid() ? scene.name : string.Empty;
                var path = go.transform._GetHierarchyPath();
                return string.IsNullOrEmpty(sceneName) ? path : ($"{sceneName}:{path}");
            }
            if (ob is Component c)
            {
                return c.gameObject._GetScenePath();
            }
            return ob.name ?? string.Empty;
        }

        /// <summary>
        /// 判断对象是否位于 Resources 文件夹（编辑器中检查资产路径）。运行时返回 false。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsInResources(this UnityEngine.Object ob)
        {
#if UNITY_EDITOR
            if (ob == null) return false;
            var path = AssetDatabase.GetAssetPath(ob);
            return !string.IsNullOrEmpty(path) && path.Contains("/Resources/", StringComparison.OrdinalIgnoreCase);
#else
            return false;
#endif
        }

        /// <summary>
        /// 安全销毁对象：在编辑器和运行时分别使用 DestroyImmediate/Destroy，且对 null 做保护。
        /// </summary>
        /// <param name="ob">目标对象（GameObject 或 Component 或其他 UnityEngine.Object）。</param>
        /// <param name="allowDestroyingAssets">是否允许销毁资产（通常不建议）。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SafeDestroy(this UnityEngine.Object ob, bool allowDestroyingAssets = false)
        {
            if (ob == null) return;
#if UNITY_EDITOR
            // 编辑器模式下，避免误销毁持久化资产
            if (!allowDestroyingAssets && EditorUtility.IsPersistent(ob)) return;
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(ob);
                return;
            }
#endif
            UnityEngine.Object.Destroy(ob);
        }

        /// <summary>
        /// 获取对象在层级视图中的路径，例如 "Root/Child/This"。
        /// </summary>
        /// <param name="ob">目标对象（支持 Component 和 GameObject）。</param>
        /// <returns>层级路径字符串；若对象为 null 返回空字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string _GetHierarchyPath(this UnityEngine.Object ob)
        {
            if (ob == null) return string.Empty;
            if (ob is GameObject go)
            {
                return go.transform._GetHierarchyPath();
            }
            if (ob is Component c)
            {
                return c.transform._GetHierarchyPath();
            }
            return ob.name ?? string.Empty;
        }

        /// <summary>
        /// 获取 Transform 的层级路径实现细节。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string _GetHierarchyPath(this Transform t)
        {
            if (t == null) return string.Empty;
            var parts = new List<string>();
            var cur = t;
            while (cur != null)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }


        /// <summary>
        /// 判断对象是否为 Prefab 资产（仅在编辑器中正确判断）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsPrefabAsset(this UnityEngine.Object ob)
        {
#if UNITY_EDITOR
            return ob != null && PrefabUtility.IsPartOfPrefabAsset(ob);
#else
            return false;
#endif
        }

        /// <summary>
        /// 判断对象是否为 Prefab 实例（场景中的实例，编辑器下可正确判断）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsPrefabInstance(this UnityEngine.Object ob)
        {
#if UNITY_EDITOR
            return ob != null && PrefabUtility.IsPartOfPrefabInstance(ob);
#else
            return false;
#endif
        }

    }
}

