#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Bitfield", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Bitfield : ScriptableObject
    {
        // ---------- Bitfield ----------

        [SerializeField]
        private int input = 0;
        
        [SerializeField, Bitfield, Space]
        private int bitfield;

        [field: SerializeField, Bitfield]
        private int Bitfield { get; set; }

        private void OnValidate()
        {
            bitfield = input;
            Bitfield = input;
        }
        
    } // class end
}

#endif