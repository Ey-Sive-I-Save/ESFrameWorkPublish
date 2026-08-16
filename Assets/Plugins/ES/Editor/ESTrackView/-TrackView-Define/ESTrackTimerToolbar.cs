using Sirenix.Utilities.Editor;
using System.Collections.Generic;
using ES.EditorInternal;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    public class ESTrackTimerToolbar : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESTrackTimerToolbar, UxmlTraits> { }

        public Button PreviewButton = new Button();
        public Button ReStartButton = new Button();
        public Button LastBlockButton = new Button();
        public Button NextBlockButton = new Button();
        public Button PlayButton = new Button();
        public TextField TimeLabel = new TextField();
        public Button SelectOtherTimeLine = new Button();
        public Button EditSkillDataButton = new Button();
        public Button BindAndPlaySkillButton = new Button();
        public Button SelectEntityButton = new Button();
        public Button OpenInspectorButton = new Button();
        public Button MoreButton = new Button();

        public Label Name = new Label();
        public Label SaveStatusLabel = new Label();
        public VisualElement EntityStatusGroup = new VisualElement();
        public Label EntityLabel = new Label();
        public Label UserEntityLabel = new Label();
        public readonly VisualElement SystemActionHost = new VisualElement
        {
            name = "ESTrackViewSystemActions",
            tooltip = "系统：窗口生命周期与休眠控制"
        };


        private readonly VisualElement m_PlaybackGroup = new VisualElement();
        private readonly VisualElement m_ContextGroup = new VisualElement();
        private readonly VisualElement m_RightGroup = new VisualElement();
        private readonly VisualElement m_MainRow = new VisualElement();
        private readonly VisualElement m_CompactContextRow = new VisualElement();
        // Unity 在高 DPI 下会让相同窗口外框承载更少的有效布局空间。
        // 低频动作必须更早让位，保证“属性/更多”等恢复与主操作始终可见。
        private const float ExpandedToolbarWidth = 900f;
        private const float ContextToolbarWidth = 700f;
        private const float NavigationToolbarWidth = 760f;
        private const float PreviewToolbarWidth = 820f;
        private const float CompactActionToolbarWidth = 760f;
        private const float UltraCompactToolbarWidth = 360f;

        public ESTrackTimerToolbar()
        {
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Stretch;
            style.flexShrink = 0;
            style.color = ESTrackViewTheme.Text;
            style.backgroundColor = ESTrackViewTheme.ToolbarBackground;
            style.borderBottomWidth = 1;
            style.borderBottomColor = ESTrackViewTheme.Divider;
            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.paddingTop = 2;
            // 工具栏自身不裁剪右侧高频动作；具体低频组由 ApplyResponsiveLayout 显隐，
            // 避免窄 Dock 或高 DPI 下“弹出编辑器/更多”被静默裁掉。
            style.overflow = Overflow.Visible;

            ConfigureGroup(m_PlaybackGroup, 0, 0);
            ConfigureGroup(m_ContextGroup, 1, 1);
            ConfigureGroup(m_RightGroup, 0, 0);
            ConfigureGroup(SystemActionHost, 0, 0);
            SystemActionHost.style.marginLeft = 4;
            m_RightGroup.style.position = Position.Relative;
            m_RightGroup.style.right = StyleKeyword.Auto;
            m_RightGroup.style.top = StyleKeyword.Auto;
            m_RightGroup.style.bottom = StyleKeyword.Auto;
            m_RightGroup.style.height = StyleKeyword.Auto;
            m_RightGroup.style.minHeight = StyleKeyword.Auto;
            ConfigureGroup(m_MainRow, 0, 0);
            m_MainRow.style.position = Position.Relative;
            m_MainRow.style.minHeight = 28;
            m_MainRow.style.flexShrink = 0;
            ConfigureGroup(m_CompactContextRow, 0, 0);
            m_CompactContextRow.style.minHeight = 26;
            m_CompactContextRow.style.flexShrink = 0;
            m_CompactContextRow.style.display = DisplayStyle.None;
            m_CompactContextRow.style.borderTopWidth = 1;
            m_CompactContextRow.style.borderTopColor = ESTrackViewTheme.Divider;
            m_CompactContextRow.style.paddingLeft = 5;
            m_CompactContextRow.style.paddingRight = 5;
            m_ContextGroup.style.marginLeft = 4;
            m_ContextGroup.style.marginRight = 4;
            m_ContextGroup.style.minWidth = 0;

            Add(m_MainRow);
            Add(m_CompactContextRow);
            m_MainRow.Add(m_PlaybackGroup);
            m_MainRow.Add(m_ContextGroup);
            m_MainRow.Add(m_RightGroup);

            CreatePlaybackControls();
            CreateContextControls();
            CreateMoreControls();
            m_RightGroup.Add(SystemActionHost);
            BindEvents();
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                // UXML 首次挂载时 resolvedStyle.width 可能仍为 NaN/0。
                // 延后一帧重新计算，保证 ReloadDomain 和布局恢复后不会停留在构造期默认布局。
                schedule.Execute(ApplyResponsiveLayoutFromResolvedWidth).StartingIn(16);
            });
            schedule.Execute(ApplyResponsiveLayoutFromResolvedWidth).StartingIn(0);
        }

        private static void ConfigureGroup(VisualElement group, float flexGrow, float flexShrink)
        {
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.flexGrow = flexGrow;
            group.style.flexShrink = flexShrink;
            group.style.minWidth = 0;
            group.style.overflow = Overflow.Visible;
        }

        private void CreatePlaybackControls()
        {
            AddToolbarButton(m_PlaybackGroup, PreviewButton, null, 30, 26, "封存当前使用者并开始预览");
            AddToolbarButton(m_PlaybackGroup, ReStartButton, EditorIcons.Refresh.Raw, 26, 26, "停止预览并回到时间 0");
            AddToolbarButton(m_PlaybackGroup, LastBlockButton, EditorIcons.ArrowLeft.Raw, 26, 26, "跳转到上一个片段起点");
            AddToolbarButton(m_PlaybackGroup, PlayButton, EditorIcons.Play.Raw, 28, 26, "播放或暂停当前时间轴");
            AddToolbarButton(m_PlaybackGroup, NextBlockButton, EditorIcons.ArrowRight.Raw, 26, 26, "跳转到下一个片段起点");

            // 构造期先采用安全紧凑布局。待获取真实宽度后再按需展开，避免窗口首帧、
            // ReloadDomain 或高 DPI 下低频按钮把右侧“属性/更多”挤出可视区域。
            PreviewButton.style.display = DisplayStyle.None;
            LastBlockButton.style.display = DisplayStyle.None;
            NextBlockButton.style.display = DisplayStyle.None;

            PreviewButton.text = "预";
            PreviewButton.style.fontSize = 12;
            ESTrackViewTheme.ApplyAccentButton(PreviewButton);

            PlayButton.style.backgroundColor = ESTrackViewTheme.PlayBackground;
            PlayButton.style.borderLeftColor = ESTrackViewTheme.Accent;
            PlayButton.style.borderTopColor = ESTrackViewTheme.Accent;

            TimeLabel.label = "";
            TimeLabel.value = "0:00.00";
            TimeLabel.isReadOnly = true;
            TimeLabel.tooltip = "当前预览时间";
            TimeLabel.style.height = 26;
            TimeLabel.style.width = 62;
            TimeLabel.style.fontSize = 13;
            TimeLabel.style.color = ESTrackViewTheme.Text;
            TimeLabel.style.marginLeft = 4;

            var input = TimeLabel.Q<VisualElement>("unity-text-input");
            if (input != null)
            {
                input.AddToClassList("normalBlock");
                input.style.color = ESTrackViewTheme.Text;
                input.style.paddingTop = 1;
                input.style.paddingLeft = 3;
                input.style.paddingBottom = 1;
            }

            var textElement = TimeLabel.Q<TextElement>();
            if (textElement != null)
            {
                textElement.style.width = Length.Percent(100);
                textElement.style.height = Length.Percent(100);
                textElement.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            m_PlaybackGroup.Add(TimeLabel);
        }

        private void CreateContextControls()
        {
            Name.text = "轴：<未选择>";
            Name.tooltip = "当前时间轴名称";
            Name.style.height = 26;
            Name.style.minWidth = 80;
            Name.style.flexGrow = 1;
            Name.style.flexShrink = 1;
            Name.style.color = ESTrackViewTheme.Text;
            Name.style.unityTextAlign = TextAnchor.MiddleLeft;
            Name.style.overflow = Overflow.Hidden;
            Name.AddToClassList("normalBlock");
            m_ContextGroup.Add(Name);

            SaveStatusLabel.text = "未选择";
            SaveStatusLabel.tooltip = "当前时间轴保存状态";
            SaveStatusLabel.style.height = 22;
            SaveStatusLabel.style.minWidth = 48;
            SaveStatusLabel.style.maxWidth = 60;
            SaveStatusLabel.style.marginLeft = 4;
            SaveStatusLabel.style.paddingLeft = 4;
            SaveStatusLabel.style.paddingRight = 4;
            SaveStatusLabel.style.fontSize = 10;
            SaveStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            SaveStatusLabel.style.color = ESTrackViewTheme.MutedText;
            SaveStatusLabel.AddToClassList("normalBlock");
            m_ContextGroup.Add(SaveStatusLabel);

            EntityStatusGroup.style.flexDirection = FlexDirection.Row;
            EntityStatusGroup.style.alignItems = Align.Center;
            EntityStatusGroup.style.height = 26;
            EntityStatusGroup.style.minWidth = 104;
            EntityStatusGroup.style.maxWidth = 190;
            EntityStatusGroup.style.flexShrink = 1;
            EntityStatusGroup.style.marginLeft = 4;
            EntityStatusGroup.style.paddingLeft = 6;
            EntityStatusGroup.style.paddingRight = 6;
            EntityStatusGroup.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            EntityStatusGroup.style.borderLeftColor = ESTrackViewTheme.Accent;
            EntityStatusGroup.style.borderLeftWidth = 2;
            EntityStatusGroup.tooltip = "点击选择预览使用者；开始预览时会封存该使用者。";
            EntityStatusGroup.AddToClassList("normalBlock");

            EntityLabel.text = "使用者：<无>";
            EntityLabel.style.height = 18;
            EntityLabel.style.flexGrow = 1;
            EntityLabel.style.flexShrink = 1;
            EntityLabel.style.color = ESTrackViewTheme.Text;
            EntityLabel.style.fontSize = 11;
            EntityLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            EntityLabel.style.overflow = Overflow.Hidden;
            EntityStatusGroup.Add(EntityLabel);

            UserEntityLabel.style.display = DisplayStyle.None;
            EntityStatusGroup.Add(UserEntityLabel);
            m_ContextGroup.Add(EntityStatusGroup);
        }

        private void CreateMoreControls()
        {
            ConfigureActionButton(SelectOtherTimeLine, "切换时间轴", "切换当前轨道窗口正在编辑的时间轴");
            ConfigureActionButton(EditSkillDataButton, "打开技能配置", "打开当前时间轴所属的技能配置数据");
            ConfigureActionButton(BindAndPlaySkillButton, "绑定并释放技能", "把当前技能配置绑定到预览使用者，并立即执行一次释放流程");
            ConfigureActionButton(SelectEntityButton, "选择预览使用者", "从当前场景中选择用于预览和释放测试的 Entity");

            // 这是高频动作，必须在顶栏常驻，不能要求用户先打开 Inspector 或“更多”菜单。
            AddToolbarButton(m_RightGroup, SelectOtherTimeLine, null, 86, 26, "切换当前编辑时间轴");
            SelectOtherTimeLine.style.minWidth = 86;
            ESTrackViewTheme.ApplyAccentButton(SelectOtherTimeLine);

            ConfigureActionButton(OpenInspectorButton, "弹出编辑器", "在独立窗口中编辑当前选中的轨道或片段；也可 Shift + 右键直接弹出");
            AddToolbarButton(m_RightGroup, OpenInspectorButton, null, 76, 26, OpenInspectorButton.tooltip);
            OpenInspectorButton.style.minWidth = 76;
            ESTrackViewTheme.ApplyAccentButton(OpenInspectorButton);

            AddToolbarButton(m_RightGroup, MoreButton, null, 42, 26, "打开时间轴低频操作菜单");
            MoreButton.text = "更多";
            MoreButton.style.fontSize = 12;
            MoreButton.style.backgroundColor = ESTrackViewTheme.ButtonBackground;
        }

        private static void ConfigureActionButton(Button button, string text, string tooltip)
        {
            button.text = text;
            button.tooltip = tooltip;
            button.style.height = 26;
            button.style.minWidth = 64;
            ESTrackViewTheme.ApplyStandardButton(button);
        }

        private void BindEvents()
        {
            EntityStatusGroup.RegisterCallback<ClickEvent>(evt => ShowEntityMenu());
            PreviewButton.clicked += () =>
            {
                ESTrackViewWindow.window?.TryStartPreview();
            };
            PlayButton.clicked += OnPlayPauseToggle;
            ReStartButton.clicked += OnStopAndReset;
            LastBlockButton.clicked += JumpToPreviousClip;
            NextBlockButton.clicked += JumpToNextClip;
            SelectOtherTimeLine.clicked += ShowTimelineMenuFromButton;
            EditSkillDataButton.clicked += () => ESTrackSkillDataEditorActions.OpenCurrentSkillDataInfoEditor(ESTrackViewWindow.window);
            BindAndPlaySkillButton.clicked += () => ESTrackSkillDataEditorActions.BindCurrentSkillDataToEntityAndPlay(ESTrackViewWindow.window);
            SelectEntityButton.clicked += ShowEntityMenu;
            OpenInspectorButton.clicked += () => ESTrackViewWindow.window?.OpenCurrentInspectorInSeparateWindow();
            MoreButton.clicked += ShowMoreMenu;
        }

        internal void UpdateInspectorAction(bool enabled)
        {
            if (OpenInspectorButton == null)
                return;

            OpenInspectorButton.SetEnabled(enabled);
            OpenInspectorButton.tooltip = enabled
                ? "在独立窗口中编辑当前选中的轨道或片段；也可 Shift + 右键直接弹出。"
                : "请先选择轨道或片段，再弹出独立编辑器。";
        }

        internal void RefreshTheme()
        {
            style.color = ESTrackViewTheme.Text;
            style.backgroundColor = ESTrackViewTheme.ToolbarBackground;
            style.borderBottomColor = ESTrackViewTheme.Divider;
            m_CompactContextRow.style.borderTopColor = ESTrackViewTheme.Divider;

            ESTrackViewTheme.ApplyAccentButton(PreviewButton);
            ESTrackViewTheme.ApplyStandardButton(ReStartButton);
            ESTrackViewTheme.ApplyStandardButton(LastBlockButton);
            ESTrackViewTheme.ApplyStandardButton(NextBlockButton);
            ESTrackViewTheme.ApplyStandardButton(PlayButton);
            PlayButton.style.backgroundColor = ESTrackViewTheme.PlayBackground;
            PlayButton.style.borderLeftColor = ESTrackViewTheme.Accent;
            PlayButton.style.borderTopColor = ESTrackViewTheme.Accent;

            ESTrackViewTheme.ApplyAccentButton(SelectOtherTimeLine);
            ESTrackViewTheme.ApplyAccentButton(OpenInspectorButton);
            ESTrackViewTheme.ApplyStandardButton(MoreButton);

            TimeLabel.style.color = ESTrackViewTheme.Text;
            TimeLabel.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            VisualElement input = TimeLabel.Q<VisualElement>("unity-text-input");
            if (input != null)
            {
                input.style.color = ESTrackViewTheme.Text;
                input.style.backgroundColor = ESTrackViewTheme.CanvasBackground;
            }

            Name.style.color = ESTrackViewTheme.Text;
            Name.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            SaveStatusLabel.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            EntityStatusGroup.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            EntityStatusGroup.style.borderLeftColor = ESTrackViewTheme.Accent;
            EntityLabel.style.color = ESTrackViewTheme.Text;
            MarkDirtyRepaint();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Abs(evt.newRect.width - evt.oldRect.width) > 0.1f)
                ApplyResponsiveLayout(evt.newRect.width);
        }

        private void ApplyResponsiveLayoutFromResolvedWidth()
        {
            float width = resolvedStyle.width;
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f)
                return;

            ApplyResponsiveLayout(width);
        }

        private void ApplyResponsiveLayout(float width)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f)
                return;

            // 时间轴名称是当前编辑上下文，窄窗口也必须保留一个可识别的压缩入口。
            bool showContext = width >= 250f;
            bool showSaveStatus = width >= ContextToolbarWidth;
            bool showEntityStatus = width >= ExpandedToolbarWidth;
            bool showNavigation = width >= NavigationToolbarWidth;
            bool showPreview = width >= PreviewToolbarWidth;
            bool compactPreview = width < ExpandedToolbarWidth;
            bool compactActions = width < CompactActionToolbarWidth;
            bool ultraCompact = width < UltraCompactToolbarWidth;

            if (ultraCompact)
            {
                if (m_ContextGroup.parent != m_CompactContextRow)
                {
                    m_ContextGroup.RemoveFromHierarchy();
                    m_CompactContextRow.Add(m_ContextGroup);
                }

                m_ContextGroup.style.display = showContext ? DisplayStyle.Flex : DisplayStyle.None;
                m_ContextGroup.style.flexGrow = 1;
                m_ContextGroup.style.flexShrink = 1;
                m_ContextGroup.style.maxWidth = StyleKeyword.None;
            }
            else
            {
                if (m_ContextGroup.parent != m_MainRow)
                {
                    m_ContextGroup.RemoveFromHierarchy();
                    m_MainRow.Insert(1, m_ContextGroup);
                }

                m_ContextGroup.style.display = showContext ? DisplayStyle.Flex : DisplayStyle.None;
                m_ContextGroup.style.flexGrow = 1;
                m_ContextGroup.style.flexShrink = 1;
            }

            m_CompactContextRow.style.display = ultraCompact && showContext ? DisplayStyle.Flex : DisplayStyle.None;
            Name.style.minWidth = ultraCompact ? 44f : width < 520f ? 58f : 80f;
            Name.style.maxWidth = ultraCompact ? StyleKeyword.None : compactActions ? 112f : StyleKeyword.None;
            m_ContextGroup.style.maxWidth = ultraCompact ? StyleKeyword.None : compactActions ? 118f : StyleKeyword.None;
            m_ContextGroup.style.marginLeft = ultraCompact ? 2f : 4f;
            m_ContextGroup.style.marginRight = ultraCompact ? 2f : 4f;
            SaveStatusLabel.style.display = showSaveStatus ? DisplayStyle.Flex : DisplayStyle.None;
            EntityStatusGroup.style.display = showEntityStatus ? DisplayStyle.Flex : DisplayStyle.None;
            LastBlockButton.style.display = showNavigation ? DisplayStyle.Flex : DisplayStyle.None;
            NextBlockButton.style.display = showNavigation ? DisplayStyle.Flex : DisplayStyle.None;
            PreviewButton.style.display = showPreview ? DisplayStyle.Flex : DisplayStyle.None;
            ReStartButton.style.display = ultraCompact ? DisplayStyle.None : DisplayStyle.Flex;
            PreviewButton.style.width = compactPreview ? 28 : 46;
            PreviewButton.text = compactPreview ? "预" : "预览";

            TimeLabel.style.width = ultraCompact ? 56f : 62f;
            TimeLabel.style.marginLeft = ultraCompact ? 2f : 4f;

            OpenInspectorButton.text = compactActions ? "属性" : "弹出编辑器";
            OpenInspectorButton.style.width = ultraCompact ? 42f : compactActions ? 52f : 76f;
            OpenInspectorButton.style.minWidth = ultraCompact ? 42f : compactActions ? 52f : 76f;
            SelectOtherTimeLine.text = ultraCompact ? "轴" : compactActions ? "切换" : "切换时间轴";
            SelectOtherTimeLine.style.width = ultraCompact ? 30f : compactActions ? 52f : 86f;
            SelectOtherTimeLine.style.minWidth = ultraCompact ? 30f : compactActions ? 52f : 86f;
            MoreButton.text = ultraCompact ? "⋯" : "更多";
            MoreButton.style.width = ultraCompact ? 30f : 42f;
            MoreButton.style.minWidth = ultraCompact ? 30f : 42f;

            if (showEntityStatus)
                EntityStatusGroup.style.maxWidth = width >= 820f ? 190 : 132;

        }

        private void AddToolbarButton(VisualElement parent, Button button, Texture2D icon, float width, float height, string tooltip = null)
        {
            if (icon != null)
                button.style.backgroundImage = icon;
            if (!string.IsNullOrEmpty(tooltip))
                button.tooltip = tooltip;

            button.style.width = width;
            button.style.height = height;
            button.style.marginLeft = 1;
            button.style.marginRight = 1;
            ESEditorPresentation.ApplyCornerRadius(
                button, ESEditorPresentation.ESCornerRadiusToken.Control);
            ESTrackViewTheme.ApplyStandardButton(button);
            button.AddToClassList("track-toolbar-button");
            parent.Add(button);
        }

        private void ShowMoreMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("预览/停止并回到时间 0"), false, OnStopAndReset);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("时间轴/切换当前编辑时间轴"), false, ShowTimelineMenuFromMoreMenu);
            if (ESTrackViewWindow.TrackContainer != null && ESTrackViewWindow.Sequence != null)
                menu.AddItem(new GUIContent("时间轴/立即保存当前时间轴"), false, ESTrackViewWindowHelper.SaveContainerNow);
            else
                menu.AddDisabledItem(new GUIContent("时间轴/立即保存当前时间轴（未选择）"));
            if (ESTrackViewWindow.window != null && ESTrackViewWindow.TrackContainer != null && ESTrackViewWindow.Sequence != null)
                menu.AddItem(new GUIContent("时间轴/保存为新资产…"), false, ESTrackViewWindowHelper.SaveContainerAsNewAsset);
            else
                menu.AddDisabledItem(new GUIContent("时间轴/保存为新资产…（未选择）"));
            if (ESTrackViewWindow.TrackContainer != null && ESTrackViewWindow.Sequence != null)
                menu.AddItem(new GUIContent("时间轴/复制结构摘要"), false, ESTrackViewWindowHelper.CopyCurrentSequenceSummary);
            else
                menu.AddDisabledItem(new GUIContent("时间轴/复制结构摘要（未选择）"));
            if (ESTrackViewWindow.window != null && ESTrackViewWindow.window.CanOpenCurrentInspectorInSeparateWindow)
                menu.AddItem(new GUIContent("属性/弹出当前属性编辑器"), false, () =>
                {
                    ESTrackViewWindow.window?.OpenCurrentInspectorInSeparateWindow();
                });
            else
                menu.AddDisabledItem(new GUIContent("属性/弹出当前属性编辑器（请先选择轨道或片段）"));
            if (ESTrackViewWindow.window != null && ESTrackViewWindow.Sequence != null)
            {
                menu.AddItem(new GUIContent("轨道显示/折叠全部轨道"), false, () =>
                {
                    ESTrackViewWindow.window?.SetAllTracksCollapsed(true);
                });
                menu.AddItem(new GUIContent("轨道显示/展开全部轨道"), false, () =>
                {
                    ESTrackViewWindow.window?.SetAllTracksCollapsed(false);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("轨道显示/折叠全部轨道（未选择）"));
                menu.AddDisabledItem(new GUIContent("轨道显示/展开全部轨道（未选择）"));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("技能/新建技能…"), false, ESCreateSkillWindow.Open);
            menu.AddItem(new GUIContent("技能/打开当前技能配置"), false, () => ESTrackSkillDataEditorActions.OpenCurrentSkillDataInfoEditor(ESTrackViewWindow.window));
            menu.AddItem(new GUIContent("技能/绑定到预览使用者并释放"), false, () => ESTrackSkillDataEditorActions.BindCurrentSkillDataToEntityAndPlay(ESTrackViewWindow.window));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("预览目标/从场景选择预览使用者"), false, ShowEntityMenu);
            menu.AddItem(
                new GUIContent("窗口行为/选择时间轴资产时自动打开"),
                ESTrackViewWindowHelper.AutoOpenFromSelection,
                () => ESTrackViewWindowHelper.AutoOpenFromSelection = !ESTrackViewWindowHelper.AutoOpenFromSelection);
            menu.AddItem(
                new GUIContent("窗口行为/跟随场景 Selection 更新预览目标"),
                ESTrackViewWindowHelper.AutoFollowPreviewEntity,
                () => ESTrackViewWindowHelper.AutoFollowPreviewEntity = !ESTrackViewWindowHelper.AutoFollowPreviewEntity);
            menu.DropDown(MoreButton.worldBound);
        }

        private void ShowTimelineMenuFromButton()
        {
            IEditorTrackSupport_GetSequence.ShowDynamicMenu(SelectOtherTimeLine.worldBound, OnTimelineSelected);
        }

        internal void OpenTimelineSelectionMenu(VisualElement anchor = null)
        {
            VisualElement menuAnchor = anchor ?? SelectOtherTimeLine;
            IEditorTrackSupport_GetSequence.ShowDynamicMenu(menuAnchor.worldBound, OnTimelineSelected);
        }

        private void ShowTimelineMenuFromMoreMenu()
        {
            EditorApplication.delayCall += ShowTimelineMenuDelayed;
        }

        private void ShowTimelineMenuDelayed()
        {
            EditorApplication.delayCall -= ShowTimelineMenuDelayed;
            IEditorTrackSupport_GetSequence.ShowDynamicMenu(MoreButton.worldBound, OnTimelineSelected);
        }

        private void OnTimelineSelected(object userData)
        {
            if (userData is IEditorTrackSupport_GetSequence editorTrackSupport_GetSequence)
            {
                ESTrackViewWindowHelper.CancelPendingSelectionTrackRefresh();
                ESTrackViewWindow.TryUpdateTrackSequence(editorTrackSupport_GetSequence);
            }
        }

        private void ShowEntityMenu()
        {
            ESTrackViewWindow.window?.ShowEntitySelectMenu();
        }

        internal void UpdateEntity(Entity preselectEntity, Entity runningEntity)
        {
            string preselectName = preselectEntity != null ? preselectEntity.name : "<无>";
            string runningName = runningEntity != null ? runningEntity.name : "<无>";
            string compactName = runningEntity != null ? runningName : preselectName;
            EntityLabel.text = $"使用者：{compactName}";
            UserEntityLabel.text = $"候选目标：{preselectName}";
            EntityStatusGroup.tooltip = $"点击选择预览使用者\n候选目标：{preselectName}\n使用者：{runningName}";
        }

        internal void UpdateTime(float time)
        {
            int totalMinutes = Mathf.FloorToInt(time / 60f);
            float seconds = time % 60f;
            TimeLabel.SetValueWithoutNotify($"{totalMinutes}:{seconds:00.00}");
        }

        #region Buttons

        private void OnPlayPauseToggle()
        {
            var player = EditorTimelinePlayer.Instance;
            if (player.ActiveSequence == null)
                return;

            if (player.ActiveSequence.IsPlaying)
            {
                player.Pause();
                return;
            }

            ESTrackViewWindow.window?.TryStartPreview();
        }

        private void OnStopAndReset()
        {
            EditorTimelinePlayer.Instance.Stop();
        }

        private void JumpToPreviousClip()
        {
            var clips = GetAllClipsSorted();
            if (clips.Count == 0)
                return;

            float current = EditorTimelinePlayer.Instance.ActiveSequence?.CurrentTime ?? 0f;
            ITrackClip target = null;
            foreach (var clip in clips)
            {
                if (clip.StartTime < current - 0.001f)
                    target = clip;
                else
                    break;
            }

            if (target != null)
                EditorTimelinePlayer.Instance.SetTime(target.StartTime);
        }

        private void JumpToNextClip()
        {
            var clips = GetAllClipsSorted();
            if (clips.Count == 0)
                return;

            float current = EditorTimelinePlayer.Instance.ActiveSequence?.CurrentTime ?? 0f;
            foreach (var clip in clips)
            {
                if (clip.StartTime > current + 0.001f)
                {
                    EditorTimelinePlayer.Instance.SetTime(clip.StartTime);
                    return;
                }
            }

            EditorTimelinePlayer.Instance.SetTime(EditorTimelinePlayer.Instance.ActiveSequence?.Duration ?? 10f);
        }

        private List<ITrackClip> GetAllClipsSorted()
        {
            var list = new List<ITrackClip>();
            var sequence = ESTrackViewWindow.Sequence;
            if (sequence != null)
            {
                foreach (var track in sequence.Tracks)
                {
                    if (track.Clips != null)
                        list.AddRange(track.Clips);
                }
            }

            list.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            return list;
        }

        #endregion
    }

    public static class ESTrackSkillDataEditorActions
    {
        public static void OpenCurrentSkillDataInfoEditor(ESTrackViewWindow trackWindow)
        {
            if (!TryGetCurrentSkillDataInfo(out var skillData))
                return;

            if (trackWindow == null)
                trackWindow = ESTrackViewWindow.window;

            ESTrackSkillDataTemporaryInspectorWindow.CloseCurrentWindow();
            if (trackWindow != null)
                trackWindow.Last_EditorWindowForSkillDataInfo = null;

            var editorWindow = ESTrackSkillDataTemporaryInspectorWindow.OpenFor(
                skillData,
                "编辑技能 <" + skillData.name + ">",
                "技能配置",
                trackWindow);

            if (trackWindow != null)
                trackWindow.Last_EditorWindowForSkillDataInfo = editorWindow;
        }

        public static bool BindCurrentSkillDataToEntityAndPlay(ESTrackViewWindow trackWindow)
        {
            if (!TryGetCurrentSkillDataInfo(out var skillData))
                return false;

            if (trackWindow == null)
                trackWindow = ESTrackViewWindow.window;

            if (trackWindow == null)
            {
                Debug.LogWarning("[轨道编辑器] 当前轨道窗口为空。");
                return false;
            }

            trackWindow.UpdatePreselectEntityFromSelection(false);
            Entity entity = trackWindow.RunningEntity != null ? trackWindow.RunningEntity : trackWindow.PreselectEntity;
            if (entity == null)
            {
                Debug.LogWarning("[轨道编辑器] 没有绑定实体。请选中带 Entity 的对象，或从工具栏选择实体。");
                return false;
            }

            if (entity.basicDomain == null)
            {
                Debug.LogWarning($"[轨道编辑器] 实体 basicDomain 为空。实体={entity.name}", entity);
                return false;
            }

            var module = EnsureSimpleSkillTestModule(entity);
            if (module == null)
                return false;

            if (module.skills == null)
                module.skills = new List<SkillTrackProcessInfo>();

            if (!module.skills.Contains(skillData))
                module.skills.Add(skillData);

            EditorUtility.SetDirty(entity);

            MoveEditorCursorToSkillStart();
            bool success = module.ReleaseSkill(skillData);
            if (!success)
            {
                Debug.LogWarning($"[轨道编辑器] 技能释放失败。技能={skillData.name} | 实体={entity.name}", entity);
                return false;
            }

            Selection.activeObject = entity.gameObject;
            Debug.Log($"[轨道编辑器] 技能已绑定并释放。技能={skillData.name} | 实体={entity.name}", entity);
            return true;
        }

        private static bool TryGetCurrentSkillDataInfo(out SkillTrackProcessInfo skillData)
        {
            skillData = ESTrackViewWindow.TrackContainer as SkillTrackProcessInfo;
            if (skillData != null)
                return true;

            Debug.LogWarning("[轨道编辑器] 当前轨道容器不是技能配置。");
            return false;
        }

        private static EntityBasicSimpleSkillTestModule EnsureSimpleSkillTestModule(Entity entity)
        {
            var domain = entity != null ? entity.basicDomain : null;
            if (domain == null)
                return null;

            var module = domain.FindMyModule<EntityBasicSimpleSkillTestModule>();
            if (module != null)
                return module;

            Undo.RecordObject(entity, "添加技能测试模块");
            module = new EntityBasicSimpleSkillTestModule();
            domain.TryAddModuleRuntime(module);
            domain.MyModules.ApplyBuffers(true);

            module = domain.FindMyModule<EntityBasicSimpleSkillTestModule>();
            if (module == null)
                Debug.LogWarning($"[轨道编辑器] 添加技能测试模块失败。实体={entity.name}", entity);

            return module;
        }

        private static void MoveEditorCursorToSkillStart()
        {
            var player = EditorTimelinePlayer.Instance;
            if (player != null && player.ActiveSequence != null)
                player.SetTime(0f);
        }
    }
}
