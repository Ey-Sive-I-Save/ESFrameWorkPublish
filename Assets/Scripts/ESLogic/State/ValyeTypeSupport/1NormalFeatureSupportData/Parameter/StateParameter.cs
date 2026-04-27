using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public struct StateParameter
    {
        public StateDefaultFloatParameter EnumValue;
        public string StringValue;

        public StateParameter(StateDefaultFloatParameter enumValue)
        {
            EnumValue = enumValue;
            StringValue = null;
        }

        public StateParameter(string stringValue)
        {
            EnumValue = StateDefaultFloatParameter.None;
            StringValue = stringValue;
        }

        public string GetStringKey
        {
            get
            {
                if (EnumValue != StateDefaultFloatParameter.None)
                {
                    if (StateDefaultFloatParameterUtility.TryGetName(EnumValue, out var enumName))
                    {
                        return enumName;
                    }

                    return "None";
                }
                return StringValue ?? string.Empty;
            }
        }

        public static implicit operator StateParameter(StateDefaultFloatParameter e) => new StateParameter(e);
        public static implicit operator StateParameter(string s) => new StateParameter(s);
    }
}
