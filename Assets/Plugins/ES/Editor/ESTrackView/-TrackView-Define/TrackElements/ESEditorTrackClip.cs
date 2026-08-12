
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace ES
{
    public class ESEditorTrackClip : VisualElement
    {
        public string ClipName
        {
            get { return trackClip != null ? trackClip.DisplayName : m_ClipNameCache; }
            private set
            {
                m_ClipNameCache = value ?? string.Empty;
                if (trackClip != null && trackClip.DisplayName != m_ClipNameCache)
                    trackClip.DisplayName = m_ClipNameCache;
                if (m_ClipNameLabel != null)
                    m_ClipNameLabel.text = m_ClipNameCache;
                if (m_ClipShortLabel != null)
                    m_ClipShortLabel.text = BuildShortClipName(m_ClipNameCache);
            }
        }
        public float StartTime { get { return trackClip.StartTime; } private set { if (trackClip.StartTime != value) { trackClip.StartTime = value; } } }
        public float Duration { get { return trackClip.DurationTime; } private set { if (trackClip.DurationTime != value) { trackClip.DurationTime = value; } } }
        public object UserData { get; set; }

        public ITrackClip trackClip;

        private VisualElement m_ClipContent;
        private VisualElement m_EditingFocusFrame;
        private VisualElement m_SelectionFrame;

        private VisualElement popup;
        private Label popLabel;
        private VisualElement m_ClipIcon;
        private VisualElement m_ResizeHandle;
        private Label m_ClipNameLabel;
        private Label m_ClipShortLabel;
        private Label m_ClipStateBadge;
        private TextField m_RenameField;
        private bool isRenaming;
        private bool m_IsSelected;
        private bool m_IsPrimarySelection;
        private bool m_IsHovering;
        private bool m_IsFocusedEditing;
        private bool m_HasValidationWarning;
        private string m_ValidationWarning;
        private Color m_LastTrackAccentColor;
        private double m_IgnoreRenameFocusOutUntil;
        private float m_LastAppliedLeft = float.NaN;
        private float m_LastAppliedWidth = float.NaN;
        private DisplayStyle m_LastAppliedDisplay = (DisplayStyle)(-1);
        private string m_ClipNameCache = string.Empty;
        private string m_LastAppliedClipName;
        private bool m_IsActive;
        private const float StylePixelEpsilon = 0.25f;
        private const float PointerGestureThreshold = 4f;
        private bool m_GestureActivated;
        private Vector2 m_PointerDownPosition;

        public event Action<ESEditorTrackClip> OnClipClicked;

        public ESEditorTrackClip(ITrackClip clip, string name, float startTime, float duration, object data = null)
        {
            trackClip = clip;
            ClipName = name;
            StartTime = startTime;
            Duration = duration;
            UserData = data;
            m_LastTrackAccentColor = ESTrackViewTheme.Accent;
            this.focusable = true;
            tooltip = name;
            // 基础样式
            AddToClassList("track-node");
            style.position = Position.Absolute;
            style.flexShrink = 0;
            style.minWidth = 30;
            style.minHeight = 26;
            style.maxHeight = 26;
            style.backgroundColor = ESTrackViewTheme.ClipSurface(m_LastTrackAccentColor);
            style.borderTopLeftRadius = 3;
            style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3;
            style.borderBottomRightRadius = 3;
            style.position = Position.Absolute;
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
                color = ESTrackViewTheme.Text,
                unityTextAlign = TextAnchor.MiddleLeft,
                whiteSpace = WhiteSpace.NoWrap,
                overflow = Overflow.Hidden,
                textOverflow = TextOverflow.Ellipsis
            }
            };

            m_ClipContent.Add(m_ClipNameLabel);

            m_ClipShortLabel = new Label(BuildShortClipName(name))
            {
                style =
                {
                    display = DisplayStyle.None,
                    flexGrow = 1,
                    minWidth = 0,
                    fontSize = 9,
                    color = ESTrackViewTheme.Text,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            m_ClipContent.Add(m_ClipShortLabel);

            m_ClipStateBadge = new Label()
            {
                style =
                {
                    display = DisplayStyle.None,
                    minWidth = 16,
                    width = 16,
                    height = 16,
                    marginLeft = 3,
                    fontSize = 9,
                    color = ESTrackViewTheme.SelectedText,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    backgroundColor = ESTrackViewTheme.StateBadgeSurface(ESTrackViewTheme.StatusWarning),
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };
            m_ClipContent.Add(m_ClipStateBadge);
            Add(m_ClipContent);

            m_ResizeHandle = new VisualElement { name = "clip-resize-handle" };
            m_ResizeHandle.pickingMode = PickingMode.Position;
            m_ResizeHandle.tooltip = "拖动右侧手柄调整片段时长；也支持按住 Shift 拖动片段。";
            m_ResizeHandle.style.position = Position.Absolute;
            m_ResizeHandle.style.right = 0;
            m_ResizeHandle.style.top = 2;
            m_ResizeHandle.style.bottom = 2;
            m_ResizeHandle.style.width = 5;
            m_ResizeHandle.style.backgroundColor = ESTrackViewTheme.WithAlpha(m_LastTrackAccentColor, 0.22f);
            m_ResizeHandle.style.borderTopLeftRadius = 2;
            m_ResizeHandle.style.borderBottomLeftRadius = 2;
            m_ResizeHandle.style.cursor = new UnityEngine.UIElements.Cursor
            {
                texture = EditorGUIUtility.Load("Cursors/d_ResizeHorizontal") as Texture2D,
                hotspot = new Vector2(8, 8)
            };
            Add(m_ResizeHandle);
            CreateSelectionFrame();
            CreateEditingFocusFrame();
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

                OnClipClicked?.Invoke(this);
            });
            RegisterCallback<ContextClickEvent>(OnContextClick);

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
        private int m_ActivePointerId = -1;

        public static float lastHandleTime = 0;
        private float startWidth = 0f;
        private float startDuration = 0f;
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
            popup.style.backgroundColor = ESTrackViewTheme.WithAlpha(ESTrackViewTheme.SecondarySurface, 0.96f);
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
            popLabel.style.color = ESTrackViewTheme.Text;
            popLabel.style.fontSize = 12;

            // 基础边框（半透明白色，像素宽度 1）
            style.borderLeftWidth = 3;
            style.borderRightWidth = 1;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftColor = m_LastTrackAccentColor;
            style.borderRightColor = ESTrackViewTheme.Divider;
            style.borderTopColor = ESTrackViewTheme.Divider;
            style.borderBottomColor = ESTrackViewTheme.Divider;

            this.Add(popup);

            this.RegisterCallback<PointerDownEvent>(OnPointerDown);

            // 鼠标释放结束拖动
            this.RegisterCallback<PointerUpEvent>(OnPointerUp);
            this.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            this.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            // 鼠标离开时如果未按下则结束拖动
            this.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            this.RegisterCallback<PointerEnterEvent>(OnPointerEnter);

            // 全局鼠标移动（用于持续拖动）
            this.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        internal void RebindTrackClip(ITrackClip clip)
        {
            if (clip == null || ReferenceEquals(trackClip, clip))
                return;

            CancelPointerInteraction(false);
            if (isRenaming)
                CancelRename();

            trackClip = clip;
            UserData = clip;
            m_ClipNameCache = clip.DisplayName ?? string.Empty;
            m_LastAppliedClipName = null;
            UpdateNodeView();
            RefreshClipIcon();
            RefreshEnabledVisual();
        }

        public void SetSelected(bool selected)
        {
            SetSelected(selected, false);
        }

        public void SetSelected(bool selected, bool primarySelection)
        {
            m_IsSelected = selected;
            m_IsPrimarySelection = selected && primarySelection;
            RefreshInteractionVisual();
        }

        public void SetFocusedEditing(bool focused)
        {
            m_IsFocusedEditing = focused;
            if (m_EditingFocusFrame == null)
                CreateEditingFocusFrame();

            ApplyVisualState();

            if (focused)
                tooltip = ClipName + "\n正在弹窗编辑";
            else
                RefreshEnabledVisual();
        }

        private void CreateEditingFocusFrame()
        {
            if (m_EditingFocusFrame != null)
                return;

            m_EditingFocusFrame = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                name = "editing-focus-frame"
            };
            m_EditingFocusFrame.style.position = Position.Absolute;
            m_EditingFocusFrame.style.left = 0;
            m_EditingFocusFrame.style.right = 0;
            m_EditingFocusFrame.style.top = 0;
            m_EditingFocusFrame.style.bottom = 0;
            m_EditingFocusFrame.style.borderLeftWidth = 3;
            m_EditingFocusFrame.style.borderRightWidth = 3;
            m_EditingFocusFrame.style.borderTopWidth = 3;
            m_EditingFocusFrame.style.borderBottomWidth = 3;
            m_EditingFocusFrame.style.borderLeftColor = ESTrackViewTheme.EditingAccent;
            m_EditingFocusFrame.style.borderRightColor = ESTrackViewTheme.EditingAccent;
            m_EditingFocusFrame.style.borderTopColor = ESTrackViewTheme.EditingAccent;
            m_EditingFocusFrame.style.borderBottomColor = ESTrackViewTheme.EditingAccent;
            m_EditingFocusFrame.style.borderTopLeftRadius = 4;
            m_EditingFocusFrame.style.borderTopRightRadius = 4;
            m_EditingFocusFrame.style.borderBottomLeftRadius = 4;
            m_EditingFocusFrame.style.borderBottomRightRadius = 4;
            m_EditingFocusFrame.style.backgroundColor = ESTrackViewTheme.WithAlpha(ESTrackViewTheme.EditingAccent, 0.07f);
            m_EditingFocusFrame.style.display = DisplayStyle.None;
            Add(m_EditingFocusFrame);
        }

        private void CreateSelectionFrame()
        {
            if (m_SelectionFrame != null)
                return;

            m_SelectionFrame = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                name = "selection-frame"
            };
            m_SelectionFrame.style.position = Position.Absolute;
            m_SelectionFrame.style.left = 0;
            m_SelectionFrame.style.right = 0;
            m_SelectionFrame.style.top = 0;
            m_SelectionFrame.style.bottom = 0;
            m_SelectionFrame.style.borderLeftWidth = 2;
            m_SelectionFrame.style.borderRightWidth = 2;
            m_SelectionFrame.style.borderTopWidth = 2;
            m_SelectionFrame.style.borderBottomWidth = 2;
            m_SelectionFrame.style.borderTopLeftRadius = 4;
            m_SelectionFrame.style.borderTopRightRadius = 4;
            m_SelectionFrame.style.borderBottomLeftRadius = 4;
            m_SelectionFrame.style.borderBottomRightRadius = 4;
            m_SelectionFrame.style.display = DisplayStyle.None;
            Add(m_SelectionFrame);
        }

        private void RefreshInteractionVisual()
        {
            ApplyVisualState();
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
            {
                bool additiveSelection = evt.ctrlKey || evt.commandKey;
                bool keepGroupSelectionForDrag = !additiveSelection
                    && ESTrackViewWindow.window != null
                    && ESTrackViewWindow.window.SelectedClipCount > 1
                    && ESTrackViewWindow.window.IsClipSelected(this);

                if (!keepGroupSelectionForDrag)
                    ESTrackViewWindow.window?.SelectClip(this, additiveSelection);

                if (additiveSelection)
                {
                    evt.StopPropagation();
                    return;
                }
            }

            if (evt.button == 1)
                return;

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

            if (evt.button == 0) // 仅左键：中键/其他按钮不得改变时序
            {
                lastHandleTime = Time.realtimeSinceStartup;
                bool resizeGesture = evt.shiftKey || ReferenceEquals(evt.target, m_ResizeHandle);
                if (!resizeGesture) // 默认拖动片段
                {
                    isDragging = true;
                    m_GestureActivated = false;
                    var mousePos = evt.position;
                    m_PointerDownPosition = mousePos;
                    offsetPOSDragLeft = mousePos.x - this.resolvedStyle.left;
                    popup.style.display = DisplayStyle.None;
                    m_ActivePointerId = evt.pointerId;
                    this.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
                else
                {
                    isExpanding = true;
                    m_GestureActivated = false;
                    var mousePos = evt.position;
                    m_PointerDownPosition = mousePos;
                    offsetPOSForMouseX = mousePos.x;
                    startWidth = this.resolvedStyle.width;
                    startDuration = Duration;
                    popup.style.display = DisplayStyle.None;
                    m_ActivePointerId = evt.pointerId;
                    this.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            }
        }

        private void OnContextClick(ContextClickEvent evt)
        {
            if (evt.button != 1)
                return;

            if (isRenaming)
            {
                evt.StopPropagation();
                return;
            }

            ESTrackViewWindow hostWindow = ESTrackViewWindow.window;
            if (evt.shiftKey)
            {
                if (hostWindow != null && !hostWindow.IsClipSelected(this))
                    hostWindow.SelectClip(this, false);
                hostWindow?.EditClip(this, true);
            }
            else
                hostWindow?.ShowClipContextMenu(this);

            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (isRenaming)
                return;

            FinishPointerInteraction(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            FinishPointerInteraction(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (isDragging || isExpanding)
                FinishPointerInteraction(evt.pointerId);
        }

        private void FinishPointerInteraction(int pointerId)
        {
            if (pointerId < 0)
                pointerId = m_ActivePointerId;

            if (isDragging)
            {
                isDragging = false;
                bool committed = m_GestureActivated;
                m_GestureActivated = false;
                RemoveFromClassList("dragging");
                ApplyVisualState();
                popup.style.display = DisplayStyle.None;
                if (pointerId >= 0 && this.HasPointerCapture(pointerId))
                    this.ReleasePointer(pointerId);
                if (committed)
                    ESTrackViewWindow.window?.EndClipGroupDrag(this);
            }

            if (isExpanding)
            {
                isExpanding = false;
                bool committed = m_GestureActivated;
                m_GestureActivated = false;
                RemoveFromClassList("expanding");
                ApplyVisualState();
                popup.style.display = DisplayStyle.None;
                if (pointerId >= 0 && this.HasPointerCapture(pointerId))
                    this.ReleasePointer(pointerId);
                if (committed)
                    ESTrackViewWindow.window?.EndClipResize(this, startDuration);
            }

            m_ActivePointerId = -1;
        }

        public void CancelPointerInteraction(bool commit)
        {
            if (!isDragging && !isExpanding)
                return;

            if (commit)
            {
                FinishPointerInteraction(m_ActivePointerId);
                return;
            }

            int pointerId = m_ActivePointerId;
            isDragging = false;
            isExpanding = false;
            m_GestureActivated = false;
            RemoveFromClassList("dragging");
            RemoveFromClassList("expanding");
            ApplyVisualState();
            if (popup != null)
                popup.style.display = DisplayStyle.None;
            if (pointerId >= 0 && this.HasPointerCapture(pointerId))
                this.ReleasePointer(pointerId);
            m_ActivePointerId = -1;
        }

        private void BeginRename()
        {
            if (trackClip == null || isRenaming)
                return;

            isRenaming = true;
            ESTrackViewWindow.window?.SetRenamingClip(this);
            isDragging = false;
            isExpanding = false;
            m_GestureActivated = false;
            RemoveFromClassList("dragging");
            RemoveFromClassList("expanding");
            popup.style.display = DisplayStyle.None;

            m_ClipNameLabel.style.display = DisplayStyle.None;
            if (m_ClipShortLabel != null)
                m_ClipShortLabel.style.display = DisplayStyle.None;
            if (m_ClipIcon != null)
                m_ClipIcon.style.display = DisplayStyle.None;
            ApplyVisualState();

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
                m_RenameField.style.color = ESTrackViewTheme.Text;
                m_RenameField.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
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

        internal void BeginRenameFromContext()
        {
            BeginRename();
        }

        internal void CommitRenameBeforeLayoutMutation()
        {
            CommitRename();
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
                if (ESTrackViewWindow.window != null)
                {
                    ESTrackViewWindow.window.ApplyAuthoringChange(
                        trackClip,
                        ESTrackAuthoringChangeFlags.ValueEdit,
                        "重命名片段");
                }
                else
                {
                    ESTrackViewWindowHelper.SaveContainerDisplayChanges("重命名片段");
                }
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

            RefreshClipIcon();
            UpdateNodeView();
            ApplyVisualState();
            UpdateCompactMetadataVisibility(style.width.value.value);
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
                if (!m_GestureActivated)
                {
                    Vector2 currentPosition = evt.position;
                    if ((currentPosition - m_PointerDownPosition).sqrMagnitude < PointerGestureThreshold * PointerGestureThreshold)
                        return;

                    m_GestureActivated = true;
                    ESTrackViewWindow.window?.BeginClipGroupDrag(this);
                    AddToClassList("dragging");
                    ApplyVisualState();
                    popup.style.display = DisplayStyle.Flex;
                }

                // 获取鼠标在轨道上的相对位置
                var mousePos = evt.position;

                // 计算相对于轨道的x位置
                this.style.left = Mathf.Max(mousePos.x - offsetPOSDragLeft, 0);
                //Debug.Log("拖动位置：" + this.resolvedStyle.left);
                // 计算对应的时间
                MatchTimeFromDynamicPos();
                ESTrackViewWindow.window?.ApplyClipGroupDrag(this, StartTime);
            }
            else if (isExpanding)
            {
                if (!m_GestureActivated)
                {
                    Vector2 currentPosition = evt.position;
                    if ((currentPosition - m_PointerDownPosition).sqrMagnitude < PointerGestureThreshold * PointerGestureThreshold)
                        return;

                    m_GestureActivated = true;
                    ESTrackViewWindow.window?.BeginClipResize(this);
                    AddToClassList("expanding");
                    ApplyVisualState();
                    popup.style.display = DisplayStyle.Flex;
                }

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
            ApplyWidthIfChanged(w);
            StartTime = newStartTime;
            AdjustFontToFit();
            UpdateCompactMetadataVisibility(w);
            popLabel.text = $"[{StartTime:F2}s -- {StartTime + Duration:F2}]";

        }


        #endregion
        public static float Cache_pixelsPerSecond = 100f;
        public static float Cahce_ShowStart = 0f;
        public static float Cache_ShowEnd = float.PositiveInfinity;
        public void SetTimeScaleAndStartShow(float pixelsPerSecond, float ShowStart)
        {
            SetTimeScaleAndStartShowVisible(pixelsPerSecond, ShowStart, float.PositiveInfinity);
        }

        public void SetTimeScaleAndStartShowVisible(float pixelsPerSecond, float showStart, float showEnd)
        {
            Cache_pixelsPerSecond = pixelsPerSecond;
            Cahce_ShowStart = showStart;
            Cache_ShowEnd = showEnd;
            //Debug.Log("TRUE"+Cache_pixelsPerSecond);
            // 根据时间尺度和持续时间设置节点宽度
            var w = Duration * pixelsPerSecond;
            float clipEnd = StartTime + Mathf.Max(0f, Duration);
            bool forceVisible = isDragging || isExpanding || isRenaming;
            bool visible = forceVisible || clipEnd >= showStart && StartTime <= showEnd;
            ApplyDisplayIfChanged(visible ? DisplayStyle.Flex : DisplayStyle.None);
            if (!visible)
                return;

            var left = (StartTime - showStart) * pixelsPerSecond;
            // Debug.Log("WW"+w+" LL"+left+" START "+ShowStart);
            bool widthChanged = ApplyWidthIfChanged(w);
            ApplyLeftIfChanged(left);
            if (!isRenaming && m_ClipIcon != null)
                m_ClipIcon.style.display = w >= 44f ? DisplayStyle.Flex : DisplayStyle.None;
            if (widthChanged)
                AdjustFontToFit();
            UpdateCompactMetadataVisibility(w);
        }

        public void SetTimeScaleAndStartShowCache()
        {
            SetTimeScaleAndStartShowVisible(Cache_pixelsPerSecond, Cahce_ShowStart, Cache_ShowEnd);
        }

        private bool ApplyLeftIfChanged(float left)
        {
            if (!float.IsNaN(m_LastAppliedLeft) && Mathf.Abs(m_LastAppliedLeft - left) < StylePixelEpsilon)
                return false;

            style.left = left;
            m_LastAppliedLeft = left;
            return true;
        }

        private bool ApplyWidthIfChanged(float width)
        {
            if (!float.IsNaN(m_LastAppliedWidth) && Mathf.Abs(m_LastAppliedWidth - width) < StylePixelEpsilon)
                return false;

            style.width = width;
            m_LastAppliedWidth = width;
            return true;
        }

        private bool ApplyDisplayIfChanged(DisplayStyle display)
        {
            if (m_LastAppliedDisplay == display)
                return false;

            style.display = display;
            m_LastAppliedDisplay = display;
            return true;
        }

        public void ForceDisplayState(DisplayStyle display)
        {
            style.display = display;
            m_LastAppliedDisplay = display;
        }

        public void SetClipColor(Color color)
        {
            m_LastTrackAccentColor = ESTrackViewTheme.SanitizeAccent(color);
            ApplyVisualState();
        }

        public void SetValidationWarning(string warning)
        {
            m_HasValidationWarning = !string.IsNullOrWhiteSpace(warning);
            m_ValidationWarning = warning;
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
            if (ESTrackViewWindow.window != null)
            {
                ESTrackViewWindow.window.ApplyAuthoringChange(
                    trackClip,
                    ESTrackAuthoringChangeFlags.ValueEdit,
                    trackClip.Enabled ? "启用片段" : "禁用片段");
            }
            else
            {
                RefreshEnabledVisual();
                ESTrackViewWindowHelper.SaveContainerDisplayChanges();
                if (ESTrackViewWindow.Sequence != null)
                    SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
            }
        }

        public void RefreshEnabledVisual()
        {
            bool enabled = trackClip == null || trackClip.Enabled;
            tooltip = m_HasValidationWarning
                ? ClipName + "\n预警：" + m_ValidationWarning
                : enabled ? ClipName : ClipName + "（已禁用）";

            if (m_ClipStateBadge != null)
            {
                m_ClipStateBadge.text = m_HasValidationWarning ? "警" : "禁";
                m_ClipStateBadge.tooltip = m_HasValidationWarning
                    ? m_ValidationWarning
                    : "片段已禁用";
            }

            ApplyVisualState();
            UpdateCompactMetadataVisibility(style.width.value.value);
        }

        private void ApplyVisualState()
        {
            // 单一渲染顺序：业务底色 → 警告/禁用 → 活跃 → 选择/悬停 → 独立编辑焦点。
            // 后一层只负责自己的视觉通道，不能缓存并回滚前一层颜色。
            bool enabled = trackClip == null || trackClip.Enabled;
            bool showActive = m_IsActive && enabled && !m_HasValidationWarning;
            Color accent = m_LastTrackAccentColor.a > 0f
                ? m_LastTrackAccentColor
                : ESTrackViewTheme.Accent;
            Color background = m_HasValidationWarning
                ? ESTrackViewTheme.ClipWarningSurface(enabled)
                : enabled
                    ? ESTrackViewTheme.ClipSurface(accent)
                    : ESTrackViewTheme.ClipDisabledSurface(accent);

            if (showActive)
                background = ESTrackViewTheme.Blend(background, ESTrackViewTheme.ActiveAccent, 0.10f);
            if (m_GestureActivated && isDragging)
                background = ESTrackViewTheme.ClipDraggingSurface(background);
            else if (m_GestureActivated && isExpanding)
                background = ESTrackViewTheme.ClipResizingSurface(background);

            Color sideAccent = m_HasValidationWarning
                ? ESTrackViewTheme.StatusWarning
                : enabled ? accent : ESTrackViewTheme.SubduedAccent(accent);
            Color edge = m_HasValidationWarning
                ? ESTrackViewTheme.WithAlpha(ESTrackViewTheme.StatusWarning, enabled ? 0.88f : 0.46f)
                : ESTrackViewTheme.Divider;

            style.opacity = enabled ? 1f : 0.80f;
            style.backgroundColor = background;
            style.borderLeftWidth = m_HasValidationWarning ? 4 : 3;
            style.borderRightWidth = m_HasValidationWarning ? 2 : 1;
            style.borderTopWidth = m_HasValidationWarning || showActive ? 2 : 1;
            style.borderBottomWidth = m_HasValidationWarning || showActive ? 2 : 1;
            style.borderLeftColor = sideAccent;
            style.borderRightColor = edge;
            style.borderTopColor = m_HasValidationWarning
                ? edge
                : showActive ? ESTrackViewTheme.ActiveAccent : ESTrackViewTheme.Divider;
            style.borderBottomColor = m_HasValidationWarning
                ? edge
                : showActive
                    ? ESTrackViewTheme.WithAlpha(ESTrackViewTheme.ActiveAccent, 0.72f)
                    : ESTrackViewTheme.Divider;

            Color textColor = m_HasValidationWarning && enabled
                ? ESTrackViewTheme.StatusWarning
                : !enabled
                    ? ESTrackViewTheme.MutedText
                    : m_IsSelected ? ESTrackViewTheme.SelectedText : ESTrackViewTheme.Text;
            if (m_ClipNameLabel != null)
                m_ClipNameLabel.style.color = textColor;
            if (m_ClipShortLabel != null)
                m_ClipShortLabel.style.color = textColor;

            if (m_ClipContent != null)
                m_ClipContent.style.backgroundColor = !m_IsSelected && m_IsHovering
                    ? ESTrackViewTheme.HoverOverlay
                    : ESTrackViewTheme.Transparent;

            if (m_SelectionFrame != null)
            {
                m_SelectionFrame.style.display = m_IsSelected ? DisplayStyle.Flex : DisplayStyle.None;
                Color selectionColor = ESTrackViewTheme.SelectionFrame(m_IsPrimarySelection);
                m_SelectionFrame.style.borderLeftColor = selectionColor;
                m_SelectionFrame.style.borderRightColor = selectionColor;
                m_SelectionFrame.style.borderTopColor = selectionColor;
                m_SelectionFrame.style.borderBottomColor = selectionColor;
                m_SelectionFrame.style.backgroundColor = ESTrackViewTheme.SelectionFill(m_IsPrimarySelection);
                if (m_IsSelected)
                    m_SelectionFrame.BringToFront();
            }

            if (m_EditingFocusFrame != null)
            {
                m_EditingFocusFrame.style.display = m_IsFocusedEditing ? DisplayStyle.Flex : DisplayStyle.None;
                m_EditingFocusFrame.style.borderLeftColor = ESTrackViewTheme.EditingAccent;
                m_EditingFocusFrame.style.borderRightColor = ESTrackViewTheme.EditingAccent;
                m_EditingFocusFrame.style.borderTopColor = ESTrackViewTheme.EditingAccent;
                m_EditingFocusFrame.style.borderBottomColor = ESTrackViewTheme.EditingAccent;
                m_EditingFocusFrame.style.backgroundColor = ESTrackViewTheme.WithAlpha(ESTrackViewTheme.EditingAccent, 0.07f);
                if (m_IsFocusedEditing)
                    m_EditingFocusFrame.BringToFront();
            }

            if (m_ClipStateBadge != null)
            {
                float currentWidth = resolvedStyle.width > 0f
                    ? resolvedStyle.width
                    : style.width.value.value;
                bool showBadge = !isRenaming
                                 && currentWidth >= 54f
                                 && (!enabled || m_HasValidationWarning);
                Color status = m_HasValidationWarning
                    ? ESTrackViewTheme.StatusWarning
                    : ESTrackViewTheme.StatusReadOnly;
                m_ClipStateBadge.style.display = showBadge ? DisplayStyle.Flex : DisplayStyle.None;
                m_ClipStateBadge.style.color = status;
                m_ClipStateBadge.style.backgroundColor = ESTrackViewTheme.StateBadgeSurface(status);
            }

            if (m_ClipIcon != null)
            {
                m_ClipIcon.style.backgroundColor = ESTrackViewTheme.IconBackground(accent);
                m_ClipIcon.style.opacity = enabled ? 1f : 0.42f;
            }

            if (m_ResizeHandle != null)
                m_ResizeHandle.style.backgroundColor = ESTrackViewTheme.WithAlpha(
                    enabled ? accent : ESTrackViewTheme.StatusReadOnly,
                    m_IsHovering || m_IsSelected ? 0.34f : 0.18f);

            if (m_RenameField != null)
            {
                m_RenameField.style.color = ESTrackViewTheme.Text;
                m_RenameField.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            }

            if (popup != null)
                popup.style.backgroundColor = ESTrackViewTheme.WithAlpha(ESTrackViewTheme.SecondarySurface, 0.96f);
            if (popLabel != null)
                popLabel.style.color = ESTrackViewTheme.Text;

            m_ResizeHandle?.BringToFront();
        }

        public void RefreshClipIcon()
        {
            if (m_ClipIcon == null)
                return;

            Texture2D icon = ESTrackViewIconUtility.ResolveClipIcon(trackClip);
            if (icon != null)
                m_ClipIcon.style.backgroundImage = icon;

            m_ClipIcon.style.backgroundColor = ESTrackViewTheme.IconBackground(m_LastTrackAccentColor);
            m_ClipIcon.tooltip = trackClip != null ? trackClip.DisplayName : "片段";
            if (!isRenaming)
                m_ClipIcon.style.display = resolvedStyle.width >= 44f ? DisplayStyle.Flex : DisplayStyle.None;
        }
        public void HighlightIfActive(float currentTime)
        {
            SetActiveHighlight(currentTime >= StartTime && currentTime <= StartTime + Duration);
        }

        public void SetActiveHighlight(bool active)
        {
            if (m_IsActive == active)
                return;

            m_IsActive = active;
            ApplyVisualState();
        }

        internal void RefreshTheme()
        {
            ApplyVisualState();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            m_IsHovering = true;
            RefreshInteractionVisual();
        }

        public void UpdateNodeView()
        {
            string clipName = ClipName;
            if (m_LastAppliedClipName == clipName)
                return;

            m_LastAppliedClipName = clipName;
            m_ClipNameLabel.text = clipName;
            if (m_ClipShortLabel != null)
                m_ClipShortLabel.text = BuildShortClipName(clipName);
            if (!m_HasValidationWarning)
                tooltip = clipName;
            AdjustFontToFit();
            UpdateCompactMetadataVisibility(style.width.value.value);
        }

        private static string BuildShortClipName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "片";

            return name.Length <= 2 ? name : name.Substring(0, 2);
        }

        private void UpdateCompactMetadataVisibility(float width)
        {
            if (isRenaming)
            {
                if (m_ClipNameLabel != null)
                    m_ClipNameLabel.style.display = DisplayStyle.None;
                if (m_ClipShortLabel != null)
                    m_ClipShortLabel.style.display = DisplayStyle.None;
                if (m_ClipIcon != null)
                    m_ClipIcon.style.display = DisplayStyle.None;
                if (m_ClipStateBadge != null)
                    m_ClipStateBadge.style.display = DisplayStyle.None;
                return;
            }

            bool compact = width > 0f && width < 64f;
            if (m_ClipNameLabel != null)
                m_ClipNameLabel.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            if (m_ClipShortLabel != null)
                m_ClipShortLabel.style.display = compact ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_ClipIcon != null)
                m_ClipIcon.style.display = width >= 44f ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_ClipStateBadge != null)
            {
                bool enabled = trackClip == null || trackClip.Enabled;
                bool showBadge = width >= 54f && (!enabled || m_HasValidationWarning);
                m_ClipStateBadge.style.display = showBadge ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void AdjustFontToFit()
        {
            float availableWidth = Mathf.Max(0f, style.width.value.value - 30f);
            if (string.IsNullOrEmpty(m_ClipNameLabel.text) || availableWidth <= 0f)
            {
                m_ClipNameLabel.style.fontSize = 10f;
                return;
            }

            float targetSize = Mathf.Lerp(9f, 11.5f, Mathf.InverseLerp(64f, 180f, availableWidth));
            m_ClipNameLabel.style.fontSize = targetSize;
        }

    }
}
