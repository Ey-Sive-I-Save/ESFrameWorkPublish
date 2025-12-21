#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_EnableIf", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_EnableIf : ScriptableObject
    {
        // ---------- EnableIf ----------

        [SerializeField]
        private bool value1;

        [SerializeField]
        private bool value2;

        [SerializeField, EnableIf(nameof(value1))]
        private int enableIfValue1True;

        [field: SerializeField, EnableIf(nameof(value2))]
        private int EnableIfValue2True { get; set; }

        [SerializeField, EnableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.AND)]
        private int enableIfBothTrue;

        [SerializeField, EnableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.OR)]
        private int enableIfEitherTrue;
        

    } // class end
}

#endif