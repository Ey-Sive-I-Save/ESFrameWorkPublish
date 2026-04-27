using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public struct Link_StateContext_DefaultFloatChange
    {
        public float Value_Pre;
        public float Value_Now;
    }

    [Serializable]
    public struct Link_StateContext_DefaultIntChange
    {
        public int Value_Pre;
        public int Value_Now;
    }

    [Serializable]
    public struct Link_StateContext_DefaultBoolChange
    {
        public bool Value_Pre;
        public bool Value_Now;
    }

    [Serializable]
    public struct Link_StateContext_TriggerFired
    {
        public float FiredTime;
    }

    [Serializable]
    public struct Link_StateContext_CurveChange
    {
        public bool Create;
        public bool Remove;
        public AnimationCurve Value_Pre;
        public AnimationCurve Value_Now;
    }
}