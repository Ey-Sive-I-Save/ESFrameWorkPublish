using ES;
using Sirenix.OdinInspector.Editor.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;

namespace ES
{

    public static class ExtensionForVector
    {
        #region V3乘除操作
        /// <summary>
        /// 三轴分别乘另外一个V3的三个轴值
        /// </summary>
        /// <param name="v"></param>
        /// <param name="vAxis"></param>
        /// <returns></returns>
        public static Vector3 _MutiVector3(this Vector3 v, Vector3 vAxis)
        {
            return new Vector3(v.x * vAxis.x, v.y * vAxis.y, v.z * vAxis.z);
        }
        /// <summary>
        /// 三轴分别除以另外一个V3的三个轴值
        /// </summary>
        /// <param name="v"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static Vector3 _SafeDivideVector3Safe(this Vector3 v, Vector3 divisor)
        {
            if (divisor.x == 0) divisor.x = 1;
            if (divisor.y == 0) divisor.y = 1;
            if (divisor.z == 0) divisor.z = 1;
            return new Vector3(v.x / divisor.x, v.y / divisor.y, v.z / divisor.z);
        }
        /// <summary>
        /// 高性能的Vector3分量除法。将第一个Vector3的每个分量除以第二个Vector3的对应分量。
        /// 注意：调用者需自行确保除数分量不为零，否则将得到 Infinity 或 NaN 结果。
        /// </summary>
        /// <param name="v">被除数的Vector3</param>
        /// <param name="divisor">作为除数的Vector3</param>
        /// <returns>计算结果的新Vector3</returns>
        public static Vector3 _DivideVector3(this Vector3 v, Vector3 divisor)
        {
            return new Vector3(v.x / divisor.x, v.y / divisor.y, v.z / divisor.z);
        }
        #endregion

        #region 简单操作
        /// <summary>
        /// 不要Y值，设置为0
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static Vector3 _NoY(this Vector3 v)
        {
            v.y = 0;
            return v;
        }
        /// <summary>
        /// Y修改（不是ref
        /// </summary>
        /// <param name="v"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        [BurstCompile(CompileSynchronously =true)]
        public static Vector3 _WithY(this Vector3 v, float y)
        {
            v.y = y;
            return v;
        }
        /// <summary>
        /// X修改（不是ref
        /// </summary>
        /// <param name="v"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        public static Vector3 _WithX(this Vector3 v, float x)
        {
            v.x = x;
            return v;
        }
        /// <summary>
        /// Z修改（不是ref
        /// </summary>
        /// <param name="v"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public static Vector3 _WithZ(this Vector3 v, float z)
        {
            v.z = z;
            return v;
        }
        /// <summary>
        /// Y乘上（不是ref
        /// </summary>
        /// <param name="v"></param>
        /// <param name="yMulti"></param>
        /// <returns></returns>
        public static Vector3 _WithYMuti(this Vector3 v, float yMulti)
        {
            v.y *= yMulti;
            return v;
        }
        /// <summary>
        /// X乘上（不是ref
        /// </summary>
        /// <param name="v"></param>
        /// <param name="xMulti"></param>
        /// <returns></returns>
        public static Vector3 _WithXMuti(this Vector3 v, float xMulti)
        {
            v.x *= xMulti;
            return v;
        }
        /// <summary>
        /// Z乘上（不是ref
        /// </summary>
        /// <param name="v"></param>
        /// <param name="zMulti"></param>
        /// <returns></returns>
        public static Vector3 _WithZMuti(this Vector3 v, float zMulti)
        {
            v.z *= zMulti;
            return v;
        }
        #endregion

        #region 其他
        /// <summary>
        /// 到一个点的水平距离
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static float _DistanceToHorizontal(this Vector3 from, Vector3 to)
        {
            return Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
        }

        /// <summary>
        /// 几乎为0？
        /// </summary>
        /// <param name="v"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static bool _IsApproximatelyZero(this Vector3 v, float threshold = 0.001f)
        {
            return v.sqrMagnitude < threshold * threshold;
        }
        #endregion
    }
}

