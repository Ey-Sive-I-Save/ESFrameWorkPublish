using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputPulseVirtualButton)]
    public sealed class ESCommand_Input_PulseVirtualButton : ESCommand
    {
        [LabelText("输入动作")]
        public ESInputActionId actionId = ESInputActionId.Interact;

        public override string CommandName
        {
            get { return "触发输入动作按钮"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.PulseButton(actionId);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputPulseVirtualControlButton)]
    public sealed class ESCommand_Input_PulseVirtualControlButton : ESCommand
    {
        [LabelText("虚拟控件 ID")]
        public string virtualControlId = "InteractButton";

        public override string CommandName
        {
            get { return "触发虚拟控件按钮"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.PulseButton(virtualControlId);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputSetVirtualButton)]
    public sealed class ESCommand_Input_SetVirtualButton : ESCommand
    {
        [LabelText("输入动作")]
        public ESInputActionId actionId = ESInputActionId.Interact;

        [LabelText("是否按住")]
        public bool held = true;

        public override string CommandName
        {
            get { return held ? "设置输入动作按下" : "设置输入动作松开"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.SetButton(actionId, held);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputClearVirtualButton)]
    public sealed class ESCommand_Input_ClearVirtualButton : ESCommand
    {
        [LabelText("输入动作")]
        public ESInputActionId actionId = ESInputActionId.Interact;

        public override string CommandName
        {
            get { return "清除输入动作按钮"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.ClearButton(actionId);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputSetVirtualVector2)]
    public sealed class ESCommand_Input_SetVirtualVector2 : ESCommand
    {
        [LabelText("输入动作")]
        public ESInputActionId actionId = ESInputActionId.Move;

        [LabelText("二维向量")]
        public Vector2 value;

        public override string CommandName
        {
            get { return "设置输入动作二维向量"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.SetVector2(actionId, value);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputSetVirtualAxis)]
    public sealed class ESCommand_Input_SetVirtualAxis : ESCommand
    {
        [LabelText("输入动作")]
        public ESInputActionId actionId = ESInputActionId.FlyVertical;

        [LabelText("单轴值")]
        public float value;

        public override string CommandName
        {
            get { return "设置输入动作单轴"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.SetAxis(actionId, value);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputClearVirtualAxis)]
    public sealed class ESCommand_Input_ClearVirtualAxis : ESCommand
    {
        [LabelText("输入动作")]
        public ESInputActionId actionId = ESInputActionId.FlyVertical;

        public override string CommandName
        {
            get { return "清除输入动作单轴"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.ClearAxis(actionId);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputClearVirtualVector2)]
    public sealed class ESCommand_Input_ClearVirtualVector2 : ESCommand
    {
        [LabelText("输入动作")]
        public ESInputActionId actionId = ESInputActionId.Move;

        public override string CommandName
        {
            get { return "清除输入动作二维向量"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.ClearVector2(actionId);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.InputClearAllVirtual)]
    public sealed class ESCommand_Input_ClearAllVirtual : ESCommand
    {
        public override string CommandName
        {
            get { return "清除全部虚拟输入"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.ClearAll();
        }
    }
}
