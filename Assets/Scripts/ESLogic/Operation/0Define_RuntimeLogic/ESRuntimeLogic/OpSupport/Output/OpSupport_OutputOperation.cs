using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [Serializable]
    public abstract class ESRuntimeOpSupport_OutputOperation : IOutputOperation<ESRuntimeTarget,IESRuntimeLogic>
    {
        public abstract void TryCancel(ESRuntimeTarget target, IESRuntimeLogic logic);

        public abstract void TryOperation(ESRuntimeTarget target, IESRuntimeLogic logic);
    }
}
