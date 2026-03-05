using System;
using System.Runtime.CompilerServices;
using UnityEngine;

// ============================================================================
// 文件：StateBase.MatchTarget.cs
// 作用：StateBase 的自定义 MatchTarget 封装。
//       不依赖 Animator.MatchTarget（兼容 KCC applyRootMotion=false 环境），
//       通过 KCC motor.SetPositionAndRotation 直接驱动 Entity 根节点插值到目标点。
//       支持所有 AvatarTarget 部位（Root / Body / 四肢）；非 Humanoid Rig 自动降级为根节点。
//
// ============================================================================
// ★ 驱动原理
// ============================================================================
//
// 【1. 为什么不用 Animator.MatchTarget】
//   Unity 原生 MatchTarget 依赖 Root Motion 驱动根节点。
//   KCC 环境下 applyRootMotion=false，Animator 根本不写根节点位移，原生接口完全无效。
//   本实现绕过动画系统，直接通过 motor.SetPositionAndRotation 写入 KCC 物理层。
//
// 【2. KCC 坐标系：必须区分这两个位置】
//   motor.TransientPosition    → KCC 物理真实位置，每次 Simulate 写入
//   entity.transform.position  → 插值渲染位置，PostSimulationInterpolation 写入，仅用于视觉
//   ProcessMatchTarget 在 Update 中运行，必须读写 TransientPosition。
//   若读 transform.position：每帧起点都被重置为上帧的插值旧位置，
//   SetPositionAndRotation 的结果立刻被下一次 Simulate 覆盖——看起来完全不动。
//
// 【3. 插值算法：MoveTowards / RotateTowards 速度驱动，终点始终为 live 目标】
//   每帧以 matchtargetTowardPosMutipler=3f 倍率和 positionApproachSpeed（单位/秒）进行：
//     newPos = MoveTowards(curPos, effectiveTargetPos, posSpeed×3f×dt)
//     newRot = RotateTowards(curRot, effectiveTargetRot, rotSpeed×1f×dt)
//   effectiveTargetPos = targetRootPos（每帧实时计算），不再使用首帧终点快照。
//   窗口首帧仅快照起点（snapshotPos）用于 Gizmos 可视化，不参与对齐计算。
//
// 【4. 为什么 live 目标对所有 bodyPart 均有效】
//   MoveTowards 是绝对速度驱动，不依赖 t∈[0,1] 区间稳定性。
//   骨骼随动画每帧移动 → effectiveTargetPos 也随之微量变化 → MoveTowards 自动跟随；
//   只要 endTime 前速度足够，最终误差可降到可接受范围。
//   对于 Root bodyPart（Mount 场景）：liveOffset=0，目标通过 SetMatchTargetTarget 每帧更新即可。
//
// 【5. liveOffset 的作用（bodyPart != Root 时）】
//   mt.position 是目标骨骼（手/脚/Body）的世界坐标，但 KCC 只能移动根节点。
//   框架将骨骼→根节点偏移转换到 Entity 局部空间，再以目标旋转重建世界偏移：
//     localBoneOffset = Inverse(entityTrs.rotation) * (entityTrs.position - bone.position)
//     liveOffset      = mt.rotation * localBoneOffset
//   这样在 Entity 旋转到 mt.rotation 后，指定骨骼精确落在 mt.position。
//   ★ 不能用 TransientPosition - bone.position：
//     TransientPos 在物理层超前于视觉骨骼，会随根节点推进增大，
//     形成正反馈，导致"围着目标绕圈"的发散行为。
//
// 【6. 速度与权重语义】
//   positionApproachSpeed = 位置逼近速度（单位/秒），内部乘以3倍率常量
//   rotationApproachSpeed = 旋转基础逼近速度（度/秒）
//   rotationWeight        = 旋转权重（0..1），最终旋转速度 = rotationApproachSpeed * rotationWeight
//
// 【7. 多阶段时序器 Config-Auto】
//   _configAutoPhaseIndex：-1=未启动，0=Phase1已启动，1=Phase2已启动
//   TickConfigAutoMatchTarget 每帧由 elapsed(hasEnterTime) 驱动：
//     elapsed >= p1.timeRange.x && index<0  → 启动 Phase1，index=0
//     elapsed >= p2.timeRange.x && index==0 → 启动 Phase2，index=1（无上限检查，防低帧率跳过）
//   Phase1 未启动时不检查 Phase2（防止 p2.timeRange.x=0 时在第一帧就抢占）。
//   每次 StartMatchTarget 清除 _matchTargetSnapshotTaken，
//   Phase 切换时自动在新窗口首帧重新快照起点（Gizmos 用途）。
//
// 【8. 目标位姿归属（关键约束）】
//   MatchTargetRequest 不再保存目标 Transform/固定位置/固定旋转。
//   目标位姿由调用方在运行时提供，并写入 _sharedMatchTargetPos/_sharedMatchTargetRot：
//   - 首次启动：ApplyMatchTarget(request, targetPos, targetRot) 或 StartMatchTargetFromConfig(targetPos, targetRot)
//   - 持续追踪：SetMatchTargetTargetWithConfigOffset(rawPos, rawRot)
//   Phase1/Phase2 共用同一份 raw 目标位姿，阶段差异只体现在各自 preset 的参数与 offset。
//
// ============================================================================
//
// Public（本文件定义的对外成员）：
//
// 【调试开关（全局）】
// - 是否输出调试日志：public static bool debugMatchTarget
// - 调试输出帧间隔：public static int debugMatchTargetFrameInterval
//
// 【启动/更新/取消】
// - 启动一次 MatchTarget：public void StartMatchTarget(...)
// - 统一入口（传入 Request + 目标位姿）：public void ApplyMatchTarget(MatchTargetRequest, Vector3, Quaternion)
// - 修改运行时目标点：public void SetMatchTargetTarget(pos, rot)
// - 修改目标并自动叠加配置 Offset：public void SetMatchTargetTargetWithConfigOffset(rawPos, rawRot)
// - 叠加 Entity 局部偏移：public void AddMatchTargetOffset(posOffset, rotOffsetY)
// - 取消：public void CancelMatchTarget()
//
// 【待执行指令（自动触发）】
// - 注册下一条指令：public void QueueNextMatchTarget(MatchTargetPendingCommand)
//
// 【状态查询】
// - 是否激活：public bool IsMatchTargetActive { get; }
// ============================================================================

namespace ES
{
    public partial class StateBase
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasYawOffset(float yaw)
            => Mathf.Abs(yaw) > 0.0001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Quaternion YawOffsetToQuaternion(float yaw)
            => Quaternion.Euler(0f, yaw, 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyPresetOffset(MatchTargetRequest preset, Quaternion playerRot, Vector3 rawPos, Quaternion rawRot,
            out Vector3 outPos, out Quaternion outRot)
        {
            outPos = (!preset.enablePositionOffset || preset.positionOffset == Vector3.zero)
                ? rawPos
                : rawPos + (playerRot * preset.positionOffset);
            outRot = (!preset.enableRotationOffset || !HasYawOffset(preset.rotationOffsetY))
                ? rawRot
                : rawRot * YawOffsetToQuaternion(preset.rotationOffsetY);
        }

        #region MatchTarget 运行时

        /// <summary>
        /// MatchTarget是否已激活
        /// </summary>
        [NonSerialized]
        private bool _matchTargetActive = false;

        [NonSerialized]
        private Vector3 _matchTargetLastAppliedPos = Vector3.zero;

        [NonSerialized]
        private Quaternion _matchTargetLastAppliedRot = Quaternion.identity;

        [NonSerialized]
        private float _matchTargetLastApplyTime = -999f;

        // ── 自定义 MatchTarget 运行时快照字段 ───────────────────────────────────────
        // 窗口首帧快照起点（KCC TransientPosition），用于 Gizmos 可视化显示起始位置。
        // 终点始终使用 live targetRootPos（always-live 策略），不再快照终点。
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>窗口首帧快照的根节点起始位置（motor.TransientPosition）。仅用于 Gizmos 可视化，不参与对齐计算。</summary>
        [NonSerialized] private Vector3    _matchTargetSnapshotPos;
        /// <summary>快照是否已完成。StartMatchTarget / Cancel / OnComplete 时清除，首帧触发快照。</summary>
        [NonSerialized] private bool       _matchTargetSnapshotTaken;

        /// <summary>待执行的 MatchTarget 指令（null = 无排队）。仅供代码驱动 API <see cref="QueueNextMatchTarget"/> 使用。</summary>
        [NonSerialized]
        private MatchTargetPendingCommand _pendingCommand;

        /// <summary>启动时缓存的 Inspector 配置局部空间位置偏移原始值（positionOffset，玩家局部坐标系），仅用于 debug 日志展示；零=无偏移或代码驱动模式。
        /// debug 块每帧用当前 playerRot 实时重算世界偏移，确保 Transform 目标移动后仍准确。</summary>
        [NonSerialized] private Vector3 _matchTargetDbgPosOffsetLocal;
        /// <summary>启动时缓存的旋转偏移 Yaw（rotationOffsetY，单位°），仅用于 debug 日志展示；0=无偏移或代码驱动模式。</summary>
        [NonSerialized] private float _matchTargetDbgRotOffsetY;
        /// <summary>调试用：记录本次 MatchTarget 的启动路径，便于分析偏移是否正确生效。
        /// 取值示例："Config-P1" / "Config-P2" / "Config-Auto-P1" / "Config-Auto-P2" / "ApplyRequest" / "Direct"</summary>
        [NonSerialized] private string _matchTargetDbgStartPath = "Direct";

        /// <summary>
        /// 运行时位置偏移开关（true=启用）。由配置的 enablePositionOffset 初始化，
        /// 调用 <see cref="AddMatchTargetOffset"/> 时自动置 true；可通过 <see cref="SetMatchTargetOffsetActive"/> 手动控制。
        /// </summary>
        [NonSerialized] private bool _matchTargetPosOffsetActive = true;
        /// <summary>
        /// 运行时旋转偏移开关（true=启用）。由配置的 enableRotationOffset 初始化，
        /// 调用 <see cref="AddMatchTargetOffset"/> 时自动置 true；可通过 <see cref="SetMatchTargetOffsetActive"/> 手动控制。
        /// </summary>
        [NonSerialized] private bool _matchTargetRotOffsetActive = true;

        /// <summary>
        /// Config-Auto 模式的当前阶段索引（时间序列驱动，重入安全）。
        /// -1 = 未启动；0 = Phase1 已开始；1 = Phase2 已开始。
        /// 该索引由 StateBase 内部维护，业务模块无需也不应手动触发 Phase2。
        /// </summary>
        [NonSerialized]
        private int _configAutoPhaseIndex = -1;

        /// <summary>
        /// 共享的原始目标位姿（不含分阶段 offset）。
        /// Phase1 / Phase2 均基于这一份 runtime 目标，阶段差异仅体现在各自 offset 与参数。
        /// </summary>
        [NonSerialized] private bool _hasSharedMatchTargetPose;
        [NonSerialized] private Vector3 _sharedMatchTargetPos;
        [NonSerialized] private Quaternion _sharedMatchTargetRot = Quaternion.identity;

        // 重施加阈值已迁移到 StateMachineConfig.matchTargetReapply（全局），不再在 State 实例上维护。

        public static bool debugMatchTarget = true;
        public static int debugMatchTargetFrameInterval = 1;

        /// <summary>
        /// 状态进入时重置 MatchTarget 时序器状态。
        /// ★ 此方法不依赖 _animationRuntime，因为 StateMachine 在 HotPlugStateToPlayable（CreatePlayable）
        ///   之前就会调用 OnStateEnter，此时 _animationRuntime 尚未创建。
        ///   必须在 runtime 创建之前就重置 _configAutoPhaseIndex，否则重入时序器无法触发任何阶段。
        /// </summary>
        private void ApplyMatchTargetConfigOnEnter()
        {
            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            if (animConfig == null || !animConfig.enableMatchTarget) return;
            if (!animConfig.autoActivateMatchTarget) return;

            // 重置阶段索引，时序器将从本帧起重新推进（支持状态重入）
            // 注意：这里不清空 _sharedMatchTargetPose；目标位姿由业务层在启动/追踪时写入。
            _configAutoPhaseIndex = -1;
            _matchTargetActive    = true;
        }

        /// <summary>
        /// 启动MatchTarget（根动作对齐到目标位置）
        /// 用于攀爬、跳跃落地等需要精确对齐的场景
        /// </summary>
        /// <param name="targetPos">目标位置</param>
        /// <param name="targetRot">目标旋转</param>
        /// <param name="bodyPart">身体部位</param>
        /// <param name="startNormTime">开始归一化时间 [0-1]</param>
        /// <param name="endNormTime">结束归一化时间 [0-1]</param>
        /// <param name="approachSpeed">位置逃近速度（单位/秒），<=0 表示不移动位置</param>
        /// <param name="approachAngleSpeed">旋转逃近速度（度/秒），<=0 表示不旋转</param>
        /// <param name="rotationWeight">旋转权重（0-1），最终旋转速度=approachAngleSpeed*rotationWeight</param>
        public void StartMatchTarget(Vector3 targetPos, Quaternion targetRot, AvatarTarget bodyPart,
            float startNormTime, float endNormTime, float approachSpeed, float approachAngleSpeed = 360f, float rotationWeight = 1f)
        {
            if (_animationRuntime == null)
            {
#if UNITY_EDITOR
                if (debugMatchTarget)
                {
                    Debug.LogWarning($"{GetMatchTargetLogTag()} Start failed: runtime null");
                }
#endif
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                var stateName = GetStateNameSafe();
                    dbg.LogWarning($"[StateBase] StartMatchTarget failed: runtime is null | State={stateName}");
                }
#endif
#endif
                return;
            }

#if UNITY_EDITOR
            if (debugMatchTarget)
            {
                Debug.Log(
                    $"{GetMatchTargetLogTag()} Start " +
                    $"pos={targetPos:F3} rot={targetRot.eulerAngles:F1} body={bodyPart} " +
                    $"time=[{startNormTime:F2},{endNormTime:F2}] approachSpeed={approachSpeed:F2} approachAngleSpeed={approachAngleSpeed:F2}");
            }
#endif

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            var dbgStart = StateMachineDebugSettings.Instance;
            if (dbgStart != null && dbgStart.IsAnimationBlendEnabled)
            {
                dbgStart.LogAnimationBlend(
                    $"{GetMatchTargetLogTag()} Start " +
                    $"pos={targetPos:F3} rot={targetRot.eulerAngles:F1} body={bodyPart} " +
                    $"time=[{startNormTime:F2},{endNormTime:F2}] approachSpeed={approachSpeed:F2} approachAngleSpeed={approachAngleSpeed:F2}");
            }
#endif
#endif

            _matchTargetActive           = true;
            _matchTargetLastAppliedPos   = Vector3.zero;
            _matchTargetLastAppliedRot   = Quaternion.identity;
            _matchTargetLastApplyTime    = -999f;
            _matchTargetSnapshotTaken    = false;          // 强制在窗口首帧重新快照
            _matchTargetDbgStartPath     = "Direct";      // 被高层 API 调用时会在后面立即覆盖
            _animationRuntime.StartMatchTarget(targetPos, targetRot, bodyPart, startNormTime, endNormTime,
                approachSpeed, approachAngleSpeed, rotationWeight);
        }

        /// <summary>
        // ================================================================
        // ★ 统一入口：ApplyMatchTarget —— 接收 MatchTargetRequest 数据包
        // ================================================================

        /// <summary>
        /// 统一 MatchTarget 应用入口。<br/>
        /// request 仅承载阶段参数与偏移；目标位置/旋转由调用方在运行时传入。
        /// <example><code>
        /// // 场景物体覆盖：
        /// state.ApplyMatchTarget(interactable.matchTargetRequest, interactable.transform.position, interactable.transform.rotation);
        ///
        /// // 代码构造：
        /// state.ApplyMatchTarget(new MatchTargetRequest {
        ///     bodyPart = AvatarTarget.Root,
        ///     timeRange = new Vector2(0.1f, 0.8f),
        ///     positionApproachSpeed = 3f,
        ///     rotationApproachSpeed = 360f,
        ///     rotationWeight      = 0.5f
        /// }, targetPos, targetRot);
        /// </code></example>
        /// </summary>
        public void ApplyMatchTarget(MatchTargetRequest request, Vector3 targetPos, Quaternion targetRot)
        {
            var playerRot = host.HostEntity.transform.rotation;
            // 同步调试缓存与偏移开关（非零自动开启，全零自动关闭）
            _matchTargetDbgPosOffsetLocal = request.positionOffset;
            _matchTargetDbgRotOffsetY = request.rotationOffsetY;
            _matchTargetPosOffsetActive   = request.positionOffset != Vector3.zero;
            _matchTargetRotOffsetActive   = HasYawOffset(request.rotationOffsetY);

            _hasSharedMatchTargetPose = true;
            _sharedMatchTargetPos = targetPos;
            _sharedMatchTargetRot = targetRot;

            ApplyPresetOffset(request, playerRot, targetPos, targetRot, out var effectivePos, out var effectiveRot);
            StartMatchTarget(
                effectivePos,
                effectiveRot,
                request.bodyPart,
                request.timeRange.x,
                request.timeRange.y,
                request.positionApproachSpeed,
                request.rotationApproachSpeed,
                request.rotationWeight);
            _matchTargetDbgStartPath = "ApplyRequest"; // StartMatchTarget 内会覆盖为 Direct，在此恢复
        }

        // ================================================================
        // ★ 简化 API：应用层只传目标位置/旋转，其余参数从 Inspector 读取
        // ================================================================

        /// <summary>
        /// 应用层简化接口（Phase1）：仅传目标位置/旋转，<br/>
        /// bodyPart / 时间范围 / 权重等参数自动从 Inspector <c>matchTargetPreset</c> 读取。
        /// <para>此接口是业务层首选启动入口（手动模式下由它启动 Phase1）。</para>
        /// <para>返回 <c>true</c> = 成功使用 Inspector 配置启动；<br/>
        /// 返回 <c>false</c> = 配置不可用（未启用 enableMatchTarget），调用方可降级到完整重载。</para>
        /// </summary>
        public bool StartMatchTargetFromConfig(Vector3 targetPos, Quaternion targetRot)
        {
            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            if (animConfig == null || !animConfig.enableMatchTarget)
            {
#if UNITY_EDITOR
                if (debugMatchTarget)
                    Debug.LogWarning($"{GetMatchTargetLogTag()} StartMatchTargetFromConfig: animConfig 未启用 MatchTarget，忽略（调用方可走 fallback）");
#endif
                return false;
            }

            var preset    = animConfig.matchTargetPreset;
            var playerRot = host.HostEntity.transform.rotation;
            ApplyPresetOffset(preset, playerRot, targetPos, targetRot, out var effectivePos, out var effectiveRot);

            _hasSharedMatchTargetPose = true;
            _sharedMatchTargetPos = targetPos;
            _sharedMatchTargetRot = targetRot;
            _configAutoPhaseIndex = 0;

            _matchTargetDbgPosOffsetLocal = preset.positionOffset;
            _matchTargetDbgRotOffsetY      = preset.rotationOffsetY;
            _matchTargetPosOffsetActive    = preset.positionOffset != Vector3.zero;    // 非零自动开启，全零自动关闭
            _matchTargetRotOffsetActive    = HasYawOffset(preset.rotationOffsetY);
            StartMatchTarget(
                effectivePos, effectiveRot,
                preset.bodyPart,
                preset.timeRange.x, preset.timeRange.y,
                preset.positionApproachSpeed, preset.rotationApproachSpeed, preset.rotationWeight);
            _matchTargetDbgStartPath = "Config-P1"; // StartMatchTarget 内会覆盖为 Direct，在此恢复
            return true;
        }

        /// <summary>
        /// 应用层简化接口（Phase2）：仅传目标位置/旋转，<br/>
        /// 参数从 Inspector <c>matchTargetPresetPhase2</c> 自动读取。
        /// <para>常规业务流不应主动调用此接口；Phase2 推荐由 StateBase 时序器自动推进。</para>
        /// <para>返回 <c>true</c> = 成功；<c>false</c> = Phase2 配置未启用，调用方可走 fallback。</para>
        /// </summary>
        public bool StartMatchTargetFromPhase2Config(Vector3 targetPos, Quaternion targetRot)
        {
            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            if (animConfig == null || !animConfig.enableMatchTarget || !animConfig.enableMatchTargetPhase2)
            {
#if UNITY_EDITOR
                if (debugMatchTarget)
                    Debug.LogWarning($"{GetMatchTargetLogTag()} StartMatchTargetFromPhase2Config: Phase2 配置未启用，忽略（调用方可走 fallback）");
#endif
                return false;
            }

            var preset    = animConfig.matchTargetPresetPhase2;
            var playerRot = host.HostEntity.transform.rotation;
            ApplyPresetOffset(preset, playerRot, targetPos, targetRot, out var effectivePos, out var effectiveRot);

            _hasSharedMatchTargetPose = true;
            _sharedMatchTargetPos = targetPos;
            _sharedMatchTargetRot = targetRot;
            _configAutoPhaseIndex = 1;

            _matchTargetDbgPosOffsetLocal = preset.positionOffset;
            _matchTargetDbgRotOffsetY      = preset.rotationOffsetY;
            _matchTargetPosOffsetActive    = preset.positionOffset != Vector3.zero;    // 非零自动开启，全零自动关闭
            _matchTargetRotOffsetActive    = HasYawOffset(preset.rotationOffsetY);
            StartMatchTarget(
                effectivePos, effectiveRot,
                preset.bodyPart,
                preset.timeRange.x, preset.timeRange.y,
                preset.positionApproachSpeed, preset.rotationApproachSpeed, preset.rotationWeight);
            _matchTargetDbgStartPath = "Config-P2"; // StartMatchTarget 内会覆盖为 Direct，在此恢复
            return true;
        }

        /// <summary>
        /// 取消MatchTarget
        /// </summary>
        public void CancelMatchTarget()
        {
            _matchTargetActive           = false;
            _pendingCommand              = null;
            _configAutoPhaseIndex        = -1;
            _matchTargetSnapshotTaken    = false;
            if (_animationRuntime != null)
            {
                _animationRuntime.ResetMatchTargetData();
            }
            _matchTargetLastAppliedPos    = Vector3.zero;
            _matchTargetLastAppliedRot    = Quaternion.identity;
            _matchTargetLastApplyTime     = -999f;
            _hasSharedMatchTargetPose     = false;
            _sharedMatchTargetPos         = Vector3.zero;
            _sharedMatchTargetRot         = Quaternion.identity;
            _matchTargetDbgPosOffsetLocal  = Vector3.zero;
            _matchTargetDbgRotOffsetY      = 0f;
            _matchTargetPosOffsetActive    = true;
            _matchTargetRotOffsetActive    = true;
            _matchTargetDbgStartPath       = "Direct";
        }

        /// <summary>
        /// MatchTarget是否处于活跃状态
        /// </summary>
        public bool IsMatchTargetActive => _matchTargetActive && _animationRuntime != null && _animationRuntime.matchTarget.active;

        // ================================================================
        // ★ 偏移开关 API
        // ================================================================

        /// <summary>
        /// 运行时手动控制位置/旋转偏移开关。
        /// 无需重启 MatchTarget，下一帧 <see cref="SetMatchTargetTargetWithConfigOffset"/> 调用时即生效。
        /// </summary>
        /// <param name="posActive">是否应用 positionOffset</param>
        /// <param name="rotActive">是否应用 rotationOffsetY（不传则与 posActive 一致）</param>
        public void SetMatchTargetOffsetActive(bool posActive, bool? rotActive = null)
        {
            _matchTargetPosOffsetActive = posActive;
            _matchTargetRotOffsetActive = rotActive ?? posActive;
        }

        // ================================================================
        // ★ 目标更新 API —— 修改运行时目标，不 new 新对象、不重新激活 MatchTarget
        // 适用：业务层已有活跃的 MatchTarget，目标点随环境变化需要纠正
        // ================================================================

        /// <summary>
        /// 同时修改运行时目标位置和旋转，不创建新对象。<br/>
        /// 追加在当前 runtime 位置/旋转上的任何展开偏移将被当前设置的 pos/rot 直接覆盖。
        /// </summary>
        public void SetMatchTargetTarget(Vector3 position, Quaternion rotation)
        {
            if (_animationRuntime == null) return;
            ref var mt = ref _animationRuntime.matchTarget;
            mt.position = position;
            mt.rotation = rotation;

            _hasSharedMatchTargetPose = true;
            _sharedMatchTargetPos = position;
            _sharedMatchTargetRot = rotation;
        }

        /// <summary>
        /// 修改运行时目标（传入 raw 位置/旋转），框架自动从当前激活阶段的 Inspector 配置读取
        /// <c>positionOffset</c> / <c>rotationOffsetY</c> 并叠加后再写入 runtime。<br/>
        /// <br/>
        /// ★ 适用场景：业务层每帧以动态 raw 位置更新 MatchTarget（如 Climb 跟踪墙面点），
        /// 同时又希望 Inspector 配置的偏移始终生效，不因每帧目标更新而被抹除。<br/>
        /// <br/>
        /// 阶段检测逻辑：若 <c>enableMatchTargetPhase2=true</c> 且
        /// <c>runtime.matchTarget.startTime &gt;= preset2.timeRange.x</c>，自动切换到 Phase2 配置；
        /// 否则使用 Phase1 配置（Config-Auto 与代码驱动模式均有效）。
        /// </summary>
        public void SetMatchTargetTargetWithConfigOffset(Vector3 rawPosition, Quaternion rawRotation)
        {
            if (_animationRuntime == null) return;
            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            var playerTrs = host.HostEntity.transform;

            _hasSharedMatchTargetPose = true;
            _sharedMatchTargetPos = rawPosition;
            _sharedMatchTargetRot = rawRotation;

            Vector3    pos = rawPosition;
            Quaternion rot = rawRotation;

            if (animConfig != null && animConfig.enableMatchTarget)
            {
                // 以 runtime startTime 判断当前激活阶段（Config-Auto 与代码驱动均适用）
                bool usePhase2 = animConfig.enableMatchTargetPhase2
                    && _animationRuntime.matchTarget.startTime >= animConfig.matchTargetPresetPhase2.timeRange.x;
                var preset = usePhase2 ? animConfig.matchTargetPresetPhase2 : animConfig.matchTargetPreset;

                if (_matchTargetPosOffsetActive && preset.positionOffset != Vector3.zero)
                    pos += playerTrs.rotation * preset.positionOffset;
                if (_matchTargetRotOffsetActive && HasYawOffset(preset.rotationOffsetY))
                    rot = rawRotation * YawOffsetToQuaternion(preset.rotationOffsetY);
            }

            SetMatchTargetTarget(pos, rot);
        }

        /// <summary>
        /// 在当前运行时目标上叠加 <b>Entity 局部空间</b> 偏移，不重置原始位置/旋转。<br/>
        /// - <c>positionOffset</c>：Entity 本地坐标偏移，内部自动通过 Entity 的世界旋转转换，
        ///   例如 (0,0,1) = "Entity 朝向前方 1 米"。<br/>
        /// - <c>rotationOffsetY</c>：Entity 本地 Yaw 偏移（度），以 Entity 当前朝向为基准前乘到目标旋转。<br/>
        /// 注意：偏移会直接叠加到运行时值上，下一次 <see cref="SetMatchTargetTarget"/> 调用时将全部覆盖。
        /// </summary>
        public void AddMatchTargetOffset(Vector3 positionOffset, float rotationOffsetY = 0f)
        {
            if (_animationRuntime == null) return;
            ref var mt = ref _animationRuntime.matchTarget;
            // 将 Entity 局部偏移转换为世界空间后叠加
            var entityRot = host.HostEntity.transform.rotation;
            if (positionOffset != Vector3.zero)
            {
                mt.position += entityRot * positionOffset;
                _matchTargetPosOffsetActive = true;   // 手动叠加自动开启开关
            }
            if (HasYawOffset(rotationOffsetY))
            {
                mt.rotation = (entityRot * YawOffsetToQuaternion(rotationOffsetY)) * mt.rotation;
                _matchTargetRotOffsetActive = true;   // 手动叠加自动开启开关
            }
        }

        // ================================================================
        // ★ Config-Auto 时间序列驱动（Time Sequencer）
        // 每帧根据 hasEnterTime 自动推进阶段，无事件、无队列、重入安全、零 GC。
        // ================================================================

        /// <summary>
        /// 每帧调用：根据 <paramref name="elapsed"/>（即 hasEnterTime）自动推进 Config-Auto 的多阶段 MatchTarget。<br/>
        /// 一旦 elapsed 到达某阶段的启动条件，且该阶段尚未启动，立即激活。<br/>
        /// 关键约束：Phase2 必须等待 Phase1 结束（elapsed &gt;= Phase1.timeRange.y）后才允许启动，
        /// 即使 Phase2.timeRange.x 更小也不会提前接管。
        /// </summary>
        private void TickConfigAutoMatchTarget(float elapsed)
        {
            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            if (animConfig == null || !animConfig.enableMatchTarget) return;
            if (_animationRuntime == null) return;
            if (!_hasSharedMatchTargetPose) return;

            var playerRot = host.HostEntity.transform.rotation;

            bool autoMode = animConfig.autoActivateMatchTarget;
            if (!autoMode)
            {
                if (!animConfig.enableMatchTargetPhase2 || _configAutoPhaseIndex != 0) return;

                var p1Manual = animConfig.matchTargetPreset;
                var p2Manual = animConfig.matchTargetPresetPhase2;
                float phase2GateTimeManual = Mathf.Max(p2Manual.timeRange.x, p1Manual.timeRange.y);
                if (elapsed < phase2GateTimeManual) return;

                _configAutoPhaseIndex = 1;
                _matchTargetDbgPosOffsetLocal = p2Manual.positionOffset;
                _matchTargetDbgRotOffsetY = p2Manual.rotationOffsetY;
                _matchTargetPosOffsetActive = p2Manual.positionOffset != Vector3.zero;
                _matchTargetRotOffsetActive = HasYawOffset(p2Manual.rotationOffsetY);

                ApplyPresetOffset(p2Manual, playerRot, _sharedMatchTargetPos, _sharedMatchTargetRot, out var p2PosManual, out var p2RotManual);
                StartMatchTarget(
                    p2PosManual, p2RotManual,
                    p2Manual.bodyPart, p2Manual.timeRange.x, p2Manual.timeRange.y,
                    p2Manual.positionApproachSpeed,
                    p2Manual.rotationApproachSpeed,
                    p2Manual.rotationWeight);
                _matchTargetDbgStartPath = "Config-Manual-P2";
#if UNITY_EDITOR
                if (debugMatchTarget)
                    Debug.Log($"{GetMatchTargetLogTag()} [Config-Manual] Phase2 自动接管 elapsed={elapsed:F3}s gate={phase2GateTimeManual:F3}s (需满足Phase1结束)");
#endif
                return;
            }

            // ★ 顺序说明：先处理 Phase1，再处理 Phase2。
            // Phase2 必须等 Phase1 已启动且达到 Phase1 结束时间后才能接管，
            // 即使 Phase2.timeRange.x 更小，也不允许越过 Phase1 结束边界。

            // Phase1：尚未开始且时间到达时启动
            if (_configAutoPhaseIndex < 0)
            {
                var p1 = animConfig.matchTargetPreset;
                if (elapsed >= p1.timeRange.x)
                {
                    _configAutoPhaseIndex = 0;
                    _matchTargetDbgPosOffsetLocal = p1.positionOffset;
                    _matchTargetDbgRotOffsetY = p1.rotationOffsetY;
                    _matchTargetPosOffsetActive   = p1.positionOffset != Vector3.zero;    // 非零自动开启，全零自动关闭
                    _matchTargetRotOffsetActive   = HasYawOffset(p1.rotationOffsetY);
                    ApplyPresetOffset(p1, playerRot, _sharedMatchTargetPos, _sharedMatchTargetRot, out var p1Pos, out var p1Rot);
                    StartMatchTarget(
                        p1Pos, p1Rot,
                        p1.bodyPart, p1.timeRange.x, p1.timeRange.y,
                        p1.positionApproachSpeed,
                        p1.rotationApproachSpeed,
                        p1.rotationWeight);
                    _matchTargetDbgStartPath = "Config-Auto-P1"; // 在 StartMatchTarget 覆盖后恢复
#if UNITY_EDITOR
                    if (debugMatchTarget)
                        Debug.Log($"{GetMatchTargetLogTag()} [Config-Auto] Phase1 启动 elapsed={elapsed:F3}s");
#endif
                }
                // Phase1 还没开始，不检查 Phase2（Phase2 必须在 Phase1 之后）
                return;
            }

            // Phase2：Phase1 已启动（index==0），且满足 “Phase1结束 + Phase2起始” 双门槛后才启动
            // ★ 不检查 elapsed <= p2.endTime：帧率低时可能一帧跳过整个窗口，导致 Phase2 永远不触发
            if (animConfig.enableMatchTargetPhase2 && _configAutoPhaseIndex == 0)
            {
                var p1 = animConfig.matchTargetPreset;
                var p2 = animConfig.matchTargetPresetPhase2;
                float phase2GateTime = Mathf.Max(p2.timeRange.x, p1.timeRange.y);
                if (elapsed >= phase2GateTime)
                {
                    _configAutoPhaseIndex = 1;
                    _matchTargetDbgPosOffsetLocal = p2.positionOffset;
                    _matchTargetDbgRotOffsetY = p2.rotationOffsetY;
                    _matchTargetPosOffsetActive   = p2.positionOffset != Vector3.zero;    // 非零自动开启，全零自动关闭
                    _matchTargetRotOffsetActive   = HasYawOffset(p2.rotationOffsetY);
                    ApplyPresetOffset(p2, playerRot, _sharedMatchTargetPos, _sharedMatchTargetRot, out var p2Pos, out var p2Rot);
                    StartMatchTarget(
                        p2Pos, p2Rot,
                        p2.bodyPart, p2.timeRange.x, p2.timeRange.y,
                        p2.positionApproachSpeed,
                        p2.rotationApproachSpeed,
                        p2.rotationWeight);
                    _matchTargetDbgStartPath = "Config-Auto-P2"; // 在 StartMatchTarget 覆盖后恢复
#if UNITY_EDITOR
                    if (debugMatchTarget)
                        Debug.Log($"{GetMatchTargetLogTag()} [Config-Auto] Phase2 启动 elapsed={elapsed:F3}s gate={phase2GateTime:F3}s (需满足Phase1结束)");
#endif
                }
            }
        }

        // ================================================================
        // ★ 代码驱动待执行指令 API（与 Config-Auto 时序器相互独立）
        // ================================================================

        /// <summary>
        /// 注册一条待执行的 MatchTarget 指令（替换当前排队中的指令）。<br/>
        /// 当 <c>hasEnterTime &gt;= cmd.triggerAt</c> 时自动激活，用 cmd.request 重新启动 MatchTarget。<br/>
        /// 状态退出 / Cancel / 完成时自动清除；传 <c>null</c> 可手动取消当前排队。
        /// </summary>
        public void QueueNextMatchTarget(MatchTargetPendingCommand cmd)
        {
            _pendingCommand = cmd;
            if (cmd != null) cmd.consumed = false;
        }

        /// <summary>
        /// MatchTarget完成回调（子类可重写）
        /// </summary>
        protected virtual void OnMatchTargetCompleted()
        {
            _matchTargetActive        = false;
            _matchTargetSnapshotTaken = false;
            // 注意：不在此处清除 _pendingCommand。
            // Phase1 完成后 Phase2 可能仍在等待触发，清除将导致 Phase2 永远无法执行。
            // _pendingCommand 仅在 CancelMatchTarget() 或状态退出时清除。
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            var shared = stateSharedData;
            var stateName = GetStateNameSafe();
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateBase] MatchTarget完成 | State={stateName}");
#endif
#endif
        }
        // matchtargetTowardPosMutipler / matchtargetTowardRotMutipler 已移除：
        // 改用「剩余时间比例 Lerp」后不再需要速度倍率系数。

        /// <summary>
        /// 由 StateMachine 在 Update 中调用处理 MatchTarget（内部接口）。<br/>
        /// 自定义实现：通过 KCC motor 直接驱动 Entity 根节点插值到目标点，
        /// 无需依赖 <c>Animator.MatchTarget</c>，兼容 KCC（applyRootMotion=false）环境。
        /// </summary>
        internal void ProcessMatchTarget(Animator animator)
        {
            var runtime = _animationRuntime;
            if (runtime == null) return;

            ref var mt = ref runtime.matchTarget;
            if (!mt.active || mt.completed) return;

            float elapsed = hasEnterTime;

            // ── 时间窗尚未开始 → 提前退出 ─────────────────────────────────────────
            // ★ 只检查下限（elapsed < startTime）；上限由末尾完成代码处理。
            //   不能同时门控上限：deltaTime 步进常常一帧从 endTime-ε 跳到 endTime+ε，
            //   若在此处 early-return，完成代码永远无法执行，MatchTarget 卡死在
            //   "active=true, completed=false" 永久激活态，Mount 收尾逻辑永远不会启动，
            //   KCC 接管物理后重力将角色拉回地面原始点。
            if (elapsed < mt.startTime)
            {
#if UNITY_EDITOR
                if (debugMatchTarget && ShouldLogMatchTarget())
                    Debug.Log($"{GetMatchTargetLogTag()} Gate: not started elapsed={elapsed:F3}s startTime={mt.startTime:F3}s");
#endif
                return;
            }

            var entity    = host.HostEntity;
            var motor     = entity.kcc.motor;
            var entityTrs = entity.transform;

            // ★ KCC 插值模式下，entityTrs.position 是"插值渲染位置"而非物理模拟真实位置。
            //   物理层真实位置 = motor.TransientPosition（FixedUpdate Simulate 写入，不受 LateUpdate 插值影响）。
            //   必须用 TransientPosition 作为 MoveTowards 的起点，否则每帧从同一插值旧位置出发，
            //   SetPositionAndRotation 的结果会被下一次 Simulate 立即覆盖，看起来"完全不动"。
            Vector3    curPos = motor.TransientPosition;
            Quaternion curRot = motor.TransientRotation;

            // ── 每帧实时计算骨骼→根偏移（骨骼随动画持续移动，不能在窗口入口快照）────
            // Root 部位：目标就是 entity 根节点，无需偏移。
            // 其余部位：liveOffset = 根节点 - 骨骼世界位置，使骨骼最终落在 mt.position。
            // ★ 两边必须用同一坐标系（均取视觉 Transform），否则 TransientPos 超前于
            //   骨骼视觉位置，liveOffset 随根节点推进而增大，产生"追着移动靶子绕圈"的反馈环。
            Vector3 liveOffset = Vector3.zero;
            if (mt.bodyPart != AvatarTarget.Root)
            {
                var boneCache = host._sharedBoneTransforms;
                int boneIdx   = (int)mt.bodyPart;
                var bone      = boneIdx < boneCache.Length ? boneCache[boneIdx] : null;
                if (bone != null)
                {
                    // ★ 将骨骼→根节点偏移转换到 Entity 局部空间，再以目标旋转重建世界偏移。
                    //   这样当 Entity 旋转到 mt.rotation 时，骨骼能精确落在 mt.position，
                    //   而不因目标旋转与当前旋转不同导致位置偏差。
                    Vector3 localBoneOffset = Quaternion.Inverse(entityTrs.rotation) * (entityTrs.position - bone.position);
                    liveOffset = mt.rotation * localBoneOffset;
                }
            }
            Vector3 targetRootPos = mt.position + liveOffset;

            // 窗口首帧：快照起点（仅用于 Gizmos 可视化显示起始位置）
            if (!_matchTargetSnapshotTaken)
            {
                _matchTargetSnapshotPos   = curPos;
                _matchTargetSnapshotTaken = true;
            }

            // 始终使用 live 目标（每帧实时更新），不使用首帧快照。
            // 骨骼随动画移动时终点跟随，确保对齐到当前帧的真实骨骼位置。
            Vector3    effectiveTargetPos = targetRootPos;
            Quaternion effectiveTargetRot = mt.rotation;

            // ── MoveTowards 速度驱动（比归一化 t Lerp 更简洁，速度感更直观）────────────
            // posSpeed = approachSpeed（单位/秒）；rotSpeed = approachAngleSpeed（度/秒）
            // 乘以速度倍率常量，控制整体逼近快慢
            const float matchtargetTowardPosMutipler = 3f;
            const float matchtargetTowardRotMutipler = 1f;

            float posSpeed = mt.positionApproachSpeed;
            float rotSpeed = mt.rotationApproachSpeed * Mathf.Clamp01(mt.rotationWeight);

            Vector3    newPos = posSpeed > 0f
                ? Vector3.MoveTowards(curPos, effectiveTargetPos, posSpeed * matchtargetTowardPosMutipler * Time.deltaTime)
                : curPos;
            Quaternion newRot = rotSpeed > 0f
                ? Quaternion.RotateTowards(curRot, effectiveTargetRot, rotSpeed * matchtargetTowardRotMutipler * Time.deltaTime)
                : curRot;

            motor.SetPositionAndRotation(newPos, newRot, bypassInterpolation: true);

            _matchTargetLastAppliedPos = newPos;
            _matchTargetLastAppliedRot = newRot;
            _matchTargetLastApplyTime  = Time.time;

#if UNITY_EDITOR
            if (debugMatchTarget && ShouldLogMatchTarget())
            {
                float posErr    = Vector3.Distance(newPos, effectiveTargetPos);
                float rotErr    = rotSpeed > 0f ? Quaternion.Angle(newRot, effectiveTargetRot) : 0f;
                string tgtMode  = mt.bodyPart == AvatarTarget.Root ? "live(Root)" : "live(non-Root,局部旋转补偿)";
                Vector3    posDelta     = newPos - curPos;
                float      posDeltaMag  = posDelta.magnitude;
                Quaternion rotDeltaQuat = rotSpeed > 0f ? newRot * Quaternion.Inverse(curRot) : Quaternion.identity;
                float      rotDelta     = rotSpeed > 0f ? Quaternion.Angle(curRot, newRot) : 0f;
                // 每帧用当前 playerRot 实时重算世界空间位置偏移，确保 Transform 目标移动后仍准确
                Vector3 dbgPosOffsetWorld = _matchTargetDbgPosOffsetLocal == Vector3.zero
                    ? Vector3.zero
                    : host.HostEntity.transform.rotation * _matchTargetDbgPosOffsetLocal;
                string dbgPosActiveTag = _matchTargetPosOffsetActive ? "启用" : "禁用";
                string dbgRotActiveTag = _matchTargetRotOffsetActive ? "启用" : "禁用";
                string dbgPosOffsetStr = _matchTargetDbgPosOffsetLocal == Vector3.zero
                    ? "(未配置 / 全零)"
                    : $"[{dbgPosActiveTag}] local={_matchTargetDbgPosOffsetLocal:F3}  world={dbgPosOffsetWorld:F3}";
                string dbgRotOffsetStr = Mathf.Abs(_matchTargetDbgRotOffsetY) <= 0.0001f
                    ? "(未配置 / 全零)"
                    : $"[{dbgRotActiveTag}] yaw={_matchTargetDbgRotOffsetY:F1}°";
                Debug.Log(
                    $"{GetMatchTargetLogTag()} [Frame={Time.frameCount}]\n" +
                    $"  时间  elapsed={elapsed:F3}s  window=[{mt.startTime:F3},{mt.endTime:F3}]s\n" +
                    $"  启动路径  {_matchTargetDbgStartPath}\n" +
                    $"  部位  bodyPart={mt.bodyPart}  终点={tgtMode}  liveOffset={liveOffset:F3}  |liveOffset|={liveOffset.magnitude:F4}\n" +
                    $"  位置  cur={curPos:F3}  target={effectiveTargetPos:F3}  now={newPos:F3}  Δ={posDelta:F4}  |Δ|={posDeltaMag:F4}  剩余误差={posErr:F4}\n" +
                    $"  旋转  cur={curRot.eulerAngles:F1}  target={effectiveTargetRot.eulerAngles:F1}  now={newRot.eulerAngles:F1}  Δeuler={rotDeltaQuat.eulerAngles:F1}  Δ={rotDelta:F2}°  剩余误差={rotErr:F2}°\n" +
                    $"  速度  posSpeed={posSpeed:F2}×{3f}  rotSpeed={rotSpeed:F2}×{1f}\n" +
                    $"  配置偏移  pos+={dbgPosOffsetStr}  rot+={dbgRotOffsetStr}");

                // ── 提交到 Gizmos 绘制单例 ──────────────────────────────────────
                // 取当前帧骨骼实际位置（bone.position）用于 Gizmos 可视化
                Vector3 gizmoBoneWorldPos = Vector3.zero;
                if (mt.bodyPart != AvatarTarget.Root)
                {
                    var boneCache2 = host._sharedBoneTransforms;
                    int boneIdx2   = (int)mt.bodyPart;
                    var bone2      = boneIdx2 < boneCache2.Length ? boneCache2[boneIdx2] : null;
                    if (bone2 != null) gizmoBoneWorldPos = bone2.position;
                }
                MatchTargetGizmosDrawer.Submit(
                    $"{GetStateNameSafe()}_{GetStateIdSafe()}",
                    new MatchTargetGizmosDrawer.FrameData
                    {
                        label              = $"{GetStateNameSafe()} [{mt.bodyPart}]",
                        snapshotPos        = _matchTargetSnapshotPos,
                        effectiveTargetPos = effectiveTargetPos,
                        currentPos         = newPos,
                        effectiveTargetRot = effectiveTargetRot,
                        currentRot         = newRot,
                        boneTargetPos      = mt.position,
                        liveOffset         = liveOffset,
                        boneWorldPos       = gizmoBoneWorldPos,
                        entityTrsPos       = entityTrs.position,
                        bodyPart           = mt.bodyPart,
                        t                  = Mathf.Clamp01(Vector3.Distance(curPos, effectiveTargetPos) < 0.001f ? 1f : 0f),
                        posErr             = posErr,
                        rotErr             = rotErr,
                    });
            }
#endif

            // ── 时间窗结束，标记完成 ──────────────────────────────────────────────
            // ★ 不在此处做强制终点 snap：snap 依赖骨骼偏移（liveOffset），
            //   endTime 帧骨骼可能还在动画中途，强制到位反而产生新的突变。
            //   残余误差由调用方（如 SyncRider MoveTowards）继续收尾，平滑过渡。
            if (elapsed >= mt.endTime)
            {
                mt.completed = true;
                OnMatchTargetCompleted();
            }
        }

        // ShouldReapplyMatchTarget 保留供重施加策略扩展（自定义实现暂不使用，但保持接口一致）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldReapplyMatchTarget(ref AnimationCalculatorRuntime.MatchTargetRuntimeData mt, StateMachineConfig cfg)
        {
            var reapply = cfg != null ? cfg.matchTargetReapply : MatchTargetReapplySettings.Default;

            if (reapply.interval > 0f && Time.time - _matchTargetLastApplyTime < reapply.interval)
            {
                return false;
            }

            // 性能关键：这里可能在“已匹配中”被频繁调用。
            // - Distance/Angle 会引入 sqrt/acos
            // - 改用 sqrMagnitude + Quaternion.Dot 阈值比较，零GC且更省。

            bool distBigEnough;
            float minDist = reapply.minDistance;
            if (minDist <= 0f)
            {
                distBigEnough = true;
            }
            else
            {
                float minDistSqr = minDist * minDist;
                distBigEnough = (_matchTargetLastAppliedPos - mt.position).sqrMagnitude >= minDistSqr;
            }

            bool angleBigEnough;
            float minAngle = reapply.minAngle;
            if (minAngle <= 0f)
            {
                angleBigEnough = true;
            }
            else
            {
                // Quaternion.Angle(a,b) >= minAngle  <=>  abs(dot(a,b)) <= cos(minAngle/2)
                float dot = Mathf.Abs(Quaternion.Dot(_matchTargetLastAppliedRot, mt.rotation));
                float cosHalf = Mathf.Cos(minAngle * 0.5f * Mathf.Deg2Rad);
                angleBigEnough = dot <= cosHalf;
            }

            return distBigEnough || angleBigEnough;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldLogMatchTarget()        {
            if (debugMatchTargetFrameInterval <= 1) return true;
            return Time.frameCount % debugMatchTargetFrameInterval == 0;
        }

        private string GetMatchTargetLogTag()
        {
            var name = GetStateNameSafe();
            var id = GetStateIdSafe();
            return $"[MatchTarget][State={name}][Id={id}]";
        }

        // ================================================================
        // ★ 待执行指令自动激活（每帧由 Animation.cs 驱动）
        // ================================================================

        /// <summary>
        /// 在动画更新循环中检查并激发待执行指令。<br/>
        /// 满足 <c>hasEnterTime &gt;= cmd.triggerAt</c> 时以 cmd.request 重新启动 MatchTarget（单次触发后自动消耗）。
        /// </summary>
        internal void CheckAndFirePendingCommand(float elapsed)
        {
            var cmd = _pendingCommand;
            if (cmd == null || cmd.consumed) return;
            if (elapsed < cmd.triggerAt) return;

            cmd.consumed   = true;
            _pendingCommand = null;

            var req = cmd.request;
            if (req == null) return;
            if (!_hasSharedMatchTargetPose) return;

#if UNITY_EDITOR
            if (debugMatchTarget)
                Debug.Log($"{GetMatchTargetLogTag()} PendingCommand fired at elapsed={elapsed:F3}s triggerAt={cmd.triggerAt:F3}s");
#endif

            var playerRot = host.HostEntity.transform.rotation;
            ApplyPresetOffset(req, playerRot, _sharedMatchTargetPos, _sharedMatchTargetRot, out var cmdPos, out var cmdRot);
            StartMatchTarget(
                cmdPos, cmdRot,
                req.bodyPart,
                req.timeRange.x, req.timeRange.y,
                req.positionApproachSpeed,
                req.rotationApproachSpeed,
                req.rotationWeight);
        }

        #endregion
    }
}
