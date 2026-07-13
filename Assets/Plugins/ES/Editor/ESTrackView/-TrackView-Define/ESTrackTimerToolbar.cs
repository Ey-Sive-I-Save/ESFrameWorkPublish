using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System.Collections.Generic;
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
        public Button MoreButton = new Button();

        public Label Name = new Label();
        public VisualElement EntityStatusGroup = new VisualElement();
        public Label EntityLabel = new Label();
        public Label UserEntityLabel = new Label();

        public Button Setting = new Button();

        private readonly VisualElement m_PlaybackGroup = new VisualElement();
        private readonly VisualElement m_ContextGroup = new VisualElement();
        private readonly VisualElement m_RightGroup = new VisualElement();

        public ESTrackTimerToolbar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.color = Color.gray;
            style.backgroundColor = new Color(0.058f, 0.064f, 0.074f, 1f);
            style.borderBottomWidth = 1;
            style.borderBottomColor = new Color(0.18f, 0.2f, 0.22f, 1f);
            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.paddingTop = 2;
            style.overflow = Overflow.Hidden;

            ConfigureGroup(m_PlaybackGroup, 0, 0);
            ConfigureGroup(m_ContextGroup, 1, 1);
            ConfigureGroup(m_RightGroup, 0, 0);
            m_ContextGroup.style.marginLeft = 4;
            m_ContextGroup.style.marginRight = 4;
            m_ContextGroup.style.minWidth = 0;

            Add(m_PlaybackGroup);
            Add(m_ContextGroup);
            Add(m_RightGroup);

            CreatePlaybackControls();
            CreateContextControls();
            CreateMoreControls();
            BindEvents();
        }

        private static void ConfigureGroup(VisualElement group, float flexGrow, float flexShrink)
        {
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.flexGrow = flexGrow;
            group.style.flexShrink = flexShrink;
            group.style.minWidth = 0;
            group.style.overflow = Overflow.Hidden;
        }

        private void CreatePlaybackControls()
        {
            AddToolbarButton(m_PlaybackGroup, PreviewButton, null, 30, 26, "封存当前使用者并开始预览");
            AddToolbarButton(m_PlaybackGroup, ReStartButton, EditorIcons.Refresh.Raw, 26, 26, "停止预览并回到时间 0");
            AddToolbarButton(m_PlaybackGroup, LastBlockButton, EditorIcons.ArrowLeft.Raw, 26, 26, "跳转到上一个片段起点");
            AddToolbarButton(m_PlaybackGroup, PlayButton, EditorIcons.Play.Raw, 28, 26, "播放或暂停当前时间轴");
            AddToolbarButton(m_PlaybackGroup, NextBlockButton, EditorIcons.ArrowRight.Raw, 26, 26, "跳转到下一个片段起点");

            PreviewButton.text = "预";
            PreviewButton.style.fontSize = 12;
            PreviewButton.style.color = new Color(0.92f, 0.96f, 1f, 1f);
            PreviewButton.style.backgroundColor = new Color(0.12f, 0.17f, 0.23f, 1f);
            PreviewButton.style.borderLeftColor = new Color(0.34f, 0.48f, 0.62f, 0.85f);
            PreviewButton.style.borderTopColor = new Color(0.34f, 0.48f, 0.62f, 0.85f);

            PlayButton.style.backgroundColor = new Color(0.15f, 0.2f, 0.17f, 1f);
            PlayButton.style.borderLeftColor = new Color(0.32f, 0.48f, 0.38f, 0.78f);
            PlayButton.style.borderTopColor = new Color(0.32f, 0.48f, 0.38f, 0.78f);

            TimeLabel.label = "";
            TimeLabel.value = "0:00.00";
            TimeLabel.isReadOnly = true;
            TimeLabel.tooltip = "当前预览时间";
            TimeLabel.style.height = 26;
            TimeLabel.style.width = 62;
            TimeLabel.style.fontSize = 13;
            TimeLabel.style.color = Color.white;
            TimeLabel.style.marginLeft = 4;

            var input = TimeLabel.Q<VisualElement>("unity-text-input");
            if (input != null)
            {
                input.AddToClassList("normalBlock");
                input.style.color = Color.white;
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
            Name.style.color = new Color(0.76f, 0.8f, 0.86f, 1f);
            Name.style.unityTextAlign = TextAnchor.MiddleLeft;
            Name.style.overflow = Overflow.Hidden;
            Name.AddToClassList("normalBlock");
            m_ContextGroup.Add(Name);

            EntityStatusGroup.style.flexDirection = FlexDirection.Row;
            EntityStatusGroup.style.alignItems = Align.Center;
            EntityStatusGroup.style.height = 26;
            EntityStatusGroup.style.minWidth = 104;
            EntityStatusGroup.style.maxWidth = 190;
            EntityStatusGroup.style.flexShrink = 1;
            EntityStatusGroup.style.marginLeft = 4;
            EntityStatusGroup.style.paddingLeft = 6;
            EntityStatusGroup.style.paddingRight = 6;
            EntityStatusGroup.style.backgroundColor = new Color(0.064f, 0.074f, 0.084f, 0.96f);
            EntityStatusGroup.style.borderLeftColor = new Color(0.28f, 0.38f, 0.48f, 0.95f);
            EntityStatusGroup.style.borderLeftWidth = 2;
            EntityStatusGroup.tooltip = "编辑器预览目标。开始预览时会封存使用者。";
            EntityStatusGroup.AddToClassList("normalBlock");

            EntityLabel.text = "使用者：<无>";
            EntityLabel.style.height = 18;
            EntityLabel.style.flexGrow = 1;
            EntityLabel.style.flexShrink = 1;
            EntityLabel.style.color = new Color(0.7f, 0.86f, 0.74f, 1f);
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

            AddToolbarButton(m_RightGroup, MoreButton, null, 42, 26, "打开时间轴低频操作菜单");
            MoreButton.text = "更多";
            MoreButton.style.fontSize = 12;
            MoreButton.style.backgroundColor = new Color(0.085f, 0.095f, 0.11f, 1f);

            AddToolbarButton(m_RightGroup, Setting, EditorIcons.SettingsCog.Raw, 26, 26, "打开时间轴工具设置菜单");
            Setting.style.backgroundColor = new Color(0.085f, 0.095f, 0.11f, 1f);
        }

        private static void ConfigureActionButton(Button button, string text, string tooltip)
        {
            button.text = text;
            button.tooltip = tooltip;
            button.style.height = 26;
            button.style.minWidth = 64;
            button.style.color = Color.white;
            button.style.backgroundColor = new Color(0.105f, 0.118f, 0.132f, 1f);
        }

        private void BindEvents()
        {
            PreviewButton.clicked += () =>
            {
                ESTrackViewWindow.window?.SealRunningEntityForPlay();
                EditorTimelinePlayer.Instance.Play();
            };
            PlayButton.clicked += OnPlayPauseToggle;
            ReStartButton.clicked += OnStopAndReset;
            LastBlockButton.clicked += JumpToPreviousClip;
            NextBlockButton.clicked += JumpToNextClip;
            SelectOtherTimeLine.clicked += ShowTimelineMenu;
            EditSkillDataButton.clicked += () => ESTrackSkillDataEditorActions.OpenCurrentSkillDataInfoEditor(ESTrackViewWindow.window);
            BindAndPlaySkillButton.clicked += () => ESTrackSkillDataEditorActions.BindCurrentSkillDataToEntityAndPlay(ESTrackViewWindow.window);
            SelectEntityButton.clicked += ShowEntityMenu;
            MoreButton.clicked += ShowMoreMenu;
            Setting.clicked += ShowMoreMenu;
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
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.backgroundColor = new Color(0.105f, 0.118f, 0.132f, 1f);
            button.style.borderLeftColor = new Color(0.25f, 0.29f, 0.34f, 0.75f);
            button.style.borderTopColor = new Color(0.25f, 0.29f, 0.34f, 0.75f);
            button.style.borderRightColor = new Color(0.045f, 0.05f, 0.06f, 0.9f);
            button.style.borderBottomColor = new Color(0.045f, 0.05f, 0.06f, 0.9f);
            parent.Add(button);
        }

        private void ShowMoreMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("【时间轴】/切换当前编辑时间轴"), false, ShowTimelineMenu);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("【技能】/打开当前技能配置"), false, () => ESTrackSkillDataEditorActions.OpenCurrentSkillDataInfoEditor(ESTrackViewWindow.window));
            menu.AddItem(new GUIContent("【技能】/绑定到预览使用者并释放"), false, () => ESTrackSkillDataEditorActions.BindCurrentSkillDataToEntityAndPlay(ESTrackViewWindow.window));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("【预览目标】/从场景选择预览使用者"), false, ShowEntityMenu);
            menu.AddSeparator("");
            menu.AddDisabledItem(new GUIContent("【设置】/时间轴工具设置暂未接入"));
            menu.DropDown(MoreButton.worldBound);
        }

        private void ShowTimelineMenu()
        {
            IEditorTrackSupport_GetSequence.ShowDynamicMenu(MoreButton.worldBound, OnTimelineSelected);
        }

        private void OnTimelineSelected(object userData)
        {
            if (userData is IEditorTrackSupport_GetSequence editorTrackSupport_GetSequence)
                ESTrackViewWindow.TryUpdateTrackSequence(editorTrackSupport_GetSequence);
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
            EntityStatusGroup.tooltip = $"候选目标：{preselectName}\n使用者：{runningName}";
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

            ESTrackViewWindow.window?.SealRunningEntityForPlay();
            player.Play();
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

            if (trackWindow != null && trackWindow.Last_EditorWindowForSkillDataInfo != null)
                trackWindow.Last_EditorWindowForSkillDataInfo.Close();

            var editorWindow = OdinEditorWindow.InspectObject(skillData);
            editorWindow.titleContent = new GUIContent("编辑技能 <" + skillData.name + ">");
            editorWindow.OnClose += () =>
            {
                EditorUtility.SetDirty(skillData);
                if (trackWindow != null)
                    trackWindow.Last_EditorWindowForSkillDataInfo = null;

                ESTrackViewWindowHelper.SaveContainerChanges();
            };

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
