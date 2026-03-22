/*
 * ═══════════════════════════════════════════════════════════
 *  Example_IKAimController — AimIK 对准目标
 * ═══════════════════════════════════════════════════════════
 *  【挂载】挂到与 StateFinalIKDriver 同一 GameObject 或其子级。
 *
 *  【Inspector 配置】
 *    aimTarget  ── 武器/手臂要对准的世界 Transform（运行时可换）
 *    aimWeight  ── AimIK 强度（0~1）
 *    autoAim    ── 是否自动每帧追踪目标
 *    toggleKey  ── 切换 autoAim 的按键（默认 F）
 *
 *  【运行行为】
 *    • autoAim=true  → 每帧调用 HandleAim(target, weight)；
 *      Driver 内部有心跳超时，停止调用后权重自动衰减为 0。
 *    • autoAim=false → 调用 HandleStopAim()，立即停止。
 *    • 按 toggleKey  → 切换 autoAim 开/关。
 *
 *  【依赖】
 *    StateFinalIKDriver（同层级 GetComponentInParent 自动查找）
 * ═══════════════════════════════════════════════════════════
 */
using UnityEngine;

namespace ES.Examples
{
    /// <summary>
    /// 案例：AimIK — 每帧驱动武器/手臂对准目标 Transform。
    ///
    /// 使用方式：
    /// 1. 把本脚本挂到与 StateFinalIKDriver 同一 GameObject（或其层级内）。
    /// 2. 在 Inspector 中指定 aimTarget（可在运行时动态更换）。
    /// 3. autoAim=true 时自动追踪目标；按 toggleKey 切换开启/关闭。
    ///
    /// 与现有系统的关系：
    /// 仅通过 StateFinalIKDriver 的公开 API 通信，完全解耦。
    /// </summary>
    public sealed class Example_IKAimController : MonoBehaviour
    {
        [Header("AimIK 目标")]
        [Tooltip("武器/手臂要对准的世界空间 Transform")]
        public Transform aimTarget;

        [Range(0f, 1f)]
        [Tooltip("AimIK 强度")]
        public float aimWeight = 1f;

        [Header("开关控制")]
        [Tooltip("是否自动追踪目标（可在运行时切换）")]
        public bool autoAim = true;

        [Tooltip("按下此键切换 autoAim 状态")]
        public KeyCode toggleKey = KeyCode.F;

        private StateFinalIKDriver _driver;

        private void Awake()
        {
            _driver = GetComponentInParent<StateFinalIKDriver>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                autoAim = !autoAim;

            if (!autoAim || aimTarget == null)
            {
                _driver.HandleStopAim();
                return;
            }

            // HandleAim 每帧调用即可；driver 内部维护心跳超时，
            // 停止调用后会自动衰减权重到 0。
            _driver.HandleAim(aimTarget, aimWeight);
        }
    }
}
