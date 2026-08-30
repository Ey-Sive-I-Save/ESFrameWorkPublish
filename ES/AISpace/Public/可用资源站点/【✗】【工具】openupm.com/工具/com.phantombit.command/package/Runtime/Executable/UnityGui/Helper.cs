using System;
using System.Collections.Generic;
using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.Extensions;
using WellFired.Command.Unity.Runtime.Wrapper;
using Color = WellFired.Command.Skins.Color;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
    internal static class Helper
    {
        private const string ScalePref = "wellfired.command.scale";
        private const float ScaleMin = 0.5f;
        private const float ScaleMax = 4.0f;
        
        private static readonly Dictionary<string, GUIStyle> Lookup = new Dictionary<string, GUIStyle>();

        public static float Scale
        {
            get => PlayerPrefs.HasKey(ScalePref) ? PlayerPrefs.GetFloat(ScalePref) : 1.0f;
            set
            {
                value = Mathf.Clamp(value, ScaleMin, ScaleMax);
                PlayerPrefs.SetFloat(ScalePref, value);
                Lookup.Clear();
            }
        }
        
        public static bool IsValidConsoleScale(float scale)
        {
            return scale >= ScaleMin && scale <= ScaleMax;
        }

        private static void Get(string key, out GUIStyle style, out bool created)
        {
            if (Lookup.TryGetValue(key, out style))
            {
                created = false;
                return;
            } 
            
            Lookup[key] = new GUIStyle();
            style = Lookup[key];
            created = true;
        }
        
        public static GUIStyle Window(ISkinData skinData)
        {
            Get("window", out var style, out var created);

            if (!created)
                return style;
            
            var mainColor = skinData.MainColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
            style.normal = new GUIStyleState {background = mainTex};
            style.hover = new GUIStyleState {background = mainTex};
            style.active = new GUIStyleState {background = mainTex};
            style.focused = new GUIStyleState {background = mainTex};

            return style;
        }

        private static GUIStyle GetButtonStyle(ISkinData skinData)
        {   
            Get("button", out var style, out var created);

            if (created)
            {
                var mainColor = skinData.ButtonColor.ToColor();
                var hoverColor = skinData.ButtonHoverColor.ToColor();
                var mainFontColor = skinData.MainFontColor.ToColor();
                var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
                var hoverTex = Extensions.Texture.Texture2D(hoverColor, 1);
                style.normal = new GUIStyleState {background = mainTex, textColor = mainFontColor};
                style.hover = new GUIStyleState {background = hoverTex, textColor = mainFontColor};
                style.active = new GUIStyleState {background = mainTex, textColor = mainFontColor};
                style.focused = new GUIStyleState {background = mainTex, textColor = mainFontColor};
                style.padding = Padding.OnPlatform(skinData.ButtonPaddingKeyboard, skinData.ButtonPaddingTouch);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = (int)(skinData.FontSize * Scale);
            }

            return style;
        }

        private static GUIStyle GetLabelStyle(ISkinData skinData)
        {   
            Get("label", out var style, out var created);

            if (!created) 
                return style;
            
            var mainColor = skinData.MainColor.ToColor();
            var mainFontColor = skinData.GeneralLabelFontColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
            style.normal = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.hover = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.active = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.focused = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.padding = Padding.OnPlatform(skinData.ButtonPaddingKeyboard, skinData.ButtonPaddingTouch);
            style.alignment = TextAnchor.MiddleLeft;
            style.fontSize = (int)(skinData.FontSize * Scale);

            return style;
        }

        private static GUIStyle GetSelectedLabelStyle(ISkinData skinData)
        {   
            Get("label_selected", out var style, out var created);

            if (!created) 
                return style;
            
            var mainColor = skinData.MainColor.ToColor();
            var mainFontColor = skinData.GeneralLabelFontColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
            style.normal = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.hover = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.active = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.focused = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.padding = Padding.OnPlatform(skinData.ButtonPaddingKeyboard, skinData.ButtonPaddingTouch);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = (int)(skinData.FontSize * Scale);

            return style;
        }

        public static void Label(ISkinData skinData, GUIContent content, params GUILayoutOption [] options)
        {
            var style = GetLabelStyle(skinData);
            GUILayout.Label(content, style, options);
        }

        public static void Label(ISkinData skinData, GUIContent content)
        {
            var style = GetLabelStyle(skinData);
            GUILayout.Label(content, style);
        }

        public static void Label(ISkinData skinData, Rect rect, GUIContent content)
        {
            var style = GetLabelStyle(skinData);
            GUI.Label(rect, content, style);
        }

        public static bool Button(ISkinData skinData, string message)
        {
            var style = GetButtonStyle(skinData);
            return GUILayout.Button(message, style);
        }

        public static bool Button(ISkinData skinData, string message, params GUILayoutOption [] options)
        {
            var style = GetButtonStyle(skinData);
            return GUILayout.Button(message, style, options);
        }

        public static bool Button(ISkinData skinData, Rect rect, string message)
        {
            var style = GetButtonStyle(skinData);
            return GUI.Button(rect, message, style);
        }

        public static Vector2 LabelSizeWithContent(ISkinData skinData, GUIContent content)
        {
            var style = GetButtonStyle(skinData);
            return style.CalcSize(content);
        }

        public static IDisposable HeaderBeginHorizontal(ISkinData skinData)
        {
            Get("header_horizontal", out var style, out var created);

            if (!created) 
                return new GuiBeginHorizontal(style);
            
            var secondaryColor = skinData.SecondaryColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(secondaryColor, 1);
            style.normal = new GUIStyleState {background = mainTex};
            style.hover = new GUIStyleState {background = mainTex};
            style.active = new GUIStyleState {background = mainTex};
            style.focused = new GUIStyleState {background = mainTex};
            style.padding = Padding.OnPlatform(skinData.HeaderPaddingKeyboard, skinData.HeaderPaddingTouch);

            return new GuiBeginHorizontal(style);
        }

        public static IDisposable HeaderBeginVertical(ISkinData skinData)
        {
            Get("header_horizontal", out var style, out var created);

            if (!created) 
                return new GuiBeginHorizontal(style);
            
            var secondaryColor = skinData.SecondaryColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(secondaryColor, 1);
            style.normal = new GUIStyleState {background = mainTex};
            style.hover = new GUIStyleState {background = mainTex};
            style.active = new GUIStyleState {background = mainTex};
            style.focused = new GUIStyleState {background = mainTex};

            return new GuiBeginVertical(style);
        }

        public static IDisposable BodyBeginHorizontal(ISkinData skinData)
        {
            Get("body_horizontal", out var style, out var created);

            if (!created) 
                return new GuiBeginHorizontal(style);
            
            var secondaryColor = skinData.MainColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(secondaryColor, 1);
            style.normal = new GUIStyleState {background = mainTex};
            style.hover = new GUIStyleState {background = mainTex};
            style.active = new GUIStyleState {background = mainTex};
            style.focused = new GUIStyleState {background = mainTex};

            return new GuiBeginHorizontal(style);
        }

        public static IDisposable BodyBeginVertical(ISkinData skinData)
        {
            Get("body_vertical", out var style, out var created);

            if (!created) 
                return new GuiBeginVertical(style);
            
            var secondaryColor = skinData.MainColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(secondaryColor, 1);
            style.normal = new GUIStyleState {background = mainTex};
            style.hover = new GUIStyleState {background = mainTex};
            style.active = new GUIStyleState {background = mainTex};
            style.focused = new GUIStyleState {background = mainTex};

            return new GuiBeginVertical(style);
        }

        public static void Space(ISkinData skinData)
        {
            GUILayoutUtility.GetRect(
                0.0f,
                0.0f,
                GUIStyle.none, 
                GUILayout.MaxWidth(4), 
                GUILayout.MinWidth(1));
        }

        public static void ShrinkableSpace(ISkinData skinData)
        {
            GUILayoutUtility.GetRect(
                0.0f,
                0.0f,
                GUIStyle.none, 
                GUILayout.ExpandWidth(true), 
                GUILayout.MaxWidth(Selector.OnPlatform(skinData.ButtonSpacing * Scale, skinData.ButtonSpacingTouch * Scale) * 4), 
                GUILayout.MinWidth(1));
        }

        public static void LogEntry(ISkinData skinData, Rect itemRect, string message, LogType type, bool hover, bool active, bool on, bool keyboardFocus)
        {
            Get("logEntry", out var style, out var created);

            var guiContent = new GUIContent(message);
            
            if (created)
            {
                var secondaryColor = skinData.MainColor.ToColor();
                var mainTex = Extensions.Texture.Texture2D(secondaryColor, 1);
                style.normal = new GUIStyleState {background = mainTex};
                style.hover = new GUIStyleState {background = mainTex};
                style.active = new GUIStyleState {background = mainTex};
                style.focused = new GUIStyleState {background = mainTex};
                style.padding = new RectOffset(4, 4, 2, 2);
                
                SetFontSizeFor(style, guiContent, EntryHeight(skinData), itemRect.width);
            }

            Color fontColor;
            switch (type)
            {
                case LogType.Assert:
                case LogType.Exception:
                    fontColor = skinData.EntryExceptionColor;
                    break;
                case LogType.Error:
                    fontColor = skinData.EntryErrorColor;
                    break;
                case LogType.Warning:
                    fontColor = skinData.EntryWarningColor;
                    break;
                case LogType.Log:
                    fontColor = skinData.EntryInfoColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            style.normal.textColor = fontColor.ToColor();
            style.hover.textColor = fontColor.ToColor();
            style.active.textColor = fontColor.ToColor();
            style.focused.textColor = fontColor.ToColor();
            
            style.Draw(itemRect, guiContent, hover, active, on, keyboardFocus);
        }

        public static string TextEntry(ISkinData skinData, string commandInput, params GUILayoutOption [] options)
        {
            Get("textEntry", out var style, out var created);
            
            GUI.skin.settings.cursorColor = style.normal.textColor;

            if (!created) 
                return GUILayout.TextField(commandInput, style, options);
            
            var mainColor = skinData.TextEntryColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
            var mainFontColor = skinData.TextEntryFontColor.ToColor();
            style.normal = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.hover = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.active = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.focused = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            
            style.padding = Padding.OnPlatform(skinData.ButtonPaddingKeyboard, skinData.ButtonPaddingTouch);
            style.fontSize = (int)(skinData.FontSize * Scale);
            
            GUI.skin.settings.cursorColor = style.normal.textColor;
            
            return GUILayout.TextField(commandInput, style, options);
        }

        public static Vector2 DrawTooltip(ISkinData skinData, Vector2 topLeft, CommandWrapper commandWrapper)
        {
            var style = GetLabelStyle(skinData);
            
            Rect commandDescriptionRect;
            if (!string.IsNullOrEmpty(commandWrapper?.Description))
            {
                var commandDescriptionContent = new GUIContent(commandWrapper.Description);
                var maxWidth = style.CalcSize(commandDescriptionContent).x;
                var commandDescriptionHeight = GUI.skin.label.CalcHeight(commandDescriptionContent, maxWidth) * Scale;
                commandDescriptionRect = new Rect(topLeft.x, topLeft.y, maxWidth + 9, commandDescriptionHeight + 5);
                GUI.Label(commandDescriptionRect, commandDescriptionContent, style);
            }
            else
                commandDescriptionRect = new Rect(topLeft.x, topLeft.y, 0, 0);
            
            return new Vector2(commandDescriptionRect.xMin, commandDescriptionRect.yMax + 5);
        }

        public static GUIStyle SuggestionButtonStyle(ISkinData skinData)
        {
            return GetButtonStyle(skinData);
        }

        public static GUIStyle SuggestionLabelStyle(ISkinData skinData)
        {
            return GetLabelStyle(skinData);
        }

        private static void SetFontSizeFor(GUIStyle style, GUIContent guiContent, int visibleHeight, float width)
        {
            style.fontSize = 2;
            var size = style.CalcHeight(guiContent, width);

            while (size < visibleHeight)
            {
                style.fontSize++;
                size = style.CalcHeight(guiContent, width);

                if (style.fontSize <= 100) 
                    continue;
                
                style.fontSize--;
                return;
            }
        }
        
        public static Vector2 BeginScrollView(ISkinData skinData, Vector2 scrollPosition, params GUILayoutOption [] options)
        {
            Get("scrollView_scrollBar", out var scrollBarStyle, out var scrollBarCreated);

            if (scrollBarCreated)
            {
                var mainColor = skinData.MainColor.ToColor();
                var secondaryColor = skinData.SecondaryColor.ToColor();
                var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
                var secondaryTex = Extensions.Texture.Texture2D(secondaryColor, 1);
                scrollBarStyle.normal = new GUIStyleState {background = mainTex};
                scrollBarStyle.hover = new GUIStyleState {background = secondaryTex};
            }

            var scrollBarSize = (int)(Selector.OnPlatform(35, 45) * Scale);
            var scrollBarMinOppositeSize = (int)(Selector.OnPlatform(45, 65) * Scale);

            var verticalScrollbar = GUI.skin.verticalScrollbar;
            var verticalScrollbarThumb = GUI.skin.verticalScrollbarThumb;
            verticalScrollbar.normal.background = scrollBarStyle.normal.background;
            verticalScrollbarThumb.normal.background = scrollBarStyle.hover.background;
            GUI.skin.verticalScrollbar = verticalScrollbar;
            GUI.skin.verticalScrollbarThumb = verticalScrollbarThumb;
            
            GUI.skin.verticalScrollbarThumb.padding = new RectOffset(0, 0, 0, scrollBarMinOppositeSize);
            GUI.skin.verticalScrollbar.fixedWidth = scrollBarSize;
            GUI.skin.verticalScrollbarThumb.fixedWidth = scrollBarSize;
            
            var horizontalScrollbar = GUI.skin.horizontalScrollbar;
            var horizontalScrollbarThumb = GUI.skin.horizontalScrollbarThumb;
            horizontalScrollbar.normal.background = scrollBarStyle.normal.background;
            horizontalScrollbarThumb.normal.background = scrollBarStyle.hover.background;
            GUI.skin.horizontalScrollbar = horizontalScrollbar;
            GUI.skin.horizontalScrollbarThumb = horizontalScrollbarThumb;
            
            GUI.skin.horizontalScrollbarThumb.padding = new RectOffset(scrollBarMinOppositeSize, 0, 0, 0);
            GUI.skin.horizontalScrollbar.fixedHeight = scrollBarSize;
            GUI.skin.horizontalScrollbarThumb.fixedHeight = scrollBarSize;
            
            return GUILayout.BeginScrollView(scrollPosition, false, false, options);
        }

        public static void TextArea(ISkinData skinData, GUIContent content, params GUILayoutOption [] layoutOptions)
        {
            Get("textArea", out var style, out var created);

            var mainFontColor = skinData.DetailedLogMessageColor.ToColor();
            if (created)
            {
                var mainColor = skinData.DetailedLogMessageBackgroundColor.ToColor();
                var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
                style.normal = new GUIStyleState {background = mainTex, textColor = mainFontColor};
                style.hover = new GUIStyleState {background = mainTex, textColor = mainFontColor};
                style.active = new GUIStyleState {background = mainTex, textColor = mainFontColor};
                style.focused = new GUIStyleState {background = mainTex, textColor = mainFontColor};
                style.fontSize = (int)(skinData.FontSize * Scale);
            }
            
            GUI.skin.settings.cursorColor = style.normal.textColor;
            
            GUILayout.TextArea(content.text, style, layoutOptions);
        }

        public static void DrawArgument(ISkinData skinData, Rect rect, GUIContent content, bool current)
        {
            var style = GetLabelStyle(skinData);
            if (current)
                style = GetSelectedLabelStyle(skinData);
            
            GUI.Label(rect, content, style);
        }

        private const string PlaceholderText = "Search...";
            
        public static string SearchField(ISkinData skinData, string commandInput, float screenWidth)
        {
            Get("textEntry-search", out var style, out var created);
            Get("textEntry-search-placeholder", out var placeholderStyle, out var placeholderCreated);
            
            GUIStyle styleToUse;
            GUIContent content;
            
            var keyboardControl = GUIUtility.keyboardControl;
            var controlId = GUIUtility.GetControlID(FocusType.Keyboard) + 1;
            
            var hasFocus = keyboardControl == controlId;

            if (!hasFocus && string.IsNullOrEmpty(commandInput))
            {
                content = new GUIContent(PlaceholderText);
                styleToUse = placeholderStyle;
            }
            else
            {
                content = new GUIContent(commandInput);
                styleToUse = style;
            }
            
            GUI.skin.settings.cursorColor = styleToUse.normal.textColor;

            if (!created && !placeholderCreated)
            {   
                var textResult = GUILayout.TextField(content.text, styleToUse, GUILayout.ExpandWidth(true), GUILayout.MinWidth(0));

                if (!hasFocus && string.IsNullOrEmpty(commandInput))
                    return commandInput;

                return textResult;
            }
            
            // Default Style
            
            var mainColor = skinData.TextEntryColor.ToColor();
            var mainFontColor = skinData.TextEntryFontColor.ToColor();
            var mainTex = Extensions.Texture.Texture2D(mainColor, 1);
            style.normal = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.hover = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.active = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.focused = new GUIStyleState {background = mainTex, textColor = mainFontColor};
            style.padding = Padding.OnPlatform(skinData.ButtonPaddingKeyboard, skinData.ButtonPaddingTouch);
            style.alignment = TextAnchor.MiddleLeft;
            style.fontSize = (int)(skinData.FontSize * Scale);
            GUI.skin.settings.cursorColor = style.normal.textColor;
            
            
            // Placeholder
            
            var placeholderMainColor = skinData.PlaceholderTextEntryColor.ToColor();
            var placeholderMainFontColor = skinData.PlaceholderTextEntryFontColor.ToColor();
            var placeholderMainTex = Extensions.Texture.Texture2D(placeholderMainColor, 1);
            
            placeholderStyle.normal = new GUIStyleState {background = placeholderMainTex, textColor = placeholderMainFontColor};
            placeholderStyle.hover = new GUIStyleState {background = placeholderMainTex, textColor = placeholderMainFontColor};
            placeholderStyle.active = new GUIStyleState {background = placeholderMainTex, textColor = placeholderMainFontColor};
            placeholderStyle.focused = new GUIStyleState {background = placeholderMainTex, textColor = placeholderMainFontColor};
            placeholderStyle.padding = Padding.OnPlatform(skinData.ButtonPaddingKeyboard, skinData.ButtonPaddingTouch);
            placeholderStyle.alignment = TextAnchor.MiddleLeft;
            placeholderStyle.fontSize = (int)(skinData.FontSize * Scale);
            
            GUILayout.TextField(PlaceholderText, placeholderStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(0));
            return string.Empty;
        }

        public static int EntryHeight(ISkinData skinData)
        {
            return (int) (skinData.EntryHeight * Scale);
        }
    }
}