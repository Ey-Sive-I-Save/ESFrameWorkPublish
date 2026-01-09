using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{

    public static class ExtForComponent 
    {
        #region 常规脚本
        /// <summary>
        /// 获取父级（不包括自身）上类型为 <typeparamref name="T"/> 的组件。
        /// Get the component of type <typeparamref name="T"/> from the parent hierarchy, excluding the current object.
        /// </summary>
        /// <typeparam name="T">组件类型 / Component type.</typeparam>
        /// <param name="component">当前组件（扩展方法的目标） / The calling component.</param>
        /// <param name="includeInactive">是否包含禁用的组件 / Whether to include inactive components. Default: true.</param>
        /// <returns>找到的组件或 null / The found component or null if none found.</returns>
        public static T _GetCompoentInParentExcludeSelf<T>(this Component component,bool includeInactive=true) where T : Component
        {
            if (component == null || component.transform.parent == null) return null;
            return component.transform.parent.GetComponentInParent<T>(includeInactive);
        }
        /// <summary>
        /// 获取当前组件所有子孙（不包括自身）上类型为 <typeparamref name="T"/> 的组件列表。
        /// Retrieve all components of type <typeparamref name="T"/> on descendants (excluding the current GameObject).
        /// This performs a single traversal via <see cref="Component.GetComponentsInChildren{T}(bool)"/> then filters out self.
        /// </summary>
        /// <typeparam name="T">组件类型 / Component type.</typeparam>
        /// <param name="component">当前组件（扩展方法的目标） / The calling component.</param>
        /// <param name="includeInactive">是否包含禁用的组件 / Whether to include inactive components.</param>
        /// <returns>找到的组件列表（不包含自身） / List of found components excluding self.</returns>
        public static List<T> _GetCompoentsInChildExcludeSelf<T>(this Component component, bool includeInactive = true) where T : Component
        {
            if (component == null) return new List<T>();

            List<T> result = new List<T>();
            T[] comps = component.GetComponentsInChildren<T>(includeInactive);
            foreach (var c in comps)
            {
                if (c != null && c.gameObject != component.gameObject)
                    result.Add(c);
            }
            return result;
        }

        /// <summary>
        /// Fast variant: 同上但不进行参数 null 检查。调用方必须保证 <paramref name="component"/> 非 null。
        /// Fast variant: same as above but without null checks. Caller must ensure <paramref name="component"/> is not null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> _GetCompoentsInChildExcludeSelfFast<T>(this Component component, bool includeInactive = true) where T : Component
        {
            List<T> result = new List<T>(3);
            T[] comps = component.GetComponentsInChildren<T>(includeInactive);
            foreach (var c in comps)
            {
                if (c.gameObject != component.gameObject)
                    result.Add(c);
            }
            return result;
        }

        /// <summary>
        /// 计算当前组件所在位置与另一个组件的位置之间的距离。
        /// Calculate the distance between this component and another component.
        /// </summary>
            /// <param name="component">当前组件 / The calling component. Must not be null.</param>
            /// <param name="target">目标组件 / Target component. Must not be null.</param>
            /// <returns>两点之间的距离 / Distance between the two points.</returns>
            /// <remarks>此方法为性能优化的热点路径，不做参数 null 检查。调用方必须保证传入非 null。</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float _DistanceTo(this Component component, Component target)
            {
                return Vector3.Distance(component.transform.position, target.transform.position);
            }
        /// <summary>
        /// 计算当前组件位置与给定世界坐标之间的距离。
        /// Calculate the distance between this component and a world-space position.
        /// </summary>
        /// <param name="component">当前组件 / The calling component.</param>
        /// <param name="position">目标世界坐标 / Target world-space position.</param>
        /// <returns>两点之间的距离；若 <paramref name="self"/> 为 null 则返回 -1f / Distance; returns -1f if <paramref name="self"/> is null.</returns>
            /// <param name="component">当前组件 / The calling component. Must not be null.</param>
            /// <param name="position">目标世界坐标 / Target world-space position.</param>
            /// <returns>两点之间的距离 / Distance between component position and given position.</returns>
            /// <remarks>此方法为性能优化的热点路径，不做参数 null 检查。调用方必须保证传入非 null。</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float _DistanceTo(this Component component, Vector3 position)
            {
                return Vector3.Distance(component.transform.position, position);
            }

        /// <summary>
        /// 判断另一个组件是否位于指定范围内（基于世界坐标距离）。
        /// Check whether another component is within a specified range (world-space distance).
        /// </summary>
        /// <param name="component">当前组件 / The calling component.</param>
        /// <param name="target">目标组件 / Target component.</param>
        /// <param name="range">判定距离 / Range to check against.</param>
        /// <returns>若双方存在且距离小于等于 range 返回 true，否则 false / True if within range; false otherwise.</returns>
            /// <param name="component">当前组件 / The calling component. Must not be null.</param>
            /// <param name="target">目标组件 / Target component. Must not be null.</param>
            /// <param name="range">判定距离 / Range to check against.</param>
            /// <returns>若距离小于等于 range 返回 true，否则 false / True if within range; false otherwise.</returns>
            /// <remarks>此方法为性能优化的热点路径，不做参数 null 检查。调用方必须保证传入非 null。</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool _IsInRange(this Component component, Component target, float range)
            {
                return component._DistanceTo(target) <= range;
            }

        /// <summary>
        /// 将当前组件的世界空间位置转换为屏幕空间坐标（像素）。
        /// Convert the component's world position to screen-space coordinates (in pixels).
        /// </summary>
        /// <param name="component">当前组件 / The calling component.</param>
        /// <param name="camera">用于投影的相机；若为 null 则使用 Camera.main / Camera used for projection; uses Camera.main if null.</param>
        /// <returns>屏幕坐标（Vector3，其中 z 为相机到点的深度）；若无法获得相机或 self 为 null 则返回 Vector3.zero / Screen position; Vector3.zero on failure.</returns>
        public static Vector3 _GetScreenPosition(this Component component, Camera camera = null)
        {

            if (camera == null) camera = Camera.main;
            if (camera == null) return Vector3.zero;

            return camera.WorldToScreenPoint(component.transform.position);
        }

        /// <summary>
        /// 返回挂在同一 GameObject 上并实现指定接口或类型 <typeparamref name="T"/> 的所有 MonoBehaviour 实例。
        /// Return all MonoBehaviour instances on the same GameObject that implement or are of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">接口或类型 / Interface or type to match.</typeparam>
        /// <param name="component">目标组件（同一 GameObject 上查找） / The component whose GameObject will be searched.</param>
        /// <returns>匹配的实例列表 / List of matching instances (may be empty).</returns>
        public static List<T> _GetInterfaces<T>(this Component component)
        {
            if (component == null) return new List<T>();

            MonoBehaviour[] scripts = component.GetComponents<MonoBehaviour>();
            List<T> interfaces = new List<T>();

            foreach (MonoBehaviour script in scripts)
            {
                if (script is T interfaceObj)
                {
                    interfaces.Add(interfaceObj);
                }
            }
            return interfaces;
        }

        /// <summary>
        /// Fast variant: 返回同一 GameObject 上实现指定类型的 MonoBehaviour 实例，不进行 null 检查。
        /// Fast variant: returns matching MonoBehaviour instances on the same GameObject without null checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> _GetInterfacesFast<T>(this Component component)
        {
            MonoBehaviour[] scripts = component.GetComponents<MonoBehaviour>();
            List<T> interfaces = new List<T>();
            foreach (MonoBehaviour script in scripts)
            {
                if (script is T interfaceObj)
                {
                    interfaces.Add(interfaceObj);
                }
            }
            return interfaces;
        }

        /// <summary>
        /// 获取指定类型的组件；若不存在则添加并返回新添加的组件。
        /// Get a component of type <typeparamref name="T"/> on the same GameObject; add it if missing.
        /// </summary>
        /// <typeparam name="T">组件类型 / Component type.</typeparam>
        /// <param name="component">目标组件（用于访问 GameObject） / The calling component used to access the GameObject.</param>
        /// <returns>存在或新添加的组件；若 <paramref name="component"/> 为 null 则返回 null / The existing or newly added component; null if <paramref name="component"/> is null.</returns>
        public static T _GetOrAddComponent<T>(this Component component) where T : Component
        {
            if (component == null) return null;
            T c = component.gameObject.GetComponent<T>();
            if (c == null) return component.gameObject.AddComponent<T>();
            return c;
        }

        /// <summary>
        /// Fast variant: 不进行参数 null 检查，直接在 component.gameObject 上获取或添加组件。
        /// Fast variant: does not perform null checks; directly gets or adds component on component.gameObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _GetOrAddComponentFast<T>(this Component component) where T : Component
        {
            T c = component.gameObject.GetComponent<T>();
            if (c == null) return component.gameObject.AddComponent<T>();
            return c;
        }
        #endregion

        #region Transform 专属的
        /// <summary>
        /// 重置 Transform 为默认变换（位置 = (0,0,0)，旋转 = identity，缩放 = (1,1,1)）。
        /// Reset the transform to default values (position=(0,0,0), rotation=identity, scale=one).
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _Reset(this Transform transform)
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 重置 Transform 的本地变换（localPosition = (0,0,0)，localRotation = identity，localScale = one）。
        /// Reset local transform values to defaults.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _ResetLocal(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 设置世界空间的 X 坐标，保留其它分量不变。
        /// Set the world-space X position while preserving Y and Z.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        /// <param name="x">新的 X 值 / New X value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetPositionX(this Transform transform, float x)
        {
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
        }

        /// <summary>
        /// 设置世界空间的 Y 坐标，保留其它分量不变。
        /// Set the world-space Y position while preserving X and Z.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        /// <param name="y">新的 Y 值 / New Y value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetPositionY(this Transform transform, float y)
        {
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        /// <summary>
        /// 设置世界空间的 Z 坐标，保留其它分量不变。
        /// Set the world-space Z position while preserving X and Y.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        /// <param name="z">新的 Z 值 / New Z value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetPositionZ(this Transform transform, float z)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, z);
        }

        /// <summary>
        /// 设置本地坐标的 X 分量，保留其他分量。
        /// Set the local X position while preserving other local components.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        /// <param name="x">新的本地 X 值 / New local X value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetLocalPositionX(this Transform transform, float x)
        {
            transform.localPosition = new Vector3(x, transform.localPosition.y, transform.localPosition.z);
        }

        /// <summary>
        /// 设置本地坐标的 Y 分量，保留其他分量。
        /// Set the local Y position while preserving other local components.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        /// <param name="y">新的本地 Y 值 / New local Y value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetLocalPositionY(this Transform transform, float y)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, y, transform.localPosition.z);
        }

        /// <summary>
        /// 设置本地坐标的 Z 分量，保留其他分量。
        /// Set the local Z position while preserving other local components.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        /// <param name="z">新的本地 Z 值 / New local Z value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetLocalPositionZ(this Transform transform, float z)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, z);
        }

        /// <summary>
        /// 获取当前 Transform 的第一层子 Transform（不包含更深的孙子层级）。
        /// Get direct child transforms (one layer deep) of the given transform.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        /// <returns>直接子 Transform 的数组 / Array of direct child Transforms.</returns>
        public static Transform[] _GetChildrensOneLayer(this Transform transform)
        {
            Transform[] children = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                children[i] = transform.GetChild(i);
            return children;
        }

        /// <summary>
        /// Fast variant: 不进行 null 检查的第一层子节点获取。
        /// Fast variant: gets first-level child transforms without null checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Transform[] _GetChildrensOneLayerFast(this Transform transform)
        {
            Transform[] children = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                children[i] = transform.GetChild(i);
            return children;
        }

        /// <summary>
        /// 销毁当前 Transform 的所有子物体。播放模式下使用 <c>Destroy</c>，编辑器模式（非播放）下使用 <c>DestroyImmediate</c>。
        /// Destroy all child GameObjects. Uses Destroy in play mode and DestroyImmediate in edit mode.
        /// </summary>
        /// <param name="transform">目标 Transform / Target Transform.</param>
        public static void _DestroyAllChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject go = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(go);
#else
                    UnityEngine.Object.Destroy(go);
#endif
                }
            }
        }
        #endregion
    }
}

