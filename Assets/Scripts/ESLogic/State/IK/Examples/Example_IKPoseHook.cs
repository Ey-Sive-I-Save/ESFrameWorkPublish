/*
 * ═══════════════════════════════════════════════════════════
 *  Example_IKPoseHook — 事件钩子强制注入 LookAt
 * ═══════════════════════════════════════════════════════════
 *  【挂载】挂到任意 GameObject（无位置要求）。
 *
 *  【Inspector 配置】
 *    entity          ── 目标角色 Entity（状态机从 entity.stateDomain.stateMachine 自动取）
 *    lookTarget      ── 注视目标 Transform（留空则钩子不生效）
 *    lookWeight      ── 注视总权重（0~1）
 *    lookBodyWeight  ── 躯干权重（0~1）
 *    lookHeadWeight  ── 头部权重（0~1）
 *    lookEyesWeight  ── 眼睛权重（0~1）
 *    lookClampWeight ── 视角夹角钳制（0~1；越小转头越少）
 *
 *  【运行行为】
 *    订阅 OnStateGeneralFinalIKDriverPosePostProcess，在所有状态 IK 聚合完毕后、
 *    Driver 应用到 FinalIK 之前执行。
 *    实际 lookWeight = Max(聚合结果, lookWeight)，本钩子始终优先。
 *    Enable/Disable 自动管理订阅，零侵入。
 *
 *  【典型用途】强制角色看向摄像机 / 对话对象 / UI 标记点。
 *
 *  【依赖】
 *    Entity → entity.stateDomain.stateMachine（Awake 缓存）
 * ═══════════════════════════════════════════════════════════
 */
using UnityEngine;

namespace ES.Examples
{
    /// <summary>
    /// 案例：通过 StateMachine.OnStateGeneralFinalIKDriverPosePostProcess 事件钩子，
    /// 在所有状态的 IK 数据聚合完毕后、Driver 应用到 FinalIK 之前，
    /// 直接修改 stateGeneralFinalIKDriverPose。
    ///
    /// 典型用途：
    /// - 强制覆盖 LookAt 方向（例如：总是看向摄像机）
    /// - 在多个状态的 IK 结果之上叠加额外的注视偏移
    /// - 临时屏蔽某肢体 IK（weight → 0）
    ///
    /// 使用方式：
    /// 1. 把本脚本挂到任意 GameObject。
    /// 2. 在 Inspector 中将角色的 Entity 拖入 entity 字段。
    /// 3. 指定 lookTarget（为空时钩子不生效）。
    ///
    /// 与现有系统的关系：
    /// 仅通过事件订阅接入，零侵入，可随时 Enable/Disable。
    /// </summary>
    public sealed class Example_IKPoseHook : MonoBehaviour
    {
        [Header("目标实体")]
        [Tooltip("拖入角色的 Entity，状态机将从 entity.stateDomain.stateMachine 自动获取")]
        public Entity entity;

        private StateMachine _stateMachine;

        [Header("LookAt 注入")]
        [Tooltip("注视目标（留空则跳过注入）")]
        public Transform lookTarget;

        [Range(0f, 1f)]
        public float lookWeight      = 1f;
        [Range(0f, 1f)]
        public float lookBodyWeight  = 0.3f;
        [Range(0f, 1f)]
        public float lookHeadWeight  = 1f;
        [Range(0f, 1f)]
        public float lookEyesWeight  = 1f;
        [Range(0f, 1f)]
        public float lookClampWeight = 0.5f;

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
        }

        // delta 由 StateMachine 每帧传入，与 Time.deltaTime 相同
        private void OnPostProcess(StateMachine machine, ref StateGeneralFinalIKDriverPose pose, float delta)
        {
            if (lookTarget == null || lookWeight <= 0f) return;

            // 覆盖写入 LookAt（权重取最大值，让本钩子始终优先于状态机聚合结果）
            float w = Mathf.Max(pose.lookAtWeight, lookWeight);
            pose.lookAtWeight     = w;
            pose.lookAtPosition   = lookTarget.position;
            pose.lookAtBodyWeight = lookBodyWeight;
            pose.lookAtHeadWeight = lookHeadWeight;
            pose.lookAtEyesWeight = lookEyesWeight;
            pose.lookAtClampWeight = lookClampWeight;
        }
    }
}
