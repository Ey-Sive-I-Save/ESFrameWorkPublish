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
        private ESOpSupport sourceSupport;
        private ESOpSupport buffSupport;
        private StateBase stateTimeSource;
        private float lastStateTime;
        private ESEffectLease valueChangeEffectLease;
        private int valueChangeEffectOwnerId;
        private bool valueChangesDirty;

        public bool IsRecycled { get; set; }

        [ShowInInspector, ReadOnly]
        public BuffDefinitionDataInfo definition;

        [ShowInInspector, ReadOnly]
        public BuffSharedData sharedData;

        [ShowInInspector, ReadOnly]
        public BuffVariableData variableData = new BuffVariableData();

        public BuffDefinitionDataInfo Definition => definition;

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
            int sourceKey)
        {
            this.domain = domain;
            this.definition = definition;
            this.sharedData = sharedData;

            this.sourceSupport = sourceSupport;
            this.stateTimeSource = stateTimeSource;
            variableData.remainingTime = duration;
            variableData.elapsedTime = 0f;
            variableData.tickAccumulator = 0f;
            variableData.stackCount = stackDelta;
            variableData.sourceKey = sourceKey;

            lastStateTime = this.stateTimeSource != null ? this.stateTimeSource.hasEnterTime : 0f;
            DefinitionKey = definitionKey;
            GroupKey = sharedData.buffGroup;
            Strength = sharedData.strength;

            int ownerId = SourceKey != 0 ? SourceKey : DefinitionKey;
            buffSupport = domain.OpSupport.CreateChild(ESOpSupportKind.Buff, definition, domain.MyCore, ownerId);
            buffSupport.BindBuff(domain, null, ownerId, domain.OpSupport);

            this.target = target != null ? target : buffSupport.RentTargetPack();
            if (this.target != null && domain.MyCore != null)
            {
                this.target.SetEntity(domain.MyCore);
                this.target.SetUser(domain.MyCore);
                this.target.SetEntityMainTarget(domain.MyCore);
            }
        }

        public bool CanMergeWith(int definitionKey, int sourceKey)
        {
            return DefinitionKey == definitionKey && SourceKey == sourceKey;
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
                TriggerOp(sharedData.onRefreshOp, true);
                return true;
            }

            if (sharedData.stackMode == ESBuffStackMode.RefreshSameBuff)
            {
                RefreshTime(duration, sharedData.timeRefreshMode);
                TriggerOp(sharedData.onRefreshOp, true);
                return true;
            }

            int previousStackCount = variableData.stackCount;
            variableData.stackCount = Mathf.Clamp(variableData.stackCount + stackDelta, 1, maxStack);
            RefreshTime(duration, sharedData.timeRefreshMode);
            if (previousStackCount != variableData.stackCount)
                RefreshValueChangesFor(ESBuffValueChangeRefreshMode.OnStackChanged);
            TriggerOp(sharedData.onRefreshOp, true);
            return true;
        }

        public void Apply()
        {
            ReleaseGameTags();
            ReleaseValueChangeDependencies();
            valueChangesDirty = false;
            ApplyGameTags(sharedData);
            ReleaseValueChangesByEffectLease();
            ApplyFloatChanges(sharedData);
            ApplyPermitChanges(sharedData);
            TriggerOp(sharedData.onApplyOp, true);
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
            float deltaTime = ResolveDeltaTime(sharedData, hostDeltaTime);
            if (deltaTime < 0f)
                deltaTime = 0f;

            variableData.elapsedTime += deltaTime;
            RefreshDirtyValueChanges();
            RefreshValueChangesFor(ESBuffValueChangeRefreshMode.EveryTick);
            TickOps(sharedData, deltaTime);

            if (IsInfinite)
                return false;

            variableData.remainingTime -= deltaTime;
            return variableData.remainingTime <= 0f;
        }

        public void Deactivate(bool triggerRemoveOps)
        {
            if (triggerRemoveOps)
            {
                TriggerOp(sharedData.onApplyOp, false);
                TriggerOp(sharedData.onRemoveOp, true);
            }

            ReleaseGameTags();
            ReleaseValueChangeDependencies();
            ReleaseValueChangesByEffectLease();

            buffSupport.TryAutoPushedToPool();
            buffSupport = null;
            target = null;
            sourceSupport = null;
            stateTimeSource = null;
            domain = null;
            definition = null;
            sharedData = null;
            variableData.stackCount = 0;
            variableData.remainingTime = 0f;
            variableData.elapsedTime = 0f;
            variableData.tickAccumulator = 0f;
            variableData.sourceKey = 0;
            lastStateTime = 0f;
            valueChangesDirty = false;
            DefinitionKey = 0;
            GroupKey = null;
            Strength = 0;
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
            if (sharedData != null || buffSupport != null)
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

        private void TickOps(BuffSharedData sharedData, float deltaTime)
        {
            ESOutputOp op = sharedData.onTickOp;
            if (op == null)
                return;

            switch (sharedData.tickMode)
            {
                case ESBuffTickMode.EveryFrame:
                case ESBuffTickMode.StateMachineTime:
                    TriggerOp(op, true);
                    break;
                case ESBuffTickMode.FixedInterval:
                    float interval = Mathf.Max(0.0001f, sharedData.tickInterval);
                    variableData.tickAccumulator += deltaTime;
                    while (variableData.tickAccumulator >= interval)
                    {
                        variableData.tickAccumulator -= interval;
                        TriggerOp(op, true);
                    }
                    break;
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
        private void ApplyGameTags(BuffSharedData data)
        {
            Entity owner = domain != null ? domain.MyCore : null;
            IReadOnlyList<ESTagStableReference> tags = data != null ? data.tags : null;
            if (owner == null || tags == null || tags.Count == 0)
                return;

            if (!gameTagLeases.TryApply(owner.Tags, tags, this, out string error))
            {
                Debug.LogWarning($"[BuffTag] 添加 Tag 失败：{error} | Buff={definition?.name ?? "<runtime>"}");
            }
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

        private void TriggerOp(ESOutputOp op, bool start)
        {
            if (op == null || domain == null)
                return;

            ESOpSupport hostSupport = sourceSupport != null ? sourceSupport : domain.MyCore != null ? domain.MyCore.OpSupport : null;
            if (start)
                op._TryStartOp(target, buffSupport, hostSupport);
            else
                op._TryStopOp(target, buffSupport, hostSupport);
        }
    }
}
