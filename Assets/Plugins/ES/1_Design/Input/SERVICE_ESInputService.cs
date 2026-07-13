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

        public ESInputRuntimeCache Cache
        {
            get { return cache; }
        }

        public ESRuntimeModeService ModeService
        {
            get { return modeService; }
        }

        public void SetCache(ESInputRuntimeCache newCache)
        {
            cache = newCache;
        }

        public void SetModeService(ESRuntimeModeService service)
        {
            modeService = service;
        }

        public void BeginFrame()
        {
            ClearFrameState();
        }

        public void WriteButton(ESInputActionId id, bool isHeld, float time)
        {
            int index = (int)id;
            if (cache == null || !cache.IsValidIndex(index))
                return;

            ref ESInputRuntimeValue value = ref cache.values[index];
            ESInputActionMeta meta = cache.metas[index];
            float longPressDuration = meta.longPressDuration > 0f ? meta.longPressDuration : 0.5f;
            float doublePressWindow = meta.doublePressWindow > 0f ? meta.doublePressWindow : 0.28f;

            if (isHeld)
            {
                if (!value.held)
                {
                    value.pressed = true;
                    value.doublePressed = value.lastPressedTime > 0f && time - value.lastPressedTime <= doublePressWindow;
                    value.lastPressedTime = time;
                    value.longPressFired = false;
                }

                value.held = true;
                value.holdTime = time - value.lastPressedTime;

                if (!value.longPressFired && value.holdTime >= longPressDuration)
                {
                    value.longPressed = true;
                    value.longPressFired = true;
                }
            }
            else
            {
                if (value.held)
                    value.released = true;

                value.held = false;
                value.holdTime = 0f;
                value.longPressFired = false;
            }
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
                   && !cache.values[index].consumed;
        }

        public bool ConsumePressed(ESInputActionId id)
        {
            if (!WasPressed(id))
                return false;

            cache.values[(int)id].consumed = true;
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
                   && cache.values[index].released;
        }

        public bool WasLongPressed(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.values[index].longPressed
                   && !cache.values[index].consumed;
        }

        public bool WasDoublePressed(ESInputActionId id)
        {
            if (!IsActionAllowed(id))
                return false;

            int index = (int)id;
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.values[index].doublePressed
                   && !cache.values[index].consumed;
        }

        public bool ConsumeLongPressed(ESInputActionId id)
        {
            if (!WasLongPressed(id))
                return false;

            cache.values[(int)id].consumed = true;
            return true;
        }

        public bool ConsumeDoublePressed(ESInputActionId id)
        {
            if (!WasDoublePressed(id))
                return false;

            cache.values[(int)id].consumed = true;
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
