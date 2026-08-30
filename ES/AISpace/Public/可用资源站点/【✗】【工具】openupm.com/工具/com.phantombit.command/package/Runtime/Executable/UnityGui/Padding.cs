using System;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
    internal static class Platform
    {
        public static bool IsMouseAndKeyboard()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.WebGLPlayer:
                case RuntimePlatform.PS4:
                case RuntimePlatform.XboxOne:
                    return true;
                case RuntimePlatform.tvOS:
                case RuntimePlatform.Switch:
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    internal static class Selector
    {
        public static T OnPlatform<T>(T mouseAndKeyboard, T touch)
        {
            return Platform.IsMouseAndKeyboard() ? mouseAndKeyboard : touch;
        }
    }
    
    internal static class Padding
    {
        public static RectOffset OnPlatform(int mouseAndKeyboard, int touch)
        {
            return Platform.IsMouseAndKeyboard()
                ? new RectOffset(mouseAndKeyboard, mouseAndKeyboard, mouseAndKeyboard, mouseAndKeyboard)
                : new RectOffset(touch, touch, touch, touch);
        }
    }
}