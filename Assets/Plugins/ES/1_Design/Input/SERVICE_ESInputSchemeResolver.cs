using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ES
{
    public sealed class ESInputSchemeResolver : IDisposable
    {
        private string activeSchemeId = ESInputSchemeIds.KeyboardMouse;
        private string lockedSchemeId;
        private float switchCooldown = 0.25f;
        private float lastSwitchTime = -999f;
        private bool switchOnDeviceConnected;
        private bool enabled;

        public string ActiveSchemeId
        {
            get { return string.IsNullOrEmpty(lockedSchemeId) ? activeSchemeId : lockedSchemeId; }
        }

        public bool IsLocked
        {
            get { return !string.IsNullOrEmpty(lockedSchemeId); }
        }

        public float SwitchCooldown
        {
            get { return switchCooldown; }
            set { switchCooldown = value < 0f ? 0f : value; }
        }

        public bool SwitchOnDeviceConnected
        {
            get { return switchOnDeviceConnected; }
            set { switchOnDeviceConnected = value; }
        }

        public event Action<string, string> SchemeChanged;

        public void Initialize(string defaultSchemeId)
        {
            activeSchemeId = string.IsNullOrEmpty(defaultSchemeId)
                ? ESInputSchemeIds.KeyboardMouse
                : defaultSchemeId;
            lockedSchemeId = null;
            lastSwitchTime = -999f;
        }

        public void Enable()
        {
            if (enabled)
                return;

            InputSystem.onDeviceChange += OnDeviceChange;
            enabled = true;
        }

        public void Disable()
        {
            if (!enabled)
                return;

            InputSystem.onDeviceChange -= OnDeviceChange;
            enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        public void LockScheme(string schemeId)
        {
            if (string.IsNullOrEmpty(schemeId))
                return;

            string previous = ActiveSchemeId;
            lockedSchemeId = schemeId;
            NotifyChanged(previous, ActiveSchemeId);
        }

        public bool SetActiveScheme(string schemeId, float time)
        {
            if (string.IsNullOrEmpty(schemeId) || IsLocked)
                return false;

            if (string.Equals(activeSchemeId, schemeId, StringComparison.Ordinal))
                return false;

            string previous = activeSchemeId;
            activeSchemeId = schemeId;
            lastSwitchTime = time;
            NotifyChanged(previous, activeSchemeId);
            return true;
        }

        public void UnlockScheme(float time)
        {
            if (string.IsNullOrEmpty(lockedSchemeId))
                return;

            string previous = ActiveSchemeId;
            lockedSchemeId = null;
            lastSwitchTime = time;
            NotifyChanged(previous, ActiveSchemeId);
        }

        public bool TryResolveFromControl(InputControl control, float time, out string schemeId)
        {
            schemeId = null;
            if (control == null)
                return false;

            return TryResolveFromDevice(control.device, time, out schemeId);
        }

        public bool TryResolveFromDevice(InputDevice device, float time, out string schemeId)
        {
            schemeId = null;
            if (device == null || IsLocked)
                return false;

            string resolved = ResolveSchemeId(device);
            if (string.IsNullOrEmpty(resolved) || string.Equals(resolved, activeSchemeId, StringComparison.Ordinal))
                return false;

            if (time - lastSwitchTime < switchCooldown)
                return false;

            SetActiveScheme(resolved, time);
            schemeId = activeSchemeId;
            return true;
        }

        public static string ResolveSchemeId(InputDevice device)
        {
            if (device == null)
                return null;

            if (device is Gamepad)
                return ESInputSchemeIds.Gamepad;

            if (device is Touchscreen)
                return ESInputSchemeIds.Touch;

            if (device is Keyboard || device is Mouse)
                return ESInputSchemeIds.KeyboardMouse;

            return null;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!switchOnDeviceConnected)
                return;

            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                TryResolveFromDevice(device, Time.unscaledTime, out _);
            }
        }

        private void NotifyChanged(string previous, string current)
        {
            if (string.Equals(previous, current, StringComparison.Ordinal))
                return;

            SchemeChanged?.Invoke(previous, current);
        }
    }
}
