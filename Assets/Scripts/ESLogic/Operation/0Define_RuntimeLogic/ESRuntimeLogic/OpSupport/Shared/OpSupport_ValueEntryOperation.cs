using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    //不如直接使用的--ValueEntryDirect
    /* [Serializable]
    public abstract class ESRuntimeOpSupport_SettleOutputOperation_Float : OutputOperationSettle<ESRuntimeTarget,IESRuntimeLogic,float>
    {
       
    }*/
    [Serializable]
    public abstract class ESRuntimeOpSupport_ValueEntryFloatOperation : ValueEntryFloatOperation<ESRuntimeTarget,IESRuntimeLogic>
    {
       
    }
}
