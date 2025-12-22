using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [Serializable]
    public abstract class ESRuntimeOpSupport_DelegateOutputOperation<MakeAction> : 
    OutputOpeationDelegate<ESRuntimeTarget,IESRuntimeLogic,MakeAction>
    where MakeAction:Delegate
    {

    }
}
