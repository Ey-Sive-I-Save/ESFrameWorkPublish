using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public sealed class ESActiveBuffRuntime : IPoolableAuto, ISharedAndVariable<BuffSharedData, BuffVariableData>
    {
        public static readonly ESSimplePool<ESActiveBuffRuntime> Pool = new ESSimplePool<ESActiveBuffRuntime>(
            factoryMethod: () => new ESActiveBuffRuntime(),
            resetMethod: null,
            initCount: 16,
            maxCount: 2048,
            poolDisplayName: "ESActiveBuffRuntime Pool"
        );

        private readonly List<ESFloatValueChangeTracker> floatTrackers = new List<ESFloatValueChangeTracker>(2);
        private readonly List<ESPermitValueChangeTracker> permitTrackers = new List<ESPermitValueChangeTracker>(2);

        private EntityBuffDomain domain;
        private ESRuntimeTargetPack target;
        private ESOpSupport sourceSupport;
        private ESOpSupport buffSupport;
        private StateBase stateTimeSource;
        private float lastStateTime;

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
                variableData.stackCount = Mathf.Clamp(stackDelta, 1, maxStack);
                RefreshTime(duration, sharedData.timeRefreshMode);
                TriggerOp(sharedData.onRefreshOp, true);
                return true;
            }

            if (sharedData.stackMode == ESBuffStackMode.RefreshSameBuff)
            {
                RefreshTime(duration, sharedData.timeRefreshMode);
                TriggerOp(sharedData.onRefreshOp, true);
                return true;
            }

            variableData.stackCount = Mathf.Clamp(variableData.stackCount + stackDelta, 1, maxStack);
            RefreshTime(duration, sharedData.timeRefreshMode);
            TriggerOp(sharedData.onRefreshOp, true);
            return true;
        }

        public void Apply()
        {
            ApplyFloatChanges(sharedData);
            ApplyPermitChanges(sharedData);
            TriggerOp(sharedData.onApplyOp, true);
        }

        public bool Tick(float hostDeltaTime)
        {
            float deltaTime = ResolveDeltaTime(sharedData, hostDeltaTime);
            if (deltaTime < 0f)
                deltaTime = 0f;

            variableData.elapsedTime += deltaTime;
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

            for (int i = floatTrackers.Count - 1; i >= 0; i--)
                floatTrackers[i].ReleaseAll();
            floatTrackers.Clear();

            for (int i = permitTrackers.Count - 1; i >= 0; i--)
                permitTrackers[i].ReleaseAll();
            permitTrackers.Clear();

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
            if (definition != null)
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

            int ownerId = domain != null && domain.MyCore != null ? domain.MyCore.GetInstanceID() : 0;
            for (int i = 0; i < changes.Count; i++)
            {
                ESBuffFloatValueChangeBinding binding = changes[i];
                if (binding == null || binding.change == null || string.IsNullOrEmpty(binding.statKey))
                    continue;

                ESFloatValueChangeSet set = domain.GetFloatStat(binding.statKey);
                if (set == null)
                    continue;

                ESFloatValueChangeTracker tracker = new ESFloatValueChangeTracker(set, ownerId, SourceKey != 0 ? SourceKey : DefinitionKey, 1);
                float value = binding.change.value != null ? binding.change.value.Evaluate(target, buffSupport) : 0f;
                tracker.Add(binding.change.op, value, binding.change.priority, binding.change.enabled);
                floatTrackers.Add(tracker);
            }
        }

        private void ApplyPermitChanges(BuffSharedData sharedData)
        {
            List<ESBuffPermitValueChangeBinding> changes = sharedData != null ? sharedData.permitChanges : null;
            if (changes == null)
                return;

            int ownerId = domain != null && domain.MyCore != null ? domain.MyCore.GetInstanceID() : 0;
            for (int i = 0; i < changes.Count; i++)
            {
                ESBuffPermitValueChangeBinding binding = changes[i];
                if (binding == null || binding.change == null || string.IsNullOrEmpty(binding.permitKey))
                    continue;

                ESPermitSet set = domain.GetPermit(binding.permitKey);
                if (set == null)
                    continue;

                ESPermitValueChangeTracker tracker = new ESPermitValueChangeTracker(set, ownerId, SourceKey != 0 ? SourceKey : DefinitionKey, 1);
                bool condition = binding.change.condition == null || binding.change.condition.Evaluate(target, buffSupport);
                ESPermitLaw law = condition ? binding.change.trueLaw : binding.change.falseLaw;
                tracker.Add(law, binding.change.priority, binding.change.enabled);
                permitTrackers.Add(tracker);
            }
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
