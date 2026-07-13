using UnityEngine;

namespace ES
{
    public struct ESInputRuntimeValue
    {
        public bool pressed;
        public bool held;
        public bool released;
        public bool consumed;
        public bool longPressed;
        public bool doublePressed;
        public bool longPressFired;

        public float axis;
        public Vector2 vector2;
        public float holdTime;
        public float lastPressedTime;

        public void ClearFrameState()
        {
            pressed = false;
            released = false;
            consumed = false;
            longPressed = false;
            doublePressed = false;
        }

        public void ResetAll()
        {
            pressed = false;
            held = false;
            released = false;
            consumed = false;
            longPressed = false;
            doublePressed = false;
            longPressFired = false;
            axis = 0f;
            vector2 = Vector2.zero;
            holdTime = 0f;
            lastPressedTime = 0f;
        }
    }

    public struct ESInputActionMeta
    {
        public ESInputActionId id;
        public ESInputValueType valueType;
        public ESInputActionCategory category;
        public bool allowRebind;
        public float longPressDuration;
        public float doublePressWindow;
        public string displayName;
    }
}
