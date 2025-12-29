
using System;
using System.Collections;
using System.Collections.Generic;
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
        private VisualElement m_TrackNodesContainer;


        #region  运行时
        public ITrackItem item;


        #endregion
        // 控制按钮
        private Button m_MuteButton;
        private Button m_LockButton;
        private Button m_DeleteButton;
        private Button m_CollapseButton;

        public List<ESEditorTrackNode> TrackNodes = new List<ESEditorTrackNode>();
        public ESEditorTrackItem()
        {
            CreateUIStructure();

            // 应用初始状态
            UpdateTrackColor();
            UpdateMuteButton();
            UpdateLockButton();
            for(int i = 0; i < 4; i++)
            {
            float fS=UnityEngine.Random.Range(0,1f)+i*2;
            float fE=UnityEngine.Random.Range(0,1.5f);
            AddNode(fS.ToString("F2")+"-"+(fS+fE).ToString("F2"), fS, fE);
            
            }
           
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

        private void CreateLeftPanel()
        {
            m_LeftPanel = new VisualElement
            {
                name = "track-left-panel",
                style =
            {
                 position= Position.Absolute,
                 left=0,
                width = 100,
                minWidth = 100,
                maxWidth = 100,
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
            m_TrackNodesContainer = new VisualElement
            {
                name = "track-nodes-container",
                style =
            {
                  left = 100,
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
            m_RightPanel.Add(m_TrackNodesContainer);
            Add(m_RightPanel);
        }
        private void UpdateTrackColor()
        {
            if (m_Icon != null)
            {
                m_Icon.style.backgroundColor = Color.white;
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
        public ESEditorTrackNode AddNode(string name, float startTime, float duration, object data = null)
        {
            var node = new ESEditorTrackNode(name, startTime, duration, data)
            {
                style =
            {
                marginLeft = 2,
                marginRight = 2
            }
            };

            m_TrackNodesContainer.Add(node);
            TrackNodes.Add(node);

            //  OnNodeAdded?.Invoke(this, node);
            return node;
        }

        public void RemoveNode(ESEditorTrackNode node)
        {
            if (TrackNodes.Remove(node))
            {
                node.RemoveFromHierarchy();
                //  OnNodeRemoved?.Invoke(this, node);
            }
        }

        public void ClearNodes()
        {
            m_TrackNodesContainer.Clear();
            TrackNodes.Clear();
        }

        // 公共方法：时间轴相关
        public void SetTimeScaleAndStartShow(float pixelsPerSecond, float startShowTime)
        {
            foreach (var node in TrackNodes)
            {
                node.SetTimeScaleAndStartShow(pixelsPerSecond, startShowTime);
            }
        }

        public void SetCurrentTime(float time)
        {
            foreach (var node in TrackNodes)
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
    }

}
