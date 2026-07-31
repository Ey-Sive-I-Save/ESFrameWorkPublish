using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public sealed class ESActiveBuffRuntime : IPoolableAuto,
        ISharedAndVariable<BuffSharedData, BuffVariableData>,
        IESExpressionDependencySink,
        IReceiveChannelLink_Context_Float,
        IReceiveChannelLink_Context_Bool
    {
        public static readonly ESSimplePool<ESActiveBuffRuntime> Pool = new ESSimplePool<ESActiveBuffRuntime>(
            factoryMethod: () => new ESActiveBuffRuntime(),
            resetMethod: null,
            initCount: 16,
            maxCount: 2048,
            poolDisplayName: "ESActiveBuffRuntime Pool"
        );

        // Bindings live on a shared Buff definition, so their runtime token must never be stored on
        // the serialized binding itself. Each active Buff owns independent tracker/token records.
        private readonly List<FloatChangeRuntime> floatChanges = new List<FloatChangeRuntime>(2);
        private readonly List<PermitChangeRuntime> permitChanges = new List<PermitChangeRuntime>(2);
        private readonly List<ContextValueChangeDependency> valueChangeDependencies = new List<ContextValueChangeDependency>(2);
        private readonly ESTagLeaseSet gameTagLeases = new ESTagLeaseSet();

        private EntityBuffDomain domain;
        private ESRuntimeTargetPack target;
        private ESOpSupport buffSupport;
        private StateBase stateTimeSource;
        private float lastStateTime;
        private ESEffectLease valueChangeEffectLease;
        private int valueChangeEffectOwnerId;
        private bool valueChangesDirty;
        private ESBuffLogicRuntime logicRuntime;

        public bool IsRecycled { get; set; }

        [ShowInInspector, ReadOnly]
        public BuffDefinitionDataInfo definition;

        [ShowInInspector, ReadOnly]
        public BuffSharedData sharedData;

        [ShowInInspector, ReadOnly]
        public BuffVariableData variableData = new BuffVariableData();

        public BuffDefinitionDataInfo Definition => definition;
        public Entity Owner => domain != null ? domain.MyCore : null;
        public ESRuntimeTargetPack TargetPack => target;
        public ESOpSupport Support => buffSupport;

        public BuffSharedData SharedData
        {
            get => sharedData;
            set => sharedData = value;
        }

        BuffSharedData ISharedAndVariable<BuffSharedData, BuffVariableData>.SharedData
        {
            get => sharedData;
            set => sharedData = value;
        }

        public BuffVariableData VariableData { get => variableData; set => variableData.DeepCloneFrom(value); }

        [ShowInInspector, ReadOnly]
        public int StackCount => variableData.stackCount;

        [ShowInInspector, ReadOnly]
        public int Level => variableData.level;

        [ShowInInspector, ReadOnly]
        public float RemainingTime => variableData.remainingTime;

        [ShowInInspector, ReadOnly]
        public float ElapsedTime => variableData.elapsedTime;

        [ShowInInspector, ReadOnly]
        public int DefinitionKey { get; private set; }

        [ShowInInspector, ReadOnly]
        public int SourceKey => variableData.sourceKey;

        [ShowInInspector, ReadOnly]
        public string GroupKey { get; private set; }

        [ShowInInspector, ReadOnly]
        public int Strength { get; private set; }

        public bool IsInfinite => variableData.remainingTime < 0f;

        // A Buff frame is an exact, source-owned state projection (BeginBuffFrame/SetBuff/
        // EndBuffFrame).  It deliberately uses a separate owner identity from SourceKey: the
        // authored source-isolation rule still controls ordinary Buff stacking, while the frame
        // owner controls which effects disappear when that frame is committed.
        internal object FrameOwner { get; private set; }
        internal ulong LastSeenFrame { get; private set; }
        internal ulong CreatedFrame { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Buff 生效标签数")]
        public int AppliedGameTagCount => gameTagLeases.Count;

        /// <summary>
        /// Per-active-Buff token record. This is intentionally a struct: the active Buff already owns
        /// the lifecycle, so allocating a Tracker object for every configured binding is unnecessary.
        /// </summary>
        private struct FloatChangeRuntime
        {
            public ESBuffFloatValueChangeBinding binding;
            public ESFloatValueChangeSet set;
            public ESValueChangeToken token;
            public int ownerId;
            public int sourceId;
        }

        /// <summary>Permit counterpart of <see cref="FloatChangeRuntime"/> with the same allocation-free lifecycle.</summary>
        private struct PermitChangeRuntime
        {
            public ESBuffPermitValueChangeBinding binding;
            public ESPermitSet set;
            public ESValueChangeToken token;
            public int ownerId;
            public int sourceId;
        }

        private enum ContextValueChangeDependencyType : byte
        {
            Float,
            Bool
        }

        private struct ContextValueChangeDependency
        {
            public ContextPool context;
            public string key;
            public ContextValueChangeDependencyType type;
        }

        public void Initialize(
            EntityBuffDomain domain,
            BuffDefinitionDataInfo definition,
            BuffSharedData sharedData,
            ESRuntimeTargetPack target,
            ESOpSupport sourceSupport,
            StateBase stateTimeSource,
            float duration,
            int stackDelta,
            int definitionKey,
            int sourceKey,
            int level = 1,
            object frameOwner = null,
            ulong frameNumber = 0)
        {
            this.domain = domain;
            this.definition = definition;
            this.sharedData = sharedData;

            this.stateTimeSource = stateTimeSource;
            variableData.remainingTime = duration;
            variableData.elapsedTime = 0f;
            variableData.tickAccumulator = 0f;
            variableData.stackCount = stackDelta;
            variableData.level = Mathf.Clamp(level, 1, Mathf.Max(1, sharedData.maxLevel));
            variableData.sourceKey = sourceKey;

            lastStateTime = this.stateTimeSource != null ? this.stateTimeSource.hasEnterTime : 0f;
            DefinitionKey = definitionKey;
            GroupKey = sharedData.buffGroup;
            Strength = sharedData.strength;
            FrameOwner = frameOwner;
            LastSeenFrame = frameNumber;
            CreatedFrame = frameNumber;

            int ownerId = SourceKey != 0 ? SourceKey : DefinitionKey;
            buffSupport = domain.OpSupport.CreateChild(ESOpSupportKind.Buff, definition, domain.MyCore, ownerId);
            buffSupport.BindBuff(domain, null, ownerId, domain.OpSupport, this);
            InitializeOwnedTarget(target, sourceSupport);
        }

        public bool CanMergeWith(int definitionKey, int sourceKey)
        {
            return DefinitionKey == definitionKey && SourceKey == sourceKey;
        }

        internal bool IsOwnedByBuffFrame(object owner)
        {
            return owner != null && ReferenceEquals(FrameOwner, owner);
        }

        internal void MarkSeenByBuffFrame(ulong frameNumber)
        {
            LastSeenFrame = frameNumber;
        }

        /// <summary>
        /// Applies the manual portions of a high-level Buff operation to this active instance.
        /// The domain resolves identity/create/remove first; this method never changes ownership.
        /// </summary>
        internal bool ApplyOperation(ESBuffOperation operation)
        {
            if (operation.action != ESBuffOperationAction.Apply || sharedData == null)
                return false;

            bool stackChanged = false;
            bool durationChanged = false;
            bool levelChanged = false;

            switch (operation.stackOperation)
            {
                case ESBuffStackOperation.Add:
                    stackChanged = SetStack(variableData.stackCount + operation.stackValue);
                    break;
                case ESBuffStackOperation.Set:
                    stackChanged = SetStack(operation.stackValue);
                    break;
            }

            switch (operation.durationOperation)
            {
                case ESBuffDurationOperation.Reset:
                    durationChanged = SetRemainingTime(sharedData.duration);
                    break;
                case ESBuffDurationOperation.Add:
                    if (!IsInfinite)
                        durationChanged = SetRemainingTime(variableData.remainingTime + operation.durationValue);
                    break;
                case ESBuffDurationOperation.Set:
                    durationChanged = SetRemainingTime(operation.durationValue);
                    break;
            }

            switch (operation.levelOperation)
            {
                case ESBuffLevelOperation.Add:
                    levelChanged = SetLevel(variableData.level + operation.levelValue);
                    break;
                case ESBuffLevelOperation.Set:
                    levelChanged = SetLevel(operation.levelValue);
                    break;
            }

            if (!stackChanged && !durationChanged && !levelChanged)
                return true;

            if (stackChanged)
                RefreshValueChangesFor(ESBuffValueChangeRefreshMode.OnStackChanged);
            if (levelChanged)
                RefreshValueChangesFor(ESBuffValueChangeRefreshMode.OnLevelChanged);

            NotifyRefreshed("Operation refresh");
            return true;
        }

        public bool AddStackOrRefresh(float duration, int stackDelta)
        {
            int maxStack = Mathf.Max(1, sharedData.maxStack);
            if (sharedData.stackMode == ESBuffStackMode.IgnoreSameBuff && variableData.stackCount >= maxStack)
                return false;

            if (sharedData.stackMode == ESBuffStackMode.ReplaceSameBuff)
            {
                int replacedStackCount = variableData.stackCount;
                variableData.stackCount = Mathf.Clamp(stackDelta, 1, maxStack);
                RefreshTime(duration, sharedData.timeRefreshMode);
                if (replacedStackCount != variableData.stackCount)
                    RefreshValueChangesFor(ESBuffValueChangeRefreshMode.OnStackChanged);
                NotifyRefreshed("Refresh");
                return true;
            }

            if (sharedData.stackMode == ESBuffStackMode.RefreshSameBuff)
            {
                RefreshTime(duration, sharedData.timeRefreshMode);
                NotifyRefreshed("Refresh");
                return true;
            }

            int previousStackCount = variableData.stackCount;
            variableData.stackCount = Mathf.Clamp(variableData.stackCount + stackDelta, 1, maxStack);
            RefreshTime(duration, sharedData.timeRefreshMode);
            if (previousStackCount != variableData.stackCount)
                RefreshValueChangesFor(ESBuffValueChangeRefreshMode.OnStackChanged);
            NotifyRefreshed("Refresh");
            return true;
        }

        /// <summary>
        /// Applies this Buff as one runtime unit. A failed Tag write, ValueChange evaluation or
        /// output operation never leaves a half-applied Buff owned by the domain.
        /// </summary>
        public bool TryApply()
        {
            try
            {
                ReleaseGameTags();
                ReleaseValueChangeDependencies();
                valueChangesDirty = false;
                if (!TryApplyGameTags(sharedData))
                    return AbortApply();

                ReleaseValueChangesByEffectLease();
                ApplyFloatChanges(sharedData);
                ApplyPermitChanges(sharedData);
                if (!TryApplyLogic())
                    return AbortApply();
                return TryTriggerOp(sharedData.onApplyOp, true, "Apply") || AbortApply();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("Apply", exception);
                return AbortApply();
            }
        }

        /// <summary>Compatibility entry. New Buff-domain code must use <see cref="TryApply"/>.</summary>
        public void Apply()
        {
            TryApply();
        }

        /// <summary>Re-evaluates all configured ValueChange expressions for this Buff instance.</summary>
        public void RefreshValueChanges()
        {
            valueChangesDirty = false;
            RefreshFloatChanges(true, ESBuffValueChangeRefreshMode.OnApplyOnly);
            RefreshPermitChanges(true, ESBuffValueChangeRefreshMode.OnApplyOnly);
        }

        /// <summary>
        /// Marks OnDirty ValueChange bindings for refresh. Use this for expression dependencies
        /// that do not expose a Context change stream, such as Entity state or external services.
        /// </summary>
        public void MarkValueChangesDirty()
        {
            valueChangesDirty = true;
        }

        /// <summary>Refreshes only OnDirty bindings and only after a dependency actually changed.</summary>
        public bool RefreshDirtyValueChanges()
        {
            if (!valueChangesDirty)
                return false;

            valueChangesDirty = false;
            RefreshValueChangesFor(ESBuffValueChangeRefreshMode.OnDirty);
            return true;
        }

        public bool Tick(float hostDeltaTime)
        {
            try
            {
                float deltaTime = ResolveDeltaTime(sharedData, hostDeltaTime);
                if (deltaTime < 0f)
                    deltaTime = 0f;

                variableData.elapsedTime += deltaTime;
                RefreshDirtyValueChanges();
                RefreshValueChangesFor(ESBuffValueChangeRefreshMode.EveryTick);
                if (!TryTickOps(sharedData, deltaTime))
                    return true;

                if (IsInfinite)
                    return false;

                variableData.remainingTime -= deltaTime;
                return variableData.remainingTime <= 0f;
            }
            catch (Exception exception)
            {
                // One broken Buff must not prevent the domain from ticking and expiring other Buffs.
                LogLifecycleFailure("Tick", exception);
                return true;
            }
        }

        public void Deactivate(bool triggerRemoveOps)
        {
            if (triggerRemoveOps)
            {
                TryTriggerOp(sharedData != null ? sharedData.onApplyOp : null, false, "Apply stop");
                TryInvokeLogicRemove();
                TryTriggerOp(sharedData != null ? sharedData.onRemoveOp : null, true, "Remove");
            }

            ReleaseRuntimeOwnership();
            ResetRuntimeState();
        }

        private bool AbortApply()
        {
            // Apply may have started a scoped operation before a later step failed. Stop it before
            // releasing Tags and ValueChanges, but do not fire the normal gameplay "remove" op for
            // a Buff that was never accepted into the active domain list.
            TryTriggerOp(sharedData != null ? sharedData.onApplyOp : null, false, "Apply rollback");
            Deactivate(false);
            return false;
        }

        private void ReleaseRuntimeOwnership()
        {
            try
            {
                ReleaseLogicRuntime();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("Logic release", exception);
            }

            try
            {
                ReleaseGameTags();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("Tag release", exception);
            }

            try
            {
                ReleaseValueChangeDependencies();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("ValueChange dependency release", exception);
            }

            try
            {
                ReleaseValueChangesByEffectLease();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("ValueChange release", exception);
            }

            try
            {
                buffSupport?.TryAutoPushedToPool();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("OpSupport release", exception);
            }
        }

        private void ResetRuntimeState()
        {
            logicRuntime = null;
            buffSupport = null;
            target = null;
            stateTimeSource = null;
            domain = null;
            definition = null;
            sharedData = null;
            variableData.stackCount = 0;
            variableData.level = 0;
            variableData.remainingTime = 0f;
            variableData.elapsedTime = 0f;
            variableData.tickAccumulator = 0f;
            variableData.sourceKey = 0;
            lastStateTime = 0f;
            valueChangesDirty = false;
            DefinitionKey = 0;
            GroupKey = null;
            Strength = 0;
            FrameOwner = null;
            LastSeenFrame = 0;
            CreatedFrame = 0;
        }

        public void Remove()
        {
            Deactivate(true);
        }

        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
                Pool.PushToPool(this);
        }

        public void OnResetAsPoolable()
        {
            if (sharedData != null || buffSupport != null || logicRuntime != null)
                Deactivate(false);
        }

        private void RefreshTime(float duration, ESBuffTimeRefreshMode mode)
        {
            switch (mode)
            {
                case ESBuffTimeRefreshMode.KeepRemaining:
                    break;
                case ESBuffTimeRefreshMode.ExtendDuration:
                    if (!IsInfinite)
                        variableData.remainingTime += Mathf.Max(0f, duration);
                    break;
                case ESBuffTimeRefreshMode.UseMaxRemaining:
                    if (!IsInfinite)
                        variableData.remainingTime = Mathf.Max(variableData.remainingTime, duration);
                    break;
                case ESBuffTimeRefreshMode.MergeRemaining:
                    if (!IsInfinite)
                        variableData.remainingTime = Mathf.Max(variableData.remainingTime, 0f) + Mathf.Max(0f, duration);
                    break;
                default:
                    variableData.remainingTime = duration;
                    break;
            }
        }

        private bool SetStack(int value)
        {
            int next = Mathf.Clamp(value, 1, Mathf.Max(1, sharedData != null ? sharedData.maxStack : 1));
            if (next == variableData.stackCount)
                return false;

            variableData.stackCount = next;
            return true;
        }

        private bool SetRemainingTime(float value)
        {
            if (Mathf.Approximately(variableData.remainingTime, value))
                return false;

            variableData.remainingTime = value;
            return true;
        }

        private bool SetLevel(int value)
        {
            int next = Mathf.Clamp(value, 1, Mathf.Max(1, sharedData != null ? sharedData.maxLevel : 1));
            if (next == variableData.level)
                return false;

            variableData.level = next;
            return true;
        }

        private float ResolveDeltaTime(BuffSharedData sharedData, float hostDeltaTime)
        {
            if (sharedData.tickMode != ESBuffTickMode.StateMachineTime)
                return hostDeltaTime;

            if (stateTimeSource == null)
                return hostDeltaTime;

            float current = stateTimeSource.hasEnterTime;
            float delta = Mathf.Max(0f, current - lastStateTime);
            lastStateTime = current;
            return delta;
        }

        private void NotifyRefreshed(string phase)
        {
            TryInvokeLogicRefresh();
            TryTriggerOp(sharedData != null ? sharedData.onRefreshOp : null, true, phase);
            domain?.NotifyBuffRefreshed(this);
        }

        private bool TryApplyLogic()
        {
            ESBuffLogic logic = sharedData != null ? sharedData.logic : null;
            if (logic == null)
                return true;

            if (logicRuntime != null)
            {
                Debug.LogError("[Buff] 同一 Active Buff 不能重复创建自定义逻辑运行状态。");
                return false;
            }

            ESBuffLogicRuntime runtime = null;
            try
            {
                runtime = logic.RentRuntime();
                if (runtime == null)
                {
                    Debug.LogError("[Buff] 自定义 Buff 逻辑未返回运行状态。");
                    return false;
                }

                runtime.Attach(this);
                logicRuntime = runtime;
                return runtime.OnApply();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("Logic apply", exception);
                bool attachedToThisBuff = runtime != null && ReferenceEquals(runtime.Buff, this);
                if (ReferenceEquals(logicRuntime, runtime))
                    logicRuntime = null;

                if (attachedToThisBuff)
                {
                    try
                    {
                        runtime.ReleaseAndReturnToPool();
                    }
                    catch (Exception releaseException)
                    {
                        LogLifecycleFailure("Logic apply rollback", releaseException);
                    }
                }

                return false;
            }
        }

        private void TryInvokeLogicRefresh()
        {
            ESBuffLogicRuntime runtime = logicRuntime;
            if (runtime == null)
                return;

            try
            {
                runtime.OnRefresh();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("Logic refresh", exception);
            }
        }

        private bool TryInvokeLogicTick(float deltaTime)
        {
            ESBuffLogicRuntime runtime = logicRuntime;
            if (runtime == null)
                return true;

            try
            {
                runtime.OnTick(deltaTime);
                return true;
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("Logic tick", exception);
                return false;
            }
        }

        private void TryInvokeLogicRemove()
        {
            ESBuffLogicRuntime runtime = logicRuntime;
            if (runtime == null)
                return;

            try
            {
                runtime.OnRemove();
            }
            catch (Exception exception)
            {
                LogLifecycleFailure("Logic remove", exception);
            }
        }

        private void ReleaseLogicRuntime()
        {
            ESBuffLogicRuntime runtime = logicRuntime;
            if (runtime == null)
                return;

            logicRuntime = null;
            runtime.ReleaseAndReturnToPool();
        }

        private bool TryTickOps(BuffSharedData sharedData, float deltaTime)
        {
            ESOutputOp op = sharedData.onTickOp;
            if (op == null && logicRuntime == null)
                return true;

            switch (sharedData.tickMode)
            {
                case ESBuffTickMode.EveryFrame:
                case ESBuffTickMode.StateMachineTime:
                    return TryInvokeLogicTick(deltaTime)
                        && TryTriggerOp(op, true, "Tick");
                case ESBuffTickMode.FixedInterval:
                    float interval = Mathf.Max(0.0001f, sharedData.tickInterval);
                    variableData.tickAccumulator += deltaTime;
                    int maxCatchUpTicks = sharedData.maxCatchUpTicksPerFrame > 0
                        ? sharedData.maxCatchUpTicksPerFrame
                        : BuffSharedData.DefaultMaxCatchUpTicksPerFrame;
                    int executedTicks = 0;
                    while (variableData.tickAccumulator >= interval && executedTicks < maxCatchUpTicks)
                    {
                        variableData.tickAccumulator -= interval;
                        executedTicks++;
                        if (!TryInvokeLogicTick(interval)
                            || !TryTriggerOp(op, true, "Tick"))
                            return false;
                    }

                    // A hitch must not create an unbounded backlog that consumes subsequent
                    // frames. Time has already advanced; skip only overdue periodic Op calls and
                    // retain the fractional remainder for the next interval.
                    if (variableData.tickAccumulator >= interval)
                        variableData.tickAccumulator = Mathf.Repeat(variableData.tickAccumulator, interval);
                    return true;
                default:
                    return true;
            }
        }

        private void ApplyFloatChanges(BuffSharedData sharedData)
        {
            List<ESBuffFloatValueChangeBinding> changes = sharedData != null ? sharedData.floatChanges : null;
            if (changes == null)
                return;

            Entity owner = domain != null ? domain.MyCore : null;
            if (owner == null)
                return;

            for (int i = 0; i < changes.Count; i++)
            {
                ESBuffFloatValueChangeBinding binding = changes[i];
                if (binding == null || binding.change == null || !binding.IsConfigured)
                    continue;

                int sourceId = SourceKey != 0 ? SourceKey : DefinitionKey;
                ESFloatValueChangeSet set = null;
                ESValueChangeToken token = ESValueChangeToken.Invalid;
                int ownerId = 0;
                if (TryEvaluateFloatChange(binding, out float value))
                {
                    set = owner.GetFloatStat(binding.attributeEnumKey, binding.statKey);
                    if (set != null)
                    {
                        if (!EnsureValueChangeEffectLease())
                            return;

                        ownerId = valueChangeEffectOwnerId;
                        token = set.Add(
                            binding.change.op,
                            value,
                            ownerId,
                            sourceId,
                            binding.change.priority,
                            binding.change.enabled);
                    }
                }

                floatChanges.Add(new FloatChangeRuntime
                {
                    binding = binding,
                    set = set,
                    ownerId = ownerId,
                    sourceId = sourceId,
                    token = token
                });
            }
        }

        private void ApplyPermitChanges(BuffSharedData sharedData)
        {
            List<ESBuffPermitValueChangeBinding> changes = sharedData != null ? sharedData.permitChanges : null;
            if (changes == null)
                return;

            Entity owner = domain != null ? domain.MyCore : null;
            if (owner == null)
                return;

            for (int i = 0; i < changes.Count; i++)
            {
                ESBuffPermitValueChangeBinding binding = changes[i];
                if (binding == null || binding.change == null || !binding.IsConfigured)
                    continue;

                if (!TryEvaluatePermitLaw(binding, out ESPermitLaw law))
                    continue;

                ESPermitSet set = owner.GetPermit(binding.attributeEnumKey, binding.permitKey);
                if (set == null)
                    continue;
                if (!EnsureValueChangeEffectLease())
                    return;

                int sourceId = SourceKey != 0 ? SourceKey : DefinitionKey;
                permitChanges.Add(new PermitChangeRuntime
                {
                    binding = binding,
                    set = set,
                    ownerId = valueChangeEffectOwnerId,
                    sourceId = sourceId,
                    token = set.Add(law, valueChangeEffectOwnerId, sourceId, binding.change.priority, binding.change.enabled)
                });
            }
        }

        private void RefreshValueChangesFor(ESBuffValueChangeRefreshMode trigger)
        {
            RefreshFloatChanges(false, trigger);
            RefreshPermitChanges(false, trigger);
        }

        private void RefreshFloatChanges(bool force, ESBuffValueChangeRefreshMode trigger)
        {
            Entity owner = domain != null ? domain.MyCore : null;
            if (owner == null)
                return;

            for (int i = 0; i < floatChanges.Count; i++)
            {
                FloatChangeRuntime runtime = floatChanges[i];
                ESBuffFloatValueChangeBinding binding = runtime.binding;
                if (binding == null || binding.change == null || !ShouldRefresh(binding.refreshMode, force, trigger))
                    continue;

                if (!TryEvaluateFloatChange(binding, out float value))
                    continue;

                if (runtime.set == null)
                {
                    runtime.set = owner.GetFloatStat(binding.attributeEnumKey, binding.statKey);
                    if (runtime.set == null || !EnsureValueChangeEffectLease())
                        continue;

                    runtime.ownerId = valueChangeEffectOwnerId;
                    runtime.sourceId = SourceKey != 0 ? SourceKey : DefinitionKey;
                    runtime.token = runtime.set.Add(
                        binding.change.op,
                        value,
                        runtime.ownerId,
                        runtime.sourceId,
                        binding.change.priority,
                        binding.change.enabled);
                }
                else if (!runtime.set.Update(runtime.token, binding.change.op, value, binding.change.priority))
                {
                    runtime.token = runtime.set.Add(
                        binding.change.op,
                        value,
                        runtime.ownerId,
                        runtime.sourceId,
                        binding.change.priority,
                        binding.change.enabled);
                }
                else
                {
                    runtime.set.SetEnabled(runtime.token, binding.change.enabled);
                }
                floatChanges[i] = runtime;
            }
        }

        private void RefreshPermitChanges(bool force, ESBuffValueChangeRefreshMode trigger)
        {
            for (int i = 0; i < permitChanges.Count; i++)
            {
                PermitChangeRuntime runtime = permitChanges[i];
                ESBuffPermitValueChangeBinding binding = runtime.binding;
                if (binding == null || binding.change == null || !ShouldRefresh(binding.refreshMode, force, trigger))
                    continue;

                if (!TryEvaluatePermitLaw(binding, out ESPermitLaw law))
                    continue;
                if (!runtime.set.Update(runtime.token, law, binding.change.priority))
                    runtime.token = runtime.set.Add(law, runtime.ownerId, runtime.sourceId, binding.change.priority, binding.change.enabled);
                else
                    runtime.set.SetEnabled(runtime.token, binding.change.enabled);
                permitChanges[i] = runtime;
            }
        }

        private void ReleaseValueChanges()
        {
            for (int i = floatChanges.Count - 1; i >= 0; i--)
            {
                FloatChangeRuntime runtime = floatChanges[i];
                runtime.set?.Release(runtime.token);
            }
            floatChanges.Clear();

            for (int i = permitChanges.Count - 1; i >= 0; i--)
            {
                PermitChangeRuntime runtime = permitChanges[i];
                runtime.set?.Release(runtime.token);
            }
            permitChanges.Clear();
        }

        private bool TryEvaluateFloatChange(ESBuffFloatValueChangeBinding binding, out float value)
        {
            if (!binding.change.IsDeterministic)
            {
                value = 0f;
                return false;
            }

            if (binding.refreshMode == ESBuffValueChangeRefreshMode.OnDirty)
            {
                using (ESExpressionDependencyCapture.Begin(this))
                    value = binding.change.value != null ? binding.change.value.Evaluate(target, buffSupport) : 0f;
            }
            else
            {
                value = binding.change.value != null ? binding.change.value.Evaluate(target, buffSupport) : 0f;
            }

            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private bool TryEvaluatePermitLaw(ESBuffPermitValueChangeBinding binding, out ESPermitLaw law)
        {
            if (!binding.change.IsDeterministic)
            {
                law = ESPermitLaw.Ignore;
                return false;
            }

            bool condition;
            if (binding.refreshMode == ESBuffValueChangeRefreshMode.OnDirty)
            {
                using (ESExpressionDependencyCapture.Begin(this))
                    condition = binding.change.condition == null || binding.change.condition.Evaluate(target, buffSupport);
            }
            else
            {
                condition = binding.change.condition == null || binding.change.condition.Evaluate(target, buffSupport);
            }

            law = condition ? binding.change.trueLaw : binding.change.falseLaw;
            return true;
        }

        void IESExpressionDependencySink.ObserveContextFloat(ContextPool context, string key)
        {
            TryAddValueChangeDependency(context, key, ContextValueChangeDependencyType.Float);
        }

        void IESExpressionDependencySink.ObserveContextBool(ContextPool context, string key)
        {
            TryAddValueChangeDependency(context, key, ContextValueChangeDependencyType.Bool);
        }

        public void OnLink(string key, Link_ContextEvent_FloatChange link)
        {
            MarkValueChangesDirty();
        }

        public void OnLink(string key, Link_ContextEvent_BoolChange link)
        {
            MarkValueChangesDirty();
        }

        private void TryAddValueChangeDependency(ContextPool context, string key, ContextValueChangeDependencyType type)
        {
            if (context == null || string.IsNullOrEmpty(key))
                return;

            for (int i = 0; i < valueChangeDependencies.Count; i++)
            {
                ContextValueChangeDependency dependency = valueChangeDependencies[i];
                if (ReferenceEquals(dependency.context, context)
                    && dependency.type == type
                    && dependency.key == key)
                {
                    return;
                }
            }

            bool acquired = type == ContextValueChangeDependencyType.Float
                ? context.TryAcquireValueChangeFloatLink(key)
                : context.TryAcquireValueChangeBoolLink(key);
            if (!acquired)
                return;

            bool subscribed = type == ContextValueChangeDependencyType.Float
                ? context.LinkRCL_Float.AddReceiver(key, this)
                : context.LinkRCL_Bool.AddReceiver(key, this);
            if (!subscribed)
            {
                if (type == ContextValueChangeDependencyType.Float)
                    context.ReleaseValueChangeFloatLink(key);
                else
                    context.ReleaseValueChangeBoolLink(key);
                return;
            }

            if (type == ContextValueChangeDependencyType.Float)
                context.LinkRCL_Float.ApplyChannelBuffers(key);
            else
                context.LinkRCL_Bool.ApplyChannelBuffers(key);

            valueChangeDependencies.Add(new ContextValueChangeDependency
            {
                context = context,
                key = key,
                type = type
            });
        }

        private void ReleaseValueChangeDependencies()
        {
            for (int i = valueChangeDependencies.Count - 1; i >= 0; i--)
            {
                ContextValueChangeDependency dependency = valueChangeDependencies[i];
                if (dependency.context == null)
                    continue;

                if (dependency.type == ContextValueChangeDependencyType.Float)
                {
                    dependency.context.LinkRCL_Float.RemoveReceiver(dependency.key, this);
                    dependency.context.LinkRCL_Float.ApplyChannelBuffers(dependency.key);
                    dependency.context.ReleaseValueChangeFloatLink(dependency.key);
                }
                else
                {
                    dependency.context.LinkRCL_Bool.RemoveReceiver(dependency.key, this);
                    dependency.context.LinkRCL_Bool.ApplyChannelBuffers(dependency.key);
                    dependency.context.ReleaseValueChangeBoolLink(dependency.key);
                }
            }

            valueChangeDependencies.Clear();
        }

        private bool EnsureValueChangeEffectLease()
        {
            if (valueChangeEffectLease.IsValid)
                return true;
            Entity owner = domain != null ? domain.MyCore : null;
            if (owner == null)
                return false;

            valueChangeEffectLease = owner.CreateValueChangeEffectLease(out valueChangeEffectOwnerId);
            return valueChangeEffectLease.IsValid;
        }

        private void ReleaseValueChangesByEffectLease()
        {
            if (valueChangeEffectLease.IsValid)
                valueChangeEffectLease.Dispose();
            else
                ReleaseValueChanges();

            valueChangeEffectLease = default;
            valueChangeEffectOwnerId = 0;
            floatChanges.Clear();
            permitChanges.Clear();
        }

        /// <summary>
        /// Buff 的 Tag 采用“实例存在即拥有”的策略，不会随 StackCount 重复叠加。
        /// 每个成功添加的 Tag 都保存独立 Lease，销毁时只撤销本 Buff 的那一次来源。
        /// </summary>
        private bool TryApplyGameTags(BuffSharedData data)
        {
            Entity owner = domain != null ? domain.MyCore : null;
            IReadOnlyList<ESTagStableReference> tags = data != null ? data.tags : null;
            if (owner == null || tags == null || tags.Count == 0)
                return tags == null || tags.Count == 0;

            if (!gameTagLeases.TryApply(owner.Tags, tags, this, out string error))
            {
                Debug.LogWarning($"[BuffTag] 添加 Tag 失败：{error} | Buff={definition?.name ?? "<runtime>"}");
                return false;
            }

            return true;
        }

        private void ReleaseGameTags()
        {
            gameTagLeases.ReleaseAll();
        }

        private static bool ShouldRefresh(ESBuffValueChangeRefreshMode configured, bool force, ESBuffValueChangeRefreshMode trigger)
        {
            if (force || configured == trigger)
                return true;

            return trigger == ESBuffValueChangeRefreshMode.OnStackChanged
                && configured == ESBuffValueChangeRefreshMode.EveryTick;
        }

        private bool TryTriggerOp(ESOutputOp op, bool start, string phase)
        {
            if (op == null || domain == null)
                return true;

            try
            {
                // Source supports commonly belong to one attack/skill invocation and can be
                // pooled before this Buff expires. A Buff always runs through its own scope with
                // the target Entity support as parent; origin identity lives in its owned target
                // snapshot instead of a retained foreign ESOpSupport reference.
                ESOpSupport hostSupport = domain.MyCore != null ? domain.MyCore.OpSupport : domain.OpSupport;
                if (start)
                    op._TryStartOp(target, buffSupport, hostSupport);
                else
                    op._TryStopOp(target, buffSupport, hostSupport);
                return true;
            }
            catch (Exception exception)
            {
                LogLifecycleFailure(phase + " Op", exception);
                return false;
            }
        }

        private void InitializeOwnedTarget(ESRuntimeTargetPack sourceTarget, ESOpSupport sourceSupport)
        {
            target = buffSupport.RentTargetPack();
            if (target == null)
                return;

            if (sourceTarget != null)
            {
                target.EnsureListCapacity(sourceTarget.targetEntities.Count, sourceTarget.targetItems.Count);
                target.CopyFrom(sourceTarget, copyTargets: true, copyExtras: false);
                target.runtimeFloat = sourceTarget.runtimeFloat;
                target.runtimeBool = sourceTarget.runtimeBool;
            }

            bool sourceIsLive = sourceSupport != null && !sourceSupport.IsDisposed && !sourceSupport.IsRecycled;
            if (target.userEntity == null && sourceIsLive && sourceSupport.CurrentEntity != null)
                target.SetUser(sourceSupport.CurrentEntity);
            if (target.userItem == null && sourceIsLive && sourceSupport.OwnerItem != null)
                target.SetUser(sourceSupport.OwnerItem);

            Entity owner = domain != null ? domain.MyCore : null;
            if (target.userEntity == null && target.userItem == null && owner != null)
                target.SetUser(owner);
            if (target.entityMainTarget == null && target.itemMainTarget == null && owner != null)
                target.SetEntityMainTarget(owner);
        }

        private void LogLifecycleFailure(string phase, Exception exception)
        {
            Debug.LogError($"[Buff] {phase} failed; this Buff is being isolated or cleaned up. Buff={definition?.name ?? "<runtime>"}");
            Debug.LogException(exception);
        }
    }
}
