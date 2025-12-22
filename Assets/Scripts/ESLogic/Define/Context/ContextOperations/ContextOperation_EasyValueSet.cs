using Sirenix.OdinInspector;
using System;


namespace ES
{

    [Serializable, TypeRegistryItem("Context操作-【浮点数】直接设置")]
    public class ContextOperation_FloatDirect : ContextOperation_Abstract
    {
        [LabelText("浮点值")] public float Value = 0;
        public override void TryOperation(ContextPool Context, ContextKeyValue keyValue)
        {
            Context.SetFloatDirect(keyValue.key, Value);
        }
    }


    [Serializable, TypeRegistryItem("Context操作-【整数】直接设置")]
    public class ContextOperation_IntDIrect : ContextOperation_Abstract
    {
        [LabelText("整数值")] public int Value = 0;
        public override void TryOperation(ContextPool Context,  ContextKeyValue keyValue)
        {
            Context.SetIntDirect(keyValue.key, Value);
        }
    }


    [Serializable, TypeRegistryItem("Context操作-【字符串】直接设置")]
    public class ContextOperation_StringDirect: ContextOperation_Abstract
    {
        [LabelText("字符串值")] public string Value = "";
        public override void TryOperation(ContextPool Context,  ContextKeyValue keyValue)
        {
            Context.SetStringDirect(keyValue.key, Value);
        }
    }


    [Serializable, TypeRegistryItem("Context操作-【标签】设置活动状态")]
    public class ContextOperation_Tag_Active : ContextOperation_Abstract
    {
        [LabelText("标签状态")] public bool Enable = true;
        public override void TryOperation(ContextPool Context,  ContextKeyValue keyValue)
        {
            if (Enable) Context.SetTagQuick_Use(keyValue.key);
            else Context.SetTagQuick_CancelUse(keyValue.key);
        }
    }


    [Serializable, TypeRegistryItem("Context操作-【标签】持续时间")]
    public class ContextOperation_Tag_Dura : ContextOperation_Abstract
    {
        [LabelText("持续时间")] public float dura;
        [LabelText("同时-激活")] public bool activeNow = true;
        public override void TryOperation(ContextPool Context,  ContextKeyValue keyValue)
        {
            if (activeNow) Context.SetTagQuick_SetUseableAndEnable(keyValue.key, dura);
            else Context.SetTagQuick_UseableTime(keyValue.key,dura);
        }
    }


    [Serializable, TypeRegistryItem("Context操作-【布尔值】直接设置")]
    public class ContextOperation_Bool_Direct : ContextOperation_Abstract
    {
        [LabelText("持续时间")] public float dura;
        [LabelText("同时-激活")] public bool activeNow = true;
        public override void TryOperation(ContextPool Context,  ContextKeyValue keyValue)
        {
            if (activeNow) Context.SetTagQuick_SetUseableAndEnable(keyValue.key, dura);
            else Context.SetTagQuick_UseableTime(keyValue.key, dura);
        }
    }


    [Serializable, TypeRegistryItem("Context操作-【整数】加")]
    public class ContextOperation_IntAdd : ContextOperation_Abstract
    {
        [LabelText("增加整数值")] public int Value = 1;
        public override void TryOperation(ContextPool Context,  ContextKeyValue keyValue)
        {
            Context.SetIntQuick_Add(keyValue.key, Value);
        }
    }

    [Serializable, TypeRegistryItem("Context操作-【布尔值】非操作")]
    public class ContextOperation_Bool_Not : ContextOperation_Abstract
    {
        public override void TryOperation(ContextPool Context,  ContextKeyValue keyValue)
        {
            Context.SetBoolQuick_Not(keyValue.key);
        }
    }

}
