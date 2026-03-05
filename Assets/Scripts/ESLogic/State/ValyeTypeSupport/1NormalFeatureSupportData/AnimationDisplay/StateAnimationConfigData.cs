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
    /// 该数据包仅承载“阶段参数与偏移”；目标位置/旋转由运行时传入。
    /// </summary>
    [Serializable]
    public class MatchTargetRequest
    {
        [LabelText("身体部位"), ValueDropdown("@ES.AvatarTargetDropdown.Options")]
        public AvatarTarget bodyPart;

        [LabelText("位置偏移(局部空间)")]
        [Tooltip("叠加在运行时传入目标位置之上的偏移，以玩家自身局部空间为基准（X=右, Y=上, Z=前）。\n框架内部自动通过玩家旋转将其转换为世界空间后应用。\n运行时可通过 AddMatchTargetOffset 叠加修改，不影响原始配置。")]
        [OnValueChanged("SyncPosOffsetEnable")]
        public Vector3 positionOffset;

        [LabelText("启用位置偏移"), ToggleLeft]
        [Tooltip("取消勾选可在不清零偏移值的前提下临时禁用 positionOffset。\n代码调用 SetMatchTargetOffsetActive(false) 亦可运行时动态关闭。")]
        [ShowIf("@positionOffset != UnityEngine.Vector3.zero")]
        public bool enablePositionOffset = false;

        [LabelText("旋转偏移Y(目标局部Yaw)")]
        [Tooltip("仅允许 Y 轴旋转偏移（Yaw，度）。\n基于运行时传入的目标旋转Y叠加。")]
        [OnValueChanged("SyncRotOffsetYawOnlyAndEnable")]
        public float rotationOffsetY;

        [LabelText("启用旋转偏移"), ToggleLeft]
        [Tooltip("取消勾选可在不清零偏移值的前提下临时禁用 rotationOffsetY。")]
        [ShowIf("@UnityEngine.Mathf.Abs(rotationOffsetY) > 0.0001f")]
        public bool enableRotationOffset = false;

        [LabelText("时间窗(开始/结束秒)")]
        [OnValueChanged("AutoCorrectTimeRange")]
        [Tooltip("X=开始时间，Y=结束时间（秒，hasEnterTime 基准）。")]
        [MinMaxSlider(0f, 10f, true)]
        public Vector2 timeRange = new Vector2(0f, 0.35f);

        [LabelText("位移逼近速度")]
        [OnValueChanged("AutoCorrectWeights")]
        [Tooltip("位置逼近速度（单位/秒），内部乘以3倍率。")]
        public float positionApproachSpeed = 4f;

        [LabelText("旋转基础速度(度/秒)")]
        [OnValueChanged("AutoCorrectWeights")]
        [Tooltip("旋转基础速度（度/秒），最终速度 = 该值 * 旋转权重。")]
        public float rotationApproachSpeed = 240f;

        [LabelText("旋转权重(0=关闭)"), Range(0f, 1f)]
        [OnValueChanged("AutoCorrectWeights")]
        [Tooltip("旋转权重（0-1），最终旋转速度 = 逼近速度.Y * 旋转权重。\n0 = 不旋转。")]
        public float rotationWeight = 1f;

        /// <summary>由 Odin OnValueChanged 回调：positionOffset 改变时自动同步 enablePositionOffset。
        /// 全零 → 关闭（偏移无意义）；非零 → 开启。</summary>
        private void SyncPosOffsetEnable() => enablePositionOffset = positionOffset != Vector3.zero;

        /// <summary>由 Odin OnValueChanged 回调：rotationOffsetY 改变时同步 enableRotationOffset。</summary>
        private void SyncRotOffsetYawOnlyAndEnable()
        {
            enableRotationOffset = Mathf.Abs(rotationOffsetY) > 0.0001f;
        }

        private void AutoCorrectTimeRange()
        {
            timeRange.x = Mathf.Max(0f, timeRange.x);
            timeRange.y = Mathf.Max(0f, timeRange.y);
            if (timeRange.y < timeRange.x) timeRange.y = timeRange.x;
        }

        private void AutoCorrectWeights()
        {
            positionApproachSpeed = Mathf.Max(0f, positionApproachSpeed);
            rotationApproachSpeed = Mathf.Max(0f, rotationApproachSpeed);
            rotationWeight = Mathf.Clamp01(rotationWeight);
        }

        public static MatchTargetRequest Default => new MatchTargetRequest
        {
            bodyPart            = AvatarTarget.Root,
            positionOffset      = Vector3.zero,
            enablePositionOffset = false,
            rotationOffsetY     = 0f,
            enableRotationOffset = false,
            timeRange = new Vector2(0f, 0.35f),
            positionApproachSpeed = 4f,
            rotationApproachSpeed = 240f,
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

        [Header("指令参数")]
        [HideLabel]
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

        [Header("左手IK")]
        [ShowIf("enableIK"), HideLabel]
        public IKLimbConfig ikLeftHand = IKLimbConfig.Default;

        [Header("右手IK")]
        [ShowIf("enableIK"), HideLabel]
        public IKLimbConfig ikRightHand = IKLimbConfig.Default;

        [Header("左脚IK")]
        [ShowIf("enableIK"), HideLabel]
        public IKLimbConfig ikLeftFoot = IKLimbConfig.Default;

        [Header("右脚IK")]
        [ShowIf("enableIK"), HideLabel]
        public IKLimbConfig ikRightFoot = IKLimbConfig.Default;

        [Header("注视IK")]
        [ShowIf("enableIK"), HideLabel]
        public IKLookAtConfig ikLookAt = IKLookAtConfig.Default;

        // ==================== MatchTarget配置（商业级 Inspector 可视化） ====================

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("启用MatchTarget"), Tooltip("是否允许此状态使用MatchTarget对齐")]
        public bool enableMatchTarget = false;

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("状态进入时自动激活"), ShowIf("enableMatchTarget")]
        [Tooltip("状态Enter时自动启动阶段时序（Phase1/Phase2 由 StateBase 按 timeRange 自动推进）。\n无论 Phase2.timeRange.x 是否更小，Phase2 都必须在 Phase1.timeRange.y（阶段一结束）之后才允许启动。\n关闭时可由代码手动启动 Phase1，Phase2 仍由 StateBase 按同一规则自动推进。")]
        public bool autoActivateMatchTarget = false;

        [FoldoutGroup("MatchTarget配置")]
        [Header("MatchTarget预设")]
        [ShowIf("enableMatchTarget"), HideLabel]
        public MatchTargetRequest matchTargetPreset = MatchTargetRequest.Default;

        [FoldoutGroup("MatchTarget配置")]
        [LabelText("启用阶段2"), ShowIf("enableMatchTarget")]
        public bool enableMatchTargetPhase2 = false;

        [FoldoutGroup("MatchTarget配置")]
        [Header("阶段2预设")]
        [ShowIf("@enableMatchTarget && enableMatchTargetPhase2"), HideLabel]
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
