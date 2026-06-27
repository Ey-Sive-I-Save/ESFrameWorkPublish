
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
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
            // 基础样式
            AddToClassList("track-node");
            style.position = Position.Absolute;
            style.flexShrink = 0;
            style.minWidth = 30;
            style.minHeight = 30;
            style.maxHeight = 30;
            style.backgroundColor = new Color(0.4f, 0.7f, 1f, 0.8f);
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

            // 基础边框（半透明白色，像素宽度 1）
            style.borderLeftWidth = 3;
            style.borderRightWidth = 2;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftColor = new Color(0, 0, 0, 0.5f);
            style.borderRightColor = new Color(1, 1, 1, 0.5f);
            style.borderTopColor = new Color(1, 1, 1, 0.5f);
            style.borderBottomColor = new Color(1, 1, 1, 0.5f);

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
            this.BringToFront();
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
            if (Time.realtimeSinceStartup - lastHandleTime < 0.15f)
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
        private Color originalBgColor;
        private bool hasSetORI=false;
        public void HighlightIfActive(float currentTime)
        {
            if (currentTime >= StartTime && currentTime <= StartTime + Duration)
            {
                style.borderTopColor = Color.yellow;
                style.borderTopWidth = 2;
                style.borderBottomColor = Color.yellow;
                style.borderBottomWidth = 2;
                if(!hasSetORI)
                originalBgColor = style.backgroundColor.value;
                hasSetORI=true;

                // 设置为醒目的高亮色（例如亮橙色半透明）
                style.backgroundColor = new Color(1f, 0.5f, 0f, 0.6f);
            }
            else
            {
                if(hasSetORI){
                style.backgroundColor = originalBgColor;
                }

                // 恢复原来的样式（需要你记录一下原来的颜色，或设为默认）
                style.borderTopWidth = 0;
                style.borderBottomWidth = 0;
            }
        }

        public void UpdateNodeView()
        {
            m_ClipNameLabel.text = ClipName;
            AdjustFontToFit();
        }

        void AdjustFontToFit() // 注意：这里应直接传入你算好的 w 和 h
        {

            float maxW = style.width.value.value * 0.6f;
            float maxH = Mathf.Clamp(m_ClipNameLabel.contentRect.height * 1.5f, 20, 80);
            string text = m_ClipNameLabel.text;

            // ❶ 输出函数入口全部已知条件
            //   Debug.Log($"[AdjustFontToFit] 开始 | 文本: '{text}' | 分配容器尺寸: {maxW} x {maxH}");

            if (string.IsNullOrEmpty(text) || maxW <= 0 || maxH <= 0)
            {
                Debug.LogWarning("[AdjustFontToFit] 提前退出：文本为空或容器尺寸无效");
                return;
            }

            // ❷ 输出你设定的搜索范围边界
            float maxPossibleSize = Mathf.Min(100f, maxW * 0.8f, maxH * 0.8f);
            float minSize = 4f;
            //Debug.Log($"[搜索范围] 最大尝试字号: {maxPossibleSize}, 最小: {minSize}, 步长: 0.5");

            float bestSize = minSize;
            float lastTestedSize = -1;
            bool found = false;

            for (float size = maxPossibleSize; size >= minSize; size -= 0.5f)
            {
                m_ClipNameLabel.style.fontSize = size;
                Vector2 textSize = m_ClipNameLabel.MeasureTextSize(
                    text,
                    float.MaxValue,
                    VisualElement.MeasureMode.Undefined,
                    float.MaxValue,
                    VisualElement.MeasureMode.Undefined
                );

                // ❸ 每个测试的字号和测量结果都打印（如果怕刷屏，可以只记录最后一个没通过和第一个通过的）
                // 这里全打出来方便你排查，以后可注释掉
                // Debug.Log($"[尝试字号] {size:F1} | 测量尺寸: {textSize.x:F1} x {textSize.y:F1} | 容器: {maxW} x {maxH}");

                if (textSize.x <= maxW && textSize.y <= maxH)
                {
                    bestSize = size;
                    found = true;
                    //  Debug.Log($"[✓ 通过] 字号 {bestSize:F1} 可放入容器");
                    break;
                }

                if (size < maxPossibleSize && !found)
                {
                    lastTestedSize = size;
                }
            }

            if (!found)
            {
                //  Debug.LogWarning($"[未找到] 最小字号 {minSize} 仍放不下，强制使用最小字号");
            }

            // ❹ 输出循环结果
            //            Debug.Log($"[循环结果] 找到的最佳字号: {bestSize:F1}");

            // ❺ 尝试微调并输出
            float fineTune = bestSize + 0.3f;
            m_ClipNameLabel.style.fontSize = fineTune;
            Vector2 fineSize = m_ClipNameLabel.MeasureTextSize(
                text, float.MaxValue, VisualElement.MeasureMode.Undefined,
                float.MaxValue, VisualElement.MeasureMode.Undefined);
            // Debug.Log($"[微调尝试] 字号 {fineTune:F1} | 测量尺寸: {fineSize.x:F1} x {fineSize.y:F1} | 容器: {maxW} x {maxH}");

            if (fineSize.x <= maxW && fineSize.y <= maxH)
            {
                bestSize = fineTune;
                //  Debug.Log($"[微调通过] 最终字号: {bestSize:F1}");
            }
            else
            {
                //  Debug.Log($"[微调未通过] 保持字号: {bestSize:F1}");
            }

            // 最终设置
            m_ClipNameLabel.style.fontSize = bestSize;
            //  Debug.Log($"[AdjustFontToFit] 完成 | 最终设置字号: {bestSize:F1}\n");
        }

    }
}
