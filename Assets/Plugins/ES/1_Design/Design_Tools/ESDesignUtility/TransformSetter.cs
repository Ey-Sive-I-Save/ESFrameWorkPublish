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
            public static void HandleTransformAtParent(Transform t, Transform parent, Vector3 pos = default, bool atWorld = true, bool localRot0 = true, bool localScale0 = true)
            {
                if (t == null) return;
                if (parent != null) t.SetParent(parent);
                if (pos != null)
                {
                    if (atWorld) t.position = pos;
                    else t.localPosition = pos;
                }
                if (localRot0) t.localRotation = Quaternion.identity;
                if (localScale0) t.localScale = Vector3.one;
            }
        }
    }
}

