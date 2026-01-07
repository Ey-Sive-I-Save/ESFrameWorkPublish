using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES {
    /*SupportOperation 和 ValueEntryOp是一对，
     * ValueEntryOp提供修改意愿的效果导向，
     * 而Support提供的是修改的值和操作类型
       
     */
    public interface ISupportOperation<Target,Logic, OpeationType, OpeationOptions>
    {
        public OpeationOptions GetOperationOptions { get; }
        public abstract OpeationType GetOpeationValue(Target target,Logic logic);

    }
    public abstract class SupportOperation<Target,Logic, OpeationType, OpeationOptions> : ISupportOperation<Target,Logic, OpeationType, OpeationOptions>
    {
        public abstract OpeationOptions GetOperationOptions { get; }

        public abstract OpeationType GetOpeationValue(Target target,Logic logic);
    }

    //演示
    #region 演示
    //直接显示输入
    public abstract class SupportOperation_DirectInput<Target,Logic, OpeationType, OpeationOptions> : SupportOperation<Target,Logic, OpeationType, OpeationOptions> {
        [LabelText("操作值")]
        public OpeationType opValue;
        [LabelText("操作类型")]
        public OpeationOptions opType;
        public sealed override OpeationOptions GetOperationOptions { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => opType; }
        public sealed override OpeationType GetOpeationValue(Target target,Logic logic)
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
