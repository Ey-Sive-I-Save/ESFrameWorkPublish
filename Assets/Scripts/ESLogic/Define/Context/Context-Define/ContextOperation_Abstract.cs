using ES;
 
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {

    [Serializable]
    /* 
     *简单值控 的实现 -》  Assets/Scripts/ESFramework/Ship_RunTimeLogic_Support/Context/ContextOperation_EasyValueSet.cs
     
     */
    public abstract class ContextOperation_Abstract : IOperation<ContextPool,ContextKeyValue>
    {
        public abstract void TryOperation(ContextPool Context,ContextKeyValue keyvalue);
    }
    //池  字符串键  值
    [Serializable]
    public abstract class ContextPoolGetterEasy 
    {
        public abstract ContextPool GetContextPool();//获得ContextPool
    }


}
