/*
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{

    /*  IValueEntryOperation
     * 值通道导向操作，他的目的是把一个效果指定到特定的无解析�?值一般有数值和引用两种>上，
         ，经常是和其他内容配合的
    
    public class Operaton : IOperation
    {
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public static float OpearationFloat_Inline(float BaseValue, float HandleValue, FloatValueEntryType settleType)
        {
            switch (settleType)
            {
                case FloatValueEntryType.Add: return BaseValue + HandleValue;
                case FloatValueEntryType.Subtract: return BaseValue - HandleValue;
                case FloatValueEntryType.PercentageUp: return BaseValue * (1 + HandleValue);
                case FloatValueEntryType.ClampMax: return Mathf.Clamp(BaseValue, BaseValue, HandleValue);
                case FloatValueEntryType.ClampMin: return Mathf.Clamp(BaseValue, HandleValue, BaseValue);
                case FloatValueEntryType.Wave: return BaseValue + UnityEngine.Random.Range(-HandleValue, HandleValue);
                default: return BaseValue;
            }
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public static float OpearationFloat_Cancel_Inline(float value, float Value, FloatValueEntryType settleType)
        {
            switch (settleType)
            {
                case FloatValueEntryType.Add: return value - Value;
                case FloatValueEntryType.Subtract: return value + Value;
                case FloatValueEntryType.PercentageUp: return value._SafeDivide(1 + Value);
                case FloatValueEntryType.ClampMax: return Mathf.Clamp(value, value, Value);
                case FloatValueEntryType.ClampMin: return Mathf.Clamp(value, Value, value);
                case FloatValueEntryType.Wave: return value + UnityEngine.Random.Range(-Value, Value);
                default: return value;
            }
        }
    }
    public interface IValueEntryOperation<Target,Logic, TargetType,OpeationType, OpeationOptions> : IOperation<Target,Logic>
    {
        public abstract void HandleValueEntryOpeation(Target target,Logic logic, OpeationType Opeation_, OpeationOptions SelectType_);

        public abstract void HandleValueEntryCancel(Target target,Logic logic, OpeationType Opeation_, OpeationOptions SelectType_);
    }

    public abstract class ValueEntryOperation<Target,Logic, ValueType_, OpeationType, WithSelector> : IValueEntryOperation<Target,Logic, ValueType_, OpeationType, WithSelector>
    {
        public abstract void HandleValueEntryCancel(Target target,Logic logic, OpeationType Opeation_, WithSelector SelectType_);
        public abstract void HandleValueEntryOpeation(Target target,Logic logic, OpeationType Opeation_, WithSelector SelectType_);
        protected abstract void ExpandOperation(ref ValueType_ or, OpeationType Opeation, WithSelector SelectType_);
        protected abstract void ExpandCancel(ref ValueType_ or, OpeationType Opeation, WithSelector SelectType_);


    }

    #region 演示
    //Float 值通道
    public abstract class ValueEntryFloatOperation<Target,Logic> : ValueEntryOperation<Target,Logic, float, float, FloatValueEntryType>
    {
        protected sealed override void ExpandOperation(ref float or, float OperationValue, FloatValueEntryType SelectType_)
        {
            or = Operaton.OpearationFloat_Inline(or, OperationValue, SelectType_);
        }
        protected sealed override void ExpandCancel(ref float or, float OperationValue, FloatValueEntryType SelectType_ = FloatValueEntryType.Add)
        {
            or = Operaton.OpearationFloat_Cancel_Inline(or, OperationValue, SelectType_);
        }
    }

  
    #endregion


}

*/