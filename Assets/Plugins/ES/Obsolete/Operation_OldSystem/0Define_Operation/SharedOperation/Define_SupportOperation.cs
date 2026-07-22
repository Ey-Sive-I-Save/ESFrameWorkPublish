/*
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES.Obsolete{
    /*SupportOperation �?ValueEntryOp是一对，
     * ValueEntryOp提供修改意愿的效果导向，
     * 而Support提供的是修改的值和操作类型
       
     
    public interface ISupportOperation<Target,Logic, OperationType, OperationOptions>
    {
        public OperationOptions GetOperationOptions { get; }
        public abstract OperationType GetOperationValue(Target target,Logic logic);

    }
    public abstract class SupportOperation<Target,Logic, OperationType, OperationOptions> : ISupportOperation<Target,Logic, OperationType, OperationOptions>
    {
        public abstract OperationOptions GetOperationOptions { get; }

        public abstract OperationType GetOperationValue(Target target,Logic logic);
    }

    //演示
    #region 演示
    //直接显示输入
    public abstract class SupportOperation_DirectInput<Target,Logic, OperationType, OperationOptions> : SupportOperation<Target,Logic, OperationType, OperationOptions> {
        [LabelText("操作�?)]
        public OperationType opValue;
        [LabelText("操作类型")]
        public OperationOptions opType;
        public sealed override OperationOptions GetOperationOptions { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => opType; }
        public sealed override OperationType GetOperationValue(Target target,Logic logic)
        {
            return opValue;
        }
    }
    [Serializable]
    public abstract  class SupportOperation_DirectFloat<Target,Logic> : SupportOperation_DirectInput<Target,Logic, float, FloatValueEntryType>
    {

    }
   
    #endregion
}

*/