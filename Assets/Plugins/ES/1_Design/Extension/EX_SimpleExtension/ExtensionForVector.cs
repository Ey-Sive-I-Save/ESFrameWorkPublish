using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtensionForVector
    {
        public static Vector3 _NoY(this Vector3 v)
        {
            v.y = 0;
            return v;
        }
        public static Vector3 _WithYFit(this Vector3 v, float Fit)
        {
            v += v.normalized._NoY() * Fit;
            return v;
        }
        public static Vector3 _WithYCut(this Vector3 v, float Fit_)
        {
            v = Vector3.Lerp(v, v._NoY().normalized * v.magnitude, Fit_);
            return v;
        }
        public static Vector3 _MutiVector3(this Vector3 v, Vector3 v2)
        {
            return new Vector3(v.x * v2.x, v.y * v2.y, v.z * v2.z);
        }
        public static Vector3 _DivideVector3(this Vector3 v, Vector3 v2)
        {
            return new Vector3(v.x ._SafeDivide( v2.x), v.y._SafeDivide(v2.y), v.z._SafeDivide(v2.z));
        }
        // 11. 设置Vector3的Y值
        public static Vector3 _WithY(this Vector3 v, float y)
        {
            v.y = y;
            return v;
        }

        // 12. 设置Vector3的X值
        public static Vector3 _WithX(this Vector3 v, float x)
        {
            v.x = x;
            return v;
        }

        // 13. 设置Vector3的Z值
        public static Vector3 _WithZ(this Vector3 v, float z)
        {
            v.z = z;
            return v;
        }

        // 14. 乘以Vector3的Y值
        public static Vector3 _WithYMuti(this Vector3 v, float yMulti)
        {
            v.y *= yMulti;
            return v;
        }

        // 15. 乘以Vector3的X值
        public static Vector3 _WithXMuti(this Vector3 v, float xMulti)
        {
            v.x *= xMulti;
            return v;
        }

        // 16. 乘以Vector3的Z值
        public static Vector3 _WithZMuti(this Vector3 v, float zMulti)
        {
            v.z *= zMulti;
            return v;
        }

        // 17. 忽略Y轴的Vector3
        public static Vector3 _IgnoreY(this Vector3 v)
        {
            return new Vector3(v.x, 0, v.z);
        }

        // 18. 计算方向向量
        public static Vector3 _DirectionTo(this Vector3 from, Vector3 to)
        {
            return (to - from).normalized;
        }

        // 19. 计算水平距离
        public static float _HorizontalDistanceTo(this Vector3 from, Vector3 to)
        {
            return Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
        }

        // 20. 检查Vector3是否近似为零
        public static bool _IsApproximatelyZero(this Vector3 v, float threshold = 0.001f)
        {
            return v.sqrMagnitude < threshold * threshold;
        }
    }
}

