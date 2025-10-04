using DG.Tweening;
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;


[Serializable/**/]
public abstract class UITweenGetter
{
    [LabelText("持续时间")]
    public float duar=1;
    public abstract Tween GetTween(ESUIElement on, ESUIElement from);
}

[Serializable,TypeRegistryItem("UITween_简易位移")]
public class UITweenGetter_EasyAnchorPos : UITweenGetter
{
    [ToggleGroup("EnableInit", "启用初始化")]
    public bool EnableInit = false;
    [LabelText("From的起始坐标"), ToggleGroup("EnableInit")]
    public Vector2 vector2From;
    [LabelText("位置")]
    public Vector2 vector2;
    [LabelText("是From的"),ToggleGroup("isFrom","是From的")]
    public bool isFrom;
  
    public override Tween GetTween(ESUIElement on, ESUIElement from)
    {
        if (on.Refer_Rect != null)
        {
            if (EnableInit) on.Refer_Rect.Value.anchoredPosition = vector2From;
            var d = on.Refer_Rect.Value.DOAnchorPos(vector2, duar);
            if (isFrom) d.From();
            return d;
        }
        return null;
    }
}

[Serializable, TypeRegistryItem("UITween_简易缩放<XY>")]
public class UITweenGetter_EasyScale_XY : UITweenGetter
{
    [ToggleGroup("EnableInit", "启用初始化")]
    public bool EnableInit = false;
    [LabelText("From的起始缩放"), ToggleGroup("EnableInit")]
    public Vector2 vector2From = Vector2.one;
    [LabelText("缩放")]
    public Vector2 vector2=Vector2.one;
    [LabelText("是From的")]
    public bool isFrom;
  
    public override Tween GetTween(ESUIElement on, ESUIElement from)
    {
        if (on.Refer_Rect != null)
        {
            if (EnableInit) on.Refer_Rect.Value.localScale = vector2From;
            var d = on.Refer_Rect.Value.DOScale(vector2, duar);
            if (isFrom) d.From();
            return d;
        }
        return null;
    }
}

[Serializable, TypeRegistryItem("UITween_简易缩放<统一>")]
public class UITweenGetter_EasyScale_Same : UITweenGetter
{
    [ToggleGroup("EnableInit", "启用初始化")]
    public bool EnableInit = false;
    [LabelText("From的起始缩放"), ToggleGroup("EnableInit")]
    public float scaleFrom = 1;
    [LabelText("缩放")]
    public float scale = 1;
    [LabelText("是From的")]
    public bool isFrom;
   
    public override Tween GetTween(ESUIElement on, ESUIElement from)
    {
        if (on.Refer_Rect != null)
        {
            if (EnableInit) on.Refer_Rect.Value.localScale =Vector2.one* scaleFrom;
            var d = on.Refer_Rect.Value.DOScale(Vector2.one * scale, duar);
            if (isFrom) d.From();
            return d;
        }
        return null;
    }
}

[Serializable, TypeRegistryItem("UITween_简易旋转(单轴)")]
public class UITweenGetter_EasyRot : UITweenGetter
{
    [ToggleGroup("EnableInit", "启用初始化")]
    public bool EnableInit = false;
    [LabelText("From的起始缩放"), ToggleGroup("EnableInit")]
    public float rotFrom;
    [LabelText("旋转轴")]
    public EnumCollect.Axis_XYZ axis;
    [LabelText("旋转")]
    public float rot;
    [LabelText("是From的")]
    public bool isFrom;
    
    public override Tween GetTween(ESUIElement on, ESUIElement from)
    {
        if (on.Refer_Rect != null)
        {
            if (EnableInit)
            {
                if(axis== EnumCollect.Axis_XYZ.X) on.Refer_Rect.Value.rotation = Quaternion.Euler(rotFrom,0,0);
                else if (axis == EnumCollect.Axis_XYZ.Y) on.Refer_Rect.Value.rotation = Quaternion.Euler(0, rotFrom, 0);
                else if (axis == EnumCollect.Axis_XYZ.Z) on.Refer_Rect.Value.rotation = Quaternion.Euler(0, 0, rotFrom);
            }
            var d = on.Refer_Rect.Value.DORotate(
                new Vector3((axis == EnumCollect.Axis_XYZ.X)? rot : 0
                , (axis == EnumCollect.Axis_XYZ.Y) ? rot : 0
                , (axis == EnumCollect.Axis_XYZ.Z) ? rot : 0), duar);
            if (isFrom) d.From();
            return d;
        }
        return null;
    }
}

