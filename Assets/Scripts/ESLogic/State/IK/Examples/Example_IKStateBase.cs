/*
 * ═══════════════════════════════════════════════════════════
 *  Example_IKStateBase — 右手抓握 + 注视（事件钩子版）
 * ═══════════════════════════════════════════════════════════
 *  【挂载】挂到任意 GameObject（无位置要求）。
 *
 *  【Inspector 配置】
 *    entity        ── 目标角色 Entity（状态机从 entity.stateDomain.stateMachine 自动取）
 *    grabTarget    ── 右手要到达的抓握点 Transform（必填）
 *    elbowHint     ── 肘部引导方向 Transform（可选，留空则忽略）
 *    grabWeight    ── 右手 IK 强度（0~1）
 *    lookAtWeight  ── 注视抓握点权重（0=不看；当已有 LookAt 权重更高时不覆盖）
 *    activateKey   ── 开启抓握的按键（默认 G）
 *    deactivateKey ── 关闭抓握的按键（默认 T）
 *
 *  【运行行为】
 *    按 activateKey 后，每帧在 OnStateGeneralFinalIKDriverPosePostProcess 钩子中写入：
 *      pose.rightHand.{weight / position / rotation / hintPosition}
 *      pose.lookAt*（仅当已有权重 < lookAtWeight 时才覆盖）
 *    按 deactivateKey 或 OnDisable 时停止写入，IK 自然恢复。
 *
 *  【本示例说明】
 *    演示「不继承 StateBase」的外部 IK 注入模式——
 *    通过 OnStateGeneralFinalIKDriverPosePostProcess 直接修改聚合后的 IK 快照，
 *    与现有状态机完全解耦，可随时挂载/移除。
 *
 *  【依赖】
 *    Entity → entity.stateDomain.stateMachine（Awake 缓存）
 * ═══════════════════════════════════════════════════════════
 */
using UnityEngine;

namespace ES.Examples
{
    /// <summary>
    /// 案例：右手抓握 IK — 通过 OnStateGeneralFinalIKDriverPosePostProcess 钩子直接注入 stateGeneralFinalIKDriverPose，
    /// 无需继承 StateBase，完全解耦。
    ///
    /// 使用方式：
    /// 1. 把本脚本挂到任意 GameObject。
    /// 2. 在 Inspector 中将角色的 Entity 拖入 entity 字段。
    /// 3. 指定 grabTarget（抓握点）和可选的 elbowHint（肘部引导）。
    /// 4. 运行时按 activateKey 开启抓握，deactivateKey 关闭。
    ///
    /// 与现有系统的关系：
    /// 仅通过事件订阅接入，OnEnable/OnDisable 自动管理，零侵入。
    /// </summary>
    public sealed class Example_IKStateBase : MonoBehaviour
    {
        [Header("目标实体")]
        public Entity entity;

        private StateMachine _stateMachine;

        [Header("IK 目标")]
        [Tooltip("右手要到达的抓握点 Transform")]
        public Transform grabTarget;
        [Tooltip("肘部引导方向 Transform（可选）")]
        public Transform elbowHint;

        [Range(0f, 1f)]
        public float grabWeight = 1f;

        [Header("注视")]
        [Range(0f, 1f)]
        [Tooltip("看向抓握点的权重（0=不看）")]
        public float lookAtWeight = 0.6f;

        [Header("控制")]
        public KeyCode activateKey   = KeyCode.G;
        public KeyCode deactivateKey = KeyCode.T;

        private bool _active;

        private void Awake()
        {
            _stateMachine = entity != null ? entity.stateDomain?.stateMachine : null;
        }

        private void OnEnable()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateGeneralFinalIKDriverPosePostProcess += OnPostProcess;
        }

        private void OnDisable()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateGeneralFinalIKDriverPosePostProcess -= OnPostProcess;
            _active = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(activateKey))   _active = true;
            if (Input.GetKeyDown(deactivateKey)) _active = false;
        }

        private void OnPostProcess(StateMachine machine, ref StateGeneralFinalIKDriverPose pose, float delta)
        {
            if (!_active || grabTarget == null) return;

            // 注入右手 IK（直接写入聚合后的 stateGeneralFinalIKDriverPose）
            pose.rightHand.weight      = grabWeight;
            pose.rightHand.position    = grabTarget.position;
            pose.rightHand.rotation    = grabTarget.rotation;
            if (elbowHint != null)
                pose.rightHand.hintPosition = elbowHint.position;

            // 注视抓握点（仅当已有 LookAt 权重更低时才覆盖）
            if (lookAtWeight > 0f && pose.lookAtWeight < lookAtWeight)
            {
                pose.lookAtWeight      = lookAtWeight;
                pose.lookAtPosition    = grabTarget.position;
                pose.lookAtBodyWeight  = 0.3f;
                pose.lookAtHeadWeight  = 1f;
                pose.lookAtEyesWeight  = 1f;
                pose.lookAtClampWeight = 0.5f;
            }
        }
    }
}
