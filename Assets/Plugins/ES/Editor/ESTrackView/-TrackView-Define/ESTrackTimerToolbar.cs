using JetBrains.Annotations;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Sirenix.OdinInspector.Editor;
using static UnityEngine.Rendering.DebugUI.Table;

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


        public Label Name = new Label();
        public VisualElement EntityStatusGroup = new VisualElement();
        public Label EntityLabel = new Label();
        public Label UserEntityLabel = new Label();

        public Button Setting = new Button();

        public ESTrackTimerToolbar()
        {
            this.style.flexDirection = FlexDirection.Row;
            this.style.color = Color.gray;
            Parivate_AddButton(PreviewButton, null, 60, 30, "封存当前 UserEntity 并开始预览");
            Parivate_AddButton(ReStartButton, EditorIcons.Refresh.Raw, 45, 30, "停止预览并回到时间 0");
            Parivate_AddButton(LastBlockButton, EditorIcons.ArrowLeft.Raw, 45, 30, "跳转到上一个片段起点");
            Parivate_AddButton(PlayButton, EditorIcons.Play.Raw, 45, 30, "播放或暂停当前时间轴");
            Parivate_AddButton(NextBlockButton, EditorIcons.ArrowRight.Raw, 45, 30, "跳转到下一个片段起点");

            {
                // ----- 新增：绑定按钮功能 -----
                PreviewButton.clicked += () =>
                {
                    ESTrackViewWindow.window?.SealRunningEntityForPlay();
                    EditorTimelinePlayer.Instance.Play();
                };
                PlayButton.clicked += OnPlayPauseToggle;
                ReStartButton.clicked += OnStopAndReset;
                LastBlockButton.clicked += JumpToPreviousClip;
                NextBlockButton.clicked += JumpToNextClip;
                EditSkillDataButton.clicked += () => ESTrackSkillDataEditorActions.OpenCurrentSkillDataInfoEditor(ESTrackViewWindow.window);
                BindAndPlaySkillButton.clicked += () => ESTrackSkillDataEditorActions.BindCurrentSkillDataToEntityAndPlay(ESTrackViewWindow.window);


            }

            PreviewButton.text = "预览";
            PreviewButton.style.color = Color.white;

            {
                TimeLabel.label = "";

                var input = TimeLabel.Q<VisualElement>("unity-text-input");
                TimeLabel.style.height = 30;
                TimeLabel.style.fontSize = 15;
                TimeLabel.style.color = Color.white;
                input.style.color = Color.white;

                input.AddToClassList("normalBlock");
                TimeLabel.value = "0:00";

                var ele = TimeLabel.Q<TextElement>();
                input.style.flexGrow = 1; input.style.flexShrink = 1;
                ele.style.width = Length.Percent(100); // 宽度100%
                ele.style.height = Length.Percent(100);
                input.style.paddingTop = 1;
                input.style.paddingLeft = 3;
                input.style.paddingBottom = 1;

                TimeLabel.isReadOnly = true;
                TimeLabel.tooltip = "当前预览时间";
                // textInputElement.style.height = Length.Percent(100); // 高度100%
                /*ele.style.width = input.style.width.value.value*0.85f;
                ele.style.flexShrink = input.style.height.value.value * 0.85f;
    */
                Add(TimeLabel);
            }


            // 时间线选择按钮（弹出多级菜单）
            {
                SelectOtherTimeLine.text = "选择其他时间线 ▾";
                SelectOtherTimeLine.tooltip = "选择要编辑的时间轴";
                SelectOtherTimeLine.style.height = 30;
                SelectOtherTimeLine.style.color = Color.white;
                SelectOtherTimeLine.clicked += ShowTimelineMenu;
                Add(SelectOtherTimeLine);
            }
            {
                EditSkillDataButton.text = "Skill";
                EditSkillDataButton.tooltip = "Edit current SKillDataInfo";
                EditSkillDataButton.style.height = 30;
                EditSkillDataButton.style.minWidth = 48;
                EditSkillDataButton.style.color = Color.white;
                Add(EditSkillDataButton);

                BindAndPlaySkillButton.text = "BindPlay";
                BindAndPlaySkillButton.tooltip = "Bind current SKillDataInfo to UserEntity and release it";
                BindAndPlaySkillButton.style.height = 30;
                BindAndPlaySkillButton.style.minWidth = 72;
                BindAndPlaySkillButton.style.color = Color.white;
                Add(BindAndPlaySkillButton);
            }
            //
            {
                Name.text = "轴名";
                Name.tooltip = "当前时间轴名称";
                Name.style.height = 30;
                Name.style.minWidth = 100;
                Name.style.color = Color.white;
                Add(Name);
                Name.AddToClassList("normalBlock");
            }

            {
                EntityStatusGroup.style.flexDirection = FlexDirection.Column;
                EntityStatusGroup.style.justifyContent = Justify.Center;
                EntityStatusGroup.style.height = 30;
                EntityStatusGroup.style.minWidth = 230;
                EntityStatusGroup.style.maxWidth = 320;
                EntityStatusGroup.style.marginLeft = 4;
                EntityStatusGroup.style.marginRight = 4;
                EntityStatusGroup.style.paddingLeft = 6;
                EntityStatusGroup.style.paddingRight = 6;
                EntityStatusGroup.style.backgroundColor = new Color(0.09f, 0.1f, 0.11f, 0.86f);
                EntityStatusGroup.style.borderLeftColor = new Color(0.25f, 0.65f, 0.95f, 0.95f);
                EntityStatusGroup.style.borderLeftWidth = 2;
                EntityStatusGroup.tooltip = "编辑器预览目标。开始预览时会封存 UserEntity。";

                EntityLabel.text = "Preselect: <None>";
                EntityLabel.style.height = 14;
                EntityLabel.style.color = new Color(0.78f, 0.86f, 1f, 1f);
                EntityLabel.style.fontSize = 11;
                EntityLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                EntityLabel.style.overflow = Overflow.Hidden;
                EntityStatusGroup.Add(EntityLabel);

                UserEntityLabel.text = "UserEntity: <None>";
                UserEntityLabel.style.height = 14;
                UserEntityLabel.style.color = new Color(0.72f, 1f, 0.76f, 1f);
                UserEntityLabel.style.fontSize = 11;
                UserEntityLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                UserEntityLabel.style.overflow = Overflow.Hidden;
                EntityStatusGroup.Add(UserEntityLabel);

                Add(EntityStatusGroup);
                EntityStatusGroup.AddToClassList("normalBlock");

                SelectEntityButton.text = "选择Entity";
                SelectEntityButton.style.height = 30;
                SelectEntityButton.text = "Entity";
                SelectEntityButton.style.minWidth = 58;
                SelectEntityButton.style.color = Color.white;
                SelectEntityButton.tooltip = "从场景中选择激活的 Entity";
                SelectEntityButton.clicked += ShowEntityMenu;
                Add(SelectEntityButton);
            }

            {
                Parivate_AddButton(Setting, EditorIcons.SettingsCog.Raw, 30, 30, "打开轨道编辑器设置");
                Setting.style.position = Position.Absolute;
                Setting.style.right = 0;
                Setting.style.top = 0;
            }

        }
        private void Parivate_AddButton(Button button, Texture2D te, float width = 45, float height = 30, string tooltip = null)
        {
            if (te != null) button.style.backgroundImage = te;
            if (!string.IsNullOrEmpty(tooltip)) button.tooltip = tooltip;
            button.style.width = width;
            button.style.height = height;
            Add(button);
        }

        /// <summary>
        /// 点击按钮时构建并显示多级菜单
        /// </summary>
        private void ShowTimelineMenu()
        {
            IEditorTrackSupport_GetSequence.ShowDynamicMenu(SelectOtherTimeLine.worldBound, OnTimelineSelected);
        }

        /// <summary>
        /// 菜单项选中回调
        /// </summary>
        private void OnTimelineSelected(object userData)
        {

            // 可以更新按钮文本为当前选择
            if (userData is IEditorTrackSupport_GetSequence editorTrackSupport_GetSequence)
                ESTrackViewWindow.TryUpdateTrackSequence(editorTrackSupport_GetSequence);
        }

        private void ShowEntityMenu()
        {
            ESTrackViewWindow.window?.ShowEntitySelectMenu();
        }

        internal void UpdateEntity(Entity preselectEntity, Entity runningEntity)
        {
            string preselectName = preselectEntity != null ? preselectEntity.name : "<None>";
            string runningName = runningEntity != null ? runningEntity.name : "<None>";
            EntityLabel.text = $"Preselect: {preselectName}";
            UserEntityLabel.text = $"UserEntity: {runningName}";
            EntityStatusGroup.tooltip = $"Preselect: {preselectName}\nUserEntity: {runningName}";
        }

        internal void UpdateTime(float time)
        {
            // 分:秒.百分秒格式（小数点后两位）
            int totalMinutes = Mathf.FloorToInt(time / 60f);
            float seconds = time % 60f;
            TimeLabel.value = $"{totalMinutes}:{seconds:00.00}";
        }

        #region 按钮

        // 播放/暂停切换
        private void OnPlayPauseToggle()
        {
            var player = EditorTimelinePlayer.Instance;
            Debug.Log("尝试播放序列"+player.ActiveSequence);
            if (player.ActiveSequence == null) return;

            if (player.ActiveSequence.IsPlaying)
                player.Pause();
            else
            {
                ESTrackViewWindow.window?.SealRunningEntityForPlay();
                player.Play();
            }
        }

        // 停止并重置到开头
        private void OnStopAndReset()
        {
            EditorTimelinePlayer.Instance.Stop(); // 内部会 SetTime(0)
        }

        // 跳转到上一个片段
        private void JumpToPreviousClip()
        {
            var clips = GetAllClipsSorted();
            if (clips.Count == 0) return;
            float current = EditorTimelinePlayer.Instance.ActiveSequence?.CurrentTime ?? 0f;

            // 找开始时间严格小于当前时间的最大者
            ITrackClip target = null;
            foreach (var c in clips)
            {
                if (c.StartTime < current - 0.001f)
                    target = c;
                else
                    break;
            }
            if (target != null)
                EditorTimelinePlayer.Instance.SetTime(target.StartTime);
        }

        // 跳转到下一个片段
        private void JumpToNextClip()
        {
            var clips = GetAllClipsSorted();
            if (clips.Count == 0) return;
            float current = EditorTimelinePlayer.Instance.ActiveSequence?.CurrentTime ?? 0f;

            // 找开始时间严格大于当前时间的最小者
            foreach (var c in clips)
            {
                if (c.StartTime > current + 0.001f)
                {
                    EditorTimelinePlayer.Instance.SetTime(c.StartTime);
                    return;
                }
            }
            // 如果没有更大的，跳到末尾
            EditorTimelinePlayer.Instance.SetTime(
                EditorTimelinePlayer.Instance.ActiveSequence?.Duration ?? 10f);
        }

        // 获取当前序列中所有片段并按时间排序
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
            editorWindow.titleContent = new GUIContent("Edit Skill<" + skillData.name + ">");
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
                Debug.LogWarning("[ESTrackSkillDataEditorActions] Track window is null.");
                return false;
            }

            trackWindow.UpdatePreselectEntityFromSelection(false);
            Entity entity = trackWindow.RunningEntity != null ? trackWindow.RunningEntity : trackWindow.PreselectEntity;
            if (entity == null)
            {
                Debug.LogWarning("[ESTrackSkillDataEditorActions] No bound Entity. Select an Entity or choose one from the toolbar.");
                return false;
            }

            if (entity.basicDomain == null)
            {
                Debug.LogWarning($"[ESTrackSkillDataEditorActions] Entity.basicDomain is null. Entity={entity.name}", entity);
                return false;
            }

            var module = EnsureSimpleSkillTestModule(entity);
            if (module == null)
                return false;

            if (module.skills == null)
                module.skills = new List<SKillDataInfo>();

            if (!module.skills.Contains(skillData))
                module.skills.Add(skillData);

            EditorUtility.SetDirty(entity);

            MoveEditorCursorToSkillStart();
            bool success = module.ReleaseSkill(skillData);
            if (!success)
            {
                Debug.LogWarning($"[ESTrackSkillDataEditorActions] Release skill failed. Skill={skillData.name} | Entity={entity.name}", entity);
                return false;
            }

            Selection.activeObject = entity.gameObject;
            Debug.Log($"[ESTrackSkillDataEditorActions] Skill bound and released. Skill={skillData.name} | Entity={entity.name}", entity);
            return true;
        }

        private static bool TryGetCurrentSkillDataInfo(out SKillDataInfo skillData)
        {
            skillData = ESTrackViewWindow.TrackContainer as SKillDataInfo;
            if (skillData != null)
                return true;

            Debug.LogWarning("[ESTrackSkillDataEditorActions] Current track container is not SKillDataInfo.");
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

            Undo.RecordObject(entity, "Add Simple Skill Test Module");
            module = new EntityBasicSimpleSkillTestModule();
            domain.TryAddModuleRuntime(module);
            domain.MyModules.ApplyBuffers(true);

            module = domain.FindMyModule<EntityBasicSimpleSkillTestModule>();
            if (module == null)
                Debug.LogWarning($"[ESTrackSkillDataEditorActions] Failed to add EntityBasicSimpleSkillTestModule. Entity={entity.name}", entity);

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
