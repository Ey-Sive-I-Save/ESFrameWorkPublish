
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;
namespace ES
{
   public class ESEditorTrackClip : VisualElement
{
    public string ClipName { get; private set; }
    public float StartTime { get; private set; }
    public float Duration { get; private set; }
    public object UserData { get; set; }
    
    private VisualElement m_ClipContent;
    private Label m_ClipNameLabel;
    
    public event Action<ESEditorTrackClip> OnClipClicked;
    public event Action<ESEditorTrackClip> OnClipDragged;
    
    public ESEditorTrackClip(string name, float startTime, float duration, object data = null)
    {
        ClipName = name;
        StartTime = startTime;
        Duration = duration;
        UserData = data;
        this.focusable=true;
        // 基础样式
        AddToClassList("track-node");  
        style.position = Position.Absolute;
        style.flexShrink=0;
        style.minWidth = 30;
        style.minHeight = 30;
        style.maxHeight = 30;
        style.backgroundColor = new Color(0.4f, 0.7f, 1f, 0.8f);
        style.borderLeftWidth = 2;
        style.borderRightWidth = 2;
        style.borderTopWidth = 2;
        style.borderBottomWidth = 2;
        style.borderLeftColor = new Color(0.3f, 0.5f, 0.8f);
        style.borderRightColor = new Color(0.3f, 0.5f, 0.8f);
        style.borderTopColor = new Color(0.5f, 0.8f, 1f);
        style.borderBottomColor = new Color(0.2f, 0.4f, 0.7f);
        style.borderTopLeftRadius = 4;
        style.borderTopRightRadius = 4;
        style.borderBottomLeftRadius = 4;
        style.borderBottomRightRadius = 4;

        
        
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


        SetTimeScaleAndStartShow(100,0);
    }
    
    public void SetTimeScaleAndStartShow(float pixelsPerSecond,float ShowStart)
    {
        // 根据时间尺度和持续时间设置节点宽度
        var w= Duration * pixelsPerSecond;
        var left=(StartTime-ShowStart) * pixelsPerSecond;
       // Debug.Log("WW"+w+" LL"+left+" START "+ShowStart);
        style.width =w;
        style.left = left;
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
