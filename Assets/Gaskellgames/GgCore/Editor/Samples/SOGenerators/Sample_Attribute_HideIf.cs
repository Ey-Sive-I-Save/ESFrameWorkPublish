#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_HideIf", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_HideIf : ScriptableObject
    {
        // ---------- Hide If ----------

        [SerializeField]
        private bool value1;

        [SerializeField]
        private bool value2;

        [SerializeField, HideIf(nameof(value1))]
        private int hideIfValue1True;

        [field: SerializeField, HideIf(nameof(value2))]
        private int HideIfValue2True { get; set; }

        [SerializeField, HideIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.AND)]
        private int hideIfBothTrue;

        [SerializeField, HideIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.OR)]
        private int hideIfEitherTrue;

    } // class end
}

#endif