using UnityEngine;

namespace ES
{
    public sealed class ESInputRuntimeCache
    {
        public ESInputRuntimeValue[] values;
        public ESInputActionMeta[] metas;

        public ESInputRuntimeCache(int actionCapacity)
        {
            if (actionCapacity < 1)
                actionCapacity = 1;

            values = new ESInputRuntimeValue[actionCapacity];
            metas = new ESInputActionMeta[actionCapacity];
        }

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < values.Length;
        }

        public void ClearFrameState()
        {
            for (int i = 0; i < values.Length; i++)
                values[i].ClearFrameState();
        }

        public void ResetAll()
        {
            for (int i = 0; i < values.Length; i++)
                values[i].ResetAll();
        }
    }

    public sealed class ESInputService
    {
        private ESInputRuntimeCache cache;
        private ESRuntimeModeService modeService;
        private ESRuntimeModePolicy currentModePolicy;
        private int[] activeValueIndices;
        private bool[] activeValueMarks;
        private int activeValueCount;
        private bool hasModePolicy;

        public ESInputService(ESInputRuntimeCache cache = null, ESRuntimeModeService modeService = null)
        {
            currentModePolicy = ESRuntimeModePolicy.Default;
            SetCache(cache);
            SetModeService(modeService);
        }

        public ESInputRuntimeCache Cache => cache;
        public ESRuntimeModeService ModeService => modeService;
        public ESRuntimeModePolicy CurrentModePolicy => currentModePolicy;
        public event System.Action<ESRuntimeModePolicy> OnInputPolicyChanged;

        #region 初始化

        public void SetCache(ESInputRuntimeCache newCache)
        {
            cache = newCache;
            EnsureActiveValueCapacity(cache != null && cache.values != null ? cache.values.Length : 0);
            ClearActiveValues();

            if (hasModePolicy)
                ResetInputsBlockedByPolicy(currentModePolicy);
        }

        public void SetModeService(ESRuntimeModeService service)
        {
            if (ReferenceEquals(modeService, service))
            {
                if (service != null)
                    HandleModePolicyChanged(service.CurrentPolicy);
                return;
            }

            if (modeService != null)
                modeService.OnPolicyChanged -= HandleModePolicyChanged;

            modeService = service;
            hasModePolicy = service != null;
            currentModePolicy = hasModePolicy ? service.CurrentPolicy : ESRuntimeModePolicy.Default;

            if (modeService != null)
                modeService.OnPolicyChanged += HandleModePolicyChanged;

            if (hasModePolicy)
                ResetInputsBlockedByPolicy(currentModePolicy);
        }

        #endregion

        #region 帧流程

        public void BeginFrame()
        {
            ClearFrameState();
            ClearInputsBlockedByCurrentPolicy();
        }

        public void EndFrame(float time)
        {
            if (cache == null || cache.values == null || cache.metas == null)
                return;

            for (int activeIndex = activeValueCount - 1; activeIndex >= 0; activeIndex--)
            {
                int i = activeValueIndices[activeIndex];
                ESInputActionMeta meta = cache.metas[i];
                ref ESInputRuntimeValue value = ref cache.values[i];

                if (meta.valueType == ESInputValueType.Button)
                {
                    if (!IsActionAllowed((ESInputActionId)i))
                    {
                        BlockButtonByPolicy(ref value, value.buttonHeldThisFrame || value.policyBlockedUntilRelease);
                    }
                    else
                    {
                        CommitButton(ref value, meta, value.buttonHeldThisFrame, time);
                    }
                }

                if (!value.HasRuntimeState())
                    RemoveActiveValueAtSwapBack(activeIndex);
            }
        }

        #endregion

        #region 写入输入

        public void WriteButton(ESInputActionId id, bool isHeld, float time)
        {
            int index = (int)id;
            if (!CanAccessIndex(index))
                return;

            ref ESInputRuntimeValue value = ref cache.values[index];

            if (!IsActionAllowed(id))
            {
                if (isHeld || value.HasRuntimeState())
                    MarkValueActive(index);

                BlockButtonByPolicy(ref value, isHeld);
                return;
            }

            if (value.policyBlockedUntilRelease)
            {
                if (isHeld)
                {
                    MarkValueActive(index);
                    BlockButtonByPolicy(ref value, true);
                    return;
                }

                MarkValueActive(index);
                value.policyBlockedUntilRelease = false;
            }

            if (isHeld || value.HasRuntimeState())
                MarkValueActive(index);

            value.buttonHeldThisFrame |= isHeld;
        }

        public void WriteAxis(ESInputActionId id, float axis)
        {
            int index = (int)id;
            if (!CanAccessIndex(index))
                return;

            if (!IsActionAllowed(id))
            {
                if (cache.values[index].axis != 0f)
                    MarkValueActive(index);

                cache.values[index].axis = 0f;
                return;
            }

            if (axis != 0f || cache.values[index].axis != 0f)
                MarkValueActive(index);

            cache.values[index].axis = axis;
        }

        public void WriteVector2(ESInputActionId id, Vector2 vector2)
        {
            int index = (int)id;
            if (!CanAccessIndex(index))
                return;

            if (!IsActionAllowed(id))
            {
                if (cache.values[index].vector2 != Vector2.zero)
                    MarkValueActive(index);

                cache.values[index].vector2 = Vector2.zero;
                return;
            }

            if (vector2 != Vector2.zero || cache.values[index].vector2 != Vector2.zero)
                MarkValueActive(index);

            cache.values[index].vector2 = vector2;
        }

        #endregion

        #region 读取与消费

        public bool WasPressed(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache.values[index].pressed
                   && !cache.values[index].pressedConsumed;
        }

        public bool ConsumePressed(ESInputActionId id)
        {
            if (!WasPressed(id))
                return false;

            cache.values[(int)id].pressedConsumed = true;
            return true;
        }

        public bool IsHeld(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache.values[index].held;
        }

        public bool WasReleased(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache.values[index].released
                   && !cache.values[index].releasedConsumed;
        }

        public bool ConsumeReleased(ESInputActionId id)
        {
            if (!WasReleased(id))
                return false;

            cache.values[(int)id].releasedConsumed = true;
            return true;
        }

        public bool WasLongPressed(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache.values[index].longPressed
                   && !cache.values[index].longPressConsumed;
        }

        public bool WasDoublePressed(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache.values[index].doublePressed
                   && !cache.values[index].doublePressConsumed;
        }

        public bool ConsumeLongPressed(ESInputActionId id)
        {
            if (!WasLongPressed(id))
                return false;

            cache.values[(int)id].longPressConsumed = true;
            return true;
        }

        public bool ConsumeDoublePressed(ESInputActionId id)
        {
            if (!WasDoublePressed(id))
                return false;

            cache.values[(int)id].doublePressConsumed = true;
            return true;
        }

        public bool WasTriggered(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            ESInputTriggerFeature features = GetEffectiveTriggerFeatures(cache.metas[index]);
            if (HasFeature(features, ESInputTriggerFeature.Pressed) && WasPressed(id))
                return true;
            if (HasFeature(features, ESInputTriggerFeature.Released) && WasReleased(id))
                return true;
            if (HasFeature(features, ESInputTriggerFeature.Held) && IsHeld(id) && !cache.values[index].heldConsumed)
                return true;
            if (HasFeature(features, ESInputTriggerFeature.LongPress) && WasLongPressed(id))
                return true;
            if (HasFeature(features, ESInputTriggerFeature.DoublePress) && WasDoublePressed(id))
                return true;

            return false;
        }

        public bool ConsumeTrigger(ESInputActionId id)
        {
            if (!WasTriggered(id))
                return false;

            int index = (int)id;
            ESInputTriggerFeature features = GetEffectiveTriggerFeatures(cache.metas[index]);
            ref ESInputRuntimeValue value = ref cache.values[index];
            if (HasFeature(features, ESInputTriggerFeature.Pressed) && value.pressed)
                value.pressedConsumed = true;
            if (HasFeature(features, ESInputTriggerFeature.Released) && value.released)
                value.releasedConsumed = true;
            if (HasFeature(features, ESInputTriggerFeature.Held) && value.held)
                value.heldConsumed = true;
            if (HasFeature(features, ESInputTriggerFeature.LongPress) && value.longPressed)
                value.longPressConsumed = true;
            if (HasFeature(features, ESInputTriggerFeature.DoublePress) && value.doublePressed)
                value.doublePressConsumed = true;
            return true;
        }

        public float ReadAxis(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return 0f;

            return cache.values[(int)id].axis;
        }

        public Vector2 ReadVector2(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return Vector2.zero;

            return cache.values[(int)id].vector2;
        }

        public float GetHoldTime(ESInputActionId id)
        {
            return cache.values[(int)id].holdTime;
        }

        #endregion

        #region 清理

        public void ClearFrameState()
        {
            if (cache == null || cache.values == null)
                return;

            for (int i = 0; i < activeValueCount; i++)
                cache.values[activeValueIndices[i]].ClearFrameState();
        }

        public void ResetAll()
        {
            if (cache == null)
                return;

            cache.ResetAll();
            ClearActiveValues();
        }

        #endregion

        #region 模式过滤

        public bool IsActionAllowed(ESInputActionId id)
        {
            if (!CanAccessIndex((int)id))
                return false;

            if (!hasModePolicy)
                return true;

            return IsCategoryAllowed(GetActionCategory(id), currentModePolicy);
        }

        private ESInputActionCategory GetActionCategory(ESInputActionId id)
        {
            int index = (int)id;
            if (cache.IsValidIndex(index))
            {
                ESInputActionCategory category = cache.metas[index].category;
                if (category != ESInputActionCategory.Common)
                    return category;
            }

            return ESInputDefineUtility.GuessCategory(id);
        }

        private void HandleModePolicyChanged(ESRuntimeModePolicy policy)
        {
            currentModePolicy = policy;
            hasModePolicy = modeService != null;
            ResetInputsBlockedByPolicy(policy);
            OnInputPolicyChanged?.Invoke(policy);
        }

        private void ResetInputsBlockedByPolicy(ESRuntimeModePolicy policy)
        {
            if (cache == null || cache.values == null || cache.metas == null)
                return;

            for (int i = 0; i < cache.values.Length; i++)
            {
                ESInputActionCategory category = GetActionCategory((ESInputActionId)i);
                if (IsCategoryAllowed(category, policy))
                    continue;

                ref ESInputRuntimeValue value = ref cache.values[i];
                bool waitRelease = cache.metas[i].valueType == ESInputValueType.Button
                                   && (value.held || value.buttonHeldThisFrame);
                value.ResetAll();
                value.policyBlockedUntilRelease = waitRelease;

                if (waitRelease)
                    MarkValueActive(i);
            }
        }

        private void ClearInputsBlockedByCurrentPolicy()
        {
            if (!hasModePolicy || cache == null || cache.values == null || cache.metas == null)
                return;

            for (int activeIndex = activeValueCount - 1; activeIndex >= 0; activeIndex--)
            {
                int i = activeValueIndices[activeIndex];
                ESInputActionCategory category = GetActionCategory((ESInputActionId)i);
                if (IsCategoryAllowed(category, currentModePolicy))
                {
                    if (!cache.values[i].HasRuntimeState())
                        RemoveActiveValueAtSwapBack(activeIndex);

                    continue;
                }

                ref ESInputRuntimeValue value = ref cache.values[i];
                bool waitRelease = cache.metas[i].valueType == ESInputValueType.Button
                                   && (value.held || value.buttonHeldThisFrame || value.policyBlockedUntilRelease);
                value.ResetAll();
                value.policyBlockedUntilRelease = waitRelease;

                if (!value.HasRuntimeState())
                    RemoveActiveValueAtSwapBack(activeIndex);
            }
        }

        private static void BlockButtonByPolicy(ref ESInputRuntimeValue value, bool physicalHeld)
        {
            value.ResetAll();
            value.policyBlockedUntilRelease = physicalHeld;
        }

        #endregion

        #region 按钮触发计算

        private static void CommitButton(ref ESInputRuntimeValue value, ESInputActionMeta meta, bool isHeld, float time)
        {
            ESInputTriggerFeature features = GetEffectiveTriggerFeatures(meta);
            bool usePressed = HasFeature(features, ESInputTriggerFeature.Pressed);
            bool useReleased = HasFeature(features, ESInputTriggerFeature.Released);
            bool useLongPress = HasFeature(features, ESInputTriggerFeature.LongPress);
            bool useDoublePress = HasFeature(features, ESInputTriggerFeature.DoublePress);

            if (isHeld)
            {
                if (!value.held)
                {
                    value.pressStartTime = time;
                    value.pressBlockedByLongPress = false;

                    if (usePressed && meta.pressPolicy == ESInputPressPolicy.PressedImmediate)
                        value.pressed = true;

                    if (useDoublePress)
                    {
                        float doublePressWindow = meta.doublePressWindow > 0f ? meta.doublePressWindow : 0.28f;
                        value.doublePressed = value.lastPressedTime > 0f && time - value.lastPressedTime <= doublePressWindow;
                    }

                    value.lastPressedTime = time;
                    value.longPressFired = false;
                }

                value.held = true;

                if (useLongPress)
                {
                    value.holdTime = time - value.pressStartTime;
                    float longPressDuration = meta.longPressDuration > 0f ? meta.longPressDuration : 0.5f;
                    if (!value.longPressFired && value.holdTime >= longPressDuration)
                    {
                        value.longPressed = true;
                        value.longPressFired = true;
                        if (meta.pressPolicy == ESInputPressPolicy.LongPressConsumesPressed)
                            value.pressBlockedByLongPress = true;
                    }
                }
            }
            else
            {
                if (value.held)
                {
                    if (useReleased)
                        value.released = true;

                    if (usePressed && ShouldFirePressedOnRelease(value, meta))
                        value.pressed = true;
                }

                value.held = false;
                value.holdTime = 0f;
                value.longPressFired = false;
            }
        }

        private static bool ShouldFirePressedOnRelease(ESInputRuntimeValue value, ESInputActionMeta meta)
        {
            switch (meta.pressPolicy)
            {
                case ESInputPressPolicy.PressedOnRelease:
                    return true;
                case ESInputPressPolicy.PressedIfNotLongPress:
                case ESInputPressPolicy.LongPressConsumesPressed:
                    return !value.longPressFired && !value.pressBlockedByLongPress;
                default:
                    return false;
            }
        }

        private static ESInputTriggerFeature GetEffectiveTriggerFeatures(ESInputActionMeta meta)
        {
            return meta.triggerFeatures != ESInputTriggerFeature.None
                ? meta.triggerFeatures
                : ESInputActionDefine.ConvertLegacyTriggerType(meta.triggerType);
        }

        private static bool HasFeature(ESInputTriggerFeature features, ESInputTriggerFeature feature)
        {
            return (features & feature) != 0;
        }

        #endregion

        #region 内部工具

        private static bool IsCategoryAllowed(ESInputActionCategory category, ESRuntimeModePolicy policy)
        {
            if (category == ESInputActionCategory.UI)
                return policy.allowUIInput;

            if (!policy.allowPlayerInput)
                return false;

            switch (category)
            {
                case ESInputActionCategory.Move:
                case ESInputActionCategory.SpecialMove:
                    return policy.allowMoveInput;
                case ESInputActionCategory.CameraLook:
                    return policy.allowCameraLook;
                case ESInputActionCategory.Combat:
                    return policy.allowCombatInput;
                case ESInputActionCategory.Interaction:
                    return policy.allowInteractionInput;
                default:
                    return true;
            }
        }

        private bool CanAccessIndex(int index)
        {
            return cache != null && cache.IsValidIndex(index);
        }

        private void EnsureActiveValueCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                activeValueIndices = null;
                activeValueMarks = null;
                return;
            }

            if (activeValueIndices == null || activeValueIndices.Length != capacity)
                activeValueIndices = new int[capacity];

            if (activeValueMarks == null || activeValueMarks.Length != capacity)
                activeValueMarks = new bool[capacity];
        }

        private void MarkValueActive(int index)
        {
            if (activeValueMarks == null || index < 0 || index >= activeValueMarks.Length)
                return;

            if (activeValueMarks[index])
                return;

            activeValueMarks[index] = true;
            activeValueIndices[activeValueCount++] = index;
        }

        private void RemoveActiveValueAtSwapBack(int activeIndex)
        {
            int removedIndex = activeValueIndices[activeIndex];
            int lastActiveIndex = activeValueCount - 1;

            activeValueMarks[removedIndex] = false;

            if (activeIndex != lastActiveIndex)
                activeValueIndices[activeIndex] = activeValueIndices[lastActiveIndex];

            activeValueIndices[lastActiveIndex] = 0;
            activeValueCount--;
        }

        private void ClearActiveValues()
        {
            if (activeValueMarks != null)
            {
                for (int i = 0; i < activeValueCount; i++)
                    activeValueMarks[activeValueIndices[i]] = false;
            }

            activeValueCount = 0;
        }

        #endregion
    }
}
