
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
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
        private Label m_ClipNameLabel;

        public event Action<ESEditorTrackClip> OnClipClicked;
        public event Action<ESEditorTrackClip> OnClipDragged;

        public ESEditorTrackClip(ITrackClip clip, string name, float startTime, float duration, object data = null)
        {
            trackClip = clip;
            ClipName = name;
            StartTime = startTime;
            Duration = duration;
            UserData = data;
            this.focusable = true;
            // 基础样式
            AddToClassList("track-node");
            style.position = Position.Absolute;
            style.flexShrink = 0;
            style.minWidth = 30;
            style.minHeight = 30;
            style.maxHeight = 30;
            style.backgroundColor = new Color(0.4f, 0.7f, 1f, 0.8f);
            style.position= Position.Absolute;
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
                flexGrow = 1,
                justifyContent = Justify.Center,
                alignItems = Align.Center,
                paddingLeft = 4,
                paddingRight = 4
            }
            };

            m_ClipNameLabel = new Label(name)
            {
                style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 10,
                color = Color.white,
                unityTextAlign = TextAnchor.MiddleCenter,
                whiteSpace = WhiteSpace.Normal,
                overflow = Overflow.Hidden,
                textOverflow = TextOverflow.Ellipsis
            }
            };

            m_ClipContent.Add(m_ClipNameLabel);
            Add(m_ClipContent);

            // 注册事件
            RegisterCallback<ClickEvent>(evt =>
            {
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
            popup.style.display = DisplayStyle.None;
            popup.Add(popLabel = new Label());
            popLabel.style.left = 0;
            popLabel.style.width = 200;
            popLabel.style.overflow = Overflow.Hidden;
            popLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            popLabel.style.textOverflow = TextOverflow.Ellipsis;
            popLabel.style.color = Color.white;
            popLabel.style.fontSize = 12;
            this.Add(popup);

            this.RegisterCallback<PointerDownEvent>(OnPointerDown);

            // 鼠标释放结束拖动
            this.RegisterCallback<PointerUpEvent>(OnPointerUp);

            // 鼠标离开时如果未按下则结束拖动
            this.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            // 全局鼠标移动（用于持续拖动）
            this.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }
        private void OnPointerDown(PointerDownEvent evt)
        {
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
            if (Time.realtimeSinceStartup - lastHandleTime < 0.125f)
            {
                //点击操作
                ESTrackViewWindowHelper.EditClip(this);
            }
            if (isDragging)
            {
                isDragging = false;
                this.RemoveFromClassList("dragging");
                popup.style.display = DisplayStyle.None;
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }

            if (isExpanding) //中
            {
                isExpanding = false;
                popup.style.display = DisplayStyle.None;
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
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
            Debug.Log( "WW" + this.style.left + " LL" + nowLEFT + " START " + Cahce_ShowStart);
            var newStartTime = Cahce_ShowStart;
            if (nowLEFT != 0)
            {
                Debug.Log(Cache_pixelsPerSecond+ "" + Cahce_ShowStart);
                newStartTime = nowLEFT / Cache_pixelsPerSecond + Cahce_ShowStart;

            }
            // Debug.Log("WW"+w+" LL"+left+" START "+ShowStart);
            style.width = w;
            StartTime = newStartTime;

            popLabel.text = $"[{StartTime:F4}s -- {StartTime + Duration:F4}]";

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
        }

        public void SetTimeScaleAndStartShowCache()
        {
            SetTimeScaleAndStartShow(Cache_pixelsPerSecond, Cahce_ShowStart);
        }

        public void SetClipColor(Color color)
        {
            style.backgroundColor = color;
            style.borderLeftColor = color * 0.7f;
            style.borderRightColor = color * 0.7f;
            style.borderTopColor = color * 1.2f;
            style.borderBottomColor = color * 0.5f;
        }

        public void HighlightIfActive(float currentTime)
        {
            if (currentTime >= StartTime && currentTime <= StartTime + Duration)
            {
                AddToClassList("active-node");
            }
            else
            {
                RemoveFromClassList("active-node");
            }
        }
    }
}
