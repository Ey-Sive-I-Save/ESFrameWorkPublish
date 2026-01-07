using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UIElements.UxmlAttributeDescription;


namespace ES
{
    public class OutputOpeationDelegateFlag : OverLoadFlag<OutputOpeationDelegateFlag>
    {

    }
    
    public class DeleAndCount
    {
        [ESMessage]
        public Delegate dele;
        public int count = 0;
    }
    [Serializable]
    public abstract class OutputOpeationDelegate<Target,Logic, MakeAction> :
        OutputOperation_MustCancel<Target,Logic>
        where MakeAction : Delegate
        where Logic : IOpStoreDictionary<IOperation, DeleAndCount, OutputOpeationDelegateFlag>
    {
        
        
        [LabelText("给与触发次数")]
        public int GiveCount = 99;
        public MakeAction GetActionOnEnableExpand(Target target,Logic logic)
        {
            MakeAction make = null;
            var cache = logic.GetFromOpStore(OutputOpeationDelegateFlag.flag);
            if(cache.TryGetValue(this, out var value))
            {
                make = value.dele as MakeAction;
                value.count += GiveCount;
            }
            else
            {
                make = MakeTheAction(target, logic);
                cache.Add(this,new DeleAndCount() { dele= make, count=GiveCount });
            }
            return make;
        }
        public MakeAction GetActionOnDisableExpand(Target target,Logic logic)
        {
            var cacher = logic.GetFromOpStore(OutputOpeationDelegateFlag.flag);
            if (cacher.TryGetValue(this, out var use))
            {
                cacher.Remove(this);
                return use.dele as MakeAction;
            }
            return default;
        }
        public void SetWhenActionHappenCountChange(Target target,Logic logic)
        {
            var cacher = logic.GetFromOpStore(OutputOpeationDelegateFlag.flag);
            if (cacher.TryGetValue(this, out var use))
            {
                Debug.Log("COUNT2     "+ use.count);
                use.count--;
                if (use.count <= 0)
                {
                    //提前退出
                    Debug.Log("COUNT3");
                    TryCancel(target,logic);
                }
            }
        }
        protected abstract MakeAction MakeTheAction(Target target,Logic logic);
        
    }
    #region 演示


    #endregion
}

