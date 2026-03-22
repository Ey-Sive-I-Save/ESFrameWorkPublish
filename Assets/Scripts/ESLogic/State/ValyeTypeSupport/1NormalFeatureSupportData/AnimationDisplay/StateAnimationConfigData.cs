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
    /// 单个IK肢体配置（仅定义是否启用、目标权重与响应速度倍率，不承担目标位姿）
    /// </summary>
    [Serializable]
    public class IKLimbConfig
    {
        [HorizontalGroup("行", Width = 0.28f)]
        [LabelText("启用"), ToggleLeft]
        public bool enabled;

        [HorizontalGroup("行", Width = 0.36f)]
        [LabelText("目标权重"), Range(0f, 1f), ShowIf("enabled")]
        [Tooltip("该节点写入最终 IK Pose 的目标权重。1 = 全量应用，0 = 不应用。")]
        public float targetWeight;

        [HorizontalGroup("行")]
        [LabelText("LerpingRate"), Range(0.05f, 8f), ShowIf("enabled")]
        [Tooltip("该节点写入 Driver 的 lerping 速度倍率。它不是权重。1 = 默认速度；小于 1 更慢，大于 1 更快。")]
        public float lerpingRate = 1f;

        public static IKLimbConfig Default => new IKLimbConfig
        {
            enabled = false,
            targetWeight = 1f,
            lerpingRate = 1f
        };
    }

    /// <summary>
    /// 注视IK配置（仅定义目标权重、响应速度倍率与骨骼分权重，不承担目标位置）
    /// </summary>
    [Serializable]
    public class IKLookAtConfig
    {
        [HorizontalGroup("基础行", Width = 0.28f)]
        [LabelText("启用注视"), ToggleLeft]
        public bool enabled;

        [HorizontalGroup("基础行", Width = 0.36f)]
        [LabelText("目标权重"), Range(0f, 1f), ShowIf("enabled")]
        [Tooltip("该 LookAt 目标写入最终 IK Pose 的目标权重。1 = 全量应用，0 = 不应用。")]
        public float targetWeight;

        [HorizontalGroup("基础行")]
        [LabelText("LerpingRate"), Range(0.05f, 8f), ShowIf("enabled")]
        [Tooltip("该 LookAt 写入 Driver 的 lerping 速度倍率。它不是权重。1 = 默认速度；小于 1 更慢，大于 1 更快。")]
        public float lerpingRate = 1f;

        [HorizontalGroup("细分行")]
        [LabelText("Body"), Range(0f, 1f), ShowIf("enabled")]
        public float bodyWeight;

        [HorizontalGroup("细分行")]
        [LabelText("Head"), Range(0f, 1f), ShowIf("enabled")]
        public float headWeight;

        [HorizontalGroup("细分行")]
        [LabelText("Eyes"), Range(0f, 1f), ShowIf("enabled")]
        public float eyesWeight;

        [HorizontalGroup("细分行")]
        [LabelText("Clamp"), Range(0f, 1f), ShowIf("enabled")]
        public float clampWeight;

        public static IKLookAtConfig Default => new IKLookAtConfig
        {
            enabled = false,
            targetWeight = 1f,
            lerpingRate = 1f,
            bodyWeight = 0f,
            headWeight = 1f,
            eyesWeight = 0.5f,
            clampWeight = 0.5f
        };
    }

    [Serializable]
    public class StateIKSegmentConfig
    {
        [HorizontalGroup("段头", Width = 0.2f)]
        [LabelText("启用"), ToggleLeft]
        public bool enabled = true;

        [HorizontalGroup("段头", Width = 0.28f)]
        [LabelText("段名")]
        public string name = "Segment";

        [HorizontalGroup("段头", Width = 0.34f)]
        [LabelText("归一化区间")]
        [MinMaxSlider(0f, 1f, true)]
        public Vector2 normalizedRange = new Vector2(0f, 1f);

        [HorizontalGroup("段头")]
        [LabelText("羽化")]
        [Range(0f, 0.5f)]
        [Tooltip("段首尾的软过渡区间。0 = 进入区间即全量生效；大于 0 时会在边缘自然渐入渐出。")]
        public float feather = 0.08f;

        [Title("左手IK", bold: false)]
        [ShowIf("enabled"), InlineProperty, HideLabel]
        public IKLimbConfig ikLeftHand = IKLimbConfig.Default;

        [Title("右手IK", bold: false)]
        [ShowIf("enabled"), InlineProperty, HideLabel]
        public IKLimbConfig ikRightHand = IKLimbConfig.Default;

        [Title("左脚IK", bold: false)]
        [ShowIf("enabled"), InlineProperty, HideLabel]
        public IKLimbConfig ikLeftFoot = IKLimbConfig.Default;

        [Title("右脚IK", bold: false)]
        [ShowIf("enabled"), InlineProperty, HideLabel]
        public IKLimbConfig ikRightFoot = IKLimbConfig.Default;

        [Title("注视IK", bold: false)]
        [ShowIf("enabled"), InlineProperty, HideLabel]
        public IKLookAtConfig ikLookAt = IKLookAtConfig.Default;

        public float EvaluateInfluence(float normalizedProgress)
        {
            if (!enabled)
                return 0f;

            float start = Mathf.Clamp01(normalizedRange.x);
            float end = Mathf.Clamp01(normalizedRange.y);
            if (end < start)
            {
                float temp = start;
                start = end;
                end = temp;
            }

            if (normalizedProgress < start || normalizedProgress > end)
                return 0f;

            float duration = end - start;
            if (duration <= 0.0001f)
                return 1f;

            float effectiveFeather = Mathf.Clamp(feather, 0f, duration * 0.5f);
            if (effectiveFeather <= 0.0001f)
                return 1f;

            float enterWeight = normalizedProgress <= start + effectiveFeather
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, start + effectiveFeather, normalizedProgress))
                : 1f;
            float exitWeight = normalizedProgress >= end - effectiveFeather
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(end, end - effectiveFeather, normalizedProgress))
                : 1f;
            return Mathf.Min(enterWeight, exitWeight);
        }

        public IKLimbConfig GetGoalConfig(IKGoal goal)
        {
            return goal switch
            {
                IKGoal.LeftHand => ikLeftHand,
                IKGoal.RightHand => ikRightHand,
                IKGoal.LeftFoot => ikLeftFoot,
                IKGoal.RightFoot => ikRightFoot,
                _ => null
            };
        }
    }

    public struct ResolvedIKLimbConfig
    {
        public bool enabled;
        public float targetWeight;
        public float lerpingRate;

        public static ResolvedIKLimbConfig Disabled => new ResolvedIKLimbConfig
        {
            enabled = false,
            targetWeight = 0f,
            lerpingRate = 1f,
        };
    }

    public struct ResolvedIKLookAtConfig
    {
        public bool enabled;
        public float targetWeight;
        public float lerpingRate;
        public float bodyWeight;
        public float headWeight;
        public float eyesWeight;
        public float clampWeight;

        public static ResolvedIKLookAtConfig Disabled => new ResolvedIKLookAtConfig
        {
            enabled = false,
            targetWeight = 0f,
            lerpingRate = 1f,
            bodyWeight = 0f,
            headWeight = 1f,
            eyesWeight = 0.5f,
            clampWeight = 0.5f,
        };
    }

    // ==================== MatchTarget配置 ====================

    /// <summary>
    /// MatchTarget 请求（统一数据包）。<br/>
    /// 同一个数据包可同时出现在：
    /// <list type="bullet">
    ///   <item>资产配置 —— <c>StateProceduralDriveData.matchTargetPreset</c></item>
    ///   <item>场景物体 —— <c>ESInteractable.matchTargetRequest</c> 等脚本字段</item>
    ///   <item>纯代码构造 —— <c>new MatchTargetRequest { bodyPart = ..., ... }</c></item>
    /// </list>
    /// 该数据包仅承载“请求参数与偏移”；目标位置/旋转由运行时传入。
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
    /// 可用于：
    /// <list type="bullet">
    ///   <item>代码调用 <c>QueueNextMatchTarget</c> 的一次性指令（此时应传入新实例，不得传入 SO 内的对象）</item>
    /// </list>
    /// ★ SO 配置里的 <c>matchTargetTimeline</c> 列表仅作为纯只读数据使用；
    ///   框架通过每实体独立的索引 <c>_configMatchTargetNextTimelineIndex</c> 推进，
    ///   <b>不会读写</b>此类上的任何运行时字段，多实体共享同一 SO 完全安全。<br/>
    /// 注册后，当 <c>hasEnterTime &gt;= triggerAt</c> 时自动激活，
    /// 以 <see cref="request"/> 参数完整重新启动一次 MatchTarget（覆盖当前时间窗口 / 身体部位 / 权重）。<br/>
    /// 触发后自动消耗（单次有效）；状态退出 / Cancel 时自动清除。<br/>
    /// 可调用 <see cref="Reset"/> 重置消耗标记后复用同一实例，避免重复分配。
    /// </summary>
    [Serializable]
    public class MatchTargetPendingCommand
    {
        [LabelText("触发时间(秒)"), Min(0f)]
        [Tooltip("状态进入后经过多少秒触发此步骤（hasEnterTime 基准），到达后覆盖当前 MatchTarget 参数")]
        public float triggerAt;

        [Header("指令参数")]
        [HideLabel]
        public MatchTargetRequest request = MatchTargetRequest.Default;

        // ── 内部运行时状态（仅用于代码驱动的 QueueNextMatchTarget 路径，不用于 SO 时间线） ──
        [NonSerialized] internal bool consumed;

        /// <summary>重置消耗标记，使此实例可以被再次排队触发（仅用于代码驱动路径，勿对 SO 内对象调用）。</summary>
        public void Reset() => consumed = false;
    }

    [Serializable]
    public partial class StateProceduralDriveData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/基础", ShowLabel = true)]
        [LabelText("启用IK"), Tooltip("是否允许对此状态进行IK控制")]
        public bool enableIK = false;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/基础", ShowLabel = true)]
        [LabelText("状态退出时禁用IK"), ShowIf("enableIK")]
        [Tooltip("当状态退出时是否自动禁用IK")]
        public bool disableIKOnExit = true;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/四肢与注视", ShowLabel = true)]
        [Title("左手IK", bold: false)]
        [ShowIf("enableIK"), InlineProperty, HideLabel]
        public IKLimbConfig ikLeftHand = IKLimbConfig.Default;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/四肢与注视", ShowLabel = true)]
        [Title("右手IK", bold: false)]
        [ShowIf("enableIK"), InlineProperty, HideLabel]
        public IKLimbConfig ikRightHand = IKLimbConfig.Default;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/四肢与注视", ShowLabel = true)]
        [Title("左脚IK", bold: false)]
        [ShowIf("enableIK"), InlineProperty, HideLabel]
        public IKLimbConfig ikLeftFoot = IKLimbConfig.Default;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/四肢与注视", ShowLabel = true)]
        [Title("右脚IK", bold: false)]
        [ShowIf("enableIK"), InlineProperty, HideLabel]
        public IKLimbConfig ikRightFoot = IKLimbConfig.Default;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/四肢与注视", ShowLabel = true)]
        [Title("注视IK", bold: false)]
        [ShowIf("enableIK"), InlineProperty, HideLabel]
        public IKLookAtConfig ikLookAt = IKLookAtConfig.Default;

        [BoxGroup("IK配置", ShowLabel = false)]
        [BoxGroup("IK配置/段列表", ShowLabel = true)]
        [LabelText("多段 IK")]
        [Tooltip("按归一化进度求值的 IK 段列表。段可以重叠，重叠区会按影响力自动混合；列表为空时仅使用上方基础 IK 配置。")]
        [ShowIf("enableIK")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowPaging = false)]
        public List<StateIKSegmentConfig> ikSegments = new List<StateIKSegmentConfig>();

        [BoxGroup("MatchTarget配置", ShowLabel = false)]
        [BoxGroup("MatchTarget配置/基础", ShowLabel = true)]
        [LabelText("启用MatchTarget"), Tooltip("是否允许此状态使用MatchTarget对齐")]
        public bool enableMatchTarget = false;

        [BoxGroup("MatchTarget配置", ShowLabel = false)]
        [BoxGroup("MatchTarget配置/基础", ShowLabel = true)]
        [LabelText("状态进入时自动激活"), ShowIf("enableMatchTarget")]
        [Tooltip("状态 Enter 时自动启动下方初始预设；若还配置了后续步骤列表，也会按 triggerAt 秒自动接管。\n关闭时可由代码手动启动初始预设，后续步骤仍会继续按时间自动推进。")]
        public bool autoActivateMatchTarget = false;

        [BoxGroup("MatchTarget配置", ShowLabel = false)]
        [BoxGroup("MatchTarget配置/初始预设", ShowLabel = true)]
        [Title("MatchTarget预设", bold: false)]
        [ShowIf("enableMatchTarget"), HideLabel]
        public MatchTargetRequest matchTargetPreset = MatchTargetRequest.Default;

        [BoxGroup("MatchTarget配置", ShowLabel = false)]
        [BoxGroup("MatchTarget配置/时序步骤", ShowLabel = true)]
        [LabelText("后续步骤"), ShowIf("enableMatchTarget")]
        [Tooltip("按 triggerAt 秒依次覆盖当前 MatchTarget。列表为空时仅使用上方初始预设。\n适合商业项目里的抓取收口、攀爬跨越、挂载对位等多段对齐。")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowPaging = false)]
        public List<MatchTargetPendingCommand> matchTargetTimeline = new List<MatchTargetPendingCommand>();

        public float GetGoalTargetWeight(IKGoal goal, float normalizedProgress = 0f)
        {
            var cfg = ResolveGoalConfig(goal, normalizedProgress);
            return cfg.enabled ? Mathf.Clamp01(cfg.targetWeight) : 0f;
        }

        public float GetGoalLerpingRate(IKGoal goal, float normalizedProgress = 0f)
        {
            var cfg = ResolveGoalConfig(goal, normalizedProgress);
            return cfg.enabled ? Mathf.Clamp(cfg.lerpingRate, 0.05f, 8f) : 1f;
        }

        public ResolvedIKLookAtConfig GetResolvedLookAtConfig(float normalizedProgress = 0f)
        {
            if (!enableIK)
                return ResolvedIKLookAtConfig.Disabled;

            float clampedProgress = Mathf.Clamp01(normalizedProgress);
            float totalInfluence = 0f;
            float totalWeight = 0f;
            float weightedTarget = 0f;
            float weightedResponse = 0f;
            float weightedBody = 0f;
            float weightedHead = 0f;
            float weightedEyes = 0f;
            float weightedClamp = 0f;

            if (ikSegments != null)
            {
                for (int i = 0; i < ikSegments.Count; i++)
                {
                    StateIKSegmentConfig segment = ikSegments[i];
                    if (segment == null || !segment.enabled)
                        continue;

                    float influence = segment.EvaluateInfluence(clampedProgress);
                    if (influence <= 0.0001f)
                        continue;

                    IKLookAtConfig segmentConfig = segment.ikLookAt;
                    if (segmentConfig == null || !segmentConfig.enabled)
                        continue;

                    totalInfluence += influence;
                    totalWeight += influence;
                    weightedTarget += Mathf.Clamp01(segmentConfig.targetWeight) * influence;
                    weightedResponse += Mathf.Clamp(segmentConfig.lerpingRate, 0.05f, 8f) * influence;
                    weightedBody += Mathf.Clamp01(segmentConfig.bodyWeight) * influence;
                    weightedHead += Mathf.Clamp01(segmentConfig.headWeight) * influence;
                    weightedEyes += Mathf.Clamp01(segmentConfig.eyesWeight) * influence;
                    weightedClamp += Mathf.Clamp01(segmentConfig.clampWeight) * influence;
                }
            }

            float fallbackInfluence = Mathf.Clamp01(1f - totalInfluence);
            if (ikLookAt != null && ikLookAt.enabled && fallbackInfluence > 0.0001f)
            {
                totalWeight += fallbackInfluence;
                weightedTarget += Mathf.Clamp01(ikLookAt.targetWeight) * fallbackInfluence;
                weightedResponse += Mathf.Clamp(ikLookAt.lerpingRate, 0.05f, 8f) * fallbackInfluence;
                weightedBody += Mathf.Clamp01(ikLookAt.bodyWeight) * fallbackInfluence;
                weightedHead += Mathf.Clamp01(ikLookAt.headWeight) * fallbackInfluence;
                weightedEyes += Mathf.Clamp01(ikLookAt.eyesWeight) * fallbackInfluence;
                weightedClamp += Mathf.Clamp01(ikLookAt.clampWeight) * fallbackInfluence;
            }

            if (totalWeight <= 0.0001f)
                return ResolvedIKLookAtConfig.Disabled;

            return new ResolvedIKLookAtConfig
            {
                enabled = true,
                targetWeight = weightedTarget / totalWeight,
                lerpingRate = weightedResponse / totalWeight,
                bodyWeight = weightedBody / totalWeight,
                headWeight = weightedHead / totalWeight,
                eyesWeight = weightedEyes / totalWeight,
                clampWeight = weightedClamp / totalWeight,
            };
        }

        public StateProceduralDriveData Clone()
        {
            return new StateProceduralDriveData
            {
                enableIK = enableIK,
                disableIKOnExit = disableIKOnExit,
                ikLeftHand = CloneLimb(ikLeftHand),
                ikRightHand = CloneLimb(ikRightHand),
                ikLeftFoot = CloneLimb(ikLeftFoot),
                ikRightFoot = CloneLimb(ikRightFoot),
                ikLookAt = CloneLookAt(ikLookAt),
                ikSegments = CloneSegments(ikSegments),
                enableMatchTarget = enableMatchTarget,
                autoActivateMatchTarget = autoActivateMatchTarget,
                matchTargetPreset = CloneRequest(matchTargetPreset),
                matchTargetTimeline = CloneTimeline(matchTargetTimeline),
            };
        }

        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            _isRuntimeInitialized = true;
        }

        private ResolvedIKLimbConfig ResolveGoalConfig(IKGoal goal, float normalizedProgress)
        {
            if (!enableIK)
                return ResolvedIKLimbConfig.Disabled;

            float clampedProgress = Mathf.Clamp01(normalizedProgress);
            float totalInfluence = 0f;
            float totalWeight = 0f;
            float weightedTarget = 0f;
            float weightedResponse = 0f;

            if (ikSegments != null)
            {
                for (int i = 0; i < ikSegments.Count; i++)
                {
                    StateIKSegmentConfig segment = ikSegments[i];
                    if (segment == null || !segment.enabled)
                        continue;

                    float influence = segment.EvaluateInfluence(clampedProgress);
                    if (influence <= 0.0001f)
                        continue;

                    IKLimbConfig segmentConfig = segment.GetGoalConfig(goal);
                    if (segmentConfig == null || !segmentConfig.enabled)
                        continue;

                    totalInfluence += influence;
                    totalWeight += influence;
                    weightedTarget += Mathf.Clamp01(segmentConfig.targetWeight) * influence;
                    weightedResponse += Mathf.Clamp(segmentConfig.lerpingRate, 0.05f, 8f) * influence;
                }
            }

            IKLimbConfig fallback = ResolveBaseGoalConfig(goal);
            float fallbackInfluence = Mathf.Clamp01(1f - totalInfluence);
            if (fallback != null && fallback.enabled && fallbackInfluence > 0.0001f)
            {
                totalWeight += fallbackInfluence;
                weightedTarget += Mathf.Clamp01(fallback.targetWeight) * fallbackInfluence;
                weightedResponse += Mathf.Clamp(fallback.lerpingRate, 0.05f, 8f) * fallbackInfluence;
            }

            if (totalWeight <= 0.0001f)
                return ResolvedIKLimbConfig.Disabled;

            return new ResolvedIKLimbConfig
            {
                enabled = true,
                targetWeight = weightedTarget / totalWeight,
                lerpingRate = weightedResponse / totalWeight,
            };
        }

        private IKLimbConfig ResolveBaseGoalConfig(IKGoal goal)
        {
            return goal switch
            {
                IKGoal.LeftHand => ikLeftHand,
                IKGoal.RightHand => ikRightHand,
                IKGoal.LeftFoot => ikLeftFoot,
                IKGoal.RightFoot => ikRightFoot,
                _ => null
            };
        }

        private static IKLimbConfig CloneLimb(IKLimbConfig source)
        {
            if (source == null) return null;
            return new IKLimbConfig
            {
                enabled = source.enabled,
                targetWeight = source.targetWeight,
                lerpingRate = source.lerpingRate,
            };
        }

        private static IKLookAtConfig CloneLookAt(IKLookAtConfig source)
        {
            if (source == null) return null;
            return new IKLookAtConfig
            {
                enabled = source.enabled,
                targetWeight = source.targetWeight,
                lerpingRate = source.lerpingRate,
                bodyWeight = source.bodyWeight,
                headWeight = source.headWeight,
                eyesWeight = source.eyesWeight,
                clampWeight = source.clampWeight,
            };
        }

        private static MatchTargetRequest CloneRequest(MatchTargetRequest source)
        {
            if (source == null) return null;
            return new MatchTargetRequest
            {
                bodyPart = source.bodyPart,
                positionOffset = source.positionOffset,
                enablePositionOffset = source.enablePositionOffset,
                rotationOffsetY = source.rotationOffsetY,
                enableRotationOffset = source.enableRotationOffset,
                timeRange = source.timeRange,
                positionApproachSpeed = source.positionApproachSpeed,
                rotationApproachSpeed = source.rotationApproachSpeed,
                rotationWeight = source.rotationWeight,
            };
        }

        private static List<StateIKSegmentConfig> CloneSegments(List<StateIKSegmentConfig> source)
        {
            var result = new List<StateIKSegmentConfig>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
            {
                var segment = source[i];
                if (segment == null)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(new StateIKSegmentConfig
                {
                    enabled = segment.enabled,
                    name = segment.name,
                    normalizedRange = segment.normalizedRange,
                    feather = segment.feather,
                    ikLeftHand = CloneLimb(segment.ikLeftHand),
                    ikRightHand = CloneLimb(segment.ikRightHand),
                    ikLeftFoot = CloneLimb(segment.ikLeftFoot),
                    ikRightFoot = CloneLimb(segment.ikRightFoot),
                    ikLookAt = CloneLookAt(segment.ikLookAt),
                });
            }

            return result;
        }

        private static List<MatchTargetPendingCommand> CloneTimeline(List<MatchTargetPendingCommand> source)
        {
            var result = new List<MatchTargetPendingCommand>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
            {
                var step = source[i];
                if (step == null)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(new MatchTargetPendingCommand
                {
                    triggerAt = step.triggerAt,
                    request = CloneRequest(step.request) ?? MatchTargetRequest.Default,
                });
            }

            return result;
        }
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

        /// <summary>
        /// 获取Clip和起始时间
        /// </summary>
        /// <param name="context">状态上下文</param>
        /// <returns>返回选定的Clip和起始归一化时间</returns>
        public virtual (AnimationClip clip, float normalizedTime) GetClipAndTime(StateMachineContext context)
        {
            return (null, 0f);
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
