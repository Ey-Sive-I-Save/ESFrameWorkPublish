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
     * 值通道导向操作，他的目的是把一个效果指定到特定的无解析值<值一般有数值和引用两种>上，
         ，经常是和其他内容配合的
    */
    public class Operaton : IOperation
    {
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public static float OpearationFloat_Inline(float value, float Value, OperationOptionsForFloat settleType)
        {
            switch (settleType)
            {
                case OperationOptionsForFloat.Add: return value + Value;
                case OperationOptionsForFloat.Sub: return value - Value;
                case OperationOptionsForFloat.PerUp: return value * (1 + Value);
                case OperationOptionsForFloat.Max: return Mathf.Clamp(value, value, Value);
                case OperationOptionsForFloat.Min: return Mathf.Clamp(value, Value, value);
                case OperationOptionsForFloat.Wave: return value + UnityEngine.Random.Range(-Value, Value);
                default: return value;
            }
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public static float OpearationFloat_Cancel_Inline(float value, float Value, OperationOptionsForFloat settleType)
        {
            switch (settleType)
            {
                case OperationOptionsForFloat.Add: return value - Value;
                case OperationOptionsForFloat.Sub: return value + Value;
                case OperationOptionsForFloat.PerUp: return value._SafeDivide(1 + Value);
                case OperationOptionsForFloat.Max: return Mathf.Clamp(value, value, Value);
                case OperationOptionsForFloat.Min: return Mathf.Clamp(value, Value, value);
                case OperationOptionsForFloat.Wave: return value + UnityEngine.Random.Range(-Value, Value);
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
    public abstract class ValueEntryFloatOperation<Target,Logic> : ValueEntryOperation<Target,Logic, float, float, OperationOptionsForFloat>
    {
        protected sealed override void ExpandOperation(ref float or, float OperationValue, OperationOptionsForFloat SelectType_)
        {
            or = Operaton.OpearationFloat_Inline(or, OperationValue, SelectType_);
        }
        protected sealed override void ExpandCancel(ref float or, float OperationValue, OperationOptionsForFloat SelectType_ = OperationOptionsForFloat.Add)
        {
            or = Operaton.OpearationFloat_Cancel_Inline(or, OperationValue, SelectType_);
        }
    }

  
    #endregion


}
