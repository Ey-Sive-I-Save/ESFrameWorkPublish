using UnityEngine;

namespace ES
{
    public struct ESInputRuntimeValue
    {
        public bool pressed;
        public bool held;
        public bool released;
        public bool pressedConsumed;
        public bool releasedConsumed;
        public bool heldConsumed;
        public bool longPressConsumed;
        public bool doublePressConsumed;
        public bool longPressed;
        public bool doublePressed;
        public bool longPressFired;
        public bool pressBlockedByLongPress;
        public bool buttonHeldThisFrame;

        public float axis;
        public Vector2 vector2;
        public float holdTime;
        public float pressStartTime;
        public float lastPressedTime;

        public void ClearFrameState()
        {
            pressed = false;
            released = false;
            pressedConsumed = false;
            releasedConsumed = false;
            heldConsumed = false;
            longPressConsumed = false;
            doublePressConsumed = false;
            longPressed = false;
            doublePressed = false;
            buttonHeldThisFrame = false;
        }

        public void ResetAll()
        {
            pressed = false;
            held = false;
            released = false;
            pressedConsumed = false;
            releasedConsumed = false;
            heldConsumed = false;
            longPressConsumed = false;
            doublePressConsumed = false;
            longPressed = false;
            doublePressed = false;
            longPressFired = false;
            pressBlockedByLongPress = false;
            buttonHeldThisFrame = false;
            axis = 0f;
            vector2 = Vector2.zero;
            holdTime = 0f;
            pressStartTime = 0f;
            lastPressedTime = 0f;
        }
    }

    public struct ESInputActionMeta
    {
        public ESInputActionId id;
        public string actionName;
        public ESInputValueType valueType;
        public ESInputActionCategory category;
        public bool allowRebind;
        public ESInputTriggerType triggerType;
        public ESInputTriggerFeature triggerFeatures;
        public ESInputPressPolicy pressPolicy;
        public float longPressDuration;
        public float doublePressWindow;
        public string displayName;
    }
}
