using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;

namespace ES
{
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
    public struct IKLookAtConfig
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
    /// MatchTarget预设配置（Inspector可配置）
    /// </summary>
    [Serializable]
    public struct MatchTargetPresetConfig
    {
        [LabelText("身体部位")]
        public AvatarTarget bodyPart;

        [LabelText("目标Transform")]
        [Tooltip("运行时对齐目标位置/旋转的Transform")]
        public Transform target;

        [LabelText("固定目标位置"), ShowIf("@target == null")]
        [Tooltip("当无目标Transform时的世界空间固定位置")]
        public Vector3 fixedPosition;

        [LabelText("固定目标旋转"), ShowIf("@target == null")]
        public Vector3 fixedRotationEuler;

        [LabelText("开始时间"), Range(0f, 1f)]
        [Tooltip("归一化时间 [0-1]，MatchTarget生效的起始点")]
        public float startNormalizedTime;

        [LabelText("结束时间"), Range(0f, 1f)]
        [Tooltip("归一化时间 [0-1]，MatchTarget生效的结束点")]
        public float endNormalizedTime;

        [LabelText("位置权重 (XYZ)")]
        [Tooltip("各轴向的位置对齐权重")]
        public Vector3 positionWeight;

        [LabelText("旋转权重"), Range(0f, 1f)]
        public float rotationWeight;

        public static MatchTargetPresetConfig Default => new MatchTargetPresetConfig
        {
            bodyPart = AvatarTarget.Root,
            target = null,
            fixedPosition = Vector3.zero,
            fixedRotationEuler = Vector3.zero,
            startNormalizedTime = 0f,
            endNormalizedTime = 1f,
            positionWeight = Vector3.one,
            rotationWeight = 1f
        };
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
        [Tooltip("ConfigOnly=仅Inspector配置\nCodeOnly=仅代码API\nConfigWithCodeOverride=Inspector配置为基础，代码可覆盖")]
        public IKSourceMode ikSourceMode = IKSourceMode.ConfigOnly;

        [FoldoutGroup("IK配置")]
        [LabelText("IK平滑时间"), Range(0f, 0.5f), ShowIf("enableIK")]
        [Tooltip("IK权重变化的平滑过渡时间（秒），0=立即")]
        public float ikSmoothTime = 0.1f;

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
        public MatchTargetPresetConfig matchTargetPreset = MatchTargetPresetConfig.Default;

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
        /// 将Inspector配置的MatchTarget数据应用到Runtime
        /// </summary>
        public void ApplyMatchTargetConfigToRuntime(AnimationCalculatorRuntime runtime)
        {
            if (!enableMatchTarget || !autoActivateMatchTarget || runtime == null) return;

            var preset = matchTargetPreset;
            Vector3 pos = preset.target != null ? preset.target.position : preset.fixedPosition;
            Quaternion rot = preset.target != null ? preset.target.rotation : Quaternion.Euler(preset.fixedRotationEuler);

            runtime.StartMatchTarget(pos, rot, preset.bodyPart,
                preset.startNormalizedTime, preset.endNormalizedTime,
                preset.positionWeight, preset.rotationWeight);
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
