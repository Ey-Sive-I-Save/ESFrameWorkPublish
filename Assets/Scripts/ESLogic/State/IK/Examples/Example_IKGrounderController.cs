/*
 * ═══════════════════════════════════════════════════════════
 *  Example_IKGrounderController — 自动接地 IK
 * ═══════════════════════════════════════════════════════════
 *  【挂载】挂到角色 GameObject（同层级需有 StateFinalIKDriver）。
 *
 *  【Inspector 配置】
 *    groundCheckOrigin ── 射线起点 Transform（留空则用本 Transform）
 *    checkDistance     ── 向下检测距离（判断是否在地面上）
 *    groundMask        ── 地形层掩码（默认 DefaultRaycastLayers）
 *    forceGrounded     ── 强制启用接地（忽略射线，适合过场动画）
 *
 *  【运行行为】
 *    • 每帧向下发射射线；接地状态变化时才调用 HandleGrounder(bool)，
 *      避免每帧重复写 enabled，最小化调用开销。
 *    • Scene 视图选中时绘制绿/红 Gizmo 射线辅助调参。
 *
 *  【依赖】
 *    StateFinalIKDriver（同层级 GetComponentInParent 自动查找）
 * ═══════════════════════════════════════════════════════════
 */
using UnityEngine;

namespace ES.Examples
{
    /// <summary>
    /// 案例：GrounderBipedIK — 根据角色是否离地自动切换接地 IK。
    ///
    /// 使用方式：
    /// 1. 把本脚本挂到角色 GameObject（需同层级有 StateFinalIKDriver）。
    /// 2. 指定 groundCheckOrigin（通常为角色根节点或脚踝中点）。
    /// 3. 配置 groundMask（地形层）和 checkDistance（离地判定距离）。
    /// 4. 也可手动切换 forceGrounded 字段，用于过场动画等特殊场景。
    /// </summary>
    public sealed class Example_IKGrounderController : MonoBehaviour
    {
        [Header("离地检测")]
        [Tooltip("射线起点（留空则用本 Transform）")]
        public Transform groundCheckOrigin;

        [Tooltip("向下检测距离")]
        public float checkDistance = 0.3f;

        [Tooltip("地形层级掩码")]
        public LayerMask groundMask = Physics.DefaultRaycastLayers;

        [Header("手动覆盖")]
        [Tooltip("强制启用接地（忽略射线，适合过场等）")]
        public bool forceGrounded = false;

        private StateFinalIKDriver _driver;
        private bool _wasGrounded = true;

        private void Awake()
        {
            _driver = GetComponentInParent<StateFinalIKDriver>();
        }

        private void Update()
        {
            bool grounded = forceGrounded || CheckGrounded();

            // 状态变化时才调用，避免每帧写 enabled
            if (grounded != _wasGrounded)
            {
                _driver.HandleGrounder(grounded);
                _wasGrounded = grounded;
            }
        }

        private bool CheckGrounded()
        {
            Vector3 origin = groundCheckOrigin != null
                ? groundCheckOrigin.position
                : transform.position;

            return Physics.Raycast(origin, Vector3.down, checkDistance, groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = groundCheckOrigin != null
                ? groundCheckOrigin.position
                : transform.position;

            Gizmos.color = _wasGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(origin, origin + Vector3.down * checkDistance);
        }
    }
}
