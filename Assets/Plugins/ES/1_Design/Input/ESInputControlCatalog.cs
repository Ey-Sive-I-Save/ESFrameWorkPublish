using System;
using System.Collections.Generic;
using ES;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ES.Internal
{
    [Serializable]
    public struct ESInputControlOption
    {
        public string displayName;
        public string shortDisplayName;
        public string path;
        public string layoutName;
        public string controlName;
        public ESInputValueType valueType;
        public ESInputDeviceKind deviceKind;
    }

    public static class ESInputControlCatalog
    {
        private const string KeyboardLayout = "Keyboard";
        private const string MouseLayout = "Mouse";
        private const string GamepadLayout = "Gamepad";

        private static readonly List<ESInputControlOption> keyboard = new List<ESInputControlOption>(128);
        private static readonly List<ESInputControlOption> mouse = new List<ESInputControlOption>(32);
        private static readonly List<ESInputControlOption> keyboardMouse = new List<ESInputControlOption>(160);
        private static readonly List<ESInputControlOption> gamepad = new List<ESInputControlOption>(64);
        private static bool built;

        public static IReadOnlyList<ESInputControlOption> KeyboardOptions
        {
            get
            {
                EnsureBuilt();
                return keyboard;
            }
        }

        public static IReadOnlyList<ESInputControlOption> MouseOptions
        {
            get
            {
                EnsureBuilt();
                return mouse;
            }
        }

        public static IReadOnlyList<ESInputControlOption> GamepadOptions
        {
            get
            {
                EnsureBuilt();
                return gamepad;
            }
        }

        public static void Rebuild()
        {
            built = false;
            keyboard.Clear();
            mouse.Clear();
            keyboardMouse.Clear();
            gamepad.Clear();
            EnsureBuilt();
        }

        public static void GetOptions(ESInputDeviceKind deviceKind, ESInputValueType valueType, List<ESInputControlOption> results)
        {
            if (results == null)
                return;

            results.Clear();
            IReadOnlyList<ESInputControlOption> source = GetOptions(deviceKind);
            for (int i = 0; i < source.Count; i++)
            {
                ESInputControlOption option = source[i];
                if (option.valueType == valueType)
                    results.Add(option);
            }
        }

        public static IReadOnlyList<ESInputControlOption> GetOptions(ESInputDeviceKind deviceKind)
        {
            EnsureBuilt();
            switch (deviceKind)
            {
                case ESInputDeviceKind.Gamepad:
                    return gamepad;
                case ESInputDeviceKind.Touch:
                    return Array.Empty<ESInputControlOption>();
                default:
                    return keyboardMouse;
            }
        }

        public static bool TryFindByPath(string path, out ESInputControlOption option)
        {
            EnsureBuilt();
            if (TryFindByPath(keyboard, path, out option))
                return true;
            if (TryFindByPath(mouse, path, out option))
                return true;
            if (TryFindByPath(gamepad, path, out option))
                return true;

            option = default;
            return false;
        }

        private static bool TryFindByPath(IReadOnlyList<ESInputControlOption> options, string path, out ESInputControlOption option)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].path, path, StringComparison.Ordinal))
                {
                    option = options[i];
                    return true;
                }
            }

            option = default;
            return false;
        }

        private static void EnsureBuilt()
        {
            if (built)
                return;

            built = true;
            keyboard.Clear();
            mouse.Clear();
            keyboardMouse.Clear();
            gamepad.Clear();

            BuildFromLayout(KeyboardLayout, ESInputDeviceKind.KeyboardMouse, keyboard);
            BuildFromLayout(MouseLayout, ESInputDeviceKind.KeyboardMouse, mouse);
            BuildFromLayout(GamepadLayout, ESInputDeviceKind.Gamepad, gamepad);

            keyboardMouse.AddRange(keyboard);
            keyboardMouse.AddRange(mouse);

            SortOptions(keyboard);
            SortOptions(mouse);
            SortOptions(keyboardMouse);
            SortOptions(gamepad);
        }

        private static void BuildFromLayout(string layoutName, ESInputDeviceKind deviceKind, List<ESInputControlOption> target)
        {
            InputDevice device = null;
            try
            {
                device = InputSystem.AddDevice(layoutName, "__ESInputCatalog_" + layoutName);
                for (int i = 0; i < device.allControls.Count; i++)
                {
                    InputControl control = device.allControls[i];
                    if (!ShouldInclude(control, layoutName, out ESInputValueType valueType))
                        continue;

                    string path = MakeLayoutPath(layoutName, device.path, control.path);
                    if (string.IsNullOrEmpty(path) || ContainsPath(target, path))
                        continue;

                    target.Add(new ESInputControlOption
                    {
                        displayName = GetChineseDisplayName(layoutName, control),
                        shortDisplayName = GetShortDisplayName(control),
                        path = path,
                        layoutName = layoutName,
                        controlName = GetControlName(device.path, control.path),
                        valueType = valueType,
                        deviceKind = deviceKind
                    });
                }
            }
            finally
            {
                if (device != null)
                    InputSystem.RemoveDevice(device);
            }
        }

        private static bool ShouldInclude(InputControl control, string layoutName, out ESInputValueType valueType)
        {
            valueType = ESInputValueType.Button;
            if (control == null)
                return false;

            bool explicitMouseVector = layoutName == MouseLayout
                                       && (control.name == "delta" || control.name == "scroll");

            if ((control.noisy || control.synthetic) && !explicitMouseVector)
                return false;

            if (control is ButtonControl)
            {
                valueType = ESInputValueType.Button;
                return true;
            }

            if (control is Vector2Control)
            {
                valueType = ESInputValueType.Vector2;
                return IsTopLevelUsefulVector2(control, layoutName);
            }

            if (control is AxisControl)
            {
                valueType = ESInputValueType.Axis;
                return IsUsefulAxis(control, layoutName);
            }

            return false;
        }

        private static bool IsTopLevelUsefulVector2(InputControl control, string layoutName)
        {
            if (layoutName == MouseLayout)
                return control.name == "delta" || control.name == "scroll";

            return control.parent != null && control.parent is InputDevice;
        }

        private static bool IsUsefulAxis(InputControl control, string layoutName)
        {
            if (layoutName == KeyboardLayout)
                return false;

            if (layoutName == MouseLayout)
                return control.name == "x" || control.name == "y";

            if (layoutName == GamepadLayout)
                return control.name == "leftTrigger" || control.name == "rightTrigger";

            return control.parent != null && control.parent is InputDevice;
        }

        private static string MakeLayoutPath(string layoutName, string devicePath, string controlPath)
        {
            if (string.IsNullOrEmpty(layoutName) || string.IsNullOrEmpty(controlPath))
                return string.Empty;

            string tail = controlPath;
            if (!string.IsNullOrEmpty(devicePath)
                && tail.StartsWith(devicePath, StringComparison.Ordinal))
            {
                tail = tail.Substring(devicePath.Length);
            }

            if (tail.StartsWith("/", StringComparison.Ordinal))
                tail = tail.Substring(1);

            return string.IsNullOrEmpty(tail) ? string.Empty : "<" + layoutName + ">/" + tail;
        }

        private static string GetControlName(string devicePath, string controlPath)
        {
            if (string.IsNullOrEmpty(controlPath))
                return string.Empty;

            string result = controlPath;
            if (!string.IsNullOrEmpty(devicePath)
                && result.StartsWith(devicePath, StringComparison.Ordinal))
            {
                result = result.Substring(devicePath.Length);
            }

            if (result.StartsWith("/", StringComparison.Ordinal))
                result = result.Substring(1);

            return result;
        }

        private static bool ContainsPath(List<ESInputControlOption> target, string path)
        {
            for (int i = 0; i < target.Count; i++)
            {
                if (string.Equals(target[i].path, path, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void SortOptions(List<ESInputControlOption> options)
        {
            options.Sort((a, b) =>
            {
                int typeCompare = a.valueType.CompareTo(b.valueType);
                return typeCompare != 0
                    ? typeCompare
                    : string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
            });
        }

        private static string GetShortDisplayName(InputControl control)
        {
            if (control == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(control.shortDisplayName))
                return control.shortDisplayName;

            return !string.IsNullOrEmpty(control.displayName)
                ? control.displayName
                : control.name;
        }

        private static string GetChineseDisplayName(string layoutName, InputControl control)
        {
            string name = control != null ? control.name : string.Empty;
            if (layoutName == KeyboardLayout)
                return GetKeyboardChineseName(name);
            if (layoutName == MouseLayout)
                return GetMouseChineseName(name);
            if (layoutName == GamepadLayout)
                return GetGamepadChineseName(name);

            return GetShortDisplayName(control);
        }

        private static string GetKeyboardChineseName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            if (name.Length == 1)
                return name.ToUpperInvariant();

            switch (name)
            {
                case "space": return "空格";
                case "enter": return "回车";
                case "escape": return "Esc";
                case "tab": return "Tab";
                case "backspace": return "退格";
                case "leftShift": return "左 Shift";
                case "rightShift": return "右 Shift";
                case "leftCtrl": return "左 Ctrl";
                case "rightCtrl": return "右 Ctrl";
                case "leftAlt": return "左 Alt";
                case "rightAlt": return "右 Alt";
                case "capsLock": return "Caps Lock";
                case "pageUp": return "Page Up";
                case "pageDown": return "Page Down";
                case "home": return "Home";
                case "end": return "End";
                case "insert": return "Insert";
                case "delete": return "Delete";
                case "upArrow": return "方向键 上";
                case "downArrow": return "方向键 下";
                case "leftArrow": return "方向键 左";
                case "rightArrow": return "方向键 右";
                case "numpadEnter": return "小键盘 回车";
                case "numpadPlus": return "小键盘 +";
                case "numpadMinus": return "小键盘 -";
                case "numpadMultiply": return "小键盘 *";
                case "numpadDivide": return "小键盘 /";
                case "printScreen": return "Print Screen";
                case "scrollLock": return "Scroll Lock";
                case "pause": return "Pause";
                default:
                    if (name.StartsWith("f", StringComparison.Ordinal) && name.Length <= 3)
                        return name.ToUpperInvariant();
                    if (name.StartsWith("digit", StringComparison.Ordinal))
                        return name.Substring(5);
                    if (name.StartsWith("numpad", StringComparison.Ordinal))
                        return "小键盘 " + name.Substring(6);
                    return NicifyName(name);
            }
        }

        private static string GetMouseChineseName(string name)
        {
            switch (name)
            {
                case "leftButton": return "鼠标左键";
                case "rightButton": return "鼠标右键";
                case "middleButton": return "鼠标中键";
                case "backButton": return "鼠标后退键";
                case "forwardButton": return "鼠标前进键";
                case "scroll": return "鼠标滚轮";
                case "delta": return "鼠标移动";
                case "x": return "鼠标 X";
                case "y": return "鼠标 Y";
                default: return NicifyName(name);
            }
        }

        private static string GetGamepadChineseName(string name)
        {
            switch (name)
            {
                case "buttonSouth": return "手柄南键";
                case "buttonNorth": return "手柄北键";
                case "buttonWest": return "手柄西键";
                case "buttonEast": return "手柄东键";
                case "leftShoulder": return "左肩键";
                case "rightShoulder": return "右肩键";
                case "leftTrigger": return "左扳机";
                case "rightTrigger": return "右扳机";
                case "leftStick": return "左摇杆";
                case "rightStick": return "右摇杆";
                case "leftStickPress": return "左摇杆按下";
                case "rightStickPress": return "右摇杆按下";
                case "start": return "开始键";
                case "select": return "选择键";
                case "dpad": return "十字键";
                case "up": return "上";
                case "down": return "下";
                case "left": return "左";
                case "right": return "右";
                default:
                    if (name.StartsWith("dpad/", StringComparison.Ordinal))
                        return "十字键 " + NicifyName(name.Substring(5));
                    return NicifyName(name);
            }
        }

        private static string NicifyName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                    builder.Append(' ');

                if (i == 0)
                    builder.Append(char.ToUpperInvariant(c));
                else
                    builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
