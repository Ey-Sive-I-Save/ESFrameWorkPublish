using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    /// <summary>
    /// Serialized reference to a built-in float parameter used by animation assets.
    /// The former string fallback is intentionally not represented here.
    /// </summary>
    [Serializable]
    public struct StateFloatParameterReference
    {
        [FormerlySerializedAs("EnumValue")]
        [SerializeField] private StateDefaultFloatParameter parameter;

        public StateDefaultFloatParameter Parameter => parameter;
        public bool IsValid => parameter != StateDefaultFloatParameter.None;

        public StateFloatParameterReference(StateDefaultFloatParameter parameter)
        {
            this.parameter = parameter;
        }

        public static implicit operator StateFloatParameterReference(StateDefaultFloatParameter parameter)
        {
            return new StateFloatParameterReference(parameter);
        }
    }
}
