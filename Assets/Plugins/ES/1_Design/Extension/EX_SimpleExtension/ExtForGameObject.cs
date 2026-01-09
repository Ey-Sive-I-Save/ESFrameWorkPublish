using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{


    public static class ExtForGameObject
    {
        /// <summary>
        /// 获取或者添加组件
        /// </summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="gameObject">目标 GameObject（允许为 null）。</param>
        /// <returns>组件实例或 null（当 gameObject 为 null）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 获取全部组件数组（包含所有 Component）。
        /// </summary>
        /// <param name="gameObject">目标 GameObject。</param>
        /// <returns>返回 Component 数组；若 gameObject 为 null 则返回空数组。</returns>
        public static Component[] _GetAllComponents(this GameObject gameObject)
        {
            return gameObject.GetComponents<Component>();
        }

        /// <summary>
        /// 安全地设置激活状态（避免重复调用 SetActive）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SafeSetActive(this GameObject gameObject, bool active)
        {
            if (gameObject == null) return;
            if (gameObject.activeSelf != active) gameObject.SetActive(active);
        }

        /// <summary>
        /// 安全切换激活状态（若 gameObject 为 null 不做操作）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SafeToggleActive(this GameObject gameObject)
        {
            if (gameObject == null) return;
            gameObject.SetActive(!gameObject.activeSelf);
        }

        /// <summary>
        /// 安全销毁
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="delay"></param>
        /// <summary>
        /// 安全销毁对象（在编辑器模式下会使用 DestroyImmediate，但延迟时仍使用 Destroy）。
        /// </summary>
        public static void _SafeDestroy(this GameObject gameObject, float delay = 0f)
        {
            if (gameObject == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying && delay <= 0f)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                return;
            }
#endif
            UnityEngine.Object.Destroy(gameObject, delay);
        }

        /// <summary>
        /// 安全设置层级
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="layer"></param>
        /// <param name="includeChildren"></param>
        /// <summary>
        /// 安全设置层级，可选择是否包含非激活子物体。
        /// </summary>
        public static void _SafeSetLayer(this GameObject gameObject, int layer, bool includeChildren = false, bool includeInactive = false)
        {
            if (gameObject == null) return;
            if (!includeChildren)
            {
                gameObject.layer = layer;
                return;
            }
            var transforms = gameObject.GetComponentsInChildren<Transform>(includeInactive);
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layer;
        }
        /// <summary>
        /// 判断是否在一个LaerMask下
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="mask"></param>
        /// <returns></returns>
        /// <summary>
        /// 判断 GameObject 所在层是否包含在 LayerMask 中（安全版本）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsInLayerMask(this GameObject gameObject, LayerMask mask)
        {
            return (mask.value & (1 << gameObject.layer)) != 0;
        }

        /// <summary>
        /// 将目标设置为父对象，同时保持世界坐标不变（可选择是否保持缩放/旋转）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetParentKeepWorld(this GameObject gameObject, Transform parent, bool keepScale = true)
        {
            if (gameObject == null) return;
            var t = gameObject.transform;
            if (parent == t.parent) return;

            // 防止创建父子循环
            // if (parent == t || (parent != null && parent.IsChildOf(t)))
            // {
            //     Debug.LogWarning("_SetParentKeepWorld：父对象为自身或其子对象，已取消以避免循环");
            //     return;
            // }

            var worldPos = t.position;
            var worldRot = t.rotation;
            var worldScale = ComputeApproximateWorldScale(t);
            t.SetParent(parent, true);
            // 确保位置/旋转被保留（SetParent 设置 worldPositionStays=true 通常会处理此项）
            t.position = worldPos;
            t.rotation = worldRot;
            if (keepScale)
            {
                // 根据父级的近似世界缩放恢复局部缩放
                var parentScale = parent == null ? Vector3.one : ComputeApproximateWorldScale(parent);
                var lx = Mathf.Approximately(parentScale.x, 0f) ? worldScale.x : worldScale.x / parentScale.x;
                var ly = Mathf.Approximately(parentScale.y, 0f) ? worldScale.y : worldScale.y / parentScale.y;
                var lz = Mathf.Approximately(parentScale.z, 0f) ? worldScale.z : worldScale.z / parentScale.z;
                t.localScale = new Vector3(lx, ly, lz);
            }
        }

        private static Vector3 ComputeApproximateWorldScale(Transform t)
        {
            if (t == null) return Vector3.one;
#if UNITY_2017_1_OR_NEWER
            // Transform.lossyScale exists on modern Unity; use it when available for better accuracy.
            try { return t.lossyScale; } catch { /* fallback below if unavailable */ }
#endif
            // Fallback: multiply local scales up the hierarchy (approximate; rotation may affect true world scale)
            var scale = t.localScale;
            var p = t.parent;
            while (p != null)
            {
                scale = Vector3.Scale(scale, p.localScale);
                p = p.parent;
            }
            return scale;
        }

        /// <summary>
        /// 查找名为 <paramref name="name"/> 的子对象；若不存在则创建并执行初始化回调。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject _FindOrCreateChild(this GameObject parent, string name, Action<GameObject> initAction = null)
        {
            if (parent == null) return null;
            var t = parent.transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            initAction?.Invoke(go);
            return go;
        }

       

        /// <summary>
        /// 销毁所有子对象（使用 Destroy 或 DestroyImmediate 依据编辑器/播放模式）。
        /// </summary>
        public static void _DestroyChildren(this GameObject gameObject, float delay = 0f)
        {
            var children = new List<GameObject>();
            for (int i = 0; i < gameObject.transform.childCount; i++) children.Add(gameObject.transform.GetChild(i).gameObject);
            for (int i = 0; i < children.Count; i++)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying && delay <= 0f)
                {
                    UnityEngine.Object.DestroyImmediate(children[i]);
                    continue;
                }
#endif
                UnityEngine.Object.Destroy(children[i], delay);
            }
        }

        /// <summary>
        /// 立即销毁所有子对象（编辑器下使用 DestroyImmediate）。
        /// </summary>
        public static void _DestroyChildrenImmediate(this GameObject gameObject)
        {
            var children = new List<GameObject>();
            for (int i = 0; i < gameObject.transform.childCount; i++) children.Add(gameObject.transform.GetChild(i).gameObject);
            for (int i = 0; i < children.Count; i++)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(children[i]);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(children[i]);
                }
            }
        }

        /// <summary>
        /// 递归设置激活状态（默认会影响当前及所有激活或非激活子对象）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _SetActiveRecursive(this GameObject gameObject, bool active)
        {
            if (gameObject == null) return;
            gameObject.SetActive(active);
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                gameObject.transform.GetChild(i).gameObject._SetActiveRecursive(active);
            }
        }

        /// <summary>
        /// 递归设置激活状态，可选择是否包含非激活子对象（通过 includeInactive 控制子对象遍历）。
        /// </summary>
        public static void _SetActiveRecursivelyIncludeInactive(this GameObject gameObject, bool active, bool includeInactive)
        {
            if (gameObject == null) return;
            // 若不包含非激活，则只遍历当前已激活的子树
            if (!includeInactive)
            {
                _SetActiveRecursive(gameObject, active);
                return;
            }
            // 包含非激活：使用 GetComponentsInChildren 来获得所有子节点（包含非激活）
            var transforms = gameObject.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.SetActive(active);
        }

    }

   
    [Flags]
    public enum TransformCopyFlags
    {
        None = 0,
        PositionLocal = 1 << 0,
        RotationLocal = 1 << 1,
        ScaleLocal = 1 << 2,
        PositionWorld = 1 << 3,
        RotationWorld = 1 << 4,
        LocalOnly = PositionLocal | RotationLocal | ScaleLocal,
        WorldOnly = PositionWorld | RotationWorld,
        All = LocalOnly | WorldOnly
    }

}

