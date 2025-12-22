using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public interface ISettleOperation : IPoolablebAuto
    {
        public void SetValue(float f);
        public void SetValue(bool b);
    }

    [TypeRegistryItem("结算操作"), Serializable]
    public abstract class SettleOperation<ValueType, Ment, This> : ISettleOperation, IComparable<This>, IOperation 
        where This : SettleOperation<ValueType, Ment, This>,new()
    {
        [NonSerialized] public object Source;    // 效果来源
        [LabelText("操作值")] public ValueType Value;      // 效果数值
        [LabelText("优先级")] public int Priority;    // 应用优先级（数值越小越优先）
        public int CompareTo(This other)
        {
            return Priority.CompareTo(other.Priority);
        }
        public abstract ValueType HandleOperation(ValueType value);

        #region 池化
        public bool IsRecycled { get; set; }

        public void OnResetAsPoolable()
        {
           
        }
        public static This GetOne()
        {
            return POOL.GetInPool();
        }
        public static ESSimplePool<This> POOL = new ESSimplePool<This>(() => new This(), null, 10);
        public void TryAutoPushedToPool()
        {
            POOL.PushToPool(this as This);
        }

        public abstract void SetValue(float f);


        public abstract void SetValue(bool b);
       
        #endregion
    }


    [Serializable, TypeRegistryItem("布尔值结算操作")]
    public class SettleOperationBool : SettleOperation<bool, SettlementBool, SettleOperationBool>
    {
        [LabelText("结算操作类型")] public OperationOptionsForBool settleType = OperationOptionsForBool.Or;
        public override bool HandleOperation(bool value)
        {
            switch (settleType)
            {
                case OperationOptionsForBool.Or: return value || Value;
                case OperationOptionsForBool.And: return value && Value;
                case OperationOptionsForBool.ON: return true;
                case OperationOptionsForBool.OFF: return false;
                case OperationOptionsForBool.Not: return !value;
                default: return value;
            }
        }

        public override void SetValue(float f)
        {
            Value = f > 0;
        }

        public override void SetValue(bool b)
        {
            Value = b;
        }
    }

    [Serializable, TypeRegistryItem("浮点结算操作")]
    public class SettleOperationFloat : SettleOperation<float, SettlementFloat, SettleOperationFloat>
    {
        [LabelText("特殊类型")] public SettleSelfTypeForFloat selfType = SettleSelfTypeForFloat.None;
        [LabelText("结算操作类型")] public OperationOptionsForFloat settleType = OperationOptionsForFloat.Add;
        public override float HandleOperation(float value)
        {
            return Operaton.OpearationFloat_Inline(value, Value, settleType);
        }

        public override void SetValue(float f)
        {
            Value = f;
        }

        public override void SetValue(bool b)
        {
            Value = b ?1 : 0;
        }

        public void TryStart(SettlementFloat to, bool ForceNormal = false)
        {
            if (to != null)
            {
                if (!ForceNormal && selfType.HasFlag(SettleSelfTypeForFloat.Dynamic)) to.AddDynamicOperation(this);
                else to.AddNormalOperation(this);
            }
        }
        public void TryStop(SettlementFloat to, bool ForceNormal = false)
        {
            if (to != null)
            {
                if (!ForceNormal && selfType.HasFlag(SettleSelfTypeForFloat.Dynamic)) to.RemoveDynamicOperation(this);
                else to.RemoveNormalOperation(this);
            }
        }
    }


  
}
