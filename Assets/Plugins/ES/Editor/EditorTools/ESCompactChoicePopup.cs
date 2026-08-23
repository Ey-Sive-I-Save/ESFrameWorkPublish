using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    /// <summary>
    /// ES 编辑器少量固定选项选择器。适合 2～12 个枚举、模式或职责模板；
    /// 大型、动态或需要搜索的数据源应继续使用 ESSearchDropdown。
    /// </summary>
    [ESWindowSleepContract(ESWindowSleepMode.Transient, "短生命周期选择弹窗")]
    [ESWindowPresentationShortTitle("选择")]
    public sealed class ESCompactChoicePopup : EditorWindow
    {
        private const string StylePath = "Assets/Plugins/ES/Editor/EditorTools/ESCompactChoicePopup.uss";
        private const int RecommendedMaximumOptions = 12;
        private static ESCompactChoicePopup activePopup;
        private static bool openingPopup;

        public readonly struct Option
        {
            public readonly string Label;
            public readonly string Subtitle;
            public readonly string Badge;
            public readonly string Tooltip;
            public readonly bool Selected;
            public readonly Action OnSelected;

            public Option(string label, Action onSelected, string subtitle = null,
                string badge = null, string tooltip = null, bool selected = false)
            {
                Label = string.IsNullOrWhiteSpace(label) ? "未命名" : label.Trim();
                Subtitle = subtitle?.Trim();
                Badge = badge?.Trim();
                Tooltip = tooltip?.Trim();
                Selected = selected;
                OnSelected = onSelected;
            }
        }

        private string popupTitle = "选择";
        private string popupHint = "少量固定选项";
        private IReadOnlyList<Option> options = Array.Empty<Option>();
        private readonly List<Button> optionButtons = new List<Button>();
        private int keyboardIndex;
        private IDisposable hostInteractionHold;
        private bool configured;

        public static bool Open(VisualElement anchor, EditorWindow hostWindow, string title,
            IReadOnlyList<Option> choices, string hint = null, Vector2? windowSize = null)
        {
            if (!TryGetScreenAnchor(anchor, hostWindow, out Rect anchorRect))
            {
                Debug.LogWarning("[ESCompactChoicePopup] 无法打开：锚点尚未加入有效的 EditorWindow 面板。");
                return false;
            }
            if (choices == null || choices.Count == 0)
            {
                Debug.LogWarning("[ESCompactChoicePopup] 没有可显示的选项。");
                return false;
            }
            if (choices.Count > RecommendedMaximumOptions)
                Debug.LogWarning("[ESCompactChoicePopup] 当前有 " + choices.Count
                    + " 个选项；超过 12 项时建议使用 ESSearchDropdown。");

            Vector2 size = windowSize ?? new Vector2(400f,
                Mathf.Clamp(62f + choices.Count * 43f, 190f, 460f));
            if (activePopup != null)
                activePopup.Close();
            openingPopup = true;
            ESCompactChoicePopup popup = null;
            try
            {
                popup = CreateInstance<ESCompactChoicePopup>();
                activePopup = popup;
                popup.hideFlags = HideFlags.DontSave;
                popup.popupTitle = string.IsNullOrWhiteSpace(title) ? "选择" : title.Trim();
                popup.popupHint = string.IsNullOrWhiteSpace(hint) ? "少量固定选项" : hint.Trim();
                popup.options = choices;
                popup.configured = true;
                popup.titleContent = new GUIContent(popup.popupTitle);
                popup.minSize = size;
                popup.maxSize = size;
                popup.hostInteractionHold = ESWindowFoundation.HoldInteraction(
                    hostWindow,
                    "ESCompactChoicePopup");
                popup.ShowAsDropDown(anchorRect, size);
                popup.Focus();
                return true;
            }
            catch
            {
                popup.ReleaseHostInteractionHold();
                if (popup != null)
                    popup.Close();
                throw;
            }
            finally
            {
                openingPopup = false;
            }
        }

        private void OnEnable()
        {
            if (openingPopup)
                return;
            EditorApplication.delayCall -= CloseIfContextWasLost;
            EditorApplication.delayCall += CloseIfContextWasLost;
        }

        public void CreateGUI()
        {
            EditorInternal.ESEditorPresentation.BindWindow(this, allowSemiSleep: false);
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null && !rootVisualElement.styleSheets.Contains(style))
                rootVisualElement.styleSheets.Add(style);
            rootVisualElement.AddToClassList("es-compact-choice-root");
            rootVisualElement.AddToClassList(
                EditorInternal.ESEditorPresentation.IsProSkin
                    ? "es-compact-choice-dark"
                    : "es-compact-choice-light");
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            VisualElement header = new VisualElement();
            header.AddToClassList("es-compact-choice-header");
            Label title = new Label(popupTitle);
            title.AddToClassList("es-compact-choice-title");
            header.Add(title);
            Label hint = new Label(popupHint);
            hint.AddToClassList("es-compact-choice-hint");
            header.Add(hint);
            rootVisualElement.Add(header);

            ScrollView list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("es-compact-choice-list");
            optionButtons.Clear();
            keyboardIndex = 0;
            for (int index = 0; index < options.Count; index++)
            {
                int capturedIndex = index;
                Option option = options[index];
                Button button = new Button(() => Select(capturedIndex));
                button.tooltip = option.Tooltip;
                button.AddToClassList("es-compact-choice-option");
                button.EnableInClassList("selected", option.Selected);

                VisualElement copy = new VisualElement();
                copy.AddToClassList("es-compact-choice-copy");
                Label label = new Label(option.Label);
                label.AddToClassList("es-compact-choice-label");
                copy.Add(label);
                if (!string.IsNullOrWhiteSpace(option.Subtitle))
                {
                    Label subtitle = new Label(option.Subtitle);
                    subtitle.AddToClassList("es-compact-choice-subtitle");
                    copy.Add(subtitle);
                }
                button.Add(copy);
                if (!string.IsNullOrWhiteSpace(option.Badge))
                {
                    Label badge = new Label(option.Badge);
                    badge.AddToClassList("es-compact-choice-badge");
                    button.Add(badge);
                }
                list.Add(button);
                optionButtons.Add(button);
                if (option.Selected)
                    keyboardIndex = index;
            }
            rootVisualElement.Add(list);
            rootVisualElement.schedule.Execute(FocusCurrent).ExecuteLater(25);
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= CloseIfContextWasLost;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            ReleaseHostInteractionHold();
            EditorInternal.ESEditorPresentation.UnbindWindow(this, true);
            configured = false;
            options = Array.Empty<Option>();
            optionButtons.Clear();
            if (ReferenceEquals(activePopup, this))
                activePopup = null;
        }

        private void CloseIfContextWasLost()
        {
            EditorApplication.delayCall -= CloseIfContextWasLost;
            if (this != null && !configured)
                Close();
        }

        private void ReleaseHostInteractionHold()
        {
            hostInteractionHold?.Dispose();
            hostInteractionHold = null;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopImmediatePropagation();
                return;
            }
            if (optionButtons.Count == 0)
                return;
            if (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow)
            {
                int direction = evt.keyCode == KeyCode.UpArrow ? -1 : 1;
                keyboardIndex = (keyboardIndex + direction + optionButtons.Count) % optionButtons.Count;
                FocusCurrent();
                evt.StopImmediatePropagation();
                return;
            }
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                Select(keyboardIndex);
                evt.StopImmediatePropagation();
            }
        }

        private void FocusCurrent()
        {
            if (keyboardIndex >= 0 && keyboardIndex < optionButtons.Count)
                optionButtons[keyboardIndex]?.Focus();
        }

        private void Select(int index)
        {
            if (index < 0 || index >= options.Count)
                return;
            Option option = options[index];
            try { option.OnSelected?.Invoke(); }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCompactChoicePopup] 选项执行失败：" + option.Label, exception));
            }
            finally { Close(); }
        }

        private static bool TryGetScreenAnchor(VisualElement anchor, EditorWindow hostWindow,
            out Rect screenRect)
        {
            screenRect = default;
            if (anchor == null || anchor.panel == null || hostWindow == null
                || hostWindow.rootVisualElement == null
                || !ReferenceEquals(anchor.panel, hostWindow.rootVisualElement.panel))
                return false;
            Rect world = anchor.worldBound;
            Rect rootWorld = hostWindow.rootVisualElement.worldBound;
            Vector2 local = world.position - rootWorld.position;
            Vector2 screen = hostWindow.position.position + local;
            if (!IsFinite(screen.x) || !IsFinite(screen.y)
                || !IsFinite(world.width) || !IsFinite(world.height))
                return false;
            screenRect = new Rect(screen, new Vector2(Mathf.Max(1f, world.width),
                Mathf.Max(1f, world.height)));
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
