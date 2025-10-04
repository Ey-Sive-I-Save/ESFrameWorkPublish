using Sirenix.OdinInspector;
using System;


namespace ES
{

    [Serializable, TypeRegistryItem("Arch操作-【浮点数】直接设置")]
    public class ArchOperation_FloatDirect : ArchOperation_Abstract
    {
        [LabelText("浮点值")] public float Value = 0;
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            arch.SetFloatDirect(key, Value);
        }
    }


    [Serializable, TypeRegistryItem("Arch操作-【整数】直接设置")]
    public class ArchOperation_IntDIrect : ArchOperation_Abstract
    {
        [LabelText("整数值")] public int Value = 0;
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            arch.SetIntDirect(key, Value);
        }
    }


    [Serializable, TypeRegistryItem("Arch操作-【字符串】直接设置")]
    public class ArchOperation_StringDirect: ArchOperation_Abstract
    {
        [LabelText("字符串值")] public string Value = "";
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            arch.SetStringDirect(key, Value);
        }
    }


    [Serializable, TypeRegistryItem("Arch操作-【标签】设置活动状态")]
    public class ArchOperation_Tag_Active : ArchOperation_Abstract
    {
        [LabelText("标签状态")] public bool Enable = true;
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            if (Enable) arch.SetTag_Use(key);
            else arch.SetTag_CancelUse(key);
        }
    }


    [Serializable, TypeRegistryItem("Arch操作-【标签】持续时间")]
    public class ArchOperation_Tag_Dura : ArchOperation_Abstract
    {
        [LabelText("持续时间")] public float dura;
        [LabelText("同时-激活")] public bool activeNow = true;
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            if (activeNow) arch.SetTag_SetUseableAndEnable(key, dura);
            else arch.SetTag_UseableTime(key,dura);
        }
    }


    [Serializable, TypeRegistryItem("Arch操作-【布尔值】直接设置")]
    public class ArchOperation_Bool_Direct : ArchOperation_Abstract
    {
        [LabelText("持续时间")] public float dura;
        [LabelText("同时-激活")] public bool activeNow = true;
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            if (activeNow) arch.SetTag_SetUseableAndEnable(key, dura);
            else arch.SetTag_UseableTime(key, dura);
        }
    }


    [Serializable, TypeRegistryItem("Arch操作-【整数】加")]
    public class ArchOperation_IntAdd : ArchOperation_Abstract
    {
        [LabelText("增加整数值")] public int Value = 1;
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            arch.SetInt_Add(key, Value);
        }
    }

    [Serializable, TypeRegistryItem("Arch操作-【布尔值】非操作")]
    public class ArchOperation_Bool_Not : ArchOperation_Abstract
    {
        public override void TryOperation(ArchPool arch, string key, object value = null)
        {
            arch.SetBool_Not(key);
        }
    }

}
