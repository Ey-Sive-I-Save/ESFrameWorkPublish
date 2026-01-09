using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Vector3 扩展方法集合（保持原有方法签名，补全注释并添加内联提示）。
    /// 不改变语义，只提高可读性与性能提示。
    /// </summary>
    public static class ExtForVector
    {
        #region V3乘除操作
        /// <summary>
        /// 三轴分别相乘（对应分量相乘）。
        /// </summary>
        /// <param name="v">被乘向量。</param>
        /// <param name="vAxis">作为乘数的向量。</param>
        /// <returns>返回新的向量，其每个分量为对应分量的乘积。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _MutiVector3(this Vector3 v, Vector3 vAxis)
        {
            return new Vector3(v.x * vAxis.x, v.y * vAxis.y, v.z * vAxis.z);
        }

        /// <summary>
        /// 三轴分别安全除法：当除数分量为 0 时使用 1 作为替代，避免除零异常/Inf。
        /// </summary>
        /// <param name="v">被除向量。</param>
        /// <param name="divisor">除数向量。</param>
        /// <returns>返回新的向量，其分量为安全除法结果。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _SafeDivideVector3(this Vector3 v, Vector3 divisor)
        {
            if (divisor.x == 0f) divisor.x = 1f;
            if (divisor.y == 0f) divisor.y = 1f;
            if (divisor.z == 0f) divisor.z = 1f;
            return new Vector3(v.x / divisor.x, v.y / divisor.y, v.z / divisor.z);
        }

        /// <summary>
        /// 对应分量除法（高性能），调用者需保证除数分量不为零。
        /// </summary>
        /// <param name="v">被除向量。</param>
        /// <param name="divisor">除数向量（不得含 0 分量）。</param>
        /// <returns>返回每个分量做除法后的新向量。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _DivideVector3(this Vector3 v, Vector3 divisor)
        {
            return new Vector3(v.x / divisor.x, v.y / divisor.y, v.z / divisor.z);
        }
        #endregion

        #region 简单操作
        /// <summary>
        /// 将 Y 分量设置为 0 并返回新的向量副本。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <returns>返回 Y=0 的向量副本。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _NoY(this Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>
        /// 返回将 Y 分量替换后的向量副本。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <param name="y">新的 Y 值。</param>
        /// <returns>返回替换 Y 分量后的向量副本。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _WithY(this Vector3 v, float y)
        {
            v.y = y;
            return v;
        }

        /// <summary>
        /// 返回将 X 分量替换后的向量副本。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <param name="x">新的 X 值。</param>
        /// <returns>返回替换 X 分量后的向量副本。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _WithX(this Vector3 v, float x)
        {
            v.x = x;
            return v;
        }

        /// <summary>
        /// 返回将 Z 分量替换后的向量副本。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <param name="z">新的 Z 值。</param>
        /// <returns>返回替换 Z 分量后的向量副本。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _WithZ(this Vector3 v, float z)
        {
            v.z = z;
            return v;
        }

        /// <summary>
        /// 返回 Y 分量乘以系数后的向量副本。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <param name="yMulti">Y 的乘数。</param>
        /// <returns>返回 Y 分量变换后的新向量。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _WithYMuti(this Vector3 v, float yMulti)
        {
            v.y *= yMulti;
            return v;
        }

        /// <summary>
        /// 返回 X 分量乘以系数后的向量副本。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <param name="xMulti">X 的乘数。</param>
        /// <returns>返回 X 分量变换后的新向量。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _WithXMuti(this Vector3 v, float xMulti)
        {
            v.x *= xMulti;
            return v;
        }

        /// <summary>
        /// 返回 Z 分量乘以系数后的向量副本。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <param name="zMulti">Z 的乘数。</param>
        /// <returns>返回 Z 分量变换后的新向量。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _WithZMuti(this Vector3 v, float zMulti)
        {
            v.z *= zMulti;
            return v;
        }
        #endregion

        #region 其他
        /// <summary>
        /// 计算两个位置在水平面（XZ）上的距离。
        /// </summary>
        /// <param name="from">起点位置。</param>
        /// <param name="to">终点位置。</param>
        /// <returns>返回 XZ 平面上的欧氏距离。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _DistanceToHorizontal(this Vector3 from, Vector3 to)
        {
            return Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
        }

        /// <summary>
        /// 判断向量是否接近零向量，使用平方长度比较以避免开根运算。
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <param name="threshold">阈值，默认 0.001。</param>
        /// <returns>若向量长度小于阈值则返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsApproximatelyZero(this Vector3 v, float threshold = 0.001f)
        {
            return v.sqrMagnitude < threshold * threshold;
        }
        #endregion

        #region 高级几何与实用

        /// <summary>
        /// 在 XZ 平面上将 Vector3 转为 Vector2（x,z）。
        /// </summary>
        /// <param name="v">输入三维向量。</param>
        /// <returns>返回 (x,z) 的二维向量。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 _ToVector2XZ(this Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>
        /// 从 XZ 平面的 Vector2 构建 Vector3，Y 使用指定值（默认 0）。
        /// </summary>
        /// <param name="v2">输入二维向量（x -> x, y -> z）。</param>
        /// <param name="y">构建时的 Y 值。</param>
        /// <returns>返回构建的三维向量。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _FromXZ(this Vector2 v2, float y = 0f) => new Vector3(v2.x, y, v2.y);

        /// <summary>
        /// 计算从点 <paramref name="from"/> 指向点 <paramref name="to"/> 在 XZ 平面上的朝向角（以度为单位），参考 X 轴，范围 (-180,180]。
        /// 注意：结果以度为单位，正值表示从 X 轴逆时针方向。
        /// <para>示例（设置 GameObject 面向目标）：</para>
        /// <code>
        /// var angle = transform.position._AngleHorizontal(target.position);
        /// transform.rotation = Quaternion.Euler(0f, angle, 0f);
        /// </code>
        /// </summary>
        /// <param name="from">起点坐标。</param>
        /// <param name="to">目标坐标。</param>
        /// <returns>返回角度（度）。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _AngleHorizontal(this Vector3 from, Vector3 to)
        {
            var d = new Vector2(to.x - from.x, to.z - from.z);
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 计算两个方向向量在 XZ 平面上的带符号夹角（度）。
        /// <para>示例（判断目标在自身左侧还是右侧）：</para>
        /// <code>
        /// var forward = transform.forward;
        /// var dirToTarget = (target.position - transform.position).normalized;
        /// var signed = forward._SignedAngleXZ(dirToTarget);
        /// if (signed > 0) { /* 目标在左侧 */ } else { /* 目标在右侧或正前 */ }
        /// </code>
        /// </summary>
        /// <param name="fromDir">起始方向向量。</param>
        /// <param name="toDir">目标方向向量。</param>
        /// <returns>返回带符号角度（度），范围 [-180,180]。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _SignedAngleXZ(this Vector3 fromDir, Vector3 toDir)
        {
            var a = new Vector2(fromDir.x, fromDir.z);
            var b = new Vector2(toDir.x, toDir.z);
            if (a.sqrMagnitude == 0f || b.sqrMagnitude == 0f) return 0f;
            return Vector2.SignedAngle(a, b);
        }

        /// <summary>
        /// 判断两个向量是否近似相等（使用平方距离比较）。常用于位置/方向比较以避免浮点抖动导致的判断失败。
        /// <para>示例（判定单位是否到达目标位置）：</para>
        /// <code>
        /// if (unit.position._ApproxEquals(targetPos, 0.01f)) {
        ///     // 视为到达
        /// }
        /// </code>
        /// </summary>
        /// <param name="a">向量 A。</param>
        /// <param name="b">向量 B。</param>
        /// <param name="eps">容差（默认 1e-6）。</param>
        /// <returns>若两向量差的平方长度小于 eps^2 返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _ApproxEquals(this Vector3 a, Vector3 b, float eps = 1e-6f) => (a - b).sqrMagnitude <= eps * eps;

        /// <summary>
        /// 返回向量在 XZ 平面上的垂直向量（未归一化）： (x,z) -> (-z, x)。Y 分量置 0。
        /// <para>示例（计算角色侧向方向用于闪避/侧移）：</para>
        /// <code>
        /// var strafeDir = moveDirection._PerpendicularXZ().normalized;
        /// character.position += strafeDir * strafeSpeed * Time.deltaTime;
        /// </code>
        /// </summary>
        /// <param name="v">输入向量。</param>
        /// <returns>返回垂直向量。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _PerpendicularXZ(this Vector3 v) => new Vector3(-v.z, 0f, v.x);



        /// <summary>
        /// 仅在 XZ 平面上向目标移动，保持 Y 不变。
        /// </summary>
        /// <param name="current">当前位置。</param>
        /// <param name="target">目标位置。</param>
        /// <param name="maxDelta">最大位移。</param>
        /// <returns>返回新的位置，其 XZ 分量已向目标移动，Y 保持不变。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 _MoveTowardsXZ(this Vector3 current, Vector3 target, float maxDelta)
        {
            var cur = new Vector2(current.x, current.z);
            var tar = new Vector2(target.x, target.z);
            var res = Vector2.MoveTowards(cur, tar, maxDelta);
            return new Vector3(res.x, current.y, res.y);
        }
        #endregion

    }
}

