using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/配置/Profile/ES 通用 Profile")]
    public sealed class ESGenericProfile : MonoBehaviour,
        IESGameObjectPoolLifecycle,
        IESGameObjectPoolLifecycleExtensionInstaller
    {
        [SerializeField]
        [ESEditorSection(
            ESProfileEditorSections.NavigatorId,
            ESProfileEditorSections.HeaderId,
            "身份与版本",
            -100f,
            "维护稳定 DefinitionKey、启用状态与 Schema 版本。")]
        [InlineProperty, HideLabel]
        private ESProfileHeader header = new ESProfileHeader();

        [SerializeField]
        [ESEditorSection(
            ESProfileEditorSections.NavigatorId,
            ESProfileEditorSections.SettingsId,
            "能力配置",
            0f,
            "Extension List 是唯一配置权威；生命周期边缘直接转发对应回调。")]
        [InlineProperty, HideLabel]
        private ESGenericProfileSettings settings = new ESGenericProfileSettings();

        [NonSerialized] private ESGenericProfileRuntimeContext runtimeContext;
        [NonSerialized] private ESGenericLife registeredLife;
        [NonSerialized] private GameObject instantiatedChild;
        [NonSerialized] private bool childCreationAttempted;
        [NonSerialized] private bool childOriginalActive;
        [NonSerialized] private bool destroyScheduled;
        [NonSerialized] private int nextPoolGeneration;

        public ESProfileHeader Header
        {
            get
            {
                EnsureSerializedState();
                return header;
            }
        }

        public ESGenericProfileSettings Settings
        {
            get
            {
                EnsureSerializedState();
                return settings;
            }
        }

        public ESGenericProfileRuntimeContext RuntimeContext => EnsureRuntimeContext();
        public GameObject InstantiatedChild => instantiatedChild;
        [ESEditorSection(
            ESProfileEditorSections.NavigatorId,
            ESProfileEditorSections.DiagnosticsId,
            "运行诊断",
            200f,
            "只读查看 Extension 与当前池代状态；不会自动修改配置。")]
        [ShowInInspector, ReadOnly, LabelText("Extension 状态")]
        private string ExtensionRuntimeStatus => runtimeContext == null
            ? "Idle"
            : "Awake=" + runtimeContext.AwakeLifecycleCompleted
                + " / Enable=" + runtimeContext.EnableLifecycleActive
                + " / Pool=" + runtimeContext.PoolLifecycleActive
                + " / Destroy=" + runtimeContext.DestroyLifecycleCompleted;

        [ESEditorSection(
            ESProfileEditorSections.NavigatorId,
            ESProfileEditorSections.DiagnosticsId,
            "运行诊断",
            200f)]
        [ShowInInspector, ReadOnly, LabelText("Pool 状态")]
        private string PoolRuntimeStatus => runtimeContext == null || !runtimeContext.IsPoolSpawned
            ? "Inactive"
            : "Spawned / Generation " + runtimeContext.PoolGeneration;

        private void Reset()
        {
            EnsureSerializedState();
        }

        private void OnValidate()
        {
            EnsureSerializedState();
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            EnsureSerializedState();
            EnsureRuntimeContext();
            TryRegisterWithExistingPoolLife();

            if (settings.AutoAwake)
                NotifyAwake();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || destroyScheduled)
                return;

            EnsureSerializedState();
            EnsureRuntimeContext();
            if (settings.AutoEnable)
                NotifyEnable();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || destroyScheduled)
                return;

            EnsureSerializedState();
            if (settings.AutoEnable)
                NotifyDisable();

            TryRegisterWithExistingPoolLife();
        }

        private void OnDestroy()
        {
            NotifyDestroy();

            if (registeredLife != null && !registeredLife.IsPoolSpawned)
                registeredLife.UnregisterPoolExtension(this);

            registeredLife = null;
            DestroyInstantiatedChild();
        }

        public void OnPoolSpawned()
        {
            EnsureSerializedState();
            ESGenericProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.IsPoolSpawned)
                return;

            if (context.PoolLifecycleActive)
            {
                Debug.LogError(
                    "[ESGenericProfile] 检测到上一 Pool 代未通知 Despawn；已在新一代 Spawn 前强制收口。",
                    this);
                NotifyPoolDespawned();
            }

            context.BeginPoolSpawn(++nextPoolGeneration);
            if (settings.AutoPoolLifecycle)
                NotifyPoolSpawned();
        }

        public void OnPoolDespawned()
        {
            EnsureSerializedState();
            if (settings.AutoPoolLifecycle)
                NotifyPoolDespawned();

            EnsureRuntimeContext().ClearPoolGeneration();
        }

        bool IESGameObjectPoolLifecycleExtensionInstaller.TryInstallPoolLifecycleExtension(ESGenericLife life)
        {
            if (life == null
                || life.gameObject != gameObject
                || gameObject.activeSelf
                || life.PoolRootLifecycleComponent == null
                || life.PoolRootLifecycleComponent == this)
                return false;

            if (registeredLife == life)
                return true;

            if (!life.RegisterPoolExtension(this))
                return false;

            if (!life.ValidatePoolLifecycle())
            {
                life.UnregisterPoolExtension(this);
                return false;
            }

            registeredLife = life;
            return true;
        }

        public bool NotifyAwake()
        {
            EnsureSerializedState();
            ESGenericProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.AwakeLifecycleCompleted)
                return true;

            if (!CanBeginLifecycle(context))
                return false;

            if (!ValidateBeforeLifecycle("Awake"))
                return false;

            if (!DispatchStartingLifecycle(ESGenericProfileLifecyclePhase.Awake, context))
                return false;

            context.MarkAwakeLifecycleCompleted();
            return true;
        }

        public bool NotifyEnable()
        {
            EnsureSerializedState();
            ESGenericProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.EnableLifecycleActive)
                return true;

            if (!CanBeginLifecycle(context) || !ValidateBeforeLifecycle("Enable"))
                return false;

            if (!DispatchStartingLifecycle(ESGenericProfileLifecyclePhase.Enable, context))
                return false;

            context.MarkEnableLifecycleActive(true);
            return true;
        }

        public bool NotifyDisable()
        {
            if (runtimeContext == null
                || !runtimeContext.HasAnyExtensionState(
                    ESGenericProfileExtensionLifecycleState.Enable))
            {
                if (runtimeContext != null)
                    runtimeContext.MarkEnableLifecycleActive(false);
                return true;
            }

            EnsureSerializedState();
            bool success = DispatchEndingLifecycle(
                ESGenericProfileLifecyclePhase.Disable,
                runtimeContext,
                settings.Extensions.Count - 1,
                ESGenericProfileExtensionLifecycleState.Enable,
                ESGenericProfileEndingCompletion.NormalPhase);
            runtimeContext.MarkEnableLifecycleActive(
                runtimeContext.HasAnyExtensionState(
                    ESGenericProfileExtensionLifecycleState.Enable));
            return success;
        }

        public bool NotifyPoolSpawned()
        {
            EnsureSerializedState();
            ESGenericProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.PoolLifecycleActive)
                return true;

            if (!CanBeginLifecycle(context) || !ValidateBeforeLifecycle("Pool Spawned"))
                return false;

            if (!context.IsPoolSpawned)
                context.BeginPoolSpawn(++nextPoolGeneration);

            if (!DispatchStartingLifecycle(ESGenericProfileLifecyclePhase.PoolSpawned, context))
            {
                if (!context.HasAnyExtensionState(ESGenericProfileExtensionLifecycleState.Pool))
                    context.ClearPoolGeneration();
                return false;
            }

            context.MarkPoolLifecycleActive(true);
            return true;
        }

        public bool NotifyPoolDespawned()
        {
            if (runtimeContext == null)
                return true;

            if (!runtimeContext.HasAnyExtensionState(
                    ESGenericProfileExtensionLifecycleState.Pool))
            {
                runtimeContext.MarkPoolLifecycleActive(false);
                runtimeContext.ClearPoolGeneration();
                return true;
            }

            EnsureSerializedState();
            bool success = DispatchEndingLifecycle(
                ESGenericProfileLifecyclePhase.PoolDespawned,
                runtimeContext,
                settings.Extensions.Count - 1,
                ESGenericProfileExtensionLifecycleState.Pool,
                ESGenericProfileEndingCompletion.NormalPhase);
            bool remainsActive = runtimeContext.HasAnyExtensionState(
                ESGenericProfileExtensionLifecycleState.Pool);
            runtimeContext.MarkPoolLifecycleActive(remainsActive);
            if (!remainsActive)
                runtimeContext.ClearPoolGeneration();
            return success;
        }

        public bool NotifyDestroy()
        {
            ESGenericProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.DestroyLifecycleCompleted)
                return true;

            EnsureSerializedState();
            bool success = true;
            if (context.HasAnyExtensionState(ESGenericProfileExtensionLifecycleState.Pool)
                && !NotifyPoolDespawned())
            {
                success = false;
            }

            if (context.HasAnyExtensionState(ESGenericProfileExtensionLifecycleState.Enable)
                && !NotifyDisable())
            {
                success = false;
            }

            if (context.HasAnyExtensionState(
                    ESGenericProfileExtensionLifecycleState.EverEntered)
                && !DispatchEndingLifecycle(
                    ESGenericProfileLifecyclePhase.Destroy,
                    context,
                    settings.Extensions.Count - 1,
                    ESGenericProfileExtensionLifecycleState.EverEntered,
                    ESGenericProfileEndingCompletion.FinalDestroy))
            {
                success = false;
            }

            context.MarkEnableLifecycleActive(false);
            context.MarkPoolLifecycleActive(false);
            context.MarkDestroyLifecycleCompleted();
            context.ClearPoolGeneration();
            DestroyInstantiatedChild();
            return success;
        }

        public bool ValidateProfile(List<string> issues)
        {
            if (issues == null)
                return false;

            EnsureSerializedState();
            int startCount = issues.Count;
            if (string.IsNullOrWhiteSpace(header.DefinitionKey))
                issues.Add("DefinitionKey 为空；OnValidate/Awake 应自动生成稳定身份。");
            if (header.RequiresMigration)
            {
                issues.Add(
                    "Header SchemaVersion 为 " + header.SchemaVersion
                    + "，当前版本为 " + ESProfileHeader.CurrentSchemaVersion
                    + "；必须在 Editor 中执行显式迁移。");
            }
            else if (header.HasUnsupportedFutureSchema)
            {
                issues.Add(
                    "Header SchemaVersion 为未来版本 " + header.SchemaVersion
                    + "，当前代码只支持到 " + ESProfileHeader.CurrentSchemaVersion + "。");
            }
            else if (!header.IsSchemaCurrent)
            {
                issues.Add("Header SchemaVersion 无效：" + header.SchemaVersion + "。");
            }

            settings.ValidateExtensions(this, issues);

            ESGenericLife life = GetComponent<ESGenericLife>();
            if (life != null)
            {
                if (life.PoolRootLifecycleComponent == null)
                    issues.Add("同根 ESGenericLife 尚未绑定合法 Pool Root；GenericProfile 不会自动抢占 Root。");
                else if (life.PoolRootLifecycleComponent == this)
                    issues.Add("GenericProfile 只能作为 Pool Extension，不能成为 ESGenericLife Root。");
            }

            return issues.Count == startCount;
        }

        internal static bool ShouldDestroyProfileComponent(bool configured, bool isEditorRuntime)
        {
            return configured && !isEditorRuntime;
        }

        internal void TrySchedulePlayerDestroy(bool configured)
        {
#if UNITY_EDITOR
            const bool isEditorRuntime = true;
#else
            const bool isEditorRuntime = false;
#endif
            if (!ShouldDestroyProfileComponent(configured, isEditorRuntime))
                return;

            destroyScheduled = true;
            Destroy(this);
        }

        internal void EnsureInstantiatedChildActive(ESGenericProfileChildPrefabSettings extension)
        {
            if (extension == null)
                return;

            if (childCreationAttempted)
            {
                if (instantiatedChild != null)
                    instantiatedChild.SetActive(childOriginalActive);
                return;
            }

            GameObject prefab = extension.Prefab;
            Transform parent = extension.Parent;
            childCreationAttempted = true;
            if (prefab == null || parent == null || !IsProfileRootOrDescendant(parent))
            {
                Debug.LogError("[ESGenericProfile] Child Prefab 配置无效：Prefab 与 Parent 必须存在，且 Parent 必须位于 Profile 根下。", this);
                return;
            }

            childOriginalActive = prefab.activeSelf;
            instantiatedChild = Instantiate(prefab, parent, false);
        }

        internal void DeactivateInstantiatedChild()
        {
            if (instantiatedChild != null)
                instantiatedChild.SetActive(false);
        }

        internal void DestroyInstantiatedChild()
        {
            if (instantiatedChild == null)
                return;

            if (Application.isPlaying)
                Destroy(instantiatedChild);
            else
                DestroyImmediate(instantiatedChild);

            instantiatedChild = null;
        }

        internal void WriteConfiguredDebug(
            ESGenericProfileDebugSettings extension,
            ESGenericProfileDebugEventMask eventType)
        {
            if (extension == null
                || !extension.Enabled
                || (extension.EventMask & eventType) == 0
                || (extension.DevelopmentOnly && !Debug.isDebugBuild))
                return;

            string message = string.IsNullOrWhiteSpace(extension.Message)
                ? "[ESGenericProfile] " + eventType
                : extension.Message;
            switch (extension.LogLevel)
            {
                case ESGenericProfileLogLevel.Warning:
                    Debug.LogWarning(message, this);
                    break;
                case ESGenericProfileLogLevel.Error:
                    Debug.LogError(message, this);
                    break;
                default:
                    Debug.Log(message, this);
                    break;
            }
        }

        internal bool IsProfileRootOrDescendant(Transform candidate)
        {
            return candidate == transform || candidate.IsChildOf(transform);
        }

        private void EnsureSerializedState()
        {
            header ??= new ESProfileHeader();
            settings ??= new ESGenericProfileSettings();
            header.EnsureDefinitionKey();
            settings.EnsureDefaults();
        }

        private ESGenericProfileRuntimeContext EnsureRuntimeContext()
        {
            return runtimeContext ??= new ESGenericProfileRuntimeContext();
        }

        private void TryRegisterWithExistingPoolLife()
        {
            if (registeredLife != null || gameObject.activeSelf)
                return;

            ESGenericLife life = GetComponent<ESGenericLife>();
            if (life == null || life.PoolRootLifecycleComponent == null)
                return;

            ((IESGameObjectPoolLifecycleExtensionInstaller)this).TryInstallPoolLifecycleExtension(life);
        }

        private bool CanBeginLifecycle(ESGenericProfileRuntimeContext context)
        {
            return header.ProfileEnabled
                && !destroyScheduled
                && !context.DestroyLifecycleCompleted;
        }

        private bool ValidateBeforeLifecycle(string lifecycleName)
        {
            if (!header.IsSchemaCurrent)
            {
                Debug.LogError(
                    "[ESGenericProfile] Header SchemaVersion=" + header.SchemaVersion
                    + "，当前版本=" + ESProfileHeader.CurrentSchemaVersion
                    + "；必须先完成显式迁移，已阻止 " + lifecycleName + " 生命周期转发。",
                    this);
                return false;
            }

            if (settings.ValidateExtensions(this, null))
                return true;

            Debug.LogError(
                "[ESGenericProfile] Extension 配置无效，已阻止 " + lifecycleName + " 生命周期转发。",
                this);
            return false;
        }

        private bool DispatchStartingLifecycle(
            ESGenericProfileLifecyclePhase phase,
            ESGenericProfileRuntimeContext context)
        {
            IReadOnlyList<ESGenericProfileExtensionSettings> extensions = settings.Extensions;
            ESGenericProfileExtensionLifecycleState phaseState = GetStartingLifecycleState(phase);
            if (context.HasAnyExtensionState(phaseState))
            {
                Debug.LogError(
                    "[ESGenericProfile] " + phase
                    + " 存在上次失败后未清理的 Extension，已阻止重复进入。",
                    this);
                return false;
            }

            context.PrepareStartingExtensionPhase(extensions.Count);
            int lastStartedIndex = -1;
            try
            {
                for (int i = 0; i < extensions.Count; i++)
                {
                    ESGenericProfileExtensionSettings extension = extensions[i];
                    if (extension == null || !extension.Enabled)
                        continue;

                    lastStartedIndex = i;
                    context.MarkExtensionEntering(i, phaseState);
                    InvokeLifecycle(extension, phase, context);
                    if (destroyScheduled)
                        break;
                }

                context.CommitStartingExtensionPhase();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ESGenericProfileLifecyclePhase rollbackPhase = phase == ESGenericProfileLifecyclePhase.Awake
                    ? ESGenericProfileLifecyclePhase.Destroy
                    : phase == ESGenericProfileLifecyclePhase.Enable
                        ? ESGenericProfileLifecyclePhase.Disable
                        : ESGenericProfileLifecyclePhase.PoolDespawned;
                DispatchEndingLifecycle(
                    rollbackPhase,
                    context,
                    lastStartedIndex,
                    phaseState,
                    ESGenericProfileEndingCompletion.RollbackStartingPhase);
                context.CommitStartingExtensionPhase();
                return false;
            }
        }

        private bool DispatchEndingLifecycle(
            ESGenericProfileLifecyclePhase phase,
            ESGenericProfileRuntimeContext context,
            int startIndex,
            ESGenericProfileExtensionLifecycleState requiredState,
            ESGenericProfileEndingCompletion completion)
        {
            bool success = true;
            IReadOnlyList<ESGenericProfileExtensionSettings> extensions = settings.Extensions;
            int boundedStartIndex = Mathf.Min(startIndex, extensions.Count - 1);
            for (int i = boundedStartIndex; i >= 0; i--)
            {
                ESGenericProfileExtensionSettings extension = extensions[i];
                if (extension == null || !context.HasExtensionState(i, requiredState))
                    continue;

                try
                {
                    InvokeLifecycle(extension, phase, context);
                    switch (completion)
                    {
                        case ESGenericProfileEndingCompletion.NormalPhase:
                            context.CompleteExtensionPhase(i, requiredState);
                            break;
                        case ESGenericProfileEndingCompletion.RollbackStartingPhase:
                            context.CompleteRolledBackExtensionPhase(i, requiredState);
                            break;
                        case ESGenericProfileEndingCompletion.FinalDestroy:
                            context.CompleteExtensionDestroy(i);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(completion),
                                completion,
                                null);
                    }
                }
                catch (Exception exception)
                {
                    success = false;
                    context.MarkExtensionEndingFailed(i);
                    Debug.LogException(exception, this);
                }
            }

            return success;
        }

        private static ESGenericProfileExtensionLifecycleState GetStartingLifecycleState(
            ESGenericProfileLifecyclePhase phase)
        {
            switch (phase)
            {
                case ESGenericProfileLifecyclePhase.Awake:
                    return ESGenericProfileExtensionLifecycleState.Awake;
                case ESGenericProfileLifecyclePhase.Enable:
                    return ESGenericProfileExtensionLifecycleState.Enable;
                case ESGenericProfileLifecyclePhase.PoolSpawned:
                    return ESGenericProfileExtensionLifecycleState.Pool;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private void InvokeLifecycle(
            ESGenericProfileExtensionSettings extension,
            ESGenericProfileLifecyclePhase phase,
            ESGenericProfileRuntimeContext context)
        {
            switch (phase)
            {
                case ESGenericProfileLifecyclePhase.Awake:
                    extension.OnProfileAwake(this, context);
                    break;
                case ESGenericProfileLifecyclePhase.Enable:
                    extension.OnProfileEnable(this, context);
                    break;
                case ESGenericProfileLifecyclePhase.Disable:
                    extension.OnProfileDisable(this, context);
                    break;
                case ESGenericProfileLifecyclePhase.PoolSpawned:
                    extension.OnProfilePoolSpawned(this, context);
                    break;
                case ESGenericProfileLifecyclePhase.PoolDespawned:
                    extension.OnProfilePoolDespawned(this, context);
                    break;
                case ESGenericProfileLifecyclePhase.Destroy:
                    extension.OnProfileDestroy(this, context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private enum ESGenericProfileLifecyclePhase
        {
            Awake,
            Enable,
            Disable,
            PoolSpawned,
            PoolDespawned,
            Destroy
        }

        private enum ESGenericProfileEndingCompletion
        {
            NormalPhase,
            RollbackStartingPhase,
            FinalDestroy
        }
    }
}
