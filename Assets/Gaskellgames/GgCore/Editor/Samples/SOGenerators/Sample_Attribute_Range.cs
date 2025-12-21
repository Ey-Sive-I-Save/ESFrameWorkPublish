#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Range", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Range : ScriptableObject
    {
        // ---------- Range ----------

        [SerializeField, Range(0, 1)]
        private float range;

        [SerializeField, Range(0, 1, true)]
        private float range1;

        [field: SerializeField, Range(0, 1, "Label 1", "Label 2")]
        private float Range2 { get; set; }

    } // class end
}

#endif