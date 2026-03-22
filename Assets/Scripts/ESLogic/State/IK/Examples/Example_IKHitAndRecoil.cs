/*
 * ═══════════════════════════════════════════════════════════
 *  Example_IKHitAndRecoil — 受击体态 + 武器后坐力
 * ═══════════════════════════════════════════════════════════
 *  【挂载】挂到角色 GameObject（同层级需有 StateFinalIKDriver）。
 *         脚本自带 RequireComponent(Collider)，需保证碰撞体存在。
 *
 *  【Inspector 配置】
 *    fireKey         ── 触发后坐力的按键（默认 Mouse0）
 *    recoilMagnitude ── 后坐力强度（0~2），实际幅度由 Recoil 曲线决定
 *    debugHitForce   ── 调试受击力方向（世界空间向量）
 *    debugHitKey     ── 手动触发一次受击的调试键（默认 H）
 *
 *  【HitReaction 前提】
 *    Driver Inspector 中必须配置好 hitPoints（Collider 列表），
 *    碰撞 Collider 需与列表中某项匹配，否则不产生效果。
 *
 *  【运行行为】
 *    • 按 fireKey            → HandleRecoil(magnitude)
 *    • 按 debugHitKey        → HandleHit(col, debugHitForce, position)
 *    • OnCollisionEnter 真实 → 自动匹配碰撞点 Collider 并调用 HandleHit
 *
 *  【依赖】
 *    StateFinalIKDriver（同层级 GetComponentInParent 自动查找）
 * ═══════════════════════════════════════════════════════════
 */
using UnityEngine;

namespace ES.Examples
{
    /// <summary>
    /// 案例：HitReaction（受击体态） + Recoil（武器后坐力）。
    ///
    /// 使用方式：
    /// 1. 把本脚本挂到角色 GameObject（需同层级有 StateFinalIKDriver）。
    /// 2. 碰撞体进入时自动触发受击；按 fireKey 触发后坐力。
    /// 3. recoilMagnitude 可在 Inspector 实时调节。
    ///
    /// 注意：HitReaction 需在 Driver Inspector 中配置好 hitPoints（Collider 列表），
    /// 碰撞的 Collider 必须与其中某个匹配，才会产生效果。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Example_IKHitAndRecoil : MonoBehaviour
    {
        [Header("Recoil")]
        [Tooltip("按此键模拟开枪，触发后坐力")]
        public KeyCode fireKey = KeyCode.Mouse0;

        [Range(0f, 2f)]
        [Tooltip("后坐力强度（0~2，由 Recoil Inspector 曲线决定实际幅度）")]
        public float recoilMagnitude = 1f;

        [Header("HitReaction 调试")]
        [Tooltip("模拟受击力方向（世界空间）")]
        public Vector3 debugHitForce = new Vector3(0f, 0f, 500f);

        [Tooltip("按此键在编辑器内手动触发一次受击动作，便于调参")]
        public KeyCode debugHitKey = KeyCode.H;

        private StateFinalIKDriver _driver;
        private Collider _col;

        private void Awake()
        {
            _driver = GetComponentInParent<StateFinalIKDriver>();
            _col    = GetComponent<Collider>();
        }

        private void Update()
        {
            // 开枪 → 后坐力
            if (Input.GetKeyDown(fireKey))
                _driver.HandleRecoil(recoilMagnitude);

            // 调试：手动受击
            if (Input.GetKeyDown(debugHitKey))
                _driver.HandleHit(_col, debugHitForce, transform.position);
        }

        // 真实碰撞时触发受击
        private void OnCollisionEnter(Collision collision)
        {
            if (!_driver.IsHitReactionReady) return;

            Vector3 force = collision.impulse;
            Vector3 point = collision.GetContact(0).point;

            // 遍历本物体上所有 Collider，找到被击中的那个（FinalIK HitReaction 按 Collider 匹配）
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                foreach (var contact in collision.contacts)
                {
                    if (contact.thisCollider == col)
                    {
                        _driver.HandleHit(col, force, point);
                        return;
                    }
                }
            }
        }
    }
}
