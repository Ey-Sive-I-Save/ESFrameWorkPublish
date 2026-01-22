using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        /// <summary>
        /// Transform变换器 - 提供复杂的Transform操作工具
        /// 【已实现】✅ 基础的父级设置和位置/旋转/缩放初始化
        /// 【建议扩展】以下功能可在未来版本中添加：
        /// - 批量Transform操作（位置/旋转/缩放）
        /// - Transform层级遍历和查询
        /// - 坐标空间转换（世界⇄本地）
        /// - Transform路径字符串生成
        /// - Transform插值和平滑移动
        /// </summary>
        public static class TransformSetter
        {
            /// <summary>
            /// 【可用】✅ 仅设置父级，并可选重置局部旋转/缩放；不会修改位置。
            /// </summary>
            /// <param name="me">要操作的Transform对象</param>
            /// <param name="parent">目标父级Transform，若为null则不改变父级</param>
            /// <param name="localRot0">是否重置局部旋转为Quaternion.identity（默认true）</param>
            /// <param name="localScale0">是否重置局部缩放为Vector3.one（默认true）</param>
            /// <remarks>
            /// 用于“换父级但保留当前位置”的场景；避免误用带 pos 默认值的重载导致位置归零。
            /// </remarks>
            public static void HandleTransformAtParent(Transform me, Transform parent, bool localRot0 = true, bool localScale0 = true)
            {
                if (me == null) return;
                if (parent != null) me.SetParent(parent, worldPositionStays: true);
                if (localRot0) me.localRotation = Quaternion.identity;
                if (localScale0) me.localScale = Vector3.one;
            }

            /// <summary>
            /// 【可用】✅ 批量版本：对多个 Transform 执行与 <see cref="HandleTransformAtParent(Transform, Transform, Vector3?, bool, bool, bool)"/> 相同的逻辑。
            /// </summary>
            public static void HandleTransformsAtParent(IEnumerable<Transform> transforms, Transform parent, Vector3? pos = null, bool atWorldPos = true, bool localRot0 = true, bool localScale0 = true)
            {
                if (transforms == null) return;

                foreach (var t in transforms)
                {
                    HandleTransformAtParent(t, parent, pos, atWorldPos, localRot0, localScale0);
                }
            }

            /// <summary>
            /// 【可用】✅ 操作一个Transform依赖父级，并进行位置/旋转/缩放的初始化设置
            /// </summary>
            /// <param name="me">要操作的Transform对象</param>
            /// <param name="parent">目标父级Transform，若为null则不改变父级</param>
            /// <param name="pos">设置的位置，默认为Vector3.zero</param>
            /// <param name="atWorldPos">true表示设置世界坐标，false表示设置本地坐标</param>
            /// <param name="localRot0">是否重置局部旋转为Quaternion.identity（默认true）</param>
            /// <param name="localScale0">是否重置局部缩放为Vector3.one（默认true）</param>
            /// <remarks>
            /// 典型使用场景：
            /// 1. UI元素挂载到Canvas下并重置位置
            /// 2. 对象池对象回收时重置Transform状态
            /// 3. 预制体实例化后的初始化配置
            /// </remarks>
            public static void HandleTransformAtParent(Transform me, Transform parent, Vector3? pos = null, bool atWorldPos = true, bool localRot0 = true, bool localScale0 = true)
            {
                if (me == null) return;

                if (parent != null)
                {
                    bool willWritePosition = pos.HasValue;
                    me.SetParent(parent, worldPositionStays: !willWritePosition);
                }

                if (pos.HasValue)
                {
                    if (atWorldPos) me.position = pos.Value;
                    else me.localPosition = pos.Value;
                }
                if (localRot0) me.localRotation = Quaternion.identity;
                if (localScale0) me.localScale = Vector3.one;
            }
        }
    }
}

