using Sirenix.OdinInspector;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{

    #region 原型值类型
    [Serializable, TypeRegistryItem("T类型Object")]
    public class ContextitectureTypeValue_ClassT : ContextitectureValue<object>
    {
        [DisplayAsString(alignment:TextAlignment.Center),ShowInInspector,HideLabel]
        public string warn => "object 类型 一般不具有序列化能力";
        protected override string KeyLabel => "【任意类型】";
        protected override string ValueLabel => "任意类型值";
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.ClassTValue;
        public override object TheSmartValue { get { return Value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetValue(object v)
        {
            Value = v;
        }
        #region GET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return Value.ToString();
        }
        #endregion
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(object value1, object value2)
        {
            return value1 != value2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ContextPool pool, object from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_ClassT.SendLink(key, new Link_ContextEvent_ClassTChange() { Value_Pre = from, Value_Now = Value, Create = create, Remove = remove });
        }
    }
    [Serializable, TypeRegistryItem("UnityObject")]
    public class ContextitectureTypeValue_UnityObject : ContextitectureValue<UnityEngine.Object>
    {
        protected override string KeyLabel => "【Unity】";
        protected override string ValueLabel => "Unity物体";
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.UnityObjectTValue;
        public override object TheSmartValue { get { return Value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetValue(System.Object v)
        {
            if (v is UnityEngine.Object uo) Value = uo; 
        }
        #region GET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return Value.ToString();
        }
        #endregion
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(UnityEngine.Object value1, UnityEngine.Object value2)
        {
            return value1 != value2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ContextPool pool, UnityEngine.Object from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_UnityObjectT.SendLink(key, new Link_ContextEvent_UnityObjectChange() { Value_Pre = from, Value_Now = Value, Create = create, Remove = remove });
        }
    }
    [Serializable, TypeRegistryItem("动态标签Tag")]
    public class ContextitectureTypeValue_DynamicTag : ContextitectureValue<float>
    {
        public override void OnCreate()
        {
            base.OnCreate();
            Value = Time.time;
        }
        protected override string KeyLabel => "【Tag标签】";
        protected override string ValueLabel => "标签获得时间";
        [LabelText("最长生效时间")]
        public float MaxUseableDURA = 999999;
        //Value=>获得时间
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.DynamicTag;
        public override object TheSmartValue { get { return Value; } }

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
                Value = b ? Time.time : -MaxUseableDURA;
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
            return MaxUseableDURA - (Time.time - Value);
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
            MaxUseableDURA = f;
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
        public sealed override void SendLinkToPool(ContextPool pool, float from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_Tag.SendLink(key, new Link_ContextEvent_TagChange() { Value_time_Pre = from, Value_time_Now = Value });
        }
        #endregion
    }
    [Serializable, TypeRegistryItem("浮点值Float")]
    public class ContextitectureTypeValue_Float : ContextitectureValue<float>
    {
        protected override string KeyLabel => "【Float】";
        protected override string ValueLabel => "浮点值";
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.FloatValue;
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
        public sealed override void SendLinkToPool(ContextPool pool, float from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_Float.SendLink(key, new Link_ContextEvent_FloatChange() { Value_Pre = from, Value_Now = Value, Create = create, Remove = remove });
        }
    }
    [Serializable, TypeRegistryItem("向量Vector3")]
    public class ContextitectureTypeValue_Vector : ContextitectureValue<Vector3>
    {
        protected override string KeyLabel => "【Vector3】";
        protected override string ValueLabel => "向量值";
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.VectorValue;
        public override object TheSmartValue { get { return Value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetValue(object v)
        {
            if (v is Vector3 vv)
            {
                Value = vv;
            }
            else if (v is float f)
            {
                Value = new Vector3(f, f, f);
            }
            else if (v is int i)
            {
                Value = new Vector3(i, i, i);
            }
            else if (v is bool b)
            {
                Value = b ? Vector3.one : default;
            }
        }
        #region GET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool GetBool()
        {
            return Value != default ? true : false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override float GetFloat()
        {
            return Value.magnitude;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override int GetInt()
        {
            return Mathf.RoundToInt(Value.magnitude);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override string GetString()
        {
            return Value.ToString();
        }
        public override Vector3 GetVector()
        {
            return Value;
        }
        #endregion
        #region SET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetBool(bool b)
        {
            Value = b ? Vector3.one : default;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetFloat(float f)
        {
            Value = new Vector3(f, f, f);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetInt(int i)
        {
            Value = new Vector3(i, i, i);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SetString(string s)
        {

        }
        public override void SetVector(Vector3 v)
        {
            Value = v;
        }
        #endregion
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override bool IsNotEqual(Vector3 value1, Vector3 value2)
        {
            return value1 != value2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void SendLinkToPool(ContextPool pool, Vector3 from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_Vector.SendLink(key, new Link_ContextEvent_VectorChange() { Value_Pre = from, Value_Now = Value, Create = create, Remove = remove });
        }
    }
    [Serializable, TypeRegistryItem("整数值Int")]
    public class ContextitectureTypeValue_Int : ContextitectureValue<int>
    {
        protected override string KeyLabel => "【Int】";
        protected override string ValueLabel => "整数值";
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.IntValue;
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
        public sealed override void SendLinkToPool(ContextPool pool, int from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_Int.SendLink(key, new Link_ContextEvent_IntChange() { Value_Pre = from, Value_Now = Value, Create = create, Remove = remove });
        }
    }
    [Serializable, TypeRegistryItem("字符串值String")]
    public class ContextitectureTypeValue_String : ContextitectureValue<string>
    {
        protected override string KeyLabel => "【String】";
        protected override string ValueLabel => "字符串值";
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.StringValue;
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
        public sealed override void SendLinkToPool(ContextPool pool, string from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_String.SendLink(key, new Link_ContextEvent_StringChange() { Value_Pre = from, Value_Now = Value, Create = create, Remove = remove });
        }
    }
    [Serializable, TypeRegistryItem("布尔值Bool")]
    public class ContextitectureTypeValue_Bool : ContextitectureValue<bool>
    {
        protected override string KeyLabel => "【Bool】";
        protected override string ValueLabel => "标签是否生效";
        public override EnumCollect.ContextValueType ContextType => EnumCollect.ContextValueType.BoolValue;
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
        public sealed override void SendLinkToPool(ContextPool pool, bool from, bool create = false, bool remove = false)
        {
            pool.LinkRCL_Bool.SendLink(key, new Link_ContextEvent_BoolChange() { Value_Pre = from, Value_Now = Value, Create = create, Remove = remove });
        }
    }
    [Serializable, TypeRegistryItem("原型参数值类型"), HideReferenceObjectPicker]
    public abstract class ContextitectureValue<ValueT> : IContextitectureValue
    {
        #region 基本修饰

        [LabelText("", Text = "@KeyLabel"), GUIColor("@ESDesignUtility.ColorSelector.Color_03"), HideInPlayMode]
        public string key = "default";
        [LabelText("", Text = "@ValueLabel"),DisableInPlayMode, SerializeField,HideReferenceObjectPicker, GUIColor("ValueColor")]
        private ValueT _value;
        public Color ValueColor()
        {
            if (Application.isPlaying) return ESDesignUtility.ColorSelector.Color_03;
            return ESDesignUtility.ColorSelector.Color_04;
        }
        public string TheKey {get => key; set { key = value;} }
        protected virtual string KeyLabel { get => "键"; }
        protected virtual string ValueLabel { get => "值"; }
        public virtual object TheSmartValue { get => _value; }
        public IContextitectureValue Context => this;
        public abstract EnumCollect.ContextValueType ContextType { get; }
        #endregion

        #region 值与修改事件
        [ESBoolOption("实时发送Link事件","仅添加移除时发射事件"), GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
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
                        if (SendLink)
                        {
                            SendLinkToPool(use, cache);
                        }
                    }
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueT GetValue() => _value;
        public bool WillSendLink { get => SendLink; set => SendLink = value; }

        public abstract bool IsNotEqual(ValueT value1, ValueT value2);

        public abstract void SendLinkToPool(ContextPool pool, ValueT from, bool create = false, bool remove = false);
        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void SetValue(object o);

        public void DeepCloneFrom(IContextitectureValue t)
        {
            key = t.TheKey;
            SetValue(t.TheSmartValue);
        }

        #region GetSet

        public virtual bool GetBool() { return default; }
        public virtual float GetFloat() { return default; }
        public virtual int GetInt() { return default; }
        public virtual string GetString() { return ""; }
        public virtual Vector3 GetVector() => default;
        public virtual void SetBool(bool b) { }
        public virtual void SetFloat(float f) { }
        public virtual void SetVector(Vector3 v) { }
        public virtual void SetInt(int i) { }
        public virtual void SetString(string s) { }
        #endregion

        #region 绑定池
        protected List<ContextPool> pools = new List<ContextPool>();
        public void AddReceivePool(ContextPool pool)
        {
            pools.Add(pool);
            OnCreate();
            SendLinkToPool(pool, _value, true);
        }

        public void RemoveReceivePool(ContextPool pool)
        {
            pools.Remove(pool);
            OnRemove();
            SendLinkToPool(pool, _value, false, true);
        }

        public virtual void OnCreate()
        {

        }
        public virtual void OnRemove()
        {

        }
        #endregion
    }
    public interface IContextitectureValue : IDeepClone<IContextitectureValue>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IContextitectureValue Create(EnumCollect.ContextValueType type, string key, bool send)
        {
            switch (type)
            {
                case EnumCollect.ContextValueType.DynamicTag: return new ContextitectureTypeValue_DynamicTag() { key = key, SendLink = send };
                case EnumCollect.ContextValueType.FloatValue: return new ContextitectureTypeValue_Float() { key = key, SendLink = send };
                case EnumCollect.ContextValueType.IntValue: return new ContextitectureTypeValue_Int() { key = key, SendLink = send };
                case EnumCollect.ContextValueType.BoolValue: return new ContextitectureTypeValue_Bool() { key = key, SendLink = send };
                case EnumCollect.ContextValueType.StringValue: return new ContextitectureTypeValue_String() { key = key, SendLink = send };
            }
            return new ContextitectureTypeValue_String();
        }
        public abstract IContextitectureValue Context { get; }
        public string TheKey { get; set; }
        public object TheSmartValue { get; }
        public bool WillSendLink { get; set; }
        public void SetValue(object o);

     
        public EnumCollect.ContextValueType ContextType { get; }

        #region GETSET-Define
        public bool GetBool();
        public float GetFloat();
        public int GetInt();
        public string GetString();
        public Vector3 GetVector();
        public void SetBool(bool b);
        public void SetFloat(float f);
        public void SetInt(int i);
        public void SetString(string s);
        public void SetVector(Vector3 v);
        #endregion

        #region 绑定到池
        public void AddReceivePool(ContextPool pool);
        public void RemoveReceivePool(ContextPool pool);

        #endregion

    }
    #endregion
}
