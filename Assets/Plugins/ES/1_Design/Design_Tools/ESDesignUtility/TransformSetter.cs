using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //变换器
        public static class TransformSetter
        {
            /// <summary>
            /// 操作一个变换依赖父级
            /// </summary>
            /// <param name="me">操作</param>
            /// <param name="parent">父级</param>
            /// <param name="pos">位置</param>
            /// <param name="atWorldPos">是世界空间？</param>
            /// <param name="localRot0">局部旋转重置</param>
            /// <param name="localScale0">局部缩放重置</param>
            public static void HandleTransformAtParent(Transform me, Transform parent, Vector3 pos = default, bool atWorldPos = true, bool localRot0 = true, bool localScale0 = true)
            {
                if (me == null) return;
                if (parent != null) me.SetParent(parent);
                if (pos != null)
                {
                    if (atWorldPos) me.position = pos;
                    else me.localPosition = pos;
                }
                if (localRot0) me.localRotation = Quaternion.identity;
                if (localScale0) me.localScale = Vector3.one;
            }
        }
    }
}

