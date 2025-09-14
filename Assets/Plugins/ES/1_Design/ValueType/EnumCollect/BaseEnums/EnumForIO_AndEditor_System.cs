using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class EnumCollect
    {
        public enum HandleIndentStringName
        {
            [InspectorName("以大写开头")]StartToUpper,
            [InspectorName("以小写开头")] StartToLower,
            [InspectorName("全大写")] AllUpper,
            [InspectorName("全小写")] AllLower
        }

    }
}

