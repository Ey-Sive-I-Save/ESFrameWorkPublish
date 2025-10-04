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
        public Delegate dele;
        public int count = 0;
    }
    [Serializable]
    public abstract class OutputOpeationDelegate<On, From, With, MakeAction> :
        OutputOperation_Abstract<On, From, With>, IOutputOperationFlag_MustCancel
        where MakeAction : Delegate
        where With : ICacheKeyValueForOutputOpeation<IOperation, DeleAndCount, OutputOpeationDelegateFlag>
    {
       
        
        [LabelText("给与触发次数")]
        public int GiveCount = 99;
        public MakeAction GetActionOnEnableExpand(On on, From from, With with)
        {
            MakeAction make = null;
            var cache = with.GetCache(OutputOpeationDelegateFlag.flag);
            if(cache.TryGetValue(this, out var value))
            {
                make = value.dele as MakeAction;
                value.count += GiveCount;
            }
            else
            {
                make = MakeTheAction(on, from, with);
                cache.Add(this,new DeleAndCount() { dele= make, count=GiveCount });
            }
            return make;
        }
        public MakeAction GetActionOnDisableExpand(On on, From from, With with)
        {
            var cacher = with.GetCache(OutputOpeationDelegateFlag.flag);
            if (cacher.TryGetValue(this, out var use))
            {
                cacher.Remove(this);
                return use.dele as MakeAction;
            }
            return default;
        }
        public void SetWhenActionHappenCountChange(On on, From from, With with)
        {
            var cacher = with.GetCache(OutputOpeationDelegateFlag.flag);
            if (cacher.TryGetValue(this, out var use))
            {
                Debug.Log("COUNT2     "+ use.count);
                use.count--;
                if (use.count <= 0)
                {
                    //提前退出
                    Debug.Log("COUNT3");
                    TryCancel(on,from,with);
                }
            }
        }
        protected abstract MakeAction MakeTheAction(On on, From from, With with);
        
    }
    #region 演示


    #endregion
}

