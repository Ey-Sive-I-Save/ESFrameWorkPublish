using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public interface IDeepClone
    {
        void DeepCloneFrom(object from);
    }
    public interface IDeepClone<in This> : IDeepClone where This : IDeepClone<This>
    {
        void DeepCloneFrom(This t);
        void IDeepClone.DeepCloneFrom(object from)
        {
            DeepCloneFrom((This)from);
        }
    }
}

