using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
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
            get { return held ? "设置虚拟按钮按下" : "设置虚拟按钮松开"; }
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
            get { return "清除虚拟按钮"; }
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
            get { return "设置虚拟二维向量"; }
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
        [LabelText("\u8f93\u5165\u52a8\u4f5c")]
        public ESInputActionId actionId = ESInputActionId.Move;

        [LabelText("\u5355\u8f74\u503c")]
        public float value;

        public override string CommandName
        {
            get { return "\u8bbe\u7f6e\u865a\u62df\u5355\u8f74"; }
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
        [LabelText("\u8f93\u5165\u52a8\u4f5c")]
        public ESInputActionId actionId = ESInputActionId.Move;

        public override string CommandName
        {
            get { return "\u6e05\u9664\u865a\u62df\u5355\u8f74"; }
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
            get { return "清除虚拟二维向量"; }
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
            get { return "\u6e05\u9664\u5168\u90e8\u865a\u62df\u8f93\u5165"; }
        }

        public override void Invoke()
        {
            ESInputRuntime input = ESCommandServices.InputRuntime;
            if (input != null)
                input.VirtualSource.ClearAll();
        }
    }
}
