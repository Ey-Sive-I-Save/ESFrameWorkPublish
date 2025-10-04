using ES;
 
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {

    [Serializable]
    /* 
     *简单值控 的实现 -》  Assets/Scripts/ESFramework/Ship_RunTimeLogic_Support/Arch/ArchOperation_EasyValueSet.cs
     
     */
    public abstract class ArchOperation_Abstract : IOperation<ArchPool, string, object>
    {
        public abstract void TryOperation(ArchPool arch, string key, object value = null);
    }
    //池  字符串键  值
    [Serializable]
    public abstract class ArchPoolGetterEasy 
    {
        public abstract ArchPool GetArchPool();//获得ArchPool
    }


}
