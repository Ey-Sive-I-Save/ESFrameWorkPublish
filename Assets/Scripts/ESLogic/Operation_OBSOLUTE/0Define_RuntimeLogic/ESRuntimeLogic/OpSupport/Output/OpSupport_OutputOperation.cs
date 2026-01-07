using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [Serializable]
    public abstract class ESRuntimeOpSupport_OutputOperation : IOutputOperation<ESRuntimeTarget,IOpSupporter_OB>
    {
        public abstract void TryCancel(ESRuntimeTarget target, IOpSupporter_OB logic);

        public abstract void TryOperation(ESRuntimeTarget target, IOpSupporter_OB logic);
    }
}
