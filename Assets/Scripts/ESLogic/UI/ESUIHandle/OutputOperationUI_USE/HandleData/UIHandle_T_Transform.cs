using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ES.EnumCollect;

namespace ES
{
    /*T Transform 系列 -》*/
    [Serializable, TypeRegistryItem("T变换-【POS】设置锚定位置(死赋值-低级)")]
    public class OutputOperationUI_Handle_SetAnchorPos : IOutputOperationUI
    {
        [LabelText("设置锚定坐标")] public Vector2 pos;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            Debug.Log("SET" + pos);
            on.Refer_Rect.Value.anchoredPosition = pos;
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }


    [Serializable, TypeRegistryItem("T变换-【POS】设置锚定位置<单轴>(死赋值-低级)")]
    public class OutputOperationUI_Handle_SetAnchorPos_SingleAxis : IOutputOperationUI
    {
        [LabelText("设置锚定坐标")] public float singleValue;
        [LabelText("生效的轴")] public Axis_XOrY axis = Axis_XOrY.X;
        public enum Axis_XOrY
        {
            [InspectorName("X轴")] X, [InspectorName("Y轴")] Y
        }
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            var rect = on.Refer_Rect.Value;

            if (axis == Axis_XOrY.X) rect.anchoredPosition=new Vector2(singleValue, rect.anchoredPosition.y);
            else rect.anchoredPosition = new Vector2(singleValue, rect.anchoredPosition.y);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }


    [Serializable, TypeRegistryItem("T变换-【POS】设置锚定位置(随机增益-中级)")]
    public class OutputOperationUI_Handle_SetAnchorPos_RandomAdding : IOutputOperationUI
    {
        [LabelText("设置基本锚定坐标")] public Vector2 pos;

        [LabelText("X增益范围")] public Vector2 rangeX;
        [LabelText("Y增益范围")] public Vector2 rangeY;

        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.Refer_Rect.Value.anchoredPosition = pos + new Vector2(UnityEngine.Random.Range(rangeX.x, rangeX.y), UnityEngine.Random.Range(rangeY.x, rangeY.y));
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }

    [Serializable, TypeRegistryItem("T变换-【Scale】设置缩放(死赋值-单值)")]
    public class OutputOperationUI_Handle_SetScale_XYSame : IOutputOperationUI
    {
        [LabelText("设置缩放")] public float scale = 1;

        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.Refer_Rect.Value.localScale = Vector3.one * scale;
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }


    [Serializable, TypeRegistryItem("T变换-【Scale】设置缩放[XY不同](死赋值-低级)")]
    public class OutputOperationUI_Handle_SetScale_XY : IOutputOperationUI
    {
        [LabelText("设置缩放")] public Vector2 xyScale = Vector3.one;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.Refer_Rect.Value.localScale = new Vector3(xyScale.x, xyScale.y, 1);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }

    [Serializable, TypeRegistryItem("T变换-【Scale】设置缩放<单轴>(死赋值-低级)")]
    public class OutputOperationUI_Handle_SetScale_SingleAxis : IOutputOperationUI
    {
        [LabelText("设置单轴缩放")] public float scaleSingle = 1;
        [LabelText("生效的轴")] public Axis_XY axis = Axis_XY.X;
        
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            var rect = on.Refer_Rect.Value;
            if (axis == Axis_XY.X) rect.localScale = new Vector3(scaleSingle, rect.localScale.y, 1);
            else rect.localScale = new Vector3(rect.localScale.x, scaleSingle, 1);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }

    [Serializable, TypeRegistryItem("T变换-【Scale】设置旋转[X](死赋值-低级)")]
    public class OutputOperationUI_Handle_SetRot_X : IOutputOperationUI
    {
        [LabelText("设置旋转X")] public float xRot;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.Refer_Rect.Value.rotation = Quaternion.Euler(xRot,0,0);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }

    [Serializable, TypeRegistryItem("T变换-【Scale】设置旋转[Y](死赋值-低级)")]
    public class OutputOperationUI_Handle_SetRot_Y : IOutputOperationUI
    {
        [LabelText("设置旋转X")] public float yRot;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.Refer_Rect.Value.rotation = Quaternion.Euler(0, yRot, 0);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }
    [Serializable, TypeRegistryItem("T变换-【Scale】设置旋转[Z](死赋值-低级)")]
    public class OutputOperationUI_Handle_SetRot_Z : IOutputOperationUI
    {
        [LabelText("设置旋转Z")] public float zRot;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.Refer_Rect.Value.rotation = Quaternion.Euler(0, 0, zRot);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }


}
