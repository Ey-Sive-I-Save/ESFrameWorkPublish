#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_ShowIf", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_ShowIf : ScriptableObject
    {
        // ---------- ShowIf ----------

        [SerializeField]
        private bool value1;

        [SerializeField]
        private bool value2;

        [SerializeField, ShowIf(nameof(value1))]
        private int showIfValue1True;

        [field: SerializeField, ShowIf(nameof(value2))]
        private int ShowIfValue2True { get; set; }

        [SerializeField, ShowIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.AND)]
        private int showIfBothTrue;

        [SerializeField, ShowIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.OR)]
        private int showIfEitherTrue;

#if UNITY_EDITOR
        private void RemoveConsoleWarnings()
        {
            if (showIfValue1True == ShowIfValue2True) { }
            if (showIfBothTrue == showIfEitherTrue) { }
        }
#endif

    } // class end
}

#endif