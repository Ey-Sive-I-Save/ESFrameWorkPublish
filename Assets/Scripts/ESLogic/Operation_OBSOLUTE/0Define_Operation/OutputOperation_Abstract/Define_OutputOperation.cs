using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /*可输出操作，可以随时装载和卸载的简单操作，
         
         OutputOperation 
         !!!在旧标准中，采用针化操作 On From With
         !!!在新的标准中，采用依赖未定上下文，而不是确定的类型参数
         
    */
     public interface IOutputOperation<On,From,With> : IOperation<On,From,With>
    {
        void TryOperation(On on,From from,With with);
        void TryCancel(On on,From from,With with);

    }
    public interface IOutputOperation<Target,Logic> : IOperation<Logic,Target>
    {
        void TryOperation(Target target, Logic logic);
        void TryCancel(Target target, Logic logic);

    }
    //必须要取消的--比如委托
    public abstract class OutputOperation_MustCancel<Target,Logic> : IOutputOperation<Target,Logic>
    {
        public static void  DefaultAction(Target target, Logic logich)
        {

        }
        public Action<Target, Logic> OnCancel = DefaultAction;
        public abstract void TryOperation(Target target, Logic logic);
        public abstract void TryCancel(Target target, Logic logic);

    }
  
}
