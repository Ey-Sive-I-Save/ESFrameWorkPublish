#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_EnumButtons", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_EnumButtons : ScriptableObject
    {
        // ---------- EnumButtons ----------

        private enum ExampleEnum
        {
            One,
            Two,
            Three,
            Four,
        }

        [Flags]
        private enum ExampleEnumFlags
        {
            None = 0,
            All = P1 | P2 | P3 | P4 | P5, // also supports being defined as '~0'

            P1 = 1 << 0,
            P2 = 1 << 1,
            P3 = 1 << 2,
            P4 = 1 << 3,
            P5 = 1 << 4,
        }

        [Title("Enum Buttons")]
        [SerializeField, EnumButtons]
        private ExampleEnum enumButtons;
        
        [Title("Enum Buttons (No Label)")]
        [SerializeField, LabelText(""), EnumButtons]
        private ExampleEnum enumButtonsNoLabel;

        [Title("Enum Buttons: Flags")]
        [SerializeField, EnumButtons(true)]
        private ExampleEnumFlags enumFlags;

    } // class end
}

#endif