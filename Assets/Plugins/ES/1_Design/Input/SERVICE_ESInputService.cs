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
            return values != null && index >= 0 && index < values.Length;
        }

        public void ClearFrameState()
        {
            if (values == null)
                return;

            for (int i = 0; i < values.Length; i++)
                values[i].ClearFrameState();
        }

        public void ResetAll()
        {
            if (values == null)
                return;

            for (int i = 0; i < values.Length; i++)
                values[i].ResetAll();
        }
    }

    public sealed class ESInputService
    {
        private ESInputRuntimeCache cache;
        private ESRuntimeModeService modeService;

        public ESInputService(ESInputRuntimeCache cache = null, ESRuntimeModeService modeService = null)
        {
            this.cache = cache;
            this.modeService = modeService;
        }

        public ESInputRuntimeCache Cache => cache;
        public ESRuntimeModeService ModeService => modeService;

        public void SetCache(ESInputRuntimeCache newCache) => cache = newCache;
        public void SetModeService(ESRuntimeModeService service) => modeService = service;
        public void BeginFrame() => ClearFrameState();

        public void EndFrame(float time)
        {
            if (cache == null || cache.values == null)
                return;

            for (int i = 0; i < cache.values.Length; i++)
            {
                ESInputActionMeta meta = cache.metas[i];
                if (meta.valueType != ESInputValueType.Button)
                    continue;

                ref ESInputRuntimeValue value = ref cache.values[i];
                CommitButton(ref value, meta, value.buttonHeldThisFrame, time);
            }
        }

        public void WriteButton(ESInputActionId id, bool isHeld, float time)
        {
            int index = (int)id;
            if (cache == null || !cache.IsValidIndex(index))
                return;

            ref ESInputRuntimeValue value = ref cache.values[index];
            value.buttonHeldThisFrame |= isHeld;
        }

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

        public void WriteAxis(ESInputActionId id, float axis)
        {
            int index = (int)id;
            if (cache == null || !cache.IsValidIndex(index))
                return;

            cache.values[index].axis = axis;
        }

        public void WriteVector2(ESInputActionId id, Vector2 vector2)
        {
            int index = (int)id;
            if (cache == null || !cache.IsValidIndex(index))
                return;

            cache.values[index].vector2 = vector2;
        }

        public bool WasPressed(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.values[index].pressed
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
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.values[index].held;
        }

        public bool WasReleased(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.values[index].released
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
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.values[index].longPressed
                   && !cache.values[index].longPressConsumed;
        }

        public bool WasDoublePressed(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.values[index].doublePressed
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
            if (cache == null || !cache.IsValidIndex(index))
                return false;

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

            int index = (int)id;
            return cache != null && cache.IsValidIndex(index)
                ? cache.values[index].axis
                : 0f;
        }

        public Vector2 ReadVector2(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return Vector2.zero;

            int index = (int)id;
            return cache != null && cache.IsValidIndex(index)
                ? cache.values[index].vector2
                : Vector2.zero;
        }

        public float GetHoldTime(ESInputActionId id)
        {
            int index = (int)id;
            return cache != null && cache.IsValidIndex(index)
                ? cache.values[index].holdTime
                : 0f;
        }

        public void ClearFrameState()
        {
            if (cache != null)
                cache.ClearFrameState();
        }

        public void ResetAll()
        {
            if (cache != null)
                cache.ResetAll();
        }

        public bool IsActionAllowed(ESInputActionId id)
        {
            if (modeService == null)
                return true;

            ESRuntimeModePolicy policy = modeService.CurrentPolicy;
            if (!policy.allowPlayerInput)
                return false;

            return IsCategoryAllowed(GetActionCategory(id), policy);
        }

        private ESInputActionCategory GetActionCategory(ESInputActionId id)
        {
            int index = (int)id;
            if (cache != null && cache.IsValidIndex(index))
            {
                ESInputActionCategory category = cache.metas[index].category;
                if (category != ESInputActionCategory.Common)
                    return category;
            }

            return ESInputDefineUtility.GuessCategory(id);
        }

        private static bool IsCategoryAllowed(ESInputActionCategory category, ESRuntimeModePolicy policy)
        {
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
                case ESInputActionCategory.UI:
                    return policy.allowUIInput;
                default:
                    return true;
            }
        }
    }
}
