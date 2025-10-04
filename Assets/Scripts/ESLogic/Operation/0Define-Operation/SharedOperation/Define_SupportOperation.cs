using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES {
    /*SupportOperation 和 Target是一对，
     * Target提供修改意愿的效果导向，
     * 而Support提供的是修改的值和操作类型
       
     */
    public interface ISupportOperation<On, From, With, OpeationType, OpeationOptions>
    {
        public OpeationOptions GetOperationOptions { get; }
        public abstract OpeationType GetOpeationValue(On on, From from, With with);

    }
    public abstract class SupportOperation<On, From, With, OpeationType, OpeationOptions> : ISupportOperation<On, From, With, OpeationType, OpeationOptions>
    {
        public abstract OpeationOptions GetOperationOptions { get; }

        public abstract OpeationType GetOpeationValue(On on, From from, With with);
    }

    //演示
    #region 演示
    //直接显示输入
    public abstract class SupportOperation_DirectInput<On, From, With, OpeationType, OpeationOptions> : SupportOperation<On, From, With, OpeationType, OpeationOptions> {
        [LabelText("操作值")]
        public OpeationType opValue;
        [LabelText("操作类型")]
        public OpeationOptions opType;
        public sealed override OpeationOptions GetOperationOptions { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => opType; }
        public sealed override OpeationType GetOpeationValue(On on, From from, With with)
        {
            return opValue;
        }
    }
    [Serializable]
    public abstract  class SupportOperation_DirectFloat<On, From, With> : SupportOperation_DirectInput<On, From, With, float, OperationOptionsForFloat>
    {

    }
   
    #endregion
}
