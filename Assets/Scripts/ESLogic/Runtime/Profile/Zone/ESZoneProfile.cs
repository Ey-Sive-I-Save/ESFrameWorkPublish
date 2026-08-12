using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ESZone))]
    [AddComponentMenu("【ES】/基础设施/Profile/ES Zone Profile")]
    public sealed class ESZoneProfile : MonoBehaviour,
        IESGameObjectPoolLifecycle,
        IESGameObjectPoolLifecycleExtensionInstaller
    {
        [SerializeField]
        [ESEditorSection(
            ESProfileEditorSections.NavigatorId,
            ESProfileEditorSections.HeaderId,
            "身份与版本",
            -100f)]
        [InlineProperty, HideLabel]
        private ESProfileHeader header = new ESProfileHeader();

        [SerializeField]
        [ESEditorSection(
            ESProfileEditorSections.NavigatorId,
            ESProfileEditorSections.SettingsId,
            "区域能力",
            0f,
            "Settings 与单层 Extension List 是 Zone 的唯一能力装配权威。")]
        [InlineProperty, HideLabel]
        private ESZoneProfileSettings settings = new ESZoneProfileSettings();

        [NonSerialized] private ESZoneProfileRuntimeContext runtimeContext;
        [NonSerialized] private ESZone zone;
        [NonSerialized] private ESGenericLife registeredLife;
        [NonSerialized] private int nextPoolGeneration;
        [NonSerialized] private bool staticConfigurationValidated;

        public ESProfileHeader Header
        {
            get
            {
                EnsureSerializedState();
                return header;
            }
        }

        public ESZoneProfileSettings Settings
        {
            get
            {
                EnsureSerializedState();
                return settings;
            }
        }

        public ESZoneProfileRuntimeContext RuntimeContext => EnsureRuntimeContext();
        public ESZone Zone => zone != null ? zone : GetComponent<ESZone>();
        public int ActiveMemberCount => runtimeContext?.ActiveMemberCount ?? 0;

        private void Reset()
        {
            EnsureSerializedState();
        }

        private void OnValidate()
        {
            staticConfigurationValidated = false;
            EnsureSerializedState();
        }

        private void Awake()
        {
            EnsureSerializedState();
            EnsureRuntimeContext();
            BindZone();
            TryRegisterWithExistingPoolLife();
            if (settings.AutoAwake)
                NotifyAwake();
        }

        private void OnEnable()
        {
            EnsureSerializedState();
            BindZone();
            if (settings.AutoEnable && NotifyEnable())
                zone?.RegisterProfile(this);
        }

        private void OnDisable()
        {
            ExitAllMembers();
            zone?.UnregisterProfile(this);
            if (settings != null && settings.AutoEnable)
                NotifyDisable();

            TryRegisterWithExistingPoolLife();
        }

        private void OnDestroy()
        {
            zone?.UnregisterProfile(this);
            NotifyDestroy();
            if (registeredLife != null && !registeredLife.IsPoolSpawned)
                registeredLife.UnregisterPoolExtension(this);

            registeredLife = null;
            zone = null;
        }

        public void OnPoolSpawned()
        {
            EnsureSerializedState();
            ESZoneProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.IsPoolSpawned)
                return;

            if (context.PoolLifecycleActive || context.ActiveMemberCount > 0)
            {
                Debug.LogError(
                    "[ESZoneProfile] 上一 Pool 代未完整 Despawn；已在新一代 Spawn 前收口。",
                    this);
                if (!NotifyPoolDespawned()
                    || context.PoolLifecycleActive
                    || context.ActiveMemberCount > 0)
                {
                    Debug.LogError(
                        "[ESZoneProfile] 上一 Pool 代清理失败；已阻止新一代覆盖剩余成员与 Lease 状态。",
                        this);
                    return;
                }
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
            else
                ExitAllMembers();

            ESZoneProfileRuntimeContext context = EnsureRuntimeContext();
            if (!context.PoolLifecycleActive && context.ActiveMemberCount == 0)
                context.ClearPoolGeneration();
        }

        bool IESGameObjectPoolLifecycleExtensionInstaller.TryInstallPoolLifecycleExtension(
            ESGenericLife life)
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
            ESZoneProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.AwakeLifecycleCompleted)
                return true;
            if (!CanBeginLifecycle(context) || !ValidateStaticConfiguration("Awake"))
                return false;
            if (!CreateRuntimeBindings(context))
                return false;

            bool success = DispatchStartingLifecycle(
                ESZoneProfileLifecyclePhase.Awake,
                ESZoneProfileExtensionLifecycleState.Awake);
            context.AwakeLifecycleCompleted = success;
            return success;
        }

        public bool NotifyEnable()
        {
            EnsureSerializedState();
            ESZoneProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.EnableLifecycleActive)
                return true;
            if (!context.AwakeLifecycleCompleted
                || !CanBeginLifecycle(context)
                || !CanUseValidatedConfiguration())
                return false;

            bool success = DispatchStartingLifecycle(
                ESZoneProfileLifecyclePhase.Enable,
                ESZoneProfileExtensionLifecycleState.Enable);
            context.EnableLifecycleActive = success;
            return success;
        }

        public bool NotifyDisable()
        {
            if (runtimeContext == null)
                return true;

            bool success = ExitAllMembers();
            if (!DispatchEndingLifecycle(
                    ESZoneProfileLifecyclePhase.Disable,
                    ESZoneProfileExtensionLifecycleState.Enable))
                success = false;

            runtimeContext.EnableLifecycleActive =
                runtimeContext.HasAnyState(ESZoneProfileExtensionLifecycleState.Enable);
            return success;
        }

        public bool NotifyPoolSpawned()
        {
            EnsureSerializedState();
            ESZoneProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.PoolLifecycleActive)
                return true;
            if (!context.AwakeLifecycleCompleted
                || !CanBeginLifecycle(context)
                || !CanUseValidatedConfiguration())
                return false;

            if (!context.IsPoolSpawned && context.ActiveMemberCount > 0
                && (!ExitAllMembers() || context.ActiveMemberCount > 0))
            {
                Debug.LogError(
                    "[ESZoneProfile] Pool Spawn 前成员清理失败；已保留状态并阻止开启新代。",
                    this);
                return false;
            }

            if (!context.IsPoolSpawned)
                context.BeginPoolSpawn(++nextPoolGeneration);

            bool success = DispatchStartingLifecycle(
                ESZoneProfileLifecyclePhase.PoolSpawned,
                ESZoneProfileExtensionLifecycleState.Pool);
            context.PoolLifecycleActive = success;
            if (!success && !context.HasAnyState(ESZoneProfileExtensionLifecycleState.Pool))
                context.ClearPoolGeneration();
            return success;
        }

        public bool NotifyPoolDespawned()
        {
            if (runtimeContext == null)
                return true;

            bool success = ExitAllMembers();
            if (!DispatchEndingLifecycle(
                    ESZoneProfileLifecyclePhase.PoolDespawned,
                    ESZoneProfileExtensionLifecycleState.Pool))
                success = false;

            runtimeContext.PoolLifecycleActive =
                runtimeContext.HasAnyState(ESZoneProfileExtensionLifecycleState.Pool);
            if (!runtimeContext.PoolLifecycleActive && runtimeContext.ActiveMemberCount == 0)
                runtimeContext.ClearPoolGeneration();
            return success;
        }

        public bool NotifyDestroy()
        {
            ESZoneProfileRuntimeContext context = EnsureRuntimeContext();
            if (context.DestroyLifecycleCompleted)
                return true;

            bool success = ExitAllMembers();
            if (!NotifyPoolDespawned())
                success = false;
            if (!NotifyDisable())
                success = false;
            if (!DispatchEndingLifecycle(
                    ESZoneProfileLifecyclePhase.Destroy,
                    ESZoneProfileExtensionLifecycleState.EverEntered))
                success = false;

            context.EnableLifecycleActive = false;
            context.PoolLifecycleActive = false;
            context.DestroyLifecycleCompleted = true;
            context.ClearPoolGeneration();
            return success;
        }

        public bool ValidateProfile(List<string> issues)
        {
            if (issues == null)
                return false;

            EnsureSerializedState();
            int initialCount = issues.Count;
            if (string.IsNullOrWhiteSpace(header.DefinitionKey))
                issues.Add("DefinitionKey 为空；OnValidate/Awake 应自动生成稳定身份。");
            if (!header.IsSchemaCurrent)
                issues.Add("Header SchemaVersion 无效或尚未迁移：" + header.SchemaVersion + "。");

            if (GetComponent<ESZone>() == null)
                issues.Add("ESZoneProfile 必须与 ESZone 同根。");
            settings.ValidateExtensions(this, issues);

            ESGenericLife life = GetComponent<ESGenericLife>();
            if (life != null)
            {
                if (life.PoolRootLifecycleComponent == null)
                    issues.Add("同根 ESGenericLife 尚未绑定合法 Pool Root；ZoneProfile 不会抢占 Root。");
                else if (life.PoolRootLifecycleComponent == this)
                    issues.Add("ZoneProfile 只能作为 Pool Extension，不能成为 ESGenericLife Root。");
            }

            return issues.Count == initialCount;
        }

        public T GetExtensionRuntime<T>() where T : ESZoneProfileExtensionRuntime
        {
            if (runtimeContext == null)
                return null;

            IReadOnlyList<ESZoneProfileRuntimeContext.ExtensionBinding> bindings =
                runtimeContext.Extensions;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Runtime is T runtime)
                    return runtime;
            }

            return null;
        }

        internal bool TryEnterMember(ESZoneMember member, out string error)
        {
            error = null;
            ESZoneProfileRuntimeContext context = EnsureRuntimeContext();
            if (!context.EnableLifecycleActive || member.Key == null)
                return true;
            if (context.ContainsMember(member.Key))
                return true;

            IReadOnlyList<ESZoneProfileRuntimeContext.ExtensionBinding> bindings = context.Extensions;
            ulong enteredMask = 0UL;

            for (int i = 0; i < bindings.Count; i++)
            {
                ESZoneProfileRuntimeContext.ExtensionBinding binding = bindings[i];
                if ((binding.State & ESZoneProfileExtensionLifecycleState.Enable) == 0)
                    continue;

                try
                {
                    ESZoneMemberEnterResult result =
                        binding.Runtime.TryEnterMember(this, context, member, out error);
                    if (result == ESZoneMemberEnterResult.Ignored)
                        continue;
                    if (result == ESZoneMemberEnterResult.AppliedTransiently)
                        continue;

                    if (result == ESZoneMemberEnterResult.Failed)
                    {
                        RollbackMemberEntry(member, ref enteredMask, i - 1);
                        PreserveMemberIfNeeded(member, enteredMask);
                        return false;
                    }

                    enteredMask |= 1UL << i;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    Debug.LogException(exception, this);
                    enteredMask |= 1UL << i;
                    RollbackMemberEntry(member, ref enteredMask, i);
                    PreserveMemberIfNeeded(member, enteredMask);
                    return false;
                }
            }

            if (enteredMask != 0UL)
                context.SetMember(
                    member.Key,
                    ESZoneProfileRuntimeContext.CreateMemberState(member, enteredMask));
            return true;
        }

        internal void ExitMember(ESZoneMember member)
        {
            if (runtimeContext == null || member.Key == null
                || !runtimeContext.TryGetMember(member.Key, out var state))
                return;

            ExitMemberState(member.Key, state);
        }

        private bool ExitAllMembers()
        {
            if (runtimeContext == null || runtimeContext.ActiveMemberCount == 0)
                return true;

            bool success = true;
            List<UnityEngine.Object> keys = runtimeContext.PrepareMemberKeyBuffer();
            for (int i = 0; i < keys.Count; i++)
            {
                UnityEngine.Object key = keys[i];
                if (runtimeContext.TryGetMember(key, out var state)
                    && !ExitMemberState(key, state))
                    success = false;
            }

            return success;
        }

        private bool ExitMemberState(
            UnityEngine.Object key,
            ESZoneProfileRuntimeContext.MemberState state)
        {
            bool success = true;
            for (int i = runtimeContext.Extensions.Count - 1; i >= 0; i--)
            {
                ulong extensionBit = 1UL << i;
                if ((state.EnteredExtensionMask & extensionBit) == 0UL)
                    continue;

                try
                {
                    runtimeContext.Extensions[i].Runtime.ExitMember(this, runtimeContext, state.Member);
                    state.EnteredExtensionMask &= ~extensionBit;
                }
                catch (Exception exception)
                {
                    success = false;
                    Debug.LogException(exception, this);
                }
            }

            if (state.EnteredExtensionMask == 0UL)
            {
                runtimeContext.RemoveMember(key);
            }
            else
            {
                runtimeContext.SetMember(key, state);
            }
            return success;
        }

        private void RollbackMemberEntry(
            ESZoneMember member,
            ref ulong enteredMask,
            int lastEnteredIndex)
        {
            for (int i = lastEnteredIndex; i >= 0; i--)
            {
                ulong extensionBit = 1UL << i;
                if ((enteredMask & extensionBit) == 0UL)
                    continue;

                try
                {
                    runtimeContext.Extensions[i].Runtime.ExitMember(this, runtimeContext, member);
                    enteredMask &= ~extensionBit;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void PreserveMemberIfNeeded(
            ESZoneMember member,
            ulong enteredMask)
        {
            if (enteredMask != 0UL)
                runtimeContext.SetMember(member.Key,
                    ESZoneProfileRuntimeContext.CreateMemberState(member, enteredMask);
        }

        private bool CreateRuntimeBindings(ESZoneProfileRuntimeContext context)
        {
            var bindings = new List<ESZoneProfileRuntimeContext.ExtensionBinding>(settings.ExtensionCount);
            try
            {
                for (int i = 0; i < settings.Extensions.Count; i++)
                {
                    ESZoneProfileExtensionSettings extension = settings.Extensions[i];
                    ESZoneProfileExtensionRuntime runtime = extension.CreateRuntime();
                    if (runtime == null)
                        throw new InvalidOperationException(extension.TypeId + " CreateRuntime 返回 null。");

                    bindings.Add(new ESZoneProfileRuntimeContext.ExtensionBinding
                    {
                        Settings = extension,
                        Runtime = runtime
                    });
                }

                context.SetExtensions(bindings);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                context.SetExtensions(null);
                return false;
            }
        }

        private bool DispatchStartingLifecycle(
            ESZoneProfileLifecyclePhase phase,
            ESZoneProfileExtensionLifecycleState phaseState)
        {
            if (runtimeContext.HasAnyState(phaseState))
            {
                Debug.LogError(
                    "[ESZoneProfile] " + phase
                    + " 存在上次失败后未清理的 Extension，已阻止重复进入。",
                    this);
                return false;
            }

            IReadOnlyList<ESZoneProfileRuntimeContext.ExtensionBinding> bindings =
                runtimeContext.Extensions;
            int lastStarted = -1;
            try
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    ESZoneProfileRuntimeContext.ExtensionBinding binding = bindings[i];
                    if (!binding.Settings.Enabled)
                        continue;

                    lastStarted = i;
                    binding.State |= phaseState | ESZoneProfileExtensionLifecycleState.EverEntered;
                    InvokeLifecycle(binding.Runtime, phase);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ESZoneProfileLifecyclePhase rollback = phase == ESZoneProfileLifecyclePhase.Awake
                    ? ESZoneProfileLifecyclePhase.Destroy
                    : phase == ESZoneProfileLifecyclePhase.Enable
                        ? ESZoneProfileLifecyclePhase.Disable
                        : ESZoneProfileLifecyclePhase.PoolDespawned;
                DispatchEndingLifecycle(rollback, phaseState, lastStarted);
                if (phase == ESZoneProfileLifecyclePhase.Awake)
                {
                    for (int i = 0; i <= lastStarted; i++)
                    {
                        ESZoneProfileRuntimeContext.ExtensionBinding binding = bindings[i];
                        if ((binding.State & ESZoneProfileExtensionLifecycleState.Awake) == 0)
                            binding.State &= ~ESZoneProfileExtensionLifecycleState.EverEntered;
                    }
                }
                return false;
            }
        }

        private bool DispatchEndingLifecycle(
            ESZoneProfileLifecyclePhase phase,
            ESZoneProfileExtensionLifecycleState requiredState,
            int startIndex = int.MaxValue)
        {
            if (runtimeContext == null)
                return true;

            bool success = true;
            IReadOnlyList<ESZoneProfileRuntimeContext.ExtensionBinding> bindings =
                runtimeContext.Extensions;
            int boundedStart = Mathf.Min(startIndex, bindings.Count - 1);
            for (int i = boundedStart; i >= 0; i--)
            {
                ESZoneProfileRuntimeContext.ExtensionBinding binding = bindings[i];
                if ((binding.State & requiredState) == 0)
                    continue;

                try
                {
                    InvokeLifecycle(binding.Runtime, phase);
                    if (requiredState == ESZoneProfileExtensionLifecycleState.EverEntered)
                        binding.State = ESZoneProfileExtensionLifecycleState.None;
                    else
                        binding.State &= ~requiredState;
                }
                catch (Exception exception)
                {
                    success = false;
                    Debug.LogException(exception, this);
                }
            }

            return success;
        }

        private void InvokeLifecycle(
            ESZoneProfileExtensionRuntime runtime,
            ESZoneProfileLifecyclePhase phase)
        {
            switch (phase)
            {
                case ESZoneProfileLifecyclePhase.Awake:
                    runtime.OnProfileAwake(this, runtimeContext);
                    break;
                case ESZoneProfileLifecyclePhase.Enable:
                    runtime.OnProfileEnable(this, runtimeContext);
                    break;
                case ESZoneProfileLifecyclePhase.Disable:
                    runtime.OnProfileDisable(this, runtimeContext);
                    break;
                case ESZoneProfileLifecyclePhase.PoolSpawned:
                    runtime.OnProfilePoolSpawned(this, runtimeContext);
                    break;
                case ESZoneProfileLifecyclePhase.PoolDespawned:
                    runtime.OnProfilePoolDespawned(this, runtimeContext);
                    break;
                case ESZoneProfileLifecyclePhase.Destroy:
                    runtime.OnProfileDestroy(this, runtimeContext);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private void BindZone()
        {
            zone ??= GetComponent<ESZone>();
        }

        private void EnsureSerializedState()
        {
            header ??= new ESProfileHeader();
            settings ??= new ESZoneProfileSettings();
            header.EnsureDefinitionKey();
            settings.EnsureDefaults();
        }

        private ESZoneProfileRuntimeContext EnsureRuntimeContext()
        {
            return runtimeContext ??= new ESZoneProfileRuntimeContext();
        }

        private bool CanBeginLifecycle(ESZoneProfileRuntimeContext context)
        {
            return header.ProfileEnabled && !context.DestroyLifecycleCompleted;
        }

        private bool CanUseValidatedConfiguration()
        {
            return staticConfigurationValidated && header.IsSchemaCurrent;
        }

        private bool ValidateStaticConfiguration(string lifecycleName)
        {
            if (!header.IsSchemaCurrent)
            {
                Debug.LogError(
                    "[ESZoneProfile] Header SchemaVersion=" + header.SchemaVersion
                    + "，必须先完成显式迁移，已阻止 " + lifecycleName + "。",
                    this);
                return false;
            }

            if (settings.ValidateExtensions(this, null))
            {
                staticConfigurationValidated = true;
                return true;
            }

            staticConfigurationValidated = false;
            Debug.LogError("[ESZoneProfile] Extension 配置无效，已阻止 " + lifecycleName + "。", this);
            return false;
        }

        private void TryRegisterWithExistingPoolLife()
        {
            if (registeredLife != null || gameObject.activeSelf)
                return;

            ESGenericLife life = GetComponent<ESGenericLife>();
            if (life == null || life.PoolRootLifecycleComponent == null)
                return;

            ((IESGameObjectPoolLifecycleExtensionInstaller)this)
                .TryInstallPoolLifecycleExtension(life);
        }

        private enum ESZoneProfileLifecyclePhase
        {
            Awake,
            Enable,
            Disable,
            PoolSpawned,
            PoolDespawned,
            Destroy
        }
    }
}
