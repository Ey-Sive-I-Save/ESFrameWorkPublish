using System;
using ES;
using UnityEngine.InputSystem;

namespace ES.Internal
{
    public static class ESInputBindingKeyUtility
    {
        public const string MainBindingName = "Main";

        public static string MakeBindingId(ESInputActionDefine action, ESInputBindingDefine binding, int duplicateIndex = 0)
        {
            string actionKey = action != null && !string.IsNullOrEmpty(action.actionName)
                ? action.actionName
                : action != null ? action.id.ToString() : "UnknownAction";
            string schemeKey = binding != null && !string.IsNullOrEmpty(binding.schemeId)
                ? binding.schemeId
                : "AnyScheme";
            string sourceKey = binding != null ? binding.source.ToString() : "UnknownSource";
            string nameKey = binding != null && !string.IsNullOrEmpty(binding.name)
                ? binding.name
                : MainBindingName;

            string bindingId = actionKey + "." + schemeKey + "." + sourceKey + "." + nameKey;
            if (duplicateIndex > 0)
                bindingId += "." + duplicateIndex;

            return bindingId;
        }

        public static string MakeBindingBaseKey(ESInputActionDefine action, ESInputBindingDefine binding)
        {
            string actionKey = action != null && !string.IsNullOrEmpty(action.actionName)
                ? action.actionName
                : action != null ? action.id.ToString() : "UnknownAction";
            string schemeKey = binding != null && !string.IsNullOrEmpty(binding.schemeId)
                ? binding.schemeId
                : "AnyScheme";
            string sourceKey = binding != null ? binding.source.ToString() : "UnknownSource";
            string nameKey = binding != null && !string.IsNullOrEmpty(binding.name)
                ? binding.name
                : MainBindingName;

            return actionKey + "." + schemeKey + "." + sourceKey + "." + nameKey;
        }

        public static void EnsureBindingName(ESInputBindingDefine binding)
        {
            if (binding == null || !string.IsNullOrEmpty(binding.name))
                return;

            binding.name = MainBindingName;
        }

        // Fallback formatter only. Authoritative runtime/editor rebinding should use
        // InputActionRebindingExtensions.RebindingOperation and consume the path it applies.
        public static string ToBindingPath(InputControl control)
        {
            if (control == null || control.device == null)
                return string.Empty;

            string deviceLayout = control.device.layout;
            string devicePath = control.device.path;
            string controlPath = control.path;
            if (string.IsNullOrEmpty(deviceLayout) || string.IsNullOrEmpty(controlPath))
                return string.Empty;

            if (!string.IsNullOrEmpty(devicePath)
                && controlPath.StartsWith(devicePath, StringComparison.Ordinal))
            {
                string tail = controlPath.Substring(devicePath.Length);
                if (tail.StartsWith("/", StringComparison.Ordinal))
                    tail = tail.Substring(1);

                return "<" + deviceLayout + ">/" + tail;
            }

            return controlPath;
        }
    }
}
