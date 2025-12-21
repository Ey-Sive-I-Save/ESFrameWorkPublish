#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_DisableIf", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_DisableIf : ScriptableObject
    {
        // ---------- DisableIf ----------

        [SerializeField]
        private bool value1;

        [SerializeField]
        private bool value2;

        [SerializeField, DisableIf(nameof(value1))]
        private int disableIfValue1True;

        [field: SerializeField, DisableIf(nameof(value2))]
        private int DisableIfValue2True { get; set; }

        [SerializeField, DisableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.AND)]
        private int disableIfBothTrue;

        [SerializeField, DisableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.OR)]
        private int disableIfEitherTrue;

    } // class end
}

#endif