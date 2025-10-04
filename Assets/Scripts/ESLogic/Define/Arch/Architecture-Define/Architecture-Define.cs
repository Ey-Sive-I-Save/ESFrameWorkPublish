using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

using static UnityEngine.UIElements.UxmlAttributeDescription;

namespace ES
{
    #region 全部类型事件
    [Serializable, TypeRegistryItem("原型事件-Tag变更")]
    public struct Link_ArchEvent_TagChange 
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("更新时间-过去")]
        public float Value_time_Pre;
        [LabelText("更新时间-现在")]
        public float Value_time_Now;
    }
    [Serializable, TypeRegistryItem("原型事件-浮点数变更")]
    public struct Link_ArchEvent_FloatChange
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("过去值")]
        public float Value_Pre;
        [LabelText("现在值")]
        public float Value_Now;
    }
    [Serializable, TypeRegistryItem("原型事件-整数变更")]
    public struct Link_ArchEvent_IntChange
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("过去值")]
        public int Value_Pre;
        [LabelText("现在值")]
        public int Value_Now;
    }
    [Serializable, TypeRegistryItem("原型事件-字符串变更")]
    public struct Link_ArchEvent_StringChange
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("过去值")]
        public string Value_Pre;
        [LabelText("现在值")]
        public string Value_Now;
    }
    [Serializable, TypeRegistryItem("原型事件-布尔值变更")]
    public struct Link_ArchEvent_BoolChange 
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("过去值")]
        public bool Value_Pre;
        [LabelText("现在值")]
        public bool Value_Now;
    }
    #endregion

    #region 全部接收类型
    public interface IReceiveChannelLink_Arch_Tag : IReceiveChannelLink<string,Link_ArchEvent_TagChange>
    {

    }
    public interface IReceiveChannelLink_Arch_Float : IReceiveChannelLink<string, Link_ArchEvent_TagChange>
    {

    }
    public interface IReceiveChannelLink_Arch_Int : IReceiveChannelLink<string, Link_ArchEvent_TagChange>
    {

    }
    public interface IReceiveChannelLink_Arch_String : IReceiveChannelLink<string, Link_ArchEvent_StringChange>
    {

    }
    public interface IReceiveChannelLink_Arch_Bool : IReceiveChannelLink<string, Link_ArchEvent_TagChange>
    {

    }
    #endregion


    [Serializable, TypeRegistryItem("标准原型值池")]
    public class ArchPool : IArchitecture
    {
       /* [LabelText("初始化数据包"), TitleGroup("初始化", alignment: TitleAlignments.Centered)]
        public ArchitectureDataPack _applyPack;
        [LabelText("初始化自选数组"), TitleGroup("初始化", alignment: TitleAlignments.Centered)]
        public ArchitectureDataInfo[] _applyInfos;*/
        [LabelText("预定义(仅编辑器配置)"), HideInPlayMode, SerializeReference, HideReferenceObjectPicker, ListDrawerSettings(DefaultExpandedState = true), TitleGroup("初始化", alignment: TitleAlignments.Centered)]
        public List<IArchitectureValue> _InitValues = new List<IArchitectureValue>();
        [LabelText("原型值池-实时"), ShowInInspector, HideReferenceObjectPicker, ListDrawerSettings(DefaultExpandedState = true), HideInEditorMode, SerializeReference]
        protected Dictionary<string, IArchitectureValue> _ArchValues = new Dictionary<string, IArchitectureValue>();

        public void Init(params object[] ps)
        {
            _ArchValues ??= new Dictionary<string, IArchitectureValue>();
            foreach (var i in _InitValues)
            {
                if (i != null)
                {
                    TryAddSameArchValueFromArchValue(i);
                }
            }
         /*   foreach (var i in _applyInfos)
            {
                TryAddNewArchValueFromSoInfoCopy(i);
            }
            if (_applyPack != null)
            {
                foreach (var i in _applyPack.allInfos.Values)
                {
                    TryAddNewArchValueFromSoInfoCopy(i);
                }
            }*/
        }
        public void TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType architecture, string key, object value,bool send=false)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetValue(value);
            }
            else
            {
                var newOne = IArchitectureValue.Create(architecture, key,send);
                newOne.SetValue(value);
                AddArchValueTruelyToCreate(key, newOne);
            }
        }
        public void TryAddSameArchValueFromArchValue(IArchitectureValue arValue)
        {
            if (arValue == null) return;
            if (_ArchValues.TryGetValue(arValue.TheKey, out var vv))
            {
                vv.SetValue(arValue.TheSmartValue);
            }
            else
            {
                AddArchValueTruelyToCreate(arValue.TheKey, arValue);
            }
        }
        public void TryAddNewArchValueFromArchValueCopy(IArchitectureValue arValue)
        {
            if (arValue == null) return;
            if (_ArchValues.TryGetValue(arValue.TheKey, out var vv))
            {
                vv.SetValue(arValue.TheSmartValue);
            }
            else
            {
                var value = IArchitectureValue.Create(arValue.ArchType, arValue.TheKey, arValue.WillSendLink);
                AddArchValueTruelyToCreate(arValue.TheKey, arValue);
            }
        }
     /*   public void TryAddNewArchValueFromSoInfoCopy(ArchitectureDataInfo info)
        {
            if (info != null && info.Values != null)
            {
                foreach (var i in info.Values)
                {
                    TryAddNewArchValueFromArchValueCopy(i);
                }
            }
        }
        public void TryAddSameArchValueFromSoInfo(ArchitectureDataInfo info)
        {
            if (info != null && info.Values != null)
            {
                foreach (var i in info.Values)
                {
                    TryAddSameArchValueFromArchValue(i);
                }
            }
        }
*/
        #region GET-仅值
        public bool Contains(string key)
        {
            if (_ArchValues.TryGetValue(key, out var _))
            {
                return true;
            }
            return false;
        }
        public bool GetBool(string key)
        {
            if (_ArchValues.TryGetValue(key, out var use))
            {
                return use.GetBool();
            }
            return false;
        }
        public float GetFloat(string key)
        {
            if (_ArchValues.TryGetValue(key, out var use))
            {
                return use.GetFloat();
            }
            return 0;
        }
        public float GetInt(string key)
        {
            if (_ArchValues.TryGetValue(key, out var use))
            {
                return use.GetInt();
            }
            return 0;
        }
        public string GetString(string key)
        {
            if (_ArchValues.TryGetValue(key, out var use))
            {
                return use.GetString();
            }
            return "NULL";
        }
        public bool GetTagIsUseable(string key)
        {
            if (_ArchValues.TryGetValue(key, out var use))
            {
                return use.GetBool();
            }
            return false;
        }
        public float GetTagRemain(string key)
        {
            if (_ArchValues.TryGetValue(key, out var use))
            {
                return use.GetFloat();
            }
            return 0;
        }
        #endregion

        #region SET一：通用操作    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBool(string key, bool use, EnumCollect.HandleTwoBool function = EnumCollect.HandleTwoBool.Set)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(ESDesignUtility.Function.FunctionForHandleTwoBool(vv.GetBool(), use, function));
            }
            else
            {
               AddArchValueTruelyToCreate(key, new ArchitectureTypeValue_Bool() { Value = ESDesignUtility.Function.FunctionForHandleTwoBool(true, use, function) });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(string key, float use, EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(ESDesignUtility.Function.FunctionForHandleTwoFloat(vv.GetFloat(), use, function));
            }
            else
            {
                AddArchValueTruelyToCreate(key, new ArchitectureTypeValue_Float() { Value = ESDesignUtility.Function.FunctionForHandleTwoFloat(0, use, function) });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt(string key, int use, EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(ESDesignUtility.Function.FunctionForHandleTwoInt(vv.GetInt(), use, function));
            }
            else
            {
                AddArchValueTruelyToCreate(key, new ArchitectureTypeValue_Int() { Value = ESDesignUtility.Function.FunctionForHandleTwoInt(0, use, function) });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloatDirect(string key, float use,bool sendIfNULL=false)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(use);
            }else
            {
                TryAddNewArchValueBySplits( EnumCollect.ArchitectureValueType.FloatValue,key,use,sendIfNULL);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIntDirect(string key, int use, bool sendIfNULL = false)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(use);
            }
            else
            {
                TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType.IntValue, key, use, sendIfNULL);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBoolDirect(string key, bool use, bool sendIfNULL = false)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(use);
            }
            else
            {
                TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType.BoolValue, key, use, sendIfNULL);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStringDirect(string key, string use, bool sendIfNULL = false)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetString(use);
            }
            else
            {
                TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType.StringValue, key, use, sendIfNULL);
            }
        }
        #endregion

        #region SET二：快捷操作
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt_Add1(string key)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(1+vv.GetInt());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt_Sub1(string key)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(vv.GetInt()-1);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt_Add(string key,int num)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(num + vv.GetInt());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat_Add(string key,float num)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(num+ vv.GetFloat());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBool_Not(string key)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(!vv.GetBool());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetString_Replace(string key,string from,string to)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetString(vv.GetString().Replace(from,to));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTag_Use(string key)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(true);//对于Tag来说--就是重制时间
            }else
            {
                TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType.DynamicTag, key, 5);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTag_CancelUse(string key)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(false);//对于Tag来说--就是重制时间
            }
            else
            {
                TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType.DynamicTag, key, 5);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTag_UseableTime(string key,float f)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(f);//对于Tag来说--就是重制时间
            }
            else
            {
                TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType.DynamicTag, key, f);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTag_SetUseableAndEnable(string key, float f)
        {
            if (_ArchValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(f);//对于Tag来说--就是重制时间
                vv.SetBool(true);
            }
            else
            {
                TryAddNewArchValueBySplits(EnumCollect.ArchitectureValueType.DynamicTag, key, f);
            }
        }
        #endregion
        #region 链事件
        [FoldoutGroup("事件收发"), LabelText("标签事件")]
        public LinkReceiveChannelPool<string,Link_ArchEvent_TagChange> LinkRCL_Tag = new LinkReceiveChannelPool<string, Link_ArchEvent_TagChange>();
        [FoldoutGroup("事件收发"), LabelText("浮点数事件")]
        public LinkReceiveChannelPool<string, Link_ArchEvent_FloatChange> LinkRCL_Float =new LinkReceiveChannelPool<string, Link_ArchEvent_FloatChange>();
        [FoldoutGroup("事件收发"), LabelText("整数事件")]
        public LinkReceiveChannelPool<string, Link_ArchEvent_IntChange> LinkRCL_Int = new LinkReceiveChannelPool<string, Link_ArchEvent_IntChange>();
        [FoldoutGroup("事件收发"), LabelText("字符串事件")]
        public LinkReceiveChannelPool<string, Link_ArchEvent_StringChange> LinkRCL_String = new LinkReceiveChannelPool<string, Link_ArchEvent_StringChange>();
        [FoldoutGroup("事件收发"), LabelText("布尔事件")]
        public LinkReceiveChannelPool<string, Link_ArchEvent_BoolChange> LinkRCL_Bool = new LinkReceiveChannelPool<string, Link_ArchEvent_BoolChange>();
        public void Enable()
        {
            if (enabled) return;
            enabled = true;
            foreach (var (k, v) in _ArchValues)
            {
                v.AddReceivePool(this);
            }
        }
        public void Disable()
        {
            if (!enabled) return;
            enabled = false;
            foreach (var (k, v) in _ArchValues)
            {
                v.RemoveReceivePool(this);
            }
        }
        [NonSerialized]
        public bool enabled = false;
        public void AddArchValueTruelyToCreate(string key, IArchitectureValue architecture)
        {
            _ArchValues.Add(key, architecture);
            if (enabled)
                architecture.AddReceivePool(this);
        }
        public void RemoveArchValueTruelyToCreate(string key, IArchitectureValue architecture)
        {
            if (_ArchValues.TryGetValue(key, out var arch))
            {
                _ArchValues.Remove(key);
                architecture.RemoveReceivePool(this);
            }
        }
        #endregion
    }
    public interface IArchitecture
    {

    }
    public interface IWithArchitecture
    {
        public abstract IArchitecture GetArchitecture { get; }
    }

}




