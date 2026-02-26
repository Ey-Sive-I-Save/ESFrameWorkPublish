using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;

namespace ES
{
    // ==================== AvatarTarget 中文选项（供 ValueDropdown 使用）====================

    /// <summary>
    /// 为 Unity 内置 <see cref="AvatarTarget"/> 枚举提供 Odin Inspector 中文下拉选项。
    /// 在任意字段上添加 <c>[ValueDropdown("@ES.AvatarTargetDropdown.Options")]</c> 即可生效。
    /// </summary>
    public static class AvatarTargetDropdown
    {
        public static IEnumerable<ValueDropdownItem<AvatarTarget>> Options =>
            new ValueDropdownItem<AvatarTarget>[]
            {
                new ValueDropdownItem<AvatarTarget>("根节点 (Root)",      AvatarTarget.Root),
                new ValueDropdownItem<AvatarTarget>("身体重心 (Body)",    AvatarTarget.Body),
                new ValueDropdownItem<AvatarTarget>("左脚 (LeftFoot)",   AvatarTarget.LeftFoot),
                new ValueDropdownItem<AvatarTarget>("右脚 (RightFoot)",  AvatarTarget.RightFoot),
                new ValueDropdownItem<AvatarTarget>("左手 (LeftHand)",   AvatarTarget.LeftHand),
                new ValueDropdownItem<AvatarTarget>("右手 (RightHand)",  AvatarTarget.RightHand),
            };
    }

    // ==================== IK肢体配置 ====================

    /// <summary>
    /// 单个IK肢体目标配置（Inspector可配置）
    /// </summary>
    [Serializable]
    public class IKLimbConfig
    {
        [LabelText("启用"), ToggleLeft]
        public bool enabled;

        [LabelText("权重"), Range(0f, 1f), ShowIf("enabled")]
        public float weight;

        [LabelText("目标Transform"), ShowIf("enabled")]
        [Tooltip("运行时IK目标位置/旋转跟踪的Transform，为null时使用下方的固定偏移")]
        public Transform target;

        [LabelText("固定位置偏移"), ShowIf("@enabled && target == null")]
        [Tooltip("当无目标Transform时，相对于角色根节点的固定位置偏移")]
        public Vector3 positionOffset;

        [LabelText("固定旋转偏移"), ShowIf("@enabled && target == null")]
        public Vector3 rotationEulerOffset;

        [LabelText("Hint Transform"), ShowIf("enabled")]
        [Tooltip("肘/膝引导方向的Transform（可选）")]
        public Transform hintTarget;

        public static IKLimbConfig Default => new IKLimbConfig
        {
            enabled = false,
            weight = 1f,
            target = null,
            positionOffset = Vector3.zero,
            rotationEulerOffset = Vector3.zero,
            hintTarget = null
        };
    }

    /// <summary>
    /// 注视IK配置
    /// </summary>
    [Serializable]
    public class IKLookAtConfig
    {
        [LabelText("启用注视"), ToggleLeft]
        public bool enabled;

        [LabelText("注视权重"), Range(0f, 1f), ShowIf("enabled")]
        public float weight;

        [LabelText("注视目标"), ShowIf("enabled")]
        [Tooltip("运行时头部朝向跟踪的Transform")]
        public Transform target;

        [LabelText("固定注视点"), ShowIf("@enabled && target == null")]
        [Tooltip("当无目标Transform时，世界空间中的固定注视位置")]
        public Vector3 fixedPosition;

        [FoldoutGroup("Body权重", expanded: false)]
        [LabelText("Body"), Range(0f, 1f), ShowIf("enabled")]
        public float bodyWeight;

        [FoldoutGroup("Body权重", expanded: false)]
        [LabelText("Head"), Range(0f, 1f), ShowIf("enabled")]
        public float headWeight;

        [FoldoutGroup("Body权重", expanded: false)]
        [LabelText("Eyes"), Range(0f, 1f), ShowIf("enabled")]
        public float eyesWeight;

        [FoldoutGroup("Body权重", expanded: false)]
        [LabelText("Clamp"), Range(0f, 1f), ShowIf("enabled")]
        public float clampWeight;

        public static IKLookAtConfig Default => new IKLookAtConfig
        {
            enabled = false,
            weight = 1f,
            target = null,
            fixedPosition = Vector3.zero,
            bodyWeight = 0f,
            headWeight = 1f,
            eyesWeight = 0.5f,
            clampWeight = 0.5f
        };
    }

    /// <summary>
    /// IK目标来源模式
    /// </summary>
    public enum IKSourceMode
    {
        [InspectorName("仅配置（Inspector固定值）")]
        ConfigOnly = 0,

        [InspectorName("仅代码（API动态设置）")]
        CodeOnly = 1,

        [InspectorName("配置+代码覆盖")]
        ConfigWithCodeOverride = 2
    }

    // ==================== MatchTarget配置 ====================

    /// <summary>
    /// MatchTarget 请求（统一数据包）。<br/>
    /// 同一个数据包可同时出现在：
    /// <list type="bullet">
    ///   <item>资产配置 —— <c>StateAnimationConfigData.matchTargetPreset</c></item>
    ///   <item>场景物体 —— <c>ESInteractable.matchTargetRequest</c> 等脚本字段</item>
    ///   <item>纯代码构造 —— <c>new MatchTargetRequest { bodyPart = ..., ... }</c></item>
    /// </list>
    /// 拿到请求后，只需调用 <c>state.ApplyMatchTarget(request)</c> 一行代码即可应用。
    /// </summary>
    [Serializable]
    public class MatchTargetRequest
    {
        [LabelText("身体部位"), ValueDropdown("@ES.AvatarTargetDropdown.Options")]
        public AvatarTarget bodyPart;

        [LabelText("目标Transform")]
        [Tooltip("运行时对齐目标的 Transform；非 null 时位置/旋转从该 Transform 读取，忽略下方固定值")]
        public Transform target;

        [LabelText("固定目标位置"), ShowIf("@target == null")]
        [Tooltip("当无 Transform 引用时的世界空间固定位置")]
        public Vector3 fixedPosition;

        [LabelText("固定目标旋转"), ShowIf("@target == null")]
        public Vector3 fixedRotationEuler;

        [LabelText("位置偏移(局部空间)")]
        [Tooltip("叠加在 target.position / fixedPosition 之上的偏移，以玩家自身局部空间为基准（X=右, Y=上, Z=前）。\n框架内部自动通过玩家旋转将其转换为世界空间后应用。\n运行时可通过 PatchMatchTargetOffset 叠加修改，不影响原始配置。")]
        [OnValueChanged("SyncPosOffsetEnable")]
        public Vector3 positionOffset;

        [LabelText("启用位置偏移"), ToggleLeft]
        [Tooltip("取消勾选可在不清零偏移值的前提下临时禁用 positionOffset。\n代码调用 SetMatchTargetOffsetActive(false) 亦可运行时动态关闭。")]
        [ShowIf("@positionOffset != UnityEngine.Vector3.zero")]
        public bool enablePositionOffset = true;

        [LabelText("旋转偏移(玩家局部欧拉)")]
        [Tooltip("在玩家当前朝向（playerTransform.rotation）的局部坐标系上叠加的旋转修正。\n即 playerRot * Euler(offset)，用于弥补动画期望朝向与玩家当前朝向的固定偏差。\n若需对齐到场景物体朝向，请由代码直接传入目标旋转，不走此偏移。")]
        [OnValueChanged("SyncRotOffsetEnable")]
        public Vector3 rotationOffsetEuler;

        [LabelText("启用旋转偏移"), ToggleLeft]
        [Tooltip("取消勾选可在不清零偏移值的前提下临时禁用 rotationOffsetEuler。")]
        [ShowIf("@rotationOffsetEuler != UnityEngine.Vector3.zero")]
        public bool enableRotationOffset = true;

        [LabelText("开始时间(秒)"), Min(0f)]
        [Tooltip("状态进入后经过多少秒开始执行 MatchTarget 对齐（hasEnterTime 基准）")]
        public float startTime;

        [LabelText("结束时间(秒)"), Min(0f)]
        [Tooltip("状态进入后经过多少秒结束 MatchTarget 对齐；到达时强制吸附并标记完成")]
        public float endTime;

        [LabelText("逼近速度 (位置X / 旋转Y)")]
        [Tooltip("X = 位置逼近速度（单位/秒），内部乘以3倍率。\nY = 旋转逼近速度（度/秒），仅「旋转启用 > 0」时生效。\nZ 暂不使用。")]
        public Vector3 positionWeight;

        [LabelText("旋转启用 (>0=开启)"), Range(0f, 1f)]
        [Tooltip("大于0则启用旋转对齐，速度由上方【逼近速度.Y】决定。\n0 = 不旋转（方便不需要对齐旋转的场景）。")]
        public float rotationWeight;

        // ── 运行时 helper ──

        /// <summary>
        /// 获取实际目标位置（Transform 优先，无则固定值），
        /// 并将 <see cref="positionOffset"/> 从 <paramref name="playerTransform"/> 的局部空间转换为世界空间后叠加。
        /// </summary>
        /// <param name="playerTransform">玩家根节点 Transform；为 null 时退化为直接世界空间叠加。</param>
        public Vector3 GetPosition(Transform playerTransform = null)
        {
            var basePos = target != null ? target.position : fixedPosition;
            if (!enablePositionOffset || positionOffset == Vector3.zero) return basePos;
            var worldOffset = playerTransform != null
                ? playerTransform.rotation * positionOffset
                : positionOffset;
            return basePos + worldOffset;
        }

        /// <summary>
        /// 获取目标旋转。始终以<b>玩家根旋转</b>为基准，再叠加 <see cref="rotationOffsetEuler"/> 偏移。<br/>
        /// 设计意图：旋转偏移应相对于玩家当前朝向（与位置偏移的坐标系对称）。<br/>
        /// 若需要对齐到场景物体朝向（如攀爬墙面），由代码直接传入 targetRot，不走此方法。
        /// </summary>
        /// <param name="playerTransform">玩家根节点，提供基准旋转；为 null 时退化为 fixedRotationEuler 世界旋转。</param>
        public Quaternion GetRotation(Transform playerTransform = null)
        {
            var baseRot = playerTransform != null
                ? playerTransform.rotation
                : Quaternion.Euler(fixedRotationEuler);
            if (!enableRotationOffset || rotationOffsetEuler == Vector3.zero) return baseRot;
            return baseRot * Quaternion.Euler(rotationOffsetEuler);
        }

        /// <summary>由 Odin OnValueChanged 回调：positionOffset 改变时自动同步 enablePositionOffset。
        /// 全零 → 关闭（偏移无意义）；非零 → 开启。</summary>
        private void SyncPosOffsetEnable() => enablePositionOffset = positionOffset != Vector3.zero;

        /// <summary>由 Odin OnValueChanged 回调：rotationOffsetEuler 改变时自动同步 enableRotationOffset。
        /// 全零 → 关闭；非零 → 开启。</summary>
        private void SyncRotOffsetEnable() => enableRotationOffset = rotationOffsetEuler != Vector3.zero;

        public static MatchTargetRequest Default => new MatchTargetRequest
        {
            bodyPart            = AvatarTarget.Root,
            target              = null,
            fixedPosition       = Vector3.zero,
            fixedRotationEuler  = Vector3.zero,
            positionOffset      = Vector3.zero,
            enablePositionOffset = true,
            rotationOffsetEuler = Vector3.zero,
            enableRotationOffset = true,
            startTime = 0f,
            endTime   = 1f,
            positionWeight      = new Vector3(3f, 360f, 0f),
            rotationWeight      = 1f
        };
    }

    // ==================== 待执行指令 ====================

    /// <summary>
    /// 待执行的 MatchTarget 指令。<br/>
    /// 注册后，当 <c>normalizedProgress &gt;= triggerAt</c> 时自动激活，
    /// 以 <see cref="request"/> 参数完整重新启动一次 MatchTarget（覆盖当前时间窗口 / 身体部位 / 权重）。<br/>
    /// 触发后自动消耗（单次有效）；状态退出 / Cancel 时自动清除。<br/>
    /// 可调用 <see cref="Reset"/> 重置消耗标记后复用同一实例，避免重复分配。
    /// </summary>
    [Serializable]
    public class MatchTargetPendingCommand
    {
        [LabelText("触发时间(秒)"), Min(0f)]
        [Tooltip("状态进入后经过多少秒触发此指令（hasEnterTime 基准），到达后覆盖当前 MatchTarget 参数")]
        public float triggerAt;

        [LabelText("指令参数"), InlineProperty, HideLabel]
        public MatchTargetRequest request = MatchTargetRequest.Default;

        // ── 内部运行时状态（框架控制，外部只需调用 Reset） ──
        [NonSerialized] internal bool consumed;

        /// <summary>重置消耗标记，使此实例可以被再次排队触发。</summary>
        public void Reset() => consumed = false;
    }

    // ==================== 主配置类 ====================

    /// <summary>
    /// 动画配置基类
    /// 用于高级Clip选择和配置,支持多种模式
    /// 所有Calculator实现已移至AnimationMixerCalculators.cs
    /// </summary>
    [Serializable]
    public class StateAnimationConfigData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [SerializeReference,LabelText("动画混合计算器")]
        public StateAnimationMixCalculator calculator = new StateAnimationMixCalculatorForSimpleClip();

        // ==================== IK配置（商业级 Inspector 可视化） ====================

        [FoldoutGroup("IK配置")]
        [LabelText("启用IK"), Tooltip("是否允许对此状态进行IK控制")]
        public bool enableIK = false;

        [FoldoutGroup("IK配置")]
        [LabelText("IK来源模式"), ShowIf("enableIK")]
        [Tooltip("选择 IK 数据从哪里来：\n- 仅使用 Inspector 配置\n- 仅使用代码接口\n- 以 Inspector 配置为基础，运行时允许代码覆盖")]
        public IKSourceMode ikSourceMode = IKSourceMode.ConfigOnly;

        [FoldoutGroup("IK配置")]
        [LabelText("IK平滑时间"), Range(0f, 0.5f), ShowIf("enableIK")]
        [Tooltip("IK权重变化的平滑过渡时间（秒），0=立即")]
        public float ikSmoothTime = 0.1f;

        [FoldoutGroup("IK配置")]
        [LabelText("使用IK权重曲线"), ShowIf("enableIK")]
        [Tooltip("启用后：IK总目标权重会再乘以曲线值（x=归一化进度0-1，y=倍率）。\n适合：出手段拉满、收招段回收等更自然的权重变化。")]
        public bool useIKTargetWeightCurve = false;

        [FoldoutGroup("IK配置")]
        [LabelText("IK权重曲线"), ShowIf("@enableIK && useIKTargetWeightCurve")]
        public AnimationCurve ikTargetWeightCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [FoldoutGroup("IK配置")]
        [LabelText("曲线倍率"), Range(0f, 2f), ShowIf("@enableIK && useIKTargetWeightCurve")]
        [Tooltip("曲线结果的额外倍率（最终倍率=Curve(x)*Scale）。")]
        public float ikTargetWeightCurveScale = 1f;

        [FoldoutGroup("IK配置")]
        [LabelText("状态退出时禁用IK"), ShowIf("enableIK")]
        [Tooltip("当状态退出时是否自动禁用IK")]
        public bool disableIKOnExit = true;

        [FoldoutGroup("IK配置/肢体目标")]
        [LabelText("左手IK"), ShowIf("enableIK"), InlineProperty]
        public IKLimbConfig ikLeftHand = IKLimbConfig.Default;

        [FoldoutGroup("IK配置/肢体目标")]
        [LabelText("右手IK"), ShowIf("enableIK"), InlineProperty]
        public IKLimbConfig ikRightHand = IKLimbConfig.Default;

        [FoldoutGroup("IK配置/肢体目标")]
        [LabelText("左脚IK"), ShowIf("enableIK"), InlineProperty]
        public IKLimbConfig ikLeftFoot = IKLimbConfig.Default;

        [FoldoutGroup("IK配置/肢体目标")]
        [LabelText("右脚IK"), ShowIf("enableIK"), InlineProperty]
        public IKLimbConfig ikRightFoot = IKLimbConfig.Default;

        [FoldoutGroup("IK配置/注视")]
        [LabelText("注视IK"), ShowIf("enableIK"), InlineProperty]
        public IKLookAtConfig ikLookAt = IKLookAtConfig.Default;

        // ==================== MatchTarget配置（商业级 Inspector 可视化） ====================

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("启用MatchTarget"), Tooltip("是否允许此状态使用MatchTarget对齐")]
        public bool enableMatchTarget = false;

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("状态进入时自动激活"), ShowIf("enableMatchTarget")]
        [Tooltip("状态Enter时自动触发MatchTarget（否则需要代码手动调用StartMatchTarget）")]
        public bool autoActivateMatchTarget = false;

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("MatchTarget预设"), ShowIf("enableMatchTarget"), InlineProperty]
        public MatchTargetRequest matchTargetPreset = MatchTargetRequest.Default;

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("启用阶段2"), ShowIf("enableMatchTarget")]
        public bool enableMatchTargetPhase2 = false;

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("阶段2预设"), ShowIf("enableMatchTargetPhase2"), InlineProperty]
        [Tooltip("阶段2 开始时间即为触发阈值，不需额外设置触发进度字段")]
        public MatchTargetRequest matchTargetPresetPhase2 = MatchTargetRequest.Default;

        // 重施加参数已迁移到 StateMachineConfig.matchTargetReapply（全局配置）。

        /// <summary>
        /// 获取Clip和起始时间
        /// </summary>
        /// <param name="context">状态上下文</param>
        /// <returns>返回选定的Clip和起始归一化时间</returns>
        public virtual (AnimationClip clip, float normalizedTime) GetClipAndTime(StateMachineContext context)
        {
            return (null, 0f);
        }

        // ==================== IK运行时应用 ====================

        /// <summary>
        /// 将Inspector配置的IK数据应用到Runtime（状态Enter时调用）
        /// </summary>
        public void ApplyIKConfigToRuntime(AnimationCalculatorRuntime runtime, Transform rootTransform = null)
        {
            if (!enableIK || runtime == null) return;
            if (ikSourceMode == IKSourceMode.CodeOnly) return; // 纯代码模式不自动应用配置

            runtime.ik.enabled = true;
            runtime.ik.targetWeight = 1f;

            // 左手
            if (ikLeftHand.enabled)
            {
                ApplyLimbConfig(ref runtime.ik.leftHandPosition, ref runtime.ik.leftHandRotation,
                    ref runtime.ik.leftHandWeight, ref runtime.ik.leftHandHintPosition,
                    ikLeftHand, rootTransform);
            }

            // 右手
            if (ikRightHand.enabled)
            {
                ApplyLimbConfig(ref runtime.ik.rightHandPosition, ref runtime.ik.rightHandRotation,
                    ref runtime.ik.rightHandWeight, ref runtime.ik.rightHandHintPosition,
                    ikRightHand, rootTransform);
            }

            // 左脚
            if (ikLeftFoot.enabled)
            {
                ApplyLimbConfig(ref runtime.ik.leftFootPosition, ref runtime.ik.leftFootRotation,
                    ref runtime.ik.leftFootWeight, ref runtime.ik.leftFootHintPosition,
                    ikLeftFoot, rootTransform);
            }

            // 右脚
            if (ikRightFoot.enabled)
            {
                ApplyLimbConfig(ref runtime.ik.rightFootPosition, ref runtime.ik.rightFootRotation,
                    ref runtime.ik.rightFootWeight, ref runtime.ik.rightFootHintPosition,
                    ikRightFoot, rootTransform);
            }

            // 注视
            if (ikLookAt.enabled)
            {
                runtime.ik.lookAtWeight = ikLookAt.weight;
                runtime.ik.lookAtPosition = ikLookAt.target != null
                    ? ikLookAt.target.position
                    : ikLookAt.fixedPosition;
            }
        }

        /// <summary>
        /// 评估IK权重曲线倍率（x=归一化进度0-1）。
        /// </summary>
        public float EvaluateIKTargetWeightMultiplier(float normalizedProgress)
        {
            if (!enableIK || !useIKTargetWeightCurve) return 1f;
            if (ikTargetWeightCurve == null) return Mathf.Max(0f, ikTargetWeightCurveScale);
            float t = Mathf.Clamp01(normalizedProgress);
            float curve = ikTargetWeightCurve.Evaluate(t);
            return Mathf.Max(0f, curve * ikTargetWeightCurveScale);
        }

        /// <summary>
        /// 每帧更新IK目标位置（仅在有Transform引用时需要）
        /// </summary>
        public void UpdateIKTargetsFromConfig(AnimationCalculatorRuntime runtime)
        {
            if (!enableIK || runtime == null || !runtime.ik.enabled) return;
            if (ikSourceMode == IKSourceMode.CodeOnly) return;

            // 仅更新有Transform引用的肢体（跟踪动态目标）
            if (ikLeftHand.enabled && ikLeftHand.target != null)
            {
                runtime.ik.leftHandPosition = ikLeftHand.target.position;
                runtime.ik.leftHandRotation = ikLeftHand.target.rotation;
            }
            if (ikLeftHand.enabled && ikLeftHand.hintTarget != null)
                runtime.ik.leftHandHintPosition = ikLeftHand.hintTarget.position;

            if (ikRightHand.enabled && ikRightHand.target != null)
            {
                runtime.ik.rightHandPosition = ikRightHand.target.position;
                runtime.ik.rightHandRotation = ikRightHand.target.rotation;
            }
            if (ikRightHand.enabled && ikRightHand.hintTarget != null)
                runtime.ik.rightHandHintPosition = ikRightHand.hintTarget.position;

            if (ikLeftFoot.enabled && ikLeftFoot.target != null)
            {
                runtime.ik.leftFootPosition = ikLeftFoot.target.position;
                runtime.ik.leftFootRotation = ikLeftFoot.target.rotation;
            }
            if (ikLeftFoot.enabled && ikLeftFoot.hintTarget != null)
                runtime.ik.leftFootHintPosition = ikLeftFoot.hintTarget.position;

            if (ikRightFoot.enabled && ikRightFoot.target != null)
            {
                runtime.ik.rightFootPosition = ikRightFoot.target.position;
                runtime.ik.rightFootRotation = ikRightFoot.target.rotation;
            }
            if (ikRightFoot.enabled && ikRightFoot.hintTarget != null)
                runtime.ik.rightFootHintPosition = ikRightFoot.hintTarget.position;

            if (ikLookAt.enabled && ikLookAt.target != null)
                runtime.ik.lookAtPosition = ikLookAt.target.position;
        }

        /// <summary>
        /// [已废弃] 此方法绕过了 StateBase.StartMatchTarget 的强化逻辑，不应被用户代码调用。<br/>
        /// 正确入口是 <c>state.ApplyMatchTarget(request)</c> 或配置 <c>autoActivateMatchTarget=true</c>。
        /// </summary>
        [System.Obsolete("Call state.ApplyMatchTarget(request) or enable autoActivateMatchTarget instead.")]
        public void ApplyMatchTargetConfigToRuntime(AnimationCalculatorRuntime runtime, Transform playerTransform = null)
        {
            if (!enableMatchTarget || !autoActivateMatchTarget || runtime == null) return;

            var preset = matchTargetPreset;
            runtime.StartMatchTarget(
                preset.GetPosition(playerTransform), preset.GetRotation(playerTransform),
                preset.bodyPart,
                preset.startTime, preset.endTime,
                preset.positionWeight, preset.rotationWeight);
        }

        /// <summary>
        /// 检查是否存在需要每帧跟踪的动态 Transform 目标（Phase1 或 Phase2 均会检查）
        /// </summary>
        public bool HasDynamicMatchTarget()
        {
            if (!enableMatchTarget) return false;
            if (matchTargetPreset.target != null) return true;
            if (enableMatchTargetPhase2 && matchTargetPresetPhase2.target != null) return true;
            return false;
        }

        /// <summary>
        /// 每帧从 Inspector 配置的 Transform 引用同步 MatchTarget 目标位置/旋转到 Runtime。
        /// 仅更新有 Transform 引用的预设（与 IK 的 UpdateIKTargetsFromConfig 设计对齐）。
        /// </summary>
        /// <param name="playerTransform">玩家根节点 Transform，用于将 positionOffset / rotationOffsetEuler 从局部空间转换为世界空间；可为 null。</param>
        public void UpdateMatchTargetFromConfig(AnimationCalculatorRuntime runtime, Transform playerTransform = null)
        {
            if (!enableMatchTarget || runtime == null) return;
            if (!runtime.matchTarget.active || runtime.matchTarget.completed) return;

            // Phase1 动态追踪：通过 GetPosition/GetRotation 保证 positionOffset / rotationOffsetEuler 被正确叠加
            if (matchTargetPreset.target != null)
            {
                runtime.matchTarget.position = matchTargetPreset.GetPosition(playerTransform);
                runtime.matchTarget.rotation = matchTargetPreset.GetRotation(playerTransform);
            }

            // Phase2 动态追踪（Phase2激活时，phase2的target覆盖）
            if (enableMatchTargetPhase2 && matchTargetPresetPhase2.target != null
                && runtime.matchTarget.startTime >= matchTargetPresetPhase2.startTime)
            {
                runtime.matchTarget.position = matchTargetPresetPhase2.GetPosition(playerTransform);
                runtime.matchTarget.rotation = matchTargetPresetPhase2.GetRotation(playerTransform);
            }
        }

        private static void ApplyLimbConfig(ref Vector3 position, ref Quaternion rotation,
            ref float weight, ref Vector3 hintPosition, IKLimbConfig config, Transform rootTransform)
        {
            weight = config.weight;

            if (config.target != null)
            {
                position = config.target.position;
                rotation = config.target.rotation;
            }
            else if (rootTransform != null)
            {
                // 相对于角色根节点的偏移
                position = rootTransform.TransformPoint(config.positionOffset);
                rotation = rootTransform.rotation * Quaternion.Euler(config.rotationEulerOffset);
            }
            else
            {
                position = config.positionOffset;
                rotation = Quaternion.Euler(config.rotationEulerOffset);
            }

            if (config.hintTarget != null)
                hintPosition = config.hintTarget.position;
        }

        /// <summary>
        /// 检查是否有任何IK肢体配置了Transform目标（需要每帧更新）
        /// </summary>
        public bool HasDynamicIKTargets()
        {
            if (!enableIK || ikSourceMode == IKSourceMode.CodeOnly) return false;
            return (ikLeftHand.enabled && ikLeftHand.target != null)
                || (ikRightHand.enabled && ikRightHand.target != null)
                || (ikLeftFoot.enabled && ikLeftFoot.target != null)
                || (ikRightFoot.enabled && ikRightFoot.target != null)
                || (ikLookAt.enabled && ikLookAt.target != null);
        }

        /// <summary>
        /// 运行时初始化
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            _isRuntimeInitialized = true;
        }
    }
}
