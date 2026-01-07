using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{
    public interface ISettleOperation 
    {
        public void SetEffectorValue(float f);
        public void SetEffectorValue(bool b);
    }

    [TypeRegistryItem("结算操作"), Serializable]
    public abstract class SettleOperation<ValueType, Ment, This> : ISettleOperation, IComparable<This>, IOperation
        where This : SettleOperation<ValueType, Ment, This>, new()
    {
        [NonSerialized] public object Source;    // 效果来源
        [LabelText("操作值")] public ValueType ValueEffector;      // 效果数值
        [LabelText("优先级")] public byte Order;    // 应用优先级（数值越小越优先）
        public int CompareTo(This other)
        {
            return Order.CompareTo(other.Order);
        }
        public abstract ValueType HandleOperation(ValueType value);
        
        public abstract void SetEffectorValue(float f);

        public abstract void SetEffectorValue(bool b);
        
        /// <summary>
        /// 解决该操作所影响的值，这个值可能被动态创建和修改
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueType GetResolveValue()
        {
            return ValueEffector;
        }

    }

    [Serializable, TypeRegistryItem("布尔值结算操作")]
    public class SettleOperationBool : SettleOperation<bool, SettlementBool, SettleOperationBool>
    {
        public override bool HandleOperation(bool value)
        {
            return value || ValueEffector; // 基类默认实现
        }

        public override void SetEffectorValue(float f)
        {
            ValueEffector = f > 0;
        }

        public override void SetEffectorValue(bool b)
        {
            ValueEffector = b;
        }
    }

    #region 布尔多态操作
    [Serializable, TypeRegistryItem("【或||】布尔结算操作")]
    public class SettleOperationBool_Or : SettleOperationBool
    {
        public SettleOperationBool_Or()
        {
            Order = 10; // 逻辑或，较低优先级
        }
        
        public override bool HandleOperation(bool value)
        {
            return value || ValueEffector;
        }
    }

    [Serializable, TypeRegistryItem("【且&&】布尔结算操作")]
    public class SettleOperationBool_And : SettleOperationBool
    {
        public SettleOperationBool_And()
        {
            Order = 30; // 逻辑与，较高优先级
        }
        
        public override bool HandleOperation(bool value)
        {
            return value && ValueEffector;
        }
    }

    [Serializable, TypeRegistryItem("【开On】布尔结算操作")]
    public class SettleOperationBool_TurnOn : SettleOperationBool
    {
        public SettleOperationBool_TurnOn()
        {
            Order = 60; // 强制开启，最高优先级
        }
        
        public override bool HandleOperation(bool value)
        {
            return true;
        }
    }

    [Serializable, TypeRegistryItem("【关Off】布尔结算操作")]
    public class SettleOperationBool_TurnOff : SettleOperationBool
    {
        public SettleOperationBool_TurnOff()
        {
            Order = 80; // 强制关闭，最高优先级
        }
        
        public override bool HandleOperation(bool value)
        {
            return false;
        }
    }

    [Serializable, TypeRegistryItem("【非】布尔结算操作")]
    public class SettleOperationBool_Not : SettleOperationBool
    {
        public SettleOperationBool_Not()
        {
            Order = 100; // 取反，最低优先级
        }
        
        public override bool HandleOperation(bool value)
        {
            return !value;
        }
    }
    #endregion

    [Serializable, TypeRegistryItem("浮点结算操作")]
    public class SettleOperationFloat : SettleOperation<float, SettlementFloat, SettleOperationFloat>
    {
        [LabelText("特殊类型")] public SettleOperationSelfType selfType = SettleOperationSelfType.None;
        public override float HandleOperation(float value)
        {
            return value + GetResolveValue();
        }

        public sealed override void SetEffectorValue(float f)
        {
            ValueEffector = f;
        }

        public sealed override void SetEffectorValue(bool b)
        {
            ValueEffector = b ? 1 : 0;
        }

        public void TryStart(SettlementFloat to, bool ForceNormal = false)
        {
            if (to != null)
            {
                if (!ForceNormal && selfType.HasFlag(SettleOperationSelfType.Dynamic)) to.AddDynamicOperation(this);
                else to.AddNormalOperation(this);
            }
        }
        public void TryStop(SettlementFloat to, bool ForceNormal = false)
        {
            if (to != null)
            {
                if (!ForceNormal && selfType.HasFlag(SettleOperationSelfType.Dynamic)) to.RemoveDynamicOperation(this);
                else to.RemoveNormalOperation(this);
            }
        }
    }

    #region  多态完成
    [Serializable, TypeRegistryItem("【加】浮点结算操作")]
    public class SettleOperationFloat_Add : SettleOperationFloat
    {
        public SettleOperationFloat_Add()
        {
            Order = 20; // 第一步：基础数值运算
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float HandleOperation(float value)
        {
            return value + GetResolveValue();
        }
    }

    [Serializable, TypeRegistryItem("【减】浮点结算操作")]
    public class SettleOperationFloat_Subtract : SettleOperationFloat
    {
        public SettleOperationFloat_Subtract()
        {
            Order = 20; // 第一步：基础数值运算
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float HandleOperation(float value)
        {
            return value - GetResolveValue();
        }
    }

    [Serializable, TypeRegistryItem("【震荡】浮点结算操作")]
    public class SettleOperationFloat_Wave : SettleOperationFloat
    {
        [LabelText("震荡频率")] public float frequency = 1f;
        
        public SettleOperationFloat_Wave()
        {
            Order = 40; // 第二步：在基础值基础上添加特效
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float HandleOperation(float value)
        {
            return value + Mathf.Sin(Time.time * frequency) * GetResolveValue();
        }
    }

    [Serializable, TypeRegistryItem("【乘】浮点结算操作")]
    public class SettleOperationFloat_Multiply : SettleOperationFloat
    {
        public SettleOperationFloat_Multiply()
        {
            Order = 60; // 第三步：倍率运算
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float HandleOperation(float value)
        {
            return value * GetResolveValue();
        }
    }

    [Serializable, TypeRegistryItem("【增益】浮点结算操作")]
    public class SettleOperationFloat_PercentageUp : SettleOperationFloat
    {
        public SettleOperationFloat_PercentageUp()
        {
            Order = 70; // 第三步：百分比增益（与乘法同级）
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float HandleOperation(float value)
        {
            return value * (1f + GetResolveValue());
        }
    }

    [Serializable, TypeRegistryItem("【限制最大】浮点结算操作")]
    public class SettleOperationFloat_ClampMax : SettleOperationFloat
    {
        public SettleOperationFloat_ClampMax()
        {
            Order = 100; // 第四步：最终限制，确保合理范围
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float HandleOperation(float value)
        {
            return Mathf.Min(value, GetResolveValue());
        }
    }

    [Serializable, TypeRegistryItem("【限制最小】浮点结算操作")]
    public class SettleOperationFloat_ClampMin : SettleOperationFloat
    {
        public SettleOperationFloat_ClampMin()
        {
            Order = 100; // 第四步：最终限制，确保合理范围
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float HandleOperation(float value)
        {
            return Mathf.Max(value, GetResolveValue());
        }
    }

    #endregion


}
