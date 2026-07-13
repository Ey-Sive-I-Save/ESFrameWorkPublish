
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;
namespace ES
{
    public class ESEditorTrackClip : VisualElement
    {
        public string ClipName { get { return trackClip.DisplayName; } private set { if (trackClip.DisplayName != value) { trackClip.DisplayName = value; m_ClipNameLabel.text = value; } } }
        public float StartTime { get { return trackClip.StartTime; } private set { if (trackClip.StartTime != value) { trackClip.StartTime = value; } } }
        public float Duration { get { return trackClip.DurationTime; } private set { if (trackClip.DurationTime != value) { trackClip.DurationTime = value; } } }
        public object UserData { get; set; }

        public ITrackClip trackClip;

        private VisualElement m_ClipContent;

        private VisualElement popup;
        private Label popLabel;
        private VisualElement m_ClipIcon;
        private Label m_ClipNameLabel;
        private TextField m_RenameField;
        private bool isRenaming;
        private bool m_IsSelected;
        private bool m_IsHovering;
        private bool m_HasValidationWarning;
        private string m_ValidationWarning;
        private Color m_LastTrackAccentColor = new Color(0.42f, 0.46f, 0.52f, 1f);
        private double m_IgnoreRenameFocusOutUntil;

        public event Action<ESEditorTrackClip> OnClipClicked;
#pragma warning disable CS0067
        public event Action<ESEditorTrackClip> OnClipDragged;
#pragma warning restore CS0067

        public ESEditorTrackClip(ITrackClip clip, string name, float startTime, float duration, object data = null)
        {
            trackClip = clip;
            ClipName = name;
            StartTime = startTime;
            Duration = duration;
            UserData = data;
            this.focusable = true;
            tooltip = name;
            // 基础样式
            AddToClassList("track-node");
            style.position = Position.Absolute;
            style.flexShrink = 0;
            style.minWidth = 30;
            style.minHeight = 26;
            style.maxHeight = 26;
            style.backgroundColor = new Color(0.18f, 0.31f, 0.44f, 0.95f);
            style.borderTopLeftRadius = 3;
            style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3;
            style.borderBottomRightRadius = 3;
            style.position = Position.Absolute;
            // style.borderLeftWidth = 2;
            // style.borderRightWidth = 2;
            // style.borderTopWidth = 2;
            // style.borderBottomWidth = 2;
            // style.borderLeftColor = new Color(0.3f, 0.5f, 0.8f);
            // style.borderRightColor = new Color(0.3f, 0.5f, 0.8f);
            // style.borderTopColor = new Color(0.5f, 0.8f, 1f);
            // style.borderBottomColor = new Color(0.2f, 0.4f, 0.7f);
            // style.borderTopLeftRadius = 4;
            // style.borderTopRightRadius = 4;
            // style.borderBottomLeftRadius = 4;
            // style.borderBottomRightRadius = 4;



            // 创建内容
            m_ClipContent = new VisualElement
            {
                style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1,
                justifyContent = Justify.FlexStart,
                alignItems = Align.Center,
                paddingLeft = 6,
                paddingRight = 6
            }
            };

            m_ClipIcon = new VisualElement
            {
                style =
            {
                width = 11,
                height = 11,
                minWidth = 11,
                marginRight = 4,
                borderTopLeftRadius = 2,
                borderTopRightRadius = 2,
                borderBottomLeftRadius = 2,
                borderBottomRightRadius = 2
            }
            };
            m_ClipContent.Add(m_ClipIcon);

            m_ClipNameLabel = new Label(name)
            {
                style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 10,
                color = new Color(0.93f, 0.96f, 1f, 1f),
                unityTextAlign = TextAnchor.MiddleLeft,
                whiteSpace = WhiteSpace.NoWrap,
                overflow = Overflow.Hidden,
                textOverflow = TextOverflow.Ellipsis
            }
            };

            m_ClipContent.Add(m_ClipNameLabel);
            Add(m_ClipContent);
            RefreshClipIcon();
            RefreshEnabledVisual();

            // 注册事件
            RegisterCallback<ClickEvent>(evt =>
            {
                if (isRenaming)
                {
                    evt.StopPropagation();
                    return;
                }

                if (evt.clickCount >= 2 && evt.button == 0)
                {
                    BeginRename();
                    evt.StopPropagation();
                    return;
                }

                ESTrackViewWindow.window?.SelectClip(this);
                OnClipClicked?.Invoke(this);
            });

            RegisterCallback<WheelEvent>(evt =>
           {
               ESTrackViewWindow.window.OnRightPanelWheel(evt);
               evt.StopPropagation(); // 节点处理后停止传播
           }, TrickleDown.TrickleDown);


            SetTimeScaleAndStartShow(Cache_pixelsPerSecond, Cahce_ShowStart);


            BindDragEvent();


        }

        private bool isDragging = false;
        private bool isExpanding = false;
        private float offsetPOSDragLeft = 0f;
        private float offsetPOSForMouseX = 0f;

        public static float lastHandleTime = 0;
        private float startWidth = 0f;
        #region  拖动功能·
        public void BindDragEvent()
        {
            popup = new VisualElement();
            // popup.AddToClassList(popupClass);
            popup.pickingMode = PickingMode.Ignore;

            popup.style.position = Position.Absolute;
            popup.style.bottom = this.resolvedStyle.height + 50;
            popup.style.left = 0;
            popup.style.width = Length.Percent(100);
            popup.style.height = 30;
            // popup.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            popup.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            popup.style.borderTopLeftRadius = 3;
            popup.style.borderTopRightRadius = 3;
            popup.style.borderBottomLeftRadius = 3;
            popup.style.borderBottomRightRadius = 3;
            popup.style.display = DisplayStyle.None;
            popup.Add(popLabel = new Label());
            popLabel.style.left = 0;
            popLabel.style.width = 200;
            popLabel.style.overflow = Overflow.Hidden;
            popLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            popLabel.style.textOverflow = TextOverflow.Ellipsis;
            popLabel.style.color = Color.white;
            popLabel.style.fontSize = 12;

            // 基础边框（半透明白色，像素宽度 1）
            style.borderLeftWidth = 3;
            style.borderRightWidth = 1;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftColor = new Color(0.04f, 0.05f, 0.06f, 0.9f);
            style.borderRightColor = new Color(0.42f, 0.5f, 0.6f, 0.28f);
            style.borderTopColor = new Color(0.52f, 0.58f, 0.66f, 0.32f);
            style.borderBottomColor = new Color(0.04f, 0.05f, 0.06f, 0.7f);

            this.Add(popup);

            this.RegisterCallback<PointerDownEvent>(OnPointerDown);

            // 鼠标释放结束拖动
            this.RegisterCallback<PointerUpEvent>(OnPointerUp);

            // 鼠标离开时如果未按下则结束拖动
            this.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            this.RegisterCallback<PointerEnterEvent>(OnPointerEnter);

            // 全局鼠标移动（用于持续拖动）
            this.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        public void SetSelected(bool selected)
        {
            m_IsSelected = selected;
            RefreshInteractionVisual();
        }

        private void RefreshInteractionVisual()
        {
            if (m_ClipContent != null)
            {
                m_ClipContent.style.backgroundColor = m_IsSelected
                    ? new Color(0.35f, 0.52f, 0.76f, 0.16f)
                    : m_IsHovering
                        ? new Color(1f, 1f, 1f, 0.035f)
                    : Color.clear;
            }

            style.borderRightWidth = m_IsSelected ? 2 : 1;
            style.borderRightColor = m_IsSelected
                ? new Color(0.48f, 0.68f, 0.95f, 0.72f)
                : new Color(0.08f, 0.09f, 0.11f, 0.82f);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (isRenaming)
            {
                evt.StopPropagation();
                return;
            }

            this.BringToFront();
            if (evt.button == 0)
                ESTrackViewWindow.window?.SelectClip(this);

            if (evt.button == 0 && evt.shiftKey)
            {
                ESTrackViewWindowHelper.EditClip(this);
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0 && evt.clickCount >= 2)
            {
                BeginRename();
                evt.StopPropagation();
                return;
            }

            if (isRenaming)
            {
                evt.StopPropagation();
                return;
            }

            if (evt.button != 1) // 左键
            {
                lastHandleTime = Time.realtimeSinceStartup;
                if (evt.shiftKey == false) // 无Shift 拖动后
                {
                    isDragging = true;
                    var mousePos = evt.position;
                    offsetPOSDragLeft = mousePos.x - this.resolvedStyle.left;
                    this.AddToClassList("dragging");
                    popup.style.display = DisplayStyle.Flex;
                    this.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
                else if (true) //zhong键
                {
                    isExpanding = true;
                    var mousePos = evt.position;
                    offsetPOSForMouseX = mousePos.x;
                    startWidth = this.resolvedStyle.width;
                    this.AddToClassList("expanding");
                    popup.style.display = DisplayStyle.Flex;
                    this.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            }
            else if (evt.button == 1) //右键
            {
                //显示菜单
                ESTrackViewWindow.window.ShowMenu_SelectClip(this);
                evt.StopPropagation();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (isRenaming)
                return;

            if (isDragging)
            {
                isDragging = false;
                this.RemoveFromClassList("dragging");
                popup.style.display = DisplayStyle.None;
                this.ReleasePointer(evt.pointerId);
                ESTrackViewWindow.window?.SyncTotalTimeFromCurrentSequence(true);
                evt.StopPropagation();
            }

            if (isExpanding) //中
            {
                isExpanding = false;
                popup.style.display = DisplayStyle.None;
                this.ReleasePointer(evt.pointerId);
                ESTrackViewWindow.window?.SyncTotalTimeFromCurrentSequence(true);
                evt.StopPropagation();
            }
        }

        private void BeginRename()
        {
            if (trackClip == null || isRenaming)
                return;

            isRenaming = true;
            ESTrackViewWindow.window?.SetRenamingClip(this);
            isDragging = false;
            isExpanding = false;
            RemoveFromClassList("dragging");
            RemoveFromClassList("expanding");
            popup.style.display = DisplayStyle.None;

            m_ClipNameLabel.style.display = DisplayStyle.None;
            if (m_ClipIcon != null)
                m_ClipIcon.style.display = DisplayStyle.None;

            if (m_RenameField == null)
            {
                m_RenameField = new TextField
                {
                    isDelayed = false
                };
                m_RenameField.selectAllOnFocus = false;
                m_RenameField.selectAllOnMouseUp = false;
                m_RenameField.style.position = Position.Absolute;
                m_RenameField.style.left = 3;
                m_RenameField.style.right = 3;
                m_RenameField.style.top = 4;
                m_RenameField.style.height = 22;
                m_RenameField.style.fontSize = 11;
                m_RenameField.style.color = new Color(0.92f, 0.95f, 1f, 1f);
                m_RenameField.style.backgroundColor = new Color(0.075f, 0.085f, 0.1f, 1f);
                m_RenameField.tooltip = "正在重命名片段：Enter 确认，Esc 取消";
                m_RenameField.RegisterCallback<KeyDownEvent>(OnRenameKeyDown);
                Add(m_RenameField);
            }

            m_RenameField.SetValueWithoutNotify(ClipName);
            m_RenameField.style.display = DisplayStyle.Flex;
            schedule.Execute(() =>
            {
                if (!isRenaming || m_RenameField == null)
                    return;

                m_RenameField.Focus();
                m_RenameField.SelectAll();
            }).ExecuteLater(0);
        }

        private void StopRenameFieldPointerEvent(PointerDownEvent evt)
        {
            MarkRenameFieldInternalClick();
            evt.StopPropagation();
        }

        private void StopRenameFieldPointerEvent(PointerUpEvent evt)
        {
            MarkRenameFieldInternalClick();
            evt.StopPropagation();
        }

        private void StopRenameFieldClickEvent(ClickEvent evt)
        {
            MarkRenameFieldInternalClick();
            evt.StopPropagation();
        }

        private void MarkRenameFieldInternalClick()
        {
            m_IgnoreRenameFocusOutUntil = EditorApplication.timeSinceStartup + 0.35d;
        }

        public void CommitRenameIfPointerOutsideRenameField(Vector2 worldPosition)
        {
            if (!isRenaming || m_RenameField == null)
                return;

            if (m_RenameField.worldBound.Contains(worldPosition))
                return;

            CommitRename();
        }

        private void OnRenameKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitRename();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                CancelRename();
                evt.StopPropagation();
            }
        }

        private void CommitRename()
        {
            if (!isRenaming)
                return;

            string newName = m_RenameField != null ? m_RenameField.value : ClipName;
            newName = string.IsNullOrWhiteSpace(newName) ? ClipName : newName.Trim();
            if (trackClip != null && trackClip.DisplayName != newName)
            {
                UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
                if (undoTarget != null)
                    Undo.RecordObject(undoTarget, "重命名轨道片段");

                ClipName = newName;
                ESTrackViewWindowHelper.SaveContainerDisplayChanges();
            }

            EndRename();
        }

        private void CancelRename()
        {
            EndRename();
        }

        private void EndRename()
        {
            isRenaming = false;
            ESTrackViewWindow.window?.ClearRenamingClip(this);
            if (m_RenameField != null)
                m_RenameField.style.display = DisplayStyle.None;

            m_ClipNameLabel.style.display = DisplayStyle.Flex;
            RefreshClipIcon();
            UpdateNodeView();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            m_IsHovering = false;
            RefreshInteractionVisual();
            if (!isDragging)
            {
                // popup.style.display = DisplayStyle.None;
            }
            if (!isExpanding)
            {
                //popup.style.display = DisplayStyle.None;
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            //            Debug.Log("拖动位置：" + this.resolvedStyle.left + " " + evt.button + " " + evt.pointerType + evt.shiftKey);
            if (isDragging)
            {

                // 获取鼠标在轨道上的相对位置
                var mousePos = evt.position;

                // 计算相对于轨道的x位置
                this.style.left = Mathf.Max(mousePos.x - offsetPOSDragLeft, 0);
                //Debug.Log("拖动位置：" + this.resolvedStyle.left);
                // 计算对应的时间
                MatchTimeFromDynamicPos();
            }
            else if (isExpanding)
            {

                // 获取鼠标在轨道上的相对位置
                var mousePos = evt.position;

                // 计算新的宽度
                //Debug.Log("扩展位置：" + offsetPOSForMouseX+"N"+(mousePos.x - offsetPOSForMouseX));
                float offset = mousePos.x - offsetPOSForMouseX;// 最小右侧为10
                float newWidth = Mathf.Max(startWidth + offset, 1); // 最小宽度为10
                this.style.width = newWidth;

                // 计算对应的持续时间
                var newDuration = newWidth / Cache_pixelsPerSecond;
                Duration = newDuration;

                popLabel.text = $"[{StartTime:F4}s -- {StartTime + Duration:F4}]";
                MatchTimeFromDynamicPos();
            }
        }

        public void MatchTimeFromDynamicPos()
        {
            var w = Duration * Cache_pixelsPerSecond;
            float nowLEFT = this.style.left.value.value;
            var newStartTime = Cahce_ShowStart;
            if (nowLEFT != 0)
            {
                newStartTime = nowLEFT / Cache_pixelsPerSecond + Cahce_ShowStart;

            }
            style.width = w;
            StartTime = newStartTime;
            AdjustFontToFit();
            popLabel.text = $"[{StartTime:F2}s -- {StartTime + Duration:F2}]";

        }


        #endregion
        public static float Cache_pixelsPerSecond = 100f;
        public static float Cahce_ShowStart = 0f;
        public void SetTimeScaleAndStartShow(float pixelsPerSecond, float ShowStart)
        {
            Cache_pixelsPerSecond = pixelsPerSecond;
            Cahce_ShowStart = ShowStart;
            //Debug.Log("TRUE"+Cache_pixelsPerSecond);
            // 根据时间尺度和持续时间设置节点宽度
            var w = Duration * pixelsPerSecond;
            var left = (StartTime - ShowStart) * pixelsPerSecond;
            // Debug.Log("WW"+w+" LL"+left+" START "+ShowStart);
            style.width = w;
            style.left = left;
            if (!isRenaming && m_ClipIcon != null)
                m_ClipIcon.style.display = w >= 44f ? DisplayStyle.Flex : DisplayStyle.None;
            AdjustFontToFit();
        }

        public void SetTimeScaleAndStartShowCache()
        {
            SetTimeScaleAndStartShow(Cache_pixelsPerSecond, Cahce_ShowStart);
        }

        public void SetClipColor(Color color)
        {
            m_LastTrackAccentColor = color;
            if (m_HasValidationWarning)
                return;

            ApplyNormalClipColor(color);
            RefreshEnabledVisual();
        }

        private void ApplyNormalClipColor(Color color)
        {
            Color baseColor = new Color(
                Mathf.Clamp01(color.r * 0.42f + 0.035f),
                Mathf.Clamp01(color.g * 0.42f + 0.035f),
                Mathf.Clamp01(color.b * 0.42f + 0.035f),
                0.96f);
            Color accentColor = new Color(color.r * 0.88f, color.g * 0.88f, color.b * 0.88f, 0.9f);
            style.backgroundColor = baseColor;
            style.borderLeftColor = accentColor;
            if (!m_IsSelected)
                style.borderRightColor = new Color(0.08f, 0.09f, 0.11f, 0.82f);
            style.borderTopColor = new Color(0.42f, 0.47f, 0.54f, 0.34f);
            style.borderBottomColor = new Color(0.02f, 0.025f, 0.03f, 0.82f);
        }

        public void SetValidationWarning(string warning)
        {
            m_HasValidationWarning = !string.IsNullOrWhiteSpace(warning);
            m_ValidationWarning = warning;
            if (!m_HasValidationWarning)
                ApplyNormalClipColor(m_LastTrackAccentColor);

            ApplyValidationVisual();
            RefreshEnabledVisual();
        }

        public void ToggleEnabled()
        {
            if (trackClip == null)
                return;

            UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
            if (undoTarget != null)
                Undo.RecordObject(undoTarget, trackClip.Enabled ? "禁用片段" : "启用片段");

            trackClip.Enabled = !trackClip.Enabled;
            RefreshEnabledVisual();
            ESTrackViewWindowHelper.SaveContainerDisplayChanges();
            if (ESTrackViewWindow.Sequence != null)
                SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
            ESTrackViewWindow.window?.RebuildActivePreviewPlayer();
        }

        public void RefreshEnabledVisual()
        {
            bool enabled = trackClip == null || trackClip.Enabled;
            style.opacity = enabled ? 1f : 0.92f;
            tooltip = m_HasValidationWarning
                ? ClipName + "\n预警：" + m_ValidationWarning
                : enabled ? ClipName : ClipName + "（已禁用）";

            if (m_ClipNameLabel != null)
                m_ClipNameLabel.style.color = enabled
                    ? new Color(0.93f, 0.96f, 1f, 1f)
                    : new Color(0.38f, 0.40f, 0.44f, 1f);

            if (!enabled && !m_HasValidationWarning)
            {
                style.backgroundColor = new Color(0.012f, 0.013f, 0.016f, 1f);
                style.borderLeftColor = new Color(0.035f, 0.038f, 0.045f, 1f);
                style.borderRightColor = new Color(0.006f, 0.007f, 0.009f, 1f);
                style.borderTopColor = new Color(0.04f, 0.043f, 0.05f, 1f);
                style.borderBottomColor = new Color(0.002f, 0.003f, 0.004f, 1f);
            }

            ApplyValidationVisual();
        }

        private void ApplyValidationVisual()
        {
            if (!m_HasValidationWarning)
                return;

            bool enabled = trackClip == null || trackClip.Enabled;
            style.opacity = enabled ? 1f : 0.92f;
            style.backgroundColor = enabled
                ? new Color(0.78f, 0.05f, 0.045f, 0.98f)
                : new Color(0.09f, 0.006f, 0.005f, 1f);
            style.borderLeftWidth = 4;
            style.borderRightWidth = 2;
            style.borderTopWidth = 2;
            style.borderBottomWidth = 2;
            style.borderLeftColor = enabled ? new Color(1f, 0.18f, 0.12f, 1f) : new Color(0.45f, 0.035f, 0.025f, 1f);
            style.borderRightColor = enabled ? new Color(1f, 0.46f, 0.34f, 0.95f) : new Color(0.22f, 0.025f, 0.02f, 1f);
            style.borderTopColor = enabled ? new Color(1f, 0.55f, 0.42f, 0.95f) : new Color(0.30f, 0.035f, 0.028f, 0.95f);
            style.borderBottomColor = enabled ? new Color(0.32f, 0f, 0f, 1f) : new Color(0.08f, 0f, 0f, 1f);

            if (m_ClipNameLabel != null)
                m_ClipNameLabel.style.color = enabled ? Color.white : new Color(0.62f, 0.46f, 0.46f, 1f);
        }

        public void RefreshClipIcon()
        {
            if (m_ClipIcon == null)
                return;

            Texture2D icon = ESTrackViewIconUtility.ResolveClipIcon(trackClip);
            if (icon != null)
                m_ClipIcon.style.backgroundImage = icon;

            m_ClipIcon.style.backgroundColor = ESTrackViewIconUtility.ResolveIconBackColor(trackClip != null ? trackClip.GetType() : null);
            m_ClipIcon.tooltip = trackClip != null ? trackClip.DisplayName : "片段";
            if (!isRenaming)
                m_ClipIcon.style.display = resolvedStyle.width >= 44f ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private Color originalBgColor;
        private bool hasSetORI=false;
        public void HighlightIfActive(float currentTime)
        {
            if (currentTime >= StartTime && currentTime <= StartTime + Duration)
            {
                if (!hasSetORI)
                    originalBgColor = style.backgroundColor.value;
                hasSetORI = true;

                style.borderTopColor = new Color(0.82f, 0.76f, 0.48f, 0.95f);
                style.borderTopWidth = 1;
                style.borderBottomColor = new Color(0.82f, 0.76f, 0.48f, 0.78f);
                style.borderBottomWidth = 1;
                style.backgroundColor = new Color(
                    Mathf.Clamp01(originalBgColor.r + 0.035f),
                    Mathf.Clamp01(originalBgColor.g + 0.035f),
                    Mathf.Clamp01(originalBgColor.b + 0.035f),
                    originalBgColor.a);
            }
            else
            {
                if (hasSetORI)
                {
                    style.backgroundColor = originalBgColor;
                }

                style.borderTopWidth = 1;
                style.borderBottomWidth = 1;
                style.borderTopColor = new Color(0.42f, 0.47f, 0.54f, 0.34f);
                style.borderBottomColor = new Color(0.02f, 0.025f, 0.03f, 0.82f);
            }
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            m_IsHovering = true;
            RefreshInteractionVisual();
        }

        public void UpdateNodeView()
        {
            m_ClipNameLabel.text = ClipName;
            tooltip = ClipName;
            AdjustFontToFit();
        }

        void AdjustFontToFit()
        {
            float availableWidth = Mathf.Max(0f, style.width.value.value - 30f);
            if (string.IsNullOrEmpty(m_ClipNameLabel.text) || availableWidth <= 0f)
            {
                m_ClipNameLabel.style.fontSize = 8f;
                return;
            }

            float targetSize = Mathf.Lerp(8f, 11.5f, Mathf.InverseLerp(40f, 180f, availableWidth));
            m_ClipNameLabel.style.fontSize = targetSize;
        }

    }
}
