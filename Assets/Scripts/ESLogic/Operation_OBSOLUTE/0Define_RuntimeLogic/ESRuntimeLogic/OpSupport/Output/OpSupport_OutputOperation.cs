using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [Serializable]
    public abstract class ESRuntimeOpSupport_OutputOperation : IOutputOperation<ESRuntimeTarget,IOpSupporter>
    {
        public abstract void TryCancel(ESRuntimeTarget target, IOpSupporter logic);

        public abstract void TryOperation(ESRuntimeTarget target, IOpSupporter logic);
    }
}
