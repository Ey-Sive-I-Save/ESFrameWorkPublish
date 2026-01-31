using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.UIElements.UxmlAttributeDescription;

namespace ES
{
    #region 全部类型事件
    [Serializable, TypeRegistryItem("原型事件-Tag变更")]
    public struct Link_ContextEvent_TagChange
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
    public struct Link_ContextEvent_FloatChange
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
    [Serializable, TypeRegistryItem("原型事件-向量变更")]
    public struct Link_ContextEvent_VectorChange
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("过去值")]
        public Vector3 Value_Pre;
        [LabelText("现在值")]
        public Vector3 Value_Now;
    }
    [Serializable, TypeRegistryItem("原型事件-整数变更")]
    public struct Link_ContextEvent_IntChange
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
    public struct Link_ContextEvent_StringChange
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
    public struct Link_ContextEvent_BoolChange
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
    [Serializable, TypeRegistryItem("原型事件-泛型类变更")]
    public struct Link_ContextEvent_ClassTChange
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("过去值")]
        public object Value_Pre;
        [LabelText("现在值")]
        public object Value_Now;
    }
    [Serializable, TypeRegistryItem("原型事件-UnityObject变更")]
    public struct Link_ContextEvent_UnityObjectChange
    {
        [LabelText("首次创建")]
        public bool Create;
        [LabelText("移除")]
        public bool Remove;
        [LabelText("过去值")]
        public UnityEngine.Object Value_Pre;
        [LabelText("现在值")]
        public UnityEngine.Object Value_Now;
    }
    #endregion

    #region 全部接收类型
    public interface IReceiveChannelLink_Context_Tag : IReceiveChannelLink<string, Link_ContextEvent_TagChange>
    {

    }
    public interface IReceiveChannelLink_Context_Float : IReceiveChannelLink<string, Link_ContextEvent_FloatChange>
    {

    }
    public interface IReceiveChannelLink_Context_Vector3 : IReceiveChannelLink<string, Link_ContextEvent_VectorChange>
    {

    }
    public interface IReceiveChannelLink_Context_Int : IReceiveChannelLink<string, Link_ContextEvent_IntChange>
    {

    }
    public interface IReceiveChannelLink_Context_String : IReceiveChannelLink<string, Link_ContextEvent_StringChange>
    {

    }
    public interface IReceiveChannelLink_Context_Bool : IReceiveChannelLink<string, Link_ContextEvent_BoolChange>
    {

    }
    public interface IReceiveChannelLink_Context_ClassT : IReceiveChannelLink<string, Link_ContextEvent_ClassTChange>
    {

    }
    public interface IReceiveChannelLink_Context_UnityObject : IReceiveChannelLink<string, Link_ContextEvent_UnityObjectChange>
    {

    }
    #endregion

    /// <summary>
    /// 原型池 Init ->Enable ->Disable 都需要执行
    /// </summary>
    [Serializable, TypeRegistryItem("标准原型值池")]
    public class ContextPool 
    {
        [LabelText("预先准备池(仅编辑器下配置)"),HideLabel, HideInPlayMode, SerializeReference, HideReferenceObjectPicker, ListDrawerSettings(DefaultExpandedState = true), OnCollectionChanged("ListChangeEditor"), TitleGroup("初始化", alignment: TitleAlignments.Centered)]
        protected List<IContextitectureValue> _InitValues = new List<IContextitectureValue>();
        [LabelText("原型值池-实时"),HideLabel, HideReferenceObjectPicker, ShowInInspector, DictionaryDrawerSettings(IsReadOnly = true,KeyLabel ="键",ValueLabel ="值"), HideInEditorMode, SerializeReference]
        protected Dictionary<string, IContextitectureValue> _ContextValues = new Dictionary<string, IContextitectureValue>();
        
        #region 初始化和添加值
        public void Init(params object[] ps)
        {
            _ContextValues ??= new Dictionary<string, IContextitectureValue>();
            for (int i = 0; i < _InitValues.Count; i++)
            {
                var use = _InitValues[i];
                if (use != null)
                {
                    TryAddSameContextValueFromContextValue(use);
                }
            }
        }
        public void TryAddNewContextValueBySplits(EnumCollect.ContextValueType Contextitecture, string key, object value, bool send = false)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetValue(value);
            }
            else
            {
                var newOne = IContextitectureValue.Create(Contextitecture, key, send);
                newOne.SetValue(value);
                AddContextValueTruelyToCreate(key, newOne);
            }
        }
        /// <summary>
        /// 把一个原型值原样本体加入池中
        /// </summary>
        /// <param name="arValue"></param>
        public void TryAddSameContextValueFromContextValue(IContextitectureValue arValue)
        {
            if (arValue == null) return;
            if (_ContextValues.TryGetValue(arValue.TheKey, out var vv))
            {
                vv.SetValue(arValue.TheSmartValue);
            }
            else
            {
                AddContextValueTruelyToCreate(arValue.TheKey, arValue);
            }
        }
        /// <summary>
        /// 依赖一个原型值里获得新的原型值加入池中
        /// </summary>
        /// <param name="arValue"></param>
        public void TryAddNewContextValueFromContextValueCopy(IContextitectureValue arValue)
        {
            if (arValue == null) return;
            if (_ContextValues.TryGetValue(arValue.TheKey, out var vv))
            {
                vv.SetValue(arValue.TheSmartValue);
            }
            else
            {
                var value = IContextitectureValue.Create(arValue.ContextType, arValue.TheKey, arValue.WillSendLink);
                AddContextValueTruelyToCreate(arValue.TheKey, arValue);
            }
        }
        #endregion

#if UNITY_EDITOR
        #region 编辑器扩展
        public void ListChangeEditor(CollectionChangeInfo info)
        {
            if (info.ChangeType == CollectionChangeType.Add || info.ChangeType == CollectionChangeType.Insert)
            {
                if (info.Value is IContextitectureValue iav)
                {
                    if (iav.TheKey == "default")
                    {
                        iav.TheKey ="default" + UnityEngine.Random.Range(0,999);
                    }
                }
            }
        }

        #endregion
#endif
        /*   public void TryAddNewContextValueFromSoInfoCopy(ContextitectureDataInfo info)
           {
               if (info != null && info.Values != null)
               {
                   foreach (var i in info.Values)
                   {
                       TryAddNewContextValueFromContextValueCopy(i);
                   }
               }
           }
           public void TryAddSameContextValueFromSoInfo(ContextitectureDataInfo info)
           {
               if (info != null && info.Values != null)
               {
                   foreach (var i in info.Values)
                   {
                       TryAddSameContextValueFromContextValue(i);
                   }
               }
           }
   */

        #region GET-仅值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(string key)
        {
            if (_ContextValues.TryGetValue(key, out var _))
            {
                return true;
            }
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.GetBool();
            }
            return defaultValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.GetFloat();
            }
            return defaultValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVector(string key, Vector3 defaultValue = default)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.GetVector();
            }
            return defaultValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt(string key, int defaultValue = 0)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.GetInt();
            }
            return defaultValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetString(string key, string defaultValue = "")
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.GetString();
            }
            return defaultValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetTagIsUseable(string key, bool defaultValue = false)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.GetBool();
            }
            return defaultValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetTagRemain(string key, float defaultValue = 0f)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.GetFloat();
            }
            return defaultValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetValue(string key)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.TheSmartValue;
            }
            return null;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetValue<T>(string key) where T : class
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                return use.TheSmartValue as T;
            }
            return null;
        }
        #endregion

        #region 便捷工具
#if UNITY_EDITOR
        [TabGroup("便捷工具")]
        [ValueDropdown("FindKeyEditor",AppendNextDrawer =true),LabelText("查键"),NonSerialized,ShowInInspector,HideInPlayMode]
#pragma warning disable CS0414 // 字段已赋值，但从未使用过
        private string FindKeyEditor_ = "";

        [TabGroup("便捷工具")]
        [ValueDropdown("FindKeyRuntime", AppendNextDrawer = true), LabelText("查键"), NonSerialized, ShowInInspector,HideInEditorMode]
        private string FindKeyRuntime_ = "";
#pragma warning restore CS0414 
        private List<string> FindKeyRuntime()
        { 
            return _ContextValues.Keys.ToList();
        }
#endif
        #endregion

        #region SET一：通用操作    
        [TabGroup("内置通用操作测试",VisibleIf = "@UnityEngine.Application.isPlaying"),Button("操作布尔值")]
        [MethodImpl(MethodImplOptions.AggressiveInlining),  ]
        public void SetBool(string key, bool use, EnumCollect.HandleTwoBool function = EnumCollect.HandleTwoBool.Set)
        {
            
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(ESDesignUtility.Function.HandleTwoBool(vv.GetBool(), use, function));
            }
            else
            {
                AddContextValueTruelyToCreate(key, new ContextitectureTypeValue_Bool() { Value = ESDesignUtility.Function.HandleTwoBool(true, use, function) });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"),Button("操作浮点值"),  ]
        public void SetFloat(string key, float use, EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(ESDesignUtility.Function.HandleTwoFloat(vv.GetFloat(), use, function));
            }
            else
            {
                AddContextValueTruelyToCreate(key, new ContextitectureTypeValue_Float() { Value = ESDesignUtility.Function.HandleTwoFloat(0, use, function) });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("操作整数值"),  ]
        public void SetInt(string key, int use, EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(ESDesignUtility.Function.HandleTwoInt(vv.GetInt(), use, function));
            }
            else
            {
                AddContextValueTruelyToCreate(key, new ContextitectureTypeValue_Int() { Value = ESDesignUtility.Function.HandleTwoInt(0, use, function) });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("直接设置浮点值"),  ]
        public void SetFloatDirect(string key, float use, bool EnableSendLinkIfCreateNew = false)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(use);
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.FloatValue, key, use, EnableSendLinkIfCreateNew);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("直接设置向量"),  ]
        public void SetVectorDirect(string key, Vector3 use, bool EnableSendLinkIfCreateNew = false)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetVector(use);
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.VectorValue, key, use, EnableSendLinkIfCreateNew);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("直接设置整数值"),  ]
        public void SetIntDirect(string key, int use, bool EnableSendLinkIfCreateNew = false)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(use);
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.IntValue, key, use, EnableSendLinkIfCreateNew);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("直接设置布尔值"),  ]
        public void SetBoolDirect(string key, bool use, bool EnableSendLinkIfCreateNew = false)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(use);
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.BoolValue, key, use, EnableSendLinkIfCreateNew);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("直接设置浮点数"),  ]
        public void SetStringDirect(string key, string use, bool EnableSendLinkIfCreateNew = false)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetString(use);
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.StringValue, key, use, EnableSendLinkIfCreateNew);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("设置任意类型值"),  ]
        public void SetValue(string key, object value)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                use.SetValue(value);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置通用操作测试"), Button("设置浮点值"),  ]
        public void SetValue<T>(string key, T value)
        {
            if (_ContextValues.TryGetValue(key, out var use))
            {
                use.SetValue(value);
            }
        }
        #endregion

        #region SET二：快捷操作
        [TabGroup("内置快捷操作测试", VisibleIf = "@UnityEngine.Application.isPlaying"), Button("整数加1"),  ]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIntQuick_Add1(string key)
        {
            
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(1 + vv.GetInt());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("整数减1"),  ]
        public void SetIntQuick_Sub1(string key)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(vv.GetInt() - 1);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("整数加？"),  ]
        public void SetIntQuick_Add(string key, int num)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetInt(num + vv.GetInt());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("浮点数加？"),  ]
        public void SetFloatQuick_Add(string key, float num)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(num + vv.GetFloat());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("布尔值设空"),  ]
        public void SetBoolQuick_Not(string key)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(!vv.GetBool());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("字符串替换"),  ]
        public void SetStringQuick_Replace(string key, string from, string to)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetString(vv.GetString().Replace(from, to));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("标签设置为可用"),  ]
        public void SetTagQuick_Use(string key)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(true);//对于Tag来说--就是重制时间
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.DynamicTag, key, 5);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("标签设置为禁用"),  ]
        public void SetTagQuick_CancelUse(string key)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetBool(false);//对于Tag来说--就是重制时间
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.DynamicTag, key, 5);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("设置标签有效时间"),  ]
        public void SetTagQuick_UseableTime(string key, float f)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(f);//对于Tag来说--就是重制时间
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.DynamicTag, key, f);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [TabGroup("内置快捷操作测试"), Button("设置标签可用并设置有效时间"),  ]
        public void SetTagQuick_SetUseableAndEnable(string key, float f)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.SetFloat(f);//对于Tag来说--就是重制时间
                vv.SetBool(true);
            }
            else
            {
                TryAddNewContextValueBySplits(EnumCollect.ContextValueType.DynamicTag, key, f);
            }
        }
        #endregion

        #region Set三 ：Func操作
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBoolFunc(string key, Func<bool, bool> func)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                if (func != null) vv.SetBool(func.Invoke(vv.GetBool()));
            }
            else
            {
                AddContextValueTruelyToCreate(key, new ContextitectureTypeValue_Bool() { Value = (func != null) ? func.Invoke(false) : false });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIntFunc(string key, Func<int, int> func)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                if (func != null) vv.SetInt(func.Invoke(vv.GetInt()));
            }
            else
            {
                AddContextValueTruelyToCreate(key, new ContextitectureTypeValue_Int() { Value = (func != null) ? func.Invoke(0) : 0 });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloatFunc(string key, Func<float, float> func)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                if (func != null) vv.SetFloat(func.Invoke(vv.GetFloat()));
            }
            else
            {
                AddContextValueTruelyToCreate(key, new ContextitectureTypeValue_Float() { Value = (func != null) ? func.Invoke(0f) : 0f });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStringFunc(string key, Func<string, string> func)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                if (func != null) vv.SetString(func.Invoke(vv.GetString()));
            }
            else
            {
                AddContextValueTruelyToCreate(key, new ContextitectureTypeValue_String() { Value = (func != null) ? func.Invoke("") : "" });
            }
        }
        #endregion

        #region Set四:杂碎(Link启用)

        public void EnableLink(string key)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.WillSendLink = true;
            }
        }
        public void DisableLink(string key)
        {
            if (_ContextValues.TryGetValue(key, out var vv))
            {
                vv.WillSendLink = false;
            }
        }
        #endregion

        #region 链事件
        [FoldoutGroup("事件收发"), LabelText("标签变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_TagChange> LinkRCL_Tag = new LinkReceiveChannelPool<string, Link_ContextEvent_TagChange>();
        [FoldoutGroup("事件收发"), LabelText("浮点数变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_FloatChange> LinkRCL_Float = new LinkReceiveChannelPool<string, Link_ContextEvent_FloatChange>();
        [FoldoutGroup("事件收发"), LabelText("向量变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_VectorChange> LinkRCL_Vector = new LinkReceiveChannelPool<string, Link_ContextEvent_VectorChange>();
        [FoldoutGroup("事件收发"), LabelText("整数变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_IntChange> LinkRCL_Int = new LinkReceiveChannelPool<string, Link_ContextEvent_IntChange>();
        [FoldoutGroup("事件收发"), LabelText("字符串变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_StringChange> LinkRCL_String = new LinkReceiveChannelPool<string, Link_ContextEvent_StringChange>();
        [FoldoutGroup("事件收发"), LabelText("布尔值变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_BoolChange> LinkRCL_Bool = new LinkReceiveChannelPool<string, Link_ContextEvent_BoolChange>();
        [FoldoutGroup("事件收发"), LabelText("任意类型变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_ClassTChange> LinkRCL_ClassT = new LinkReceiveChannelPool<string, Link_ContextEvent_ClassTChange>();
        [FoldoutGroup("事件收发"), LabelText("UnityObject类型变更事件"), ReadOnly]
        public LinkReceiveChannelPool<string, Link_ContextEvent_UnityObjectChange> LinkRCL_UnityObjectT = new LinkReceiveChannelPool<string, Link_ContextEvent_UnityObjectChange>();

        public void Enable()
        {
            if (enabled) return;
            enabled = true;
            foreach (var (k, v) in _ContextValues)
            {
                v.AddReceivePool(this);
            }
        }
        public void Disable()
        {
            if (!enabled) return;
            enabled = false;
            foreach (var (k, v) in _ContextValues)
            {
                v.RemoveReceivePool(this);
            }
        }
        [NonSerialized]
        public bool enabled = false;

        public void AddContextValueTruelyToCreate(string key, IContextitectureValue Contextitecture)
        {
            _ContextValues.Add(key, Contextitecture);
            if (enabled)
                Contextitecture.AddReceivePool(this);
        }
        public void RemoveContextValueTruelyToCreate(string key, IContextitectureValue Contextitecture)
        {
            if (_ContextValues.TryGetValue(key, out var Context))
            {
                _ContextValues.Remove(key);
                Contextitecture.RemoveReceivePool(this);
            }
        }
        #endregion
    }
    
    [Serializable]
    public struct ContextKeyValue
    {
        public string key;
        public object value;
    }
}




