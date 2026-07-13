
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using RootMotion.Demos;
using UnityEngine;
using UnityEngine.UIElements;
namespace ES
{
    public class ESEditorTrackItem : UnityEngine.UIElements.VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESEditorTrackItem, UxmlTraits> { }
        private const float TrackRowHeight = 40f;
        private VisualElement m_LeftPanel;
        private VisualElement m_RightPanel;
        private VisualElement m_Header;
        private VisualElement m_Icon;
        private VisualElement m_AccentBar;
        private Label m_TrackNameLabel;
        private TextField m_RenameField;
        private VisualElement m_TrackClipsContainer;
        private bool m_IsSortDragging;
        private bool m_CanStartSortDrag;
        private Vector2 m_SortDragStartPosition;
        private bool m_IsRenaming;


        #region  运行时
        public ITrackItem item;
        public bool IsProtectedBasicTrack { get; private set; }


        #endregion
        // 控制按钮
        private Button m_EnableButton;
        private Button m_MuteButton;
        private Button m_LockButton;
        private Button m_DeleteButton;
        private Button m_CollapseButton;

        public Vector2 recordLocalClipsMousePos;

        public List<ESEditorTrackClip> TrackClips = new List<ESEditorTrackClip>();
        public ESEditorTrackItem()
        {
            CreateUIStructure();
            // 应用初始状态
            UpdateMuteButton();
            UpdateLockButton();


        }

        public ESEditorTrackItem InitWithItem(ITrackItem trackItem, bool isProtectedBasicTrack = false)
        {
            item = trackItem;
            IsProtectedBasicTrack = isProtectedBasicTrack;

            UpdateTrackMessage();
            UpdateTrackColor();
            UpdateBasicTrackStyle();
            UpdateTrackEnabledVisual();
            UpdateNodeMatchAndForeachUpdate(true);
            //Debug.Log("初始化轨道项：" + item.GetType() + item.DisplayName);
            return this;
        }
        public void UpdateWhenEdit()
        {
            UpdateTrackMessage();
        }
        private void UpdateTrackMessage()
        {
            m_TrackNameLabel.text = item.DisplayName;
            UpdateTrackIcon();
        }

        private void CreateUIStructure()
        {
            // 整个轨道项采用水平布局
            style.flexDirection = FlexDirection.Row;
            style.flexShrink = 0;
            style.height = TrackRowHeight;
            style.minHeight = TrackRowHeight;
            style.maxHeight = TrackRowHeight;

            // 右侧面板 - 可扩展，显示轨道节点
            CreateRightPanel();
            // 左侧面板 - 固定宽度，显示轨道信息
            CreateLeftPanel();

            BindClipsArea();
            RegisterCallback<PointerDownEvent>(OnTrackPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnTrackPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnTrackPointerUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnTrackPointerCancel, TrickleDown.TrickleDown);


            // 分隔线
            var separator = new VisualElement
            {
                name = "track-separator",
                style =
            {
                width = 1,
                backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f)
            }
            };
            Add(separator);
        }

        private void BindClipsArea()
        {
            m_TrackClipsContainer.RegisterCallback<ContextClickEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    recordLocalClipsMousePos = evt.localMousePosition;
                    //  Debug.Log("右键点击轨道节点区域");
                    ESTrackViewWindow.window.ShowMenu_SelectTrackAndAddTrack(this);
                }

            });

            m_TrackClipsContainer.RegisterCallback<WheelEvent>(evt =>
            {
                ESTrackViewWindow.window.OnRightPanelWheel(evt);
                evt.StopPropagation(); // 节点处理后停止传播
            }, TrickleDown.TrickleDown);

        }

        private void CreateLeftPanel()
        {
            m_LeftPanel = new VisualElement
            {
                name = "track-left-panel",
                style =
            {
                 position= Position.Absolute,
                 left=0,
                width =  ESTrackViewWindow.LeftTrackPixel,
                minWidth = ESTrackViewWindow.LeftTrackPixel,
                maxWidth = ESTrackViewWindow.LeftTrackPixel,
                height = TrackRowHeight,
                minHeight = TrackRowHeight,
                maxHeight = TrackRowHeight,
                flexDirection = FlexDirection.Column,
                paddingTop = 4,
                paddingBottom = 4,
                paddingLeft = 8,
                paddingRight = 8,
                backgroundColor = new StyleColor(new Color(0.074f, 0.081f, 0.092f, 1f)),
                borderRightWidth = 1,
                borderRightColor = new Color(0.22f, 0.24f, 0.27f, 1f)
            }
            };

            // 轨道标题栏
            CreateHeader();

            // 添加控制按钮区域
            //   CreateControlButtons();

            Add(m_LeftPanel);
        }
        private void CreateHeader()
        {
            m_Header = new VisualElement
            {
                name = "track-header",
                style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                height = 24,
                marginBottom = 4,
                paddingLeft = 2,
                paddingRight = 2
            }
            };

            m_AccentBar = new VisualElement
            {
                name = "track-accent-bar",
                style =
                {
                    width = 3,
                    height = 15,
                    marginRight = 7,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    backgroundColor = new Color(0.4f, 0.45f, 0.52f, 1f)
                }
            };
            m_Header.Add(m_AccentBar);

            m_EnableButton = new Button(ToggleTrackEnabled)
            {
                name = "track-enable-button",
                text = "启用",
                tooltip = "启用/禁用当前轨道。禁用后运行时烘焙、运行和编辑器预览都会跳过这条轨道。",
                style =
                {
                    width = 34,
                    minWidth = 34,
                    height = 18,
                    marginRight = 6,
                    paddingLeft = 0,
                    paddingRight = 0,
                    fontSize = 10,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    borderTopWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1
                }
            };
            m_Header.Add(m_EnableButton);

            // 折叠/展开按钮
            // m_CollapseButton = new Button(ToggleCollapse)
            // {
            //     name = "collapse-button",
            //     text = "▼",
            //     style =
            //     {
            //         width = 20,
            //         height = 20,
            //         marginRight = 4,
            //         fontSize = 10
            //     }
            // };
            // m_CollapseButton.AddToClassList("track-control-button");
            // m_Header.Add(m_CollapseButton);

            // 轨道图标
            m_Icon = new VisualElement
            {
                name = "track-icon",
                style =
            {
                width = 14,
                height = 14,
                minWidth = 14,
                marginRight = 7,
                borderTopLeftRadius = 2,
                borderTopRightRadius = 2,
                borderBottomLeftRadius = 2,
                borderBottomRightRadius = 2,
                //backgroundColor = m_TrackColor
            }
            };
            m_Icon.AddToClassList("icon-default");
            m_Header.Add(m_Icon);

            // 轨道名称
            m_TrackNameLabel = new Label("轨道")
            {
                name = "track-name",
                style =
            {
                flexGrow = 1,
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 11,
                color = new Color(0.74f, 0.8f, 0.87f, 1f),
                unityTextAlign = TextAnchor.MiddleLeft,
                overflow = Overflow.Hidden,
                textOverflow = TextOverflow.Ellipsis,
                whiteSpace = WhiteSpace.NoWrap
            }
            };
            m_Header.Add(m_TrackNameLabel);

            m_LeftPanel.Add(m_Header);

            m_LeftPanel.RegisterCallback<ContextClickEvent>(evt =>
            {
                recordLocalClipsMousePos = new Vector2(0f, evt.localMousePosition.y);
                ESTrackViewWindow.window?.ShowMenu_SelectTrackAndAddTrack(this);
                evt.StopPropagation();
            });
        }
        private void CreateRightPanel()
        {
            m_RightPanel = new VisualElement
            {
                name = "track-right-panel",
                style =
            {

                flexGrow = 1,
                flexDirection = FlexDirection.Column,
                backgroundColor = new StyleColor(new Color(0.052f, 0.057f, 0.066f, 1f)),
                height = TrackRowHeight,
                minHeight = TrackRowHeight,
                maxHeight = TrackRowHeight
            }
            };
            // 轨道节点容器
            m_TrackClipsContainer = new VisualElement
            {
                name = "track-nodes-container",
                focusable = true,
                style =
            {
                  left =  ESTrackViewWindow.LeftTrackPixel,
                width=9999,
                minWidth=9999,
                 position= Position.Absolute,
                flexGrow = 1,
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                height = TrackRowHeight,
                minHeight = TrackRowHeight,
                maxHeight = TrackRowHeight,
                 //overflow=  Overflow.Hidden,
                flexShrink = 0  ,       // ✅ 不允许收缩
                 flexBasis = 0,
            }
            };
            // 轨道节点容器 - 修改为相对定位
            m_RightPanel.Add(m_TrackClipsContainer);
            Add(m_RightPanel);
        }
        private void UpdateTrackColor()
        {
            Color accent = ResolveTrackAccentColor();
            if (m_Icon != null)
            {
                m_Icon.style.backgroundColor = ESTrackViewIconUtility.ResolveIconBackColor(item != null ? item.GetType() : null);
            }
            if (m_AccentBar != null)
            {
                m_AccentBar.style.backgroundColor = accent;
            }
            if (m_TrackClipsContainer != null)
            {
                m_TrackClipsContainer.style.backgroundColor = new Color(
                    0.054f + accent.r * 0.035f,
                    0.058f + accent.g * 0.035f,
                    0.066f + accent.b * 0.035f,
                    0.72f);
                m_TrackClipsContainer.style.borderBottomWidth = 1;
                m_TrackClipsContainer.style.borderBottomColor = new Color(0.105f, 0.115f, 0.135f, 0.66f);
            }

            UpdateTrackEnabledVisual();
        }

        private void ToggleTrackEnabled()
        {
            if (item == null)
                return;

            UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
            if (undoTarget != null)
                UnityEditor.Undo.RecordObject(undoTarget, item.Enabled ? "禁用轨道" : "启用轨道");

            item.Enabled = !item.Enabled;
            UpdateTrackEnabledVisual();
            ESTrackViewWindowHelper.SaveContainerChanges();
            ESTrackViewWindow.window?.RebuildActivePreviewPlayer();
        }

        private void UpdateTrackEnabledVisual()
        {
            bool enabled = item == null || item.Enabled;
            Color accent = ResolveTrackAccentColor();

            if (m_EnableButton != null)
            {
                m_EnableButton.text = enabled ? "启用" : "禁用";
                m_EnableButton.tooltip = enabled
                    ? "当前轨道已启用。点击后禁用，运行时烘焙、运行和编辑器预览都会跳过这条轨道。"
                    : "当前轨道已禁用。点击后重新启用。";
                m_EnableButton.style.color = enabled
                    ? new Color(0.78f, 1f, 0.78f, 1f)
                    : new Color(1f, 0.72f, 0.52f, 1f);
                m_EnableButton.style.backgroundColor = enabled
                    ? new Color(0.08f, 0.18f, 0.12f, 0.92f)
                    : new Color(0.2f, 0.105f, 0.075f, 0.95f);
                Color borderColor = enabled
                    ? new Color(0.25f, 0.62f, 0.34f, 0.9f)
                    : new Color(0.72f, 0.35f, 0.2f, 0.95f);
                m_EnableButton.style.borderTopColor = borderColor;
                m_EnableButton.style.borderRightColor = borderColor;
                m_EnableButton.style.borderBottomColor = borderColor;
                m_EnableButton.style.borderLeftColor = borderColor;
            }

            if (m_TrackNameLabel != null)
            {
                m_TrackNameLabel.style.color = enabled
                    ? new Color(0.74f, 0.8f, 0.87f, 1f)
                    : new Color(0.48f, 0.5f, 0.54f, 1f);
            }

            if (m_Icon != null)
                m_Icon.style.opacity = enabled ? 1f : 0.34f;

            if (m_AccentBar != null)
            {
                m_AccentBar.style.opacity = enabled ? 1f : 0.38f;
                m_AccentBar.style.backgroundColor = enabled ? accent : new Color(0.26f, 0.27f, 0.3f, 0.8f);
            }

            if (m_TrackClipsContainer != null)
                m_TrackClipsContainer.style.opacity = enabled ? 1f : 0.42f;
        }

        private void OnTrackPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            if (IsPointerOnHeaderButton(evt.position))
                return;

            if (m_IsRenaming)
                return;

            ESTrackViewWindow.window?.SelectTrack(this);
            if (evt.clickCount >= 2 && IsPointerInLeftPanel(evt.position))
            {
                BeginRename();
                evt.StopPropagation();
                return;
            }

            if (IsProtectedBasicTrack || !IsPointerInLeftPanel(evt.position))
                return;

            m_CanStartSortDrag = true;
            m_SortDragStartPosition = evt.position;
            this.CapturePointer(evt.pointerId);
        }

        private void OnTrackPointerMove(PointerMoveEvent evt)
        {
            if (!m_CanStartSortDrag && !m_IsSortDragging)
                return;

            if (!m_IsSortDragging)
            {
                if (Vector2.Distance(evt.position, m_SortDragStartPosition) < 5f)
                    return;

                m_IsSortDragging = true;
                ESTrackViewWindow.window?.BeginTrackSortDrag(this);
            }

            ESTrackViewWindow.window?.UpdateTrackSortDrag(evt.position);
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void OnTrackPointerUp(PointerUpEvent evt)
        {
            if (!m_CanStartSortDrag && !m_IsSortDragging)
                return;

            bool commit = m_IsSortDragging;
            m_CanStartSortDrag = false;
            m_IsSortDragging = false;
            if (this.HasPointerCapture(evt.pointerId))
                this.ReleasePointer(evt.pointerId);

            ESTrackViewWindow.window?.EndTrackSortDrag(commit);
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void OnTrackPointerCancel(PointerCancelEvent evt)
        {
            if (!m_CanStartSortDrag && !m_IsSortDragging)
                return;

            m_CanStartSortDrag = false;
            m_IsSortDragging = false;
            if (this.HasPointerCapture(evt.pointerId))
                this.ReleasePointer(evt.pointerId);

            ESTrackViewWindow.window?.EndTrackSortDrag(false);
        }

        private bool IsPointerInLeftPanel(Vector2 worldPosition)
        {
            return m_LeftPanel != null && m_LeftPanel.worldBound.Contains(worldPosition);
        }

        private bool IsPointerOnHeaderButton(Vector2 worldPosition)
        {
            return m_EnableButton != null && m_EnableButton.worldBound.Contains(worldPosition);
        }

        private void UpdateBasicTrackStyle()
        {
            if (!IsProtectedBasicTrack)
                return;

            string basicTrackTooltip = "基础轨道：不可删除，不参与轨道拖拽排序。扩展轨道只能排在基础轨道之后。";
            tooltip = string.Empty;
            style.borderLeftWidth = 1;
            style.borderTopWidth = 0;
            style.borderBottomWidth = 0;
            style.borderLeftColor = new Color(0.56f, 0.5f, 0.28f, 0.55f);
            style.borderTopColor = Color.clear;
            style.borderBottomColor = Color.clear;

            if (m_LeftPanel != null)
            {
                m_LeftPanel.style.backgroundColor = new Color(0.086f, 0.092f, 0.102f, 1f);
                m_LeftPanel.style.borderTopWidth = 0;
                m_LeftPanel.style.borderBottomWidth = 0;
                m_LeftPanel.style.borderLeftWidth = 2;
                m_LeftPanel.style.borderLeftColor = new Color(0.54f, 0.49f, 0.28f, 0.65f);
                m_LeftPanel.style.borderTopColor = Color.clear;
                m_LeftPanel.style.borderBottomColor = Color.clear;
            }

            if (m_Header != null)
                m_Header.tooltip = basicTrackTooltip;

            if (m_LeftPanel != null)
                m_LeftPanel.tooltip = basicTrackTooltip;

            if (m_TrackClipsContainer != null)
                m_TrackClipsContainer.tooltip = string.Empty;

            if (m_RightPanel != null)
                m_RightPanel.tooltip = string.Empty;
        }

        public void SetSelected(bool selected)
        {
            if (m_LeftPanel == null)
                return;

            if (selected)
            {
                m_LeftPanel.style.backgroundColor = new Color(0.105f, 0.125f, 0.15f, 1f);
                m_LeftPanel.style.borderLeftWidth = 3;
                m_LeftPanel.style.borderLeftColor = new Color(0.38f, 0.58f, 0.78f, 0.9f);
                return;
            }

            m_LeftPanel.style.backgroundColor = IsProtectedBasicTrack
                ? new Color(0.086f, 0.092f, 0.102f, 1f)
                : new Color(0.074f, 0.081f, 0.092f, 1f);
            m_LeftPanel.style.borderLeftWidth = IsProtectedBasicTrack ? 2 : 0;
            m_LeftPanel.style.borderLeftColor = IsProtectedBasicTrack
                ? new Color(0.54f, 0.49f, 0.28f, 0.65f)
                : Color.clear;
        }

        public void SetSortDragging(bool dragging)
        {
            if (m_LeftPanel == null)
                return;

            m_LeftPanel.style.opacity = dragging ? 0.72f : 1f;
        }

        private void BeginRename()
        {
            if (item == null || m_IsRenaming)
                return;

            m_IsRenaming = true;
            m_CanStartSortDrag = false;
            m_IsSortDragging = false;
            ESTrackViewWindow.window?.SetRenamingTrack(this);

            m_TrackNameLabel.style.display = DisplayStyle.None;
            if (m_RenameField == null)
            {
                m_RenameField = new TextField
                {
                    isDelayed = false
                };
                m_RenameField.selectAllOnFocus = false;
                m_RenameField.selectAllOnMouseUp = false;
                m_RenameField.style.position = Position.Absolute;
                m_RenameField.style.left = 30;
                m_RenameField.style.right = 6;
                m_RenameField.style.top = 3;
                m_RenameField.style.height = 22;
                m_RenameField.style.fontSize = 11;
                m_RenameField.style.color = new Color(0.92f, 0.95f, 1f, 1f);
                m_RenameField.style.backgroundColor = new Color(0.075f, 0.085f, 0.1f, 1f);
                m_RenameField.tooltip = "正在重命名轨道：Enter 确认，Esc 取消；点击输入框外保存";
                m_RenameField.RegisterCallback<KeyDownEvent>(OnRenameKeyDown);
                m_LeftPanel.Add(m_RenameField);
            }

            m_RenameField.SetValueWithoutNotify(item.DisplayName);
            m_RenameField.style.display = DisplayStyle.Flex;
            schedule.Execute(() =>
            {
                if (!m_IsRenaming || m_RenameField == null)
                    return;

                m_RenameField.Focus();
                m_RenameField.SelectAll();
            }).ExecuteLater(0);
        }

        public void CommitRenameIfPointerOutsideRenameField(Vector2 worldPosition)
        {
            if (!m_IsRenaming || m_RenameField == null)
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
            if (!m_IsRenaming)
                return;

            string newName = m_RenameField != null ? m_RenameField.value : item.DisplayName;
            newName = string.IsNullOrWhiteSpace(newName) ? item.DisplayName : newName.Trim();
            if (item != null && item.DisplayName != newName)
            {
                UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
                if (undoTarget != null)
                    UnityEditor.Undo.RecordObject(undoTarget, "重命名轨道");

                item.DisplayName = newName;
                m_TrackNameLabel.text = newName;
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
            m_IsRenaming = false;
            ESTrackViewWindow.window?.ClearRenamingTrack(this);
            if (m_RenameField != null)
                m_RenameField.style.display = DisplayStyle.None;

            m_TrackNameLabel.style.display = DisplayStyle.Flex;
            UpdateTrackMessage();
        }

        private void UpdateTrackIcon()
        {
            if (m_Icon == null)
                return;

            Texture2D icon = ESTrackViewIconUtility.ResolveTrackIcon(item);
            if (icon != null)
                m_Icon.style.backgroundImage = icon;

            m_Icon.tooltip = item != null ? item.DisplayName : "轨道";
        }

        private Color ResolveTrackAccentColor()
        {
            if (item == null)
                return new Color(0.42f, 0.46f, 0.52f, 1f);

            Color color = item.ItemBGColor;
            return new Color(
                Mathf.Clamp01(color.r * 0.85f + 0.08f),
                Mathf.Clamp01(color.g * 0.85f + 0.08f),
                Mathf.Clamp01(color.b * 0.85f + 0.08f),
                0.82f);
        }
        private void UpdateMuteButton()
        {
            if (m_MuteButton != null)
            {
                // m_MuteButton.EnableInClassList("muted", m_IsMuted);
                // m_RightPanel.EnableInClassList("muted", m_IsMuted);
            }
        }
        private void UpdateLockButton()
        {
            if (m_LockButton != null)
            {
                // m_LockButton.EnableInClassList("locked", m_IsLocked);
                // m_TrackNodesContainer.SetEnabled(!m_IsLocked);
            }
        }
        public ESEditorTrackClip AddClipTEST(string name, float startTime, float duration, object data = null)
        {
            var node = new ESEditorTrackClip(null, name, startTime, duration, data)
            {
                style =
            {
                marginLeft = 2,
                marginRight = 2
            }
            };

            m_TrackClipsContainer.Add(node);
            TrackClips.Add(node);

            //  OnNodeAdded?.Invoke(this, node);
            return node;
        }



        public ESEditorTrackClip AddClip(ITrackClip clip, bool onlyUpdate = true)
        {
            if (clip == null)
            {
                Debug.LogError("尝试添加空的轨道片段");
                return null;
            }
            //如果只是 Update 的话 就可以直接加  如果 是新建的话 就需要新加入
            if (onlyUpdate || item.TryAddTrackClip(clip))
            {
                return CreateNewEditorClipByNormalClip(clip);
            }

            return null;

            //  OnNodeAdded?.Invoke(this, node);

        }
        private ESEditorTrackClip CreateNewEditorClipByNormalClip(ITrackClip clip)
        {
            var clipEditor = new ESEditorTrackClip(clip, clip.DisplayName, clip.StartTime, clip.DurationTime, clip)
            {
                style =
            {
                marginLeft = 2,
                marginRight = 2
            }
            };
            clipEditor.SetClipColor(ResolveTrackAccentColor());
            clipEditor.RefreshClipIcon();

            m_TrackClipsContainer.Add(clipEditor);
            TrackClips.Add(clipEditor);
            return clipEditor;
        }
        public void RemoveClip(ESEditorTrackClip clip)
        {
            TrackClips.Remove(clip);

            if (clip != null)
            {
                clip.RemoveFromHierarchy();
                //  OnNodeRemoved?.Invoke(this, node);
                item.TryRemoveTrackClip(clip.trackClip);
            }
        }

        public void ClearClips()
        {
            m_TrackClipsContainer.Clear();
            TrackClips.Clear();
        }

        // 公共方法：时间轴相关
        public void SetTimeScaleAndStartShow(float pixelsPerSecond, float startShowTime)
        {
            foreach (var node in TrackClips)
            {
                node.SetTimeScaleAndStartShow(pixelsPerSecond, startShowTime);
            }
        }

        

        public void SetCurrentTime(float time)
        {
            foreach (var node in TrackClips)
            {
                node.HighlightIfActive(time);
            }
        }

        // 公共方法：设置轨道高度
        public void SetTrackHeight(float height)
        {
            // m_ExpandedHeight = height;
            // if (!m_IsCollapsed)
            // {
            //     style.height = height;
            // }
        }

        

        internal void UpdateNodes()
        {
            SetTimeScaleAndStartShow(ESTrackViewWindow.window.pixelPerSecond, ESTrackViewWindow.window.StartShow);
            foreach(var node in TrackClips)
            {
                node.UpdateNodeView();
            }
       
        }
        //检查节点是否对其
        public void UpdateNodeMatchAndForeachUpdate(bool update = true)
        {
            var listEditorNow = this.TrackClips.ToList();
            foreach (var clip in item.Clips)
            {
                var matchNode = listEditorNow.Find(n => n.trackClip == clip);
                if (matchNode != null)
                {
                    //好啊 
                    listEditorNow.Remove(matchNode);
                }
                else
                {
                    // 添加新节点
                    AddClip(clip, true);
                }
            }

            foreach (var toRemove in listEditorNow)
            {
                // 移除多余节点
                RemoveClip(toRemove);
            }

            UpdateNodes();
        }
    }

    internal static class ESTrackViewIconUtility
    {
        public const int ProtectedBasicTrackCount = 4;

        public static Texture2D ResolveTrackIcon(ITrackItem item)
        {
            return ResolveIcon(item != null ? item.GetType() : null);
        }

        public static Texture2D ResolveClipIcon(ITrackClip clip)
        {
            return ResolveIcon(clip != null ? clip.GetType() : null);
        }

        public static Color ResolveIconBackColor(Type type)
        {
            string typeName = type != null ? type.Name : string.Empty;
            if (typeName.Contains("Animation"))
                return new Color(0.18f, 0.31f, 0.54f, 0.92f);
            if (typeName.Contains("GameObject"))
                return new Color(0.17f, 0.38f, 0.22f, 0.92f);
            if (typeName.Contains("Audio"))
                return new Color(0.48f, 0.34f, 0.12f, 0.92f);
            if (typeName.Contains("Operation"))
                return new Color(0.50f, 0.10f, 0.34f, 0.92f);

            return new Color(0.24f, 0.27f, 0.32f, 0.92f);
        }

        public static bool TryGetBasicTrackKey(ITrackItem item, out string key)
        {
            key = null;
            string typeName = item != null ? item.GetType().Name : string.Empty;
            if (typeName.Contains("Animation"))
                key = "Animation";
            else if (typeName.Contains("GameObject"))
                key = "GameObject";
            else if (typeName.Contains("Audio"))
                key = "Audio";
            else if (typeName.Contains("Operation"))
                key = "Operation";

            return key != null;
        }

        public static int ClampUserTrackInsertIndex(int requestedIndex, int trackCount)
        {
            int maxIndex = Mathf.Max(ProtectedBasicTrackCount, trackCount);
            return Mathf.Clamp(requestedIndex, ProtectedBasicTrackCount, maxIndex);
        }

        private static Texture2D ResolveIcon(Type type)
        {
            string typeName = type != null ? type.Name : string.Empty;
            if (typeName.Contains("Animation"))
                return GetUnityObjectIcon(typeof(AnimationClip));
            if (typeName.Contains("GameObject"))
                return GetUnityObjectIcon(typeof(GameObject));
            if (typeName.Contains("Audio"))
                return GetUnityObjectIcon(typeof(AudioClip));
            if (typeName.Contains("Operation"))
                return GetUnityObjectIcon(typeof(UnityEditor.MonoScript));

            return GetUnityObjectIcon(typeof(ScriptableObject));
        }

        private static Texture2D GetUnityObjectIcon(Type type)
        {
            return UnityEditor.EditorGUIUtility.ObjectContent(null, type).image as Texture2D;
        }
    }

}
