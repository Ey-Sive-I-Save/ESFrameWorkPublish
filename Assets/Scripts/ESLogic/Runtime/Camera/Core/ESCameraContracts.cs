using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

[assembly: InternalsVisibleTo("ES_Design.ConfigKey.Tests")]

namespace ES
{
    // 此文件属于 ES_Logic.asmdef；保留该注释以便 AssetDatabase 对当前相机契约文本重新导入。
    /// <summary>
    /// 一个本地观测视口的稳定标识。当前项目默认只注册 MainView；请求与 Lease
    /// 仍必须携带该标识，以免未来回放、观战或分屏共享错误的相机仲裁集合。
    /// </summary>
    [Serializable]
    public readonly struct ESCameraViewId : IEquatable<ESCameraViewId>
    {
        public static readonly ESCameraViewId Main = new ESCameraViewId("MainView");

        [SerializeField] private readonly string key;
        [NonSerialized] private readonly int runtimeHashCode;
        [NonSerialized] private readonly bool runtimeIsValid;

        public ESCameraViewId(string key)
        {
            this.key = key;
            runtimeIsValid = !string.IsNullOrWhiteSpace(key);
            runtimeHashCode = runtimeIsValid ? StringComparer.Ordinal.GetHashCode(key) : 0;
        }

        public string Key => key;
        public bool IsValid => runtimeIsValid || !string.IsNullOrWhiteSpace(key);

        public bool Equals(ESCameraViewId other)
        {
            if (ReferenceEquals(key, other.key))
                return true;
            if (runtimeHashCode != 0 && other.runtimeHashCode != 0 && runtimeHashCode != other.runtimeHashCode)
                return false;
            return string.Equals(key, other.key, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) => obj is ESCameraViewId other && Equals(other);
        public override int GetHashCode()
        {
            return runtimeHashCode != 0
                ? runtimeHashCode
                : key != null ? StringComparer.Ordinal.GetHashCode(key) : 0;
        }
        public override string ToString() => key ?? string.Empty;

        public static bool operator ==(ESCameraViewId left, ESCameraViewId right) => left.Equals(right);
        public static bool operator !=(ESCameraViewId left, ESCameraViewId right) => !left.Equals(right);
    }

    /// <summary>
    /// Base 在同一 View 中只允许一个获胜者；Shot 使用同一仲裁面但拥有更高类型权重。
    /// Modifier 将在 P1 以独立的字段合成契约接入，不能拿 Base 的优先级覆盖逻辑冒充实现。
    /// </summary>
    public enum ESCameraRequestKind : byte
    {
        Base = 0,
        Shot = 1,
        Modifier = 2,
    }

    /// <summary>Modifier 对一个字段的明确合成方式；禁止以提交先后隐式决定字段语义。</summary>
    public enum ESCameraModifierOperation : byte
    {
        None = 0,
        Override = 1,
        Add = 2,
        Multiply = 3,
    }

    [Serializable]
    public struct ESCameraScalarModifier
    {
        public ESCameraModifierOperation operation;
        public float value;

        public bool IsConfigured => operation != ESCameraModifierOperation.None;

        public bool IsValid => IsFinite(value)
                               && (operation == ESCameraModifierOperation.Override
                                   || operation == ESCameraModifierOperation.Add
                                   || operation == ESCameraModifierOperation.Multiply);

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public struct ESCameraVectorModifier
    {
        public ESCameraModifierOperation operation;
        public Vector3 value;

        public bool IsConfigured => operation != ESCameraModifierOperation.None;

        public bool IsValid => IsFinite(value)
                               && (operation == ESCameraModifierOperation.Override
                                   || operation == ESCameraModifierOperation.Add);

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Modifier 请求的数据面。每个字段独立按 Override / Add / Multiply 合成；Override
    /// 按 priority、再按稳定 submissionSequence 选胜者，Add/Multiply 均聚合所有兼容请求。
    /// </summary>
    [Serializable]
    public struct ESCameraModifier
    {
        public ESCameraScalarModifier fieldOfView;
        public ESCameraScalarModifier distanceScale;
        public ESCameraVectorModifier shoulderOffset;
        public ESCameraScalarModifier shakeAmplitude;

        public bool IsValid
        {
            get
            {
                bool hasConfiguredField = fieldOfView.IsConfigured
                                           || distanceScale.IsConfigured
                                           || shoulderOffset.IsConfigured
                                           || shakeAmplitude.IsConfigured;
                if (!hasConfiguredField)
                    return false;

                // None means an intentionally unused sparse field. Every declared field still
                // has to be valid; one good field must never mask a NaN or unknown operation in
                // another field and poison the resolved camera pose.
                return (!fieldOfView.IsConfigured || fieldOfView.IsValid)
                       && (!distanceScale.IsConfigured || distanceScale.IsValid)
                       && (!shoulderOffset.IsConfigured || shoulderOffset.IsValid)
                       && (!shakeAmplitude.IsConfigured || shakeAmplitude.IsValid);
            }
        }
    }

    /// <summary>
    /// 只描述镜头意图，不持有 Cinemachine Virtual Camera、Rig Prefab 或任意场景实例。
    /// definition 是业务与相机内容之间唯一允许使用的稳定内容引用。Director 接收后会
    /// 在当前 View 的唯一 Catalog 中解析为 RuntimeHandle；业务绝不构造裸 RuntimeKey。
    /// </summary>
    [Serializable]
    public struct ESCameraRequest
    {
        public ESCameraViewId viewId;

        public ESCameraRequestKind kind;

        public ESCameraDefinitionReference definition;

        public int priority;

        public UnityEngine.Object owner;

        public Transform follow;

        public Transform lookAt;

        /// <summary>仅 Modifier 使用；非空时只对相同 Definition 的当前获胜 Base/Shot 生效。</summary>
        public ESCameraDefinitionReference compatibleDefinition;

        [NonSerialized] internal ESCameraDefinitionRuntimeHandle definitionHandle;
        [NonSerialized] internal ESCameraDefinitionRuntimeHandle compatibleDefinitionHandle;

        public ESCameraModifier modifier;

        public bool IsStructurallyValid
        {
            get
            {
                if (!viewId.IsValid || owner == null)
                    return false;

                if (kind == ESCameraRequestKind.Modifier)
                    return modifier.IsValid;

                return (kind == ESCameraRequestKind.Base || kind == ESCameraRequestKind.Shot)
                       && definition.IsConfigured
                       && follow != null;
            }
        }

        public static ESCameraRequest CreateBase(
            ESCameraViewId viewId,
            ESCameraDefinitionReference definition,
            int priority,
            UnityEngine.Object owner,
            Transform follow,
            Transform lookAt = null)
        {
            return new ESCameraRequest
            {
                viewId = viewId,
                kind = ESCameraRequestKind.Base,
                definition = definition,
                priority = priority,
                owner = owner,
                follow = follow,
                lookAt = lookAt,
            };
        }

        public static ESCameraRequest CreateShot(
            ESCameraViewId viewId,
            ESCameraDefinitionReference definition,
            int priority,
            UnityEngine.Object owner,
            Transform follow,
            Transform lookAt = null)
        {
            ESCameraRequest request = CreateBase(viewId, definition, priority, owner, follow, lookAt);
            request.kind = ESCameraRequestKind.Shot;
            return request;
        }

        public static ESCameraRequest CreateModifier(
            ESCameraViewId viewId,
            int priority,
            UnityEngine.Object owner,
            ESCameraModifier modifier,
            ESCameraDefinitionReference compatibleDefinition = default)
        {
            return new ESCameraRequest
            {
                viewId = viewId,
                kind = ESCameraRequestKind.Modifier,
                priority = priority,
                owner = owner,
                modifier = modifier,
                compatibleDefinition = compatibleDefinition,
            };
        }
    }

    /// <summary>
    /// Slot + generation + scene epoch 的相机租约。旧 Lease 永远不能释放复用后的槽位，
    /// 也不能跨 SceneBinding 生命周期影响新场景。
    /// </summary>
    [Serializable]
    public readonly struct ESCameraLease : IEquatable<ESCameraLease>, IDisposable
    {
        public static readonly ESCameraLease Invalid = default;

        internal readonly ESCameraViewId viewId;
        internal readonly int sceneEpoch;
        internal readonly int slot;
        internal readonly int generation;

        internal ESCameraLease(ESCameraViewId viewId, int sceneEpoch, int slot, int generation)
        {
            this.viewId = viewId;
            this.sceneEpoch = sceneEpoch;
            this.slot = slot;
            this.generation = generation;
        }

        public ESCameraViewId ViewId => viewId;
        public bool IsValid => viewId.IsValid && sceneEpoch > 0 && slot >= 0 && generation > 0;

        /// <summary>
        /// 语义化释放入口。generation/epoch 校验仍由 Camera 模块完成；旧 Lease 调用安全无效。
        /// </summary>
        public void Dispose()
        {
            ESGameManager.Camera?.Release(this);
        }

        /// <summary>仅本地观测 Owner 的有效 Lease 可以提交 Look。</summary>
        public bool TrySetLook(Vector2 lookInput)
        {
            return ESGameManager.Camera != null && ESGameManager.Camera.TrySetLook(this, lookInput);
        }

        /// <summary>仅本地观测 Owner 的有效 Lease 可以更新 Follow/LookAt 目标。</summary>
        public bool TrySetTarget(Transform follow, Transform lookAt = null)
        {
            return ESGameManager.Camera != null && ESGameManager.Camera.TrySetTarget(this, follow, lookAt);
        }

        public bool Equals(ESCameraLease other)
        {
            return viewId == other.viewId
                   && sceneEpoch == other.sceneEpoch
                   && slot == other.slot
                   && generation == other.generation;
        }

        public override bool Equals(object obj) => obj is ESCameraLease other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = viewId.GetHashCode();
                hash = (hash * 397) ^ sceneEpoch;
                hash = (hash * 397) ^ slot;
                return (hash * 397) ^ generation;
            }
        }
        public static bool operator ==(ESCameraLease left, ESCameraLease right) => left.Equals(right);
        public static bool operator !=(ESCameraLease left, ESCameraLease right) => !left.Equals(right);
    }

    /// <summary>Director 给执行层的单帧获胜结果。执行层不得反向仲裁或保存业务 Lease。</summary>
    public readonly struct ESCameraResolvedView
    {
        public readonly bool hasWinner;
        public readonly bool configurationChanged;
        public readonly ESCameraDefinitionReference definition;
        public readonly ESCameraDefinitionRuntimeHandle definitionHandle;
        public readonly Transform follow;
        public readonly Transform lookAt;
        public readonly UnityEngine.Object owner;
        public readonly Vector2 lookInput;
        public readonly bool hasLookInput;
        public readonly ESCameraResolvedModifiers modifiers;

        internal ESCameraResolvedView(
            bool hasWinner,
            bool configurationChanged,
            ESCameraDefinitionReference definition,
            ESCameraDefinitionRuntimeHandle definitionHandle,
            Transform follow,
            Transform lookAt,
            UnityEngine.Object owner,
            Vector2 lookInput,
            bool hasLookInput,
            ESCameraResolvedModifiers modifiers)
        {
            this.hasWinner = hasWinner;
            this.configurationChanged = configurationChanged;
            this.definition = definition;
            this.definitionHandle = definitionHandle;
            this.follow = follow;
            this.lookAt = lookAt;
            this.owner = owner;
            this.lookInput = lookInput;
            this.hasLookInput = hasLookInput;
            this.modifiers = modifiers;
        }
    }

    /// <summary>
    /// 相机运行时的只读诊断快照。仅暴露仲裁结果和生命周期身份，不暴露 VCam、Rig 或
    /// 内部槽位引用；调用方可将其写入外部证据回执而不改变相机状态。
    /// </summary>
    public readonly struct ESCameraDiagnosticSnapshot
    {
        public readonly ESCameraViewId viewId;
        public readonly int sceneEpoch;
        public readonly int activeRequestCount;
        public readonly bool hasWinner;
        public readonly ESCameraRequestKind winnerKind;
        public readonly ESCameraDefinitionReference winnerDefinition;
        public readonly UnityEngine.Object winnerOwner;

        internal ESCameraDiagnosticSnapshot(
            ESCameraViewId viewId,
            int sceneEpoch,
            int activeRequestCount,
            bool hasWinner,
            ESCameraRequestKind winnerKind,
            ESCameraDefinitionReference winnerDefinition,
            UnityEngine.Object winnerOwner)
        {
            this.viewId = viewId;
            this.sceneEpoch = sceneEpoch;
            this.activeRequestCount = activeRequestCount;
            this.hasWinner = hasWinner;
            this.winnerKind = winnerKind;
            this.winnerDefinition = winnerDefinition;
            this.winnerOwner = winnerOwner;
        }

        public string WinnerDefinitionKey => winnerDefinition.stringKey ?? string.Empty;

        public override string ToString()
        {
            return string.Format(
                "View={0};Epoch={1};Active={2};Winner={3};Kind={4};Definition={5};Owner={6}",
                viewId,
                sceneEpoch,
                activeRequestCount,
                hasWinner,
                winnerKind,
                WinnerDefinitionKey,
                winnerOwner != null ? winnerOwner.name : "<none>");
        }
    }

    /// <summary>
    /// 可序列化的相机诊断回执投影。它只包含稳定标量/字符串和赢家身份，适合写入
    /// PlayMode 或 Profiler 证据；运行时对象引用不会进入回执。
    /// </summary>
    [Serializable]
    public struct ESCameraDiagnosticReceipt
    {
        public int frame;
        public string viewKey;
        public int sceneEpoch;
        public int activeRequestCount;
        public bool hasWinner;
        public ESCameraRequestKind winnerKind;
        public string winnerDefinitionKey;
        public string winnerOwnerName;
        public string scenePath;
        public string platform;
        public string buildId;

        public static ESCameraDiagnosticReceipt FromSnapshot(in ESCameraDiagnosticSnapshot snapshot, int frame)
        {
            return FromSnapshot(snapshot, frame, string.Empty, string.Empty, string.Empty);
        }

        public static ESCameraDiagnosticReceipt FromSnapshot(
            in ESCameraDiagnosticSnapshot snapshot,
            int frame,
            string scenePath,
            string platform,
            string buildId)
        {
            return new ESCameraDiagnosticReceipt
            {
                frame = frame,
                viewKey = snapshot.viewId.Key ?? string.Empty,
                sceneEpoch = snapshot.sceneEpoch,
                activeRequestCount = snapshot.activeRequestCount,
                hasWinner = snapshot.hasWinner,
                winnerKind = snapshot.winnerKind,
                winnerDefinitionKey = snapshot.WinnerDefinitionKey,
                winnerOwnerName = snapshot.winnerOwner != null ? snapshot.winnerOwner.name : string.Empty,
                scenePath = scenePath ?? string.Empty,
                platform = platform ?? string.Empty,
                buildId = buildId ?? string.Empty,
            };
        }
    }

    /// <summary>Director 已完成仲裁的 Modifier 结果，Adapter 只执行，不可重新仲裁。</summary>
    public readonly struct ESCameraResolvedModifiers
    {
        public static readonly ESCameraResolvedModifiers Identity = new ESCameraResolvedModifiers(
            new ESCameraScalarComposition(false, 0f, 0f, 1f),
            new ESCameraScalarComposition(false, 0f, 0f, 1f),
            new ESCameraVectorComposition(false, Vector3.zero, Vector3.zero),
            new ESCameraScalarComposition(false, 0f, 0f, 1f));

        public readonly ESCameraScalarComposition fieldOfView;
        public readonly ESCameraScalarComposition distanceScale;
        public readonly ESCameraVectorComposition shoulderOffset;
        public readonly ESCameraScalarComposition shakeAmplitude;

        internal ESCameraResolvedModifiers(
            ESCameraScalarComposition fieldOfView,
            ESCameraScalarComposition distanceScale,
            ESCameraVectorComposition shoulderOffset,
            ESCameraScalarComposition shakeAmplitude)
        {
            this.fieldOfView = fieldOfView;
            this.distanceScale = distanceScale;
            this.shoulderOffset = shoulderOffset;
            this.shakeAmplitude = shakeAmplitude;
        }
    }

    public readonly struct ESCameraScalarComposition
    {
        public readonly bool hasOverride;
        public readonly float overrideValue;
        public readonly float additiveValue;
        public readonly float multiplier;

        internal ESCameraScalarComposition(bool hasOverride, float overrideValue, float additiveValue, float multiplier)
        {
            this.hasOverride = hasOverride;
            this.overrideValue = overrideValue;
            this.additiveValue = additiveValue;
            this.multiplier = multiplier;
        }

        public float Apply(float baseValue)
        {
            return ((hasOverride ? overrideValue : baseValue) + additiveValue) * multiplier;
        }
    }

    public readonly struct ESCameraVectorComposition
    {
        public readonly bool hasOverride;
        public readonly Vector3 overrideValue;
        public readonly Vector3 additiveValue;

        internal ESCameraVectorComposition(bool hasOverride, Vector3 overrideValue, Vector3 additiveValue)
        {
            this.hasOverride = hasOverride;
            this.overrideValue = overrideValue;
            this.additiveValue = additiveValue;
        }

        public Vector3 Apply(Vector3 baseValue)
        {
            return (hasOverride ? overrideValue : baseValue) + additiveValue;
        }
    }

    /// <summary>
    /// Cinemachine 之外的纯执行边界。唯一的 CM2 写入实现位于 Cinemachine2 目录；
    /// Core、Skill 与 Entity 均不能获得该实现的 Virtual Camera 引用。
    /// </summary>
    public interface IESCameraViewAdapter
    {
        bool IsReady { get; }
        Transform OutputTransform { get; }
        bool TryResolveDefinition(ESCameraDefinitionReference reference, out ESCameraDefinitionRuntimeHandle handle);
        /// <summary>执行已仲裁视图；返回 false 表示执行层未能应用该视图。</summary>
        bool Apply(in ESCameraResolvedView resolved);
        void Clear();
    }
}
