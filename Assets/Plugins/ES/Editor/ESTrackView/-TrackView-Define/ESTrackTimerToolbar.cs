using JetBrains.Annotations;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
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


        public Label Name = new Label();

        public Button Setting = new Button();

        public ESTrackTimerToolbar()
        {
            this.style.flexDirection = FlexDirection.Row;
            this.style.color = Color.gray;
            Parivate_AddButton(PreviewButton, null, 60);
            Parivate_AddButton(ReStartButton, EditorIcons.Refresh.Raw);
            Parivate_AddButton(LastBlockButton, EditorIcons.ArrowLeft.Raw);
            Parivate_AddButton(PlayButton, EditorIcons.Play.Raw);
            Parivate_AddButton(NextBlockButton, EditorIcons.ArrowRight.Raw);

            {
                // ----- 新增：绑定按钮功能 -----
                PreviewButton.clicked += () => EditorTimelinePlayer.Instance.Play();
                PlayButton.clicked += OnPlayPauseToggle;
                ReStartButton.clicked += OnStopAndReset;
                LastBlockButton.clicked += JumpToPreviousClip;
                NextBlockButton.clicked += JumpToNextClip;


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
                // textInputElement.style.height = Length.Percent(100); // 高度100%
                /*ele.style.width = input.style.width.value.value*0.85f;
                ele.style.flexShrink = input.style.height.value.value * 0.85f;
    */
                Add(TimeLabel);
            }


            // 时间线选择按钮（弹出多级菜单）
            {
                SelectOtherTimeLine.text = "选择其他时间线 ▾";
                SelectOtherTimeLine.style.height = 30;
                SelectOtherTimeLine.style.color = Color.white;
                SelectOtherTimeLine.clicked += ShowTimelineMenu;
                Add(SelectOtherTimeLine);
            }
            //
            {
                Name.text = "轴名";
                Name.style.height = 30;
                Name.style.minWidth = 100;
                Name.style.color = Color.white;
                Add(Name);
                Name.AddToClassList("normalBlock");
            }

            {
                Parivate_AddButton(Setting, EditorIcons.SettingsCog.Raw, 30);
                Setting.style.position = Position.Absolute;
                Setting.style.right = 0;
                Setting.style.top = 0;
            }

        }
        private void Parivate_AddButton(Button button, Texture2D te, float width = 45, float height = 30)
        {
            if (te != null) button.style.backgroundImage = te;
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
                player.Play();
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
}
