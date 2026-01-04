
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
namespace ES
{
    public class ESEditorTrackItem : UnityEngine.UIElements.VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESEditorTrackItem, UxmlTraits> { }
        private VisualElement m_LeftPanel;
        private VisualElement m_RightPanel;
        private VisualElement m_Header;
        private VisualElement m_Icon;
        private Label m_TrackNameLabel;
        private VisualElement m_TrackClipsContainer;


        #region  运行时
        public ITrackItem item;


        #endregion
        // 控制按钮
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

        public ESEditorTrackItem InitWithItem(ITrackItem trackItem)
        {
            item = trackItem;

            UpdateTrackMessage();
            UpdateTrackColor();
            UpdateNodeMatch(true);
            //Debug.Log("初始化轨道项：" + item.GetType() + item.DisplayName);
            return this;
        }

        private void UpdateTrackMessage()
        {
            m_TrackNameLabel.text = item.DisplayName;
        }

        private void CreateUIStructure()
        {
            // 整个轨道项采用水平布局
            style.flexDirection = FlexDirection.Row;
            style.flexShrink = 0;

            // 右侧面板 - 可扩展，显示轨道节点
            CreateRightPanel();
            // 左侧面板 - 固定宽度，显示轨道信息
            CreateLeftPanel();

            BindClipsArea();


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
                flexDirection = FlexDirection.Column,
                paddingTop = 4,
                paddingBottom = 4,
                paddingLeft = 8,
                paddingRight = 8,
                backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f))
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
                marginBottom = 4
            }
            };

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
                width = 16,
                height = 16,
                marginRight = 6,
                //backgroundColor = m_TrackColor
            }
            };
            m_Icon.AddToClassList("icon-default");
            m_Header.Add(m_Icon);

            // 轨道名称
            m_TrackNameLabel = new Label("Track")
            {
                name = "track-name",
                style =
            {
                flexGrow = 1,
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 12,
                unityTextAlign = TextAnchor.MiddleLeft
            }
            };
            m_Header.Add(m_TrackNameLabel);

            m_LeftPanel.Add(m_Header);
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
                backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 1f)),
                minHeight = 40
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
                minHeight = 40,
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
            if (m_Icon != null)
            {
                m_Icon.style.backgroundColor = Color.white;
            }
            if (m_TrackClipsContainer != null)
            {
                m_TrackClipsContainer.style.backgroundColor = item.ItemBGColor;
            }



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

        internal void UpdateNodesPos()
        {
            SetTimeScaleAndStartShow(ESTrackViewWindow.window.pixelPerSecond, ESTrackViewWindow.window.StartShow);
        }

        public void UpdateNodeMatch(bool update = true)
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

            UpdateNodesPos();
        }
    }

}
