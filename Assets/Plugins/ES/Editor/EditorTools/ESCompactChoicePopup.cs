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
    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Popup,
        "短生命周期选择弹窗")]
    [ESWindowPresentationShortTitle("选择")]
    public sealed class ESCompactChoicePopup : EditorWindow
    {
        private const string StylePath = "Assets/Plugins/ES/Editor/EditorTools/ESCompactChoicePopup.uss";
        private const int RecommendedMaximumOptions = 12;
        private const int MaximumSupportedOptions = 256;
        private const float MinimumPopupWidth = 280f;
        private const float MaximumPopupWidth = 1000f;
        private const float MinimumPopupHeight = 190f;
        private const float MaximumPopupHeight = 700f;
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
        private EditorWindow hostWindow;
        private VisualElement hostRoot;
        private IVisualElementScheduledItem focusSchedule;
        private bool configured;
        private bool selectionInProgress;

        public static bool Open(VisualElement anchor, EditorWindow hostWindow, string title,
            IReadOnlyList<Option> choices, string hint = null, Vector2? windowSize = null)
        {
            if (openingPopup)
            {
                Debug.LogWarning("[ESCompactChoicePopup] 弹窗正在打开，已拒绝重入 Open。");
                return false;
            }
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
            if (choices.Count > MaximumSupportedOptions)
            {
                Debug.LogWarning("[ESCompactChoicePopup] 选项数量超过安全上限 "
                    + MaximumSupportedOptions + "，请改用 ESSearchDropdown。");
                return false;
            }
            if (choices.Count > RecommendedMaximumOptions)
                Debug.LogWarning("[ESCompactChoicePopup] 当前有 " + choices.Count
                    + " 个选项；超过 12 项时建议使用 ESSearchDropdown。");

            Vector2 size = windowSize ?? new Vector2(400f,
                Mathf.Clamp(62f + choices.Count * 43f, MinimumPopupHeight, 460f));
            size = NormalizePopupSize(size, choices.Count);
            // Popup creation and CreateGUI are separated by Unity's window lifecycle.
            // Snapshot the bounded choices so a mutable caller list cannot change the
            // button/index contract while the popup is being created or displayed.
            Option[] choiceSnapshot = new Option[choices.Count];
            for (int index = 0; index < choices.Count; index++)
                choiceSnapshot[index] = choices[index];
            openingPopup = true;
            if (activePopup != null)
            {
                try
                {
                    activePopup.Close();
                }
                catch (Exception closeException)
                {
                    // Do not open a second instance while the previous native
                    // popup could not be closed; preserve the single-instance contract.
                    Debug.LogException(new InvalidOperationException(
                        "[ESCompactChoicePopup] 现有弹窗关闭失败，已拒绝创建第二个实例。",
                        closeException));
                    openingPopup = false;
                    return false;
                }
                if (activePopup != null)
                {
                    openingPopup = false;
                    return false;
                }
            }
            ESCompactChoicePopup popup = null;
            try
            {
                popup = CreateInstance<ESCompactChoicePopup>();
                activePopup = popup;
                popup.hideFlags = HideFlags.DontSave;
                popup.popupTitle = string.IsNullOrWhiteSpace(title) ? "选择" : title.Trim();
                popup.popupHint = string.IsNullOrWhiteSpace(hint) ? "少量固定选项" : hint.Trim();
                popup.options = choiceSnapshot;
                popup.hostWindow = hostWindow;
                popup.hostRoot = hostWindow != null ? hostWindow.rootVisualElement : null;
                popup.configured = true;
                popup.titleContent = new GUIContent(popup.popupTitle);
                popup.minSize = size;
                popup.maxSize = size;
                popup.hostInteractionHold = ESWindowFoundation.HoldInteraction(
                    hostWindow,
                    "ESCompactChoicePopup");
                popup.hostRoot?.RegisterCallback<DetachFromPanelEvent>(popup.OnHostDetached);
                popup.ShowAsDropDown(anchorRect, size);
                popup.Focus();
                EditorApplication.delayCall -= popup.CloseIfContextWasLost;
                EditorApplication.delayCall += popup.CloseIfContextWasLost;
                return true;
            }
            catch
            {
                if (popup != null)
                {
                    popup.ReleaseHostInteractionHold();
                    try { popup.Close(); }
                    catch (Exception closeException)
                    {
                        Debug.LogException(closeException);
                    }
                    if (ReferenceEquals(activePopup, popup))
                        activePopup = null;
                }
                throw;
            }
            finally
            {
                openingPopup = false;
            }
        }

        private static Vector2 NormalizePopupSize(Vector2 requested, int optionCount)
        {
            float fallbackHeight = Mathf.Clamp(
                62f + Mathf.Max(0, optionCount) * 43f,
                MinimumPopupHeight,
                460f);
            float width = IsFinite(requested.x) && requested.x > 0f ? requested.x : 400f;
            float height = IsFinite(requested.y) && requested.y > 0f ? requested.y : fallbackHeight;
            return new Vector2(
                Mathf.Clamp(width, MinimumPopupWidth, MaximumPopupWidth),
                Mathf.Clamp(height, MinimumPopupHeight, MaximumPopupHeight));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
            if (!configured)
            {
                Close();
                return;
            }
            ESWindowFoundation.BindTransient(this);
            focusSchedule?.Pause();
            focusSchedule = null;
            // Unity may rebuild an EditorWindow visual tree without recreating the
            // managed window. Make CreateGUI idempotent so options and callbacks
            // cannot accumulate across a panel rebuild.
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.Clear();
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
            focusSchedule = rootVisualElement.schedule.Execute(FocusCurrent);
            focusSchedule.ExecuteLater(25);
        }

        private void OnDisable()
        {
            try { EditorApplication.delayCall -= CloseIfContextWasLost; }
            catch (Exception exception) { Debug.LogException(exception); }
            try { rootVisualElement?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown); }
            catch (Exception exception) { Debug.LogException(exception); }
            try { focusSchedule?.Pause(); }
            catch (Exception exception) { Debug.LogException(exception); }
            focusSchedule = null;
            try { hostRoot?.UnregisterCallback<DetachFromPanelEvent>(OnHostDetached); }
            catch (Exception exception) { Debug.LogException(exception); }
            hostRoot = null;
            ReleaseHostInteractionHold();
            hostWindow = null;
            try { ESWindowFoundation.Suspend(this); }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                configured = false;
                selectionInProgress = false;
                options = Array.Empty<Option>();
                optionButtons.Clear();
                if (ReferenceEquals(activePopup, this))
                    activePopup = null;
            }
        }

        private void OnDestroy()
        {
            try
            {
                ESWindowFoundation.Close(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCompactChoicePopup] 销毁关闭协议失败，已阻止异常穿出编辑器回调。", exception));
            }
        }

        private void CloseIfContextWasLost()
        {
            EditorApplication.delayCall -= CloseIfContextWasLost;
            bool hostContextLost = !IsHostContextValid();
            if (this != null && (!configured || hostContextLost))
                Close();
        }

        private bool IsHostContextValid()
        {
            if (!configured || hostWindow == null || hostRoot == null
                || hostWindow.rootVisualElement == null || rootVisualElement == null)
                return false;
            try
            {
                return hostWindow.rootVisualElement.panel != null
                    && rootVisualElement.panel != null
                    && ReferenceEquals(rootVisualElement.panel, hostWindow.rootVisualElement.panel);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCompactChoicePopup] 宿主上下文校验失败，已按失效处理。", exception));
                return false;
            }
        }

        private void OnHostDetached(DetachFromPanelEvent evt)
        {
            if (this != null)
                Close();
        }

        private void ReleaseHostInteractionHold()
        {
            IDisposable hold = hostInteractionHold;
            hostInteractionHold = null;
            if (hold == null)
                return;
            try
            {
                hold.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCompactChoicePopup] 宿主交互保持释放失败。", exception));
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!IsHostContextValid())
            {
                if (this != null) Close();
                evt.StopImmediatePropagation();
                return;
            }
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
            if (!configured || rootVisualElement == null || rootVisualElement.panel == null)
                return;
            if (keyboardIndex >= 0 && keyboardIndex < optionButtons.Count)
                optionButtons[keyboardIndex]?.Focus();
        }

        private void Select(int index)
        {
            if (selectionInProgress || index < 0 || index >= options.Count)
                return;
            if (!IsHostContextValid())
            {
                if (this != null) Close();
                return;
            }
            selectionInProgress = true;
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
            Rect hostBounds = hostWindow.position;
            if (hostBounds.width > 0f && hostBounds.height > 0f)
            {
                screenRect.x = Mathf.Clamp(
                    screenRect.x,
                    hostBounds.x,
                    Mathf.Max(hostBounds.x, hostBounds.xMax - screenRect.width));
                screenRect.y = Mathf.Clamp(
                    screenRect.y,
                    hostBounds.y,
                    Mathf.Max(hostBounds.y, hostBounds.yMax - screenRect.height));
            }
            return true;
        }

    }
}
