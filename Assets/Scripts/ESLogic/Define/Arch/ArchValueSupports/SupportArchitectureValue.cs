using Sirenix.OdinInspector;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{

    #region 原型值类型
    [Serializable, TypeRegistryItem("原型值_动态标签")]
    public class ArchitectureTypeValue_DynamicTag : ArchitectureValue<float>
    {
        protected override string KeyLabel => "【tag】";
        protected override string ValueLabel => "标签获得时间";
        [LabelText("最长生效时间")]
        public float MaxUseableDURA = 999999;
        //Value=>获得时间
        public override EnumCollect.ArchitectureValueType ArchType => EnumCollect.ArchitectureValueType.DynamicTag;
        public override object TheSmartValue { get { return Value; } }

        public ArchitectureTypeValue_DynamicTag()
        {
            Value = Time.time;
        }

        //设置的是 -- 最大持续时间

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetValue(object o)
        {
            if (o is float f)
            {
                Value = f;
            }
            else if (o is int i)
            {
                Value = i;
            }
            else if (o is bool b)
            {
                Value = b ? Time.time: -MaxUseableDURA;
            }
        }
        #region GET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool GetBool()
        {
            return Time.time - Value < MaxUseableDURA;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override float GetFloat()
        {
            //获得剩余持续时间
            return MaxUseableDURA-(Time.time - Value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override int GetInt()
        {
            return Mathf.RoundToInt(MaxUseableDURA - (Time.time - Value));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return (MaxUseableDURA - (Time.time - Value)).ToString();
        }
        #endregion
        #region SET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetBool(bool b)
        {
            Value = b ? Time.time : -MaxUseableDURA;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetFloat(float f)
        {
            MaxUseableDURA=f;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetInt(int i)
        {
            MaxUseableDURA = i;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetString(string s)
        {
            MaxUseableDURA = s?.Length ?? 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(float value1, float value2)
        {
            return value2 != value1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ArchPool pool, float from)
        {
            pool.LinkRCL_Tag.SendLink(key, new Link_ArchEvent_TagChange() { Value_time_Pre = from, Value_time_Now = Value });
        }
        #endregion
    }
    [Serializable, TypeRegistryItem("原型值_浮点值")]
    public class ArchitectureTypeValue_Float : ArchitectureValue<float>
    {
        protected override string KeyLabel => "【float】";
        protected override string ValueLabel => "浮点值";
        public override EnumCollect.ArchitectureValueType ArchType => EnumCollect.ArchitectureValueType.FloatValue;
        public override object TheSmartValue { get { return Value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetValue(object v)
        {
            if (v is float f)
            {
                Value = f;
            }
            else if (v is int i)
            {
                Value = i;
            }
            else if (v is bool b)
            {
                Value = b ? 1 : 0;
            }
        }
        #region GET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool GetBool()
        {
            return Value > 0 ? true : false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override float GetFloat()
        {
            return Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override int GetInt()
        {
            return Mathf.RoundToInt(Value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return Value.ToString();
        }
        #endregion
        #region SET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetBool(bool b)
        {
            Value = b ? 1 : 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetFloat(float f)
        {
            Value = f;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetInt(int i)
        {
            Value = i;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetString(string s)
        {
            Value = s?.Length ?? 0;
        }
        #endregion
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(float value1, float value2)
        {
            return value1 != value2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ArchPool pool, float from)
        {
            pool.LinkRCL_Float.SendLink(key,new Link_ArchEvent_FloatChange() {Value_Pre = from, Value_Now = Value });
        }
    }
    [Serializable, TypeRegistryItem("原型值_整数值")]
    public class ArchitectureTypeValue_Int : ArchitectureValue<int>
    {
        protected override string KeyLabel => "【int】";
        protected override string ValueLabel => "整数值";
        public override EnumCollect.ArchitectureValueType ArchType => EnumCollect.ArchitectureValueType.IntValue;
        public override object TheSmartValue { get { return Value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetValue(object v)
        {
            if (v is int i)
            {
                Value = i;
            }
            else if (v is float f)
            {
                Value = Mathf.RoundToInt(f);
            }
            else if (v is bool b)
            {
                Value = b ? 1 : 0;
            }
            else if (v is string s)
            {
                Value = s?.Length ?? 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool GetBool()
        {
            return Value > 0 ? true : false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override float GetFloat()
        {
            return Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override int GetInt()
        {
            return Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return Value.ToString();
        }
        #region SET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetBool(bool b)
        {
            Value = b ? 1 : 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetFloat(float f)
        {
            Value = Mathf.RoundToInt(f);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetInt(int i)
        {
            Value = i;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetString(string s)
        {
            Value = s?.Length ?? 0;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(int value1, int value2)
        {
            return value1 != value2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ArchPool pool, int from)
        {
            pool.LinkRCL_Int.SendLink(key, new Link_ArchEvent_IntChange() {Value_Pre = from, Value_Now = Value });
        }
    }
    [Serializable, TypeRegistryItem("原型参数值_字符串值")]
    public class ArchitectureTypeValue_String : ArchitectureValue<string>
    {
        protected override string KeyLabel => "【string】";
        protected override string ValueLabel => "字符串值";
        public override EnumCollect.ArchitectureValueType ArchType => EnumCollect.ArchitectureValueType.StringValue;
        public override object TheSmartValue { get { return Value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetValue(object v)
        {
            Value = v.ToString();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool GetBool()
        {
            return Value?.IsNullOrWhitespace() ?? false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override float GetFloat()
        {
            return Value?.Length ?? 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override int GetInt()
        {
            return Value?.Length ?? 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return Value.ToString();
        }
        #region SET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetBool(bool b)
        {
            Value = b.ToString();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetFloat(float f)
        {
            Value = f.ToString();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetInt(int i)
        {
            Value = i.ToString();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetString(string s)
        {
            Value = s;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(string value1, string value2)
        {
            return value1 != value2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ArchPool pool, string from)
        {
            pool.LinkRCL_String.SendLink(key, new Link_ArchEvent_StringChange() { Value_Pre = from, Value_Now = Value });
        }
    }
    [Serializable, TypeRegistryItem("原型参数值_布尔值")]
    public class ArchitectureTypeValue_Bool : ArchitectureValue<bool>
    {
        protected override string KeyLabel => "【bool】";
        protected override string ValueLabel => "标签是否生效";
        public override EnumCollect.ArchitectureValueType ArchType => EnumCollect.ArchitectureValueType.BoolValue;
        public override object TheSmartValue { get { return Value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetValue(object o)
        {
            if (o is bool b)
            {
                Value = b;
            }
            else if (o is float f)
            {
                Value = f > 0;
            }
            else if (o is string s)
            {
                Value = s.IsNullOrWhitespace();
            }
            else if (o is UnityEngine.Object uo)
            {
                Value = uo != null;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool GetBool()
        {
            return Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override float GetFloat()
        {
            return Value ? 1 : 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override int GetInt()
        {
            return Value ? 1 : 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return Value.ToString();
        }
        #region SET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetBool(bool b)
        {
            Value = b;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetFloat(float f)
        {
            Value = f > 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetInt(int i)
        {
            Value = i > 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetString(string s)
        {
            Value = !s?.IsNullOrWhitespace() ?? false;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(bool value1, bool value2)
        {
            return value1 != value2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ArchPool pool, bool from)
        {
            pool.LinkRCL_Bool.SendLink(key, new Link_ArchEvent_BoolChange() { Value_Pre = from, Value_Now = Value });
        }
    }
    [Serializable, TypeRegistryItem("原型参数值类型")]
    public abstract class ArchitectureValue<ValueT> : IArchitectureValue
    {
        #region 基本修饰

        [LabelText("", Text = "@KeyLabel"), GUIColor("@KeyValueMatchingUtility.ColorSelector.Color_03")]
        public string key = "default";
        [LabelText("", Text = "@ValueLabel"), ReadOnly, SerializeField, GUIColor("@KeyValueMatchingUtility.ColorSelector.Color_02")]
        private ValueT _value;

        public string TheKey => key;
        protected abstract string KeyLabel { get; }
        protected abstract string ValueLabel { get; }
        public abstract object TheSmartValue { get; }
        public IArchitectureValue getArch => this;
        public abstract EnumCollect.ArchitectureValueType ArchType { get; }
        #endregion

        #region 值与修改事件
        [LabelText("发送Link事件"), GUIColor("@KeyValueMatchingUtility.ColorSelector.Color_01")]
        public bool SendLink = false;
        public ValueT Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _value; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (IsNotEqual(_value, value))
                {
                    ValueT cache = _value;
                    _value = value;
                    int count = pools.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var use = pools[i];
                        if (use == null) continue;
                        if (SendLink) { SendLinkToPool(use, cache);
                        }
                    }
                }
            }
        }

        public bool WillSendLink { get => SendLink; set => SendLink=value; }

        public abstract bool IsNotEqual(ValueT value1, ValueT value2);

        public abstract void SendLinkToPool(ArchPool pool, ValueT from);
        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void SetValue(object o);

        public void DeepCloneFrom(IArchitectureValue t)
        {
            key = t.TheKey;
            SetValue(t.TheSmartValue);
        }

        #region Getter

        public abstract bool GetBool();
        public abstract float GetFloat();
        public abstract int GetInt();
        public abstract string GetString();

        public abstract void SetBool(bool b);
        public abstract void SetFloat(float f);
        public abstract void SetInt(int i);
        public abstract void SetString(string s);
        #endregion

        #region 绑定池
        protected List<ArchPool> pools = new List<ArchPool>();
        public void AddReceivePool(ArchPool pool)
        {
            pools.Add(pool);
        }

        public void RemoveReceivePool(ArchPool pool)
        {
            pools.Remove(pool);
        }
        #endregion
    }
    public interface IArchitectureValue : IDeepClone<IArchitectureValue>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IArchitectureValue Create(EnumCollect.ArchitectureValueType type, string key,bool send)
        {
            switch (type)
            {
                case EnumCollect.ArchitectureValueType.DynamicTag: return new ArchitectureTypeValue_DynamicTag() { key = key,SendLink=send };
                case EnumCollect.ArchitectureValueType.FloatValue: return new ArchitectureTypeValue_Float() { key = key, SendLink = send };
                case EnumCollect.ArchitectureValueType.IntValue: return new ArchitectureTypeValue_Int() { key = key, SendLink = send };
                case EnumCollect.ArchitectureValueType.BoolValue: return new ArchitectureTypeValue_Bool() { key = key, SendLink = send };
                case EnumCollect.ArchitectureValueType.StringValue: return new ArchitectureTypeValue_String() { key = key, SendLink = send };
            }
            return new ArchitectureTypeValue_String();
        }
        public abstract IArchitectureValue getArch { get; }
        public string TheKey { get; }
        public object TheSmartValue { get; }
        public bool WillSendLink { get; set; }
        public void SetValue(object o);
        public EnumCollect.ArchitectureValueType ArchType { get; }

        #region GETSET
        public abstract bool GetBool();
        public abstract float GetFloat();
        public abstract int GetInt();
        public abstract string GetString();
        public abstract void SetBool(bool b);
        public abstract void SetFloat(float f);
        public abstract void SetInt(int i);
        public abstract void SetString(string s);
        #endregion

        #region 绑定
        public void AddReceivePool(ArchPool pool);
        public void RemoveReceivePool(ArchPool pool);

        #endregion

    }
    #endregion
}
