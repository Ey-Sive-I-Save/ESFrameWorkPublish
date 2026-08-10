using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public readonly struct ESActionIntent
    {
        public readonly ESActionConfigKey actionKey;
        public readonly int lifecycleGeneration;
        public readonly int sourcePulseId;
        public readonly UnityEngine.Object owner;
        public readonly Transform target;
        public readonly Vector3 targetPoint;
        public readonly bool hasTargetPoint;
        public readonly float issuedTime;

        public ESActionIntent(
            ESActionConfigKey actionKey,
            int lifecycleGeneration,
            int sourcePulseId,
            UnityEngine.Object owner,
            Transform target = null,
            Vector3 targetPoint = default,
            bool hasTargetPoint = false,
            float issuedTime = -1f)
        {
            this.actionKey = actionKey;
            this.lifecycleGeneration = lifecycleGeneration;
            this.sourcePulseId = sourcePulseId;
            this.owner = owner;
            this.target = target;
            this.targetPoint = targetPoint;
            this.hasTargetPoint = hasTargetPoint;
            this.issuedTime = issuedTime;
        }

        public bool IsConfigured => actionKey != null && actionKey.IsConfigured;
    }

    public readonly struct ESActionRuntimeHandle : IEquatable<ESActionRuntimeHandle>
    {
        internal readonly int runtimeKey;
        internal readonly int lifecycleGeneration;
        internal readonly int catalogVersion;

        internal ESActionRuntimeHandle(int runtimeKey, int lifecycleGeneration, int catalogVersion)
        {
            this.runtimeKey = runtimeKey;
            this.lifecycleGeneration = lifecycleGeneration;
            this.catalogVersion = catalogVersion;
        }

        public bool IsValid => runtimeKey > 0 && lifecycleGeneration > 0 && catalogVersion > 0;

        public bool Equals(ESActionRuntimeHandle other)
        {
            return runtimeKey == other.runtimeKey
                   && lifecycleGeneration == other.lifecycleGeneration
                   && catalogVersion == other.catalogVersion;
        }

        public override bool Equals(object obj) => obj is ESActionRuntimeHandle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = runtimeKey;
                hash = (hash * 397) ^ lifecycleGeneration;
                return (hash * 397) ^ catalogVersion;
            }
        }

        public static bool operator ==(ESActionRuntimeHandle left, ESActionRuntimeHandle right) => left.Equals(right);
        public static bool operator !=(ESActionRuntimeHandle left, ESActionRuntimeHandle right) => !left.Equals(right);
    }

    public readonly struct ESActionHitResult
    {
        public readonly bool isHit;
        public readonly bool isBlocked;
        public readonly float damageMultiplier;
        public readonly float hitstopSeconds;
        public readonly UnityEngine.Object target;
        public readonly ESActionRuntimeHandle handle;

        internal ESActionHitResult(
            bool isHit,
            bool isBlocked,
            float damageMultiplier,
            float hitstopSeconds,
            UnityEngine.Object target,
            ESActionRuntimeHandle handle)
        {
            this.isHit = isHit;
            this.isBlocked = isBlocked;
            this.damageMultiplier = damageMultiplier;
            this.hitstopSeconds = hitstopSeconds;
            this.target = target;
            this.handle = handle;
        }

        public bool IsValid => handle.IsValid;
    }

    public readonly struct ESActionEvent
    {
        public readonly ESActionEventKind kind;
        public readonly ESActionConfigKey actionKey;
        public readonly ESActionRuntimeHandle handle;
        public readonly ESActionPhaseKind phase;
        public readonly int comboIndex;
        public readonly int emissionId;
        public readonly ESWeaponConfigKey weaponKey;
        public readonly ESActionHitResult hitResult;

        internal ESActionEvent(
            ESActionEventKind kind,
            ESActionConfigKey actionKey,
            ESActionRuntimeHandle handle,
            ESActionPhaseKind phase,
            int comboIndex,
            int emissionId,
            ESWeaponConfigKey weaponKey,
            ESActionHitResult hitResult)
        {
            this.kind = kind;
            this.actionKey = actionKey;
            this.handle = handle;
            this.phase = phase;
            this.comboIndex = comboIndex;
            this.emissionId = emissionId;
            this.weaponKey = weaponKey;
            this.hitResult = hitResult;
        }
    }

    public sealed class ESActionEventHub
    {
        public event Action<ESActionEvent> Published;

        public void Publish(in ESActionEvent evt)
        {
            Published?.Invoke(evt);
        }

        public void Reset()
        {
            Published = null;
        }
    }

    public sealed class ESActionRuntime
    {
        private static int nextCatalogVersion;

        private readonly ESActionConfigKeyTable table;
        private readonly ESActionEventHub events;
        private readonly Entity owner;
        private readonly int catalogVersion = ++nextCatalogVersion;

        private ESActionRuntimeData current;
        private ESActionRuntimeHandle currentHandle;
        private int lifecycleGeneration = 1;
        private int phaseIndex;
        private int comboIndex;
        private int nextEmissionId;
        private float phaseElapsed;
        private bool isRunning;
        private bool hitWindowOpened;
        private ESActionIntent bufferedIntent;
        private bool hasBufferedIntent;

        public ESActionRuntime(
            ESActionConfigKeyTable table,
            ESActionEventHub events,
            Entity owner = null)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.owner = owner;
        }

        public bool IsRunning => isRunning;
        public int LifecycleGeneration => lifecycleGeneration;
        public ESActionRuntimeHandle CurrentHandle => currentHandle;
        public ESActionPhaseKind CurrentPhase => isRunning ? GetCurrentPhaseKind() : ESActionPhaseKind.None;
        public int ComboIndex => comboIndex;
        public bool HasBufferedIntent => hasBufferedIntent;

        public bool TrySubmit(in ESActionIntent intent, out string error)
        {
            error = null;
            if (owner != null && intent.owner != null && !ReferenceEquals(owner, intent.owner))
            {
                error = "ActionIntent Owner 不属于当前 ActionRuntime。";
                return false;
            }

            if (intent.lifecycleGeneration > 0 && intent.lifecycleGeneration != lifecycleGeneration)
            {
                error = "ActionIntent 使用了过期 Generation。";
                return false;
            }

            if (!intent.IsConfigured)
            {
                error = "ActionIntent 缺少有效 ActionKey。";
                return false;
            }

            if (!table.TryGet(intent.actionKey, out ESActionRuntimeData runtimeData))
            {
                error = "Action Key 未注册到当前 Catalog：" + ESConfigKeyMatch.Describe(
                    intent.actionKey.EnumKeyInt,
                    intent.actionKey.StringKey);
                return false;
            }

            if (isRunning)
            {
                if (current == null || !current.allowBufferedInput)
                {
                    error = "当前 Action 不允许输入缓冲。";
                    return false;
                }

                if (!IsInputBufferOpen())
                {
                    error = "当前 Phase 已过输入缓冲窗口。";
                    return false;
                }

                bufferedIntent = intent;
                hasBufferedIntent = true;
                return true;
            }

            StartAction(runtimeData, intent, 0);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!isRunning || current == null || current.phases == null || current.phases.Count == 0)
                return;

            phaseElapsed += deltaTime;
            ESActionPhaseData phase = current.phases[phaseIndex];
            if (phase.kind == ESActionPhaseKind.Active && !hitWindowOpened)
            {
                hitWindowOpened = true;
                Publish(ESActionEventKind.HitWindowOpened, default);
            }

            if (phaseElapsed < phase.duration)
                return;

            phaseIndex++;
            if (phaseIndex < current.phases.Count)
            {
                phaseElapsed = 0f;
                hitWindowOpened = false;
                Publish(ESActionEventKind.PhaseEntered, default);
                return;
            }

            FinishAction();
        }

        public bool TryResolveHit(
            ESActionRuntimeHandle handle,
            UnityEngine.Object target,
            out ESActionHitResult result)
        {
            result = default;
            if (!isRunning || !handle.IsValid || handle != currentHandle)
                return false;

            ESActionPhaseData phase = GetCurrentPhaseData();
            if (phase == null || phase.kind != ESActionPhaseKind.Active
                || phase.hitWindow == null || !phase.hitWindow.enabled)
                return false;

            result = new ESActionHitResult(
                true,
                false,
                phase.hitWindow.damageMultiplier,
                phase.hitstopSeconds,
                target,
                currentHandle);
            Publish(ESActionEventKind.HitResolved, result);
            return true;
        }

        public bool TryCancel(
            ESActionCategory category,
            ESActionConfigKey targetActionKey,
            out string error)
        {
            error = null;
            if (!isRunning || current == null)
            {
                error = "当前没有正在运行的 Action。";
                return false;
            }

            ESActionPhaseKind phaseKind = GetCurrentPhaseKind();
            if (current.cancelRules != null)
            {
                for (int i = 0; i < current.cancelRules.Count; i++)
                {
                    ESActionCancelRuleData rule = current.cancelRules[i];
                    if (rule == null || rule.sourcePhase != phaseKind)
                        continue;
                    if (category != ESActionCategory.None && rule.targetCategory != category)
                        continue;
                    if (rule.targetActionKey != null && rule.targetActionKey.IsConfigured
                        && (targetActionKey == null || !ESConfigKeyMatch.Matches(
                            rule.targetActionKey.EnumKeyInt,
                            rule.targetActionKey.StringKey,
                            targetActionKey.EnumKeyInt,
                            targetActionKey.StringKey)))
                        continue;
                    if (phaseElapsed < rule.windowStart || phaseElapsed > rule.windowStart + rule.windowDuration)
                        continue;

                    Publish(ESActionEventKind.ActionCancelled, default);
                    ResetActionState();
                    return true;
                }
            }

            error = "当前 Action 不允许该取消。";
            return false;
        }

        public void Interrupt()
        {
            if (!isRunning)
                return;

            Publish(ESActionEventKind.ActionInterrupted, default);
            ResetActionState();
        }

        public void ResetForLifecycle()
        {
            lifecycleGeneration++;
            ResetActionState();
            bufferedIntent = default;
            hasBufferedIntent = false;
            events?.Reset();
        }

        private void StartAction(ESActionRuntimeData runtimeData, in ESActionIntent intent, int comboOverride)
        {
            lifecycleGeneration++;
            current = runtimeData;
            isRunning = true;
            phaseIndex = 0;
            phaseElapsed = 0f;
            hitWindowOpened = false;
            comboIndex = comboOverride >= 0 ? comboOverride : 0;
            currentHandle = new ESActionRuntimeHandle(
                GetRuntimeKey(runtimeData),
                lifecycleGeneration,
                catalogVersion);
            Publish(ESActionEventKind.ActionStarted, default);
            Publish(ESActionEventKind.PhaseEntered, default);
        }

        private void FinishAction()
        {
            Publish(ESActionEventKind.ActionFinished, default);
            if (hasBufferedIntent && TryResolveComboNext(out int nextCombo))
            {
                ESActionIntent nextIntent = bufferedIntent;
                bufferedIntent = default;
                hasBufferedIntent = false;
                if (table.TryGet(nextIntent.actionKey, out ESActionRuntimeData nextData))
                    StartAction(nextData, nextIntent, nextCombo);
                else
                    ResetActionState();
                return;
            }

            bufferedIntent = default;
            hasBufferedIntent = false;
            ResetActionState();
        }

        private bool TryResolveComboNext(out int nextCombo)
        {
            nextCombo = 0;
            if (current == null || current.comboTransitions == null || !hasBufferedIntent)
                return false;

            for (int i = 0; i < current.comboTransitions.Count; i++)
            {
                ESActionComboTransitionData transition = current.comboTransitions[i];
                if (transition == null || transition.fromStep != comboIndex)
                    continue;
                if (transition.targetActionKey != null && transition.targetActionKey.IsConfigured
                    && !ESConfigKeyMatch.Matches(
                        transition.targetActionKey.EnumKeyInt,
                        transition.targetActionKey.StringKey,
                        bufferedIntent.actionKey.EnumKeyInt,
                        bufferedIntent.actionKey.StringKey))
                    continue;
                nextCombo = transition.toStep;
                return true;
            }

            return false;
        }

        private int GetRuntimeKey(ESActionRuntimeData runtimeData)
        {
            if (runtimeData == null || runtimeData.actionKey == null || !runtimeData.actionKey.IsConfigured)
                return 0;

            ESActionConfigKey key = runtimeData.actionKey;
            return table.TryGetRuntimeKey(key, out int runtimeKey) ? runtimeKey : 0;
        }

        private bool IsInputBufferOpen()
        {
            ESActionPhaseData phase = GetCurrentPhaseData();
            if (phase == null)
                return false;

            float window = phase.inputBufferWindow > 0f ? phase.inputBufferWindow : current.globalInputBufferWindow;
            return window > 0f && phaseElapsed <= window;
        }

        private ESActionPhaseKind GetCurrentPhaseKind()
        {
            ESActionPhaseData phase = GetCurrentPhaseData();
            return phase != null ? phase.kind : ESActionPhaseKind.None;
        }

        private ESActionPhaseData GetCurrentPhaseData()
        {
            if (current == null || current.phases == null || phaseIndex < 0 || phaseIndex >= current.phases.Count)
                return null;
            return current.phases[phaseIndex];
        }

        private void ResetActionState()
        {
            current = null;
            currentHandle = default;
            isRunning = false;
            phaseIndex = 0;
            phaseElapsed = 0f;
            hitWindowOpened = false;
            comboIndex = 0;
        }

        private void Publish(ESActionEventKind kind, in ESActionHitResult hitResult)
        {
            if (events == null)
                return;

            ESActionConfigKey key = current != null ? current.actionKey : null;
            nextEmissionId++;
            events.Publish(new ESActionEvent(
                kind,
                key,
                currentHandle,
                GetCurrentPhaseKind(),
                comboIndex,
                nextEmissionId,
                null,
                hitResult));
        }
    }
}
