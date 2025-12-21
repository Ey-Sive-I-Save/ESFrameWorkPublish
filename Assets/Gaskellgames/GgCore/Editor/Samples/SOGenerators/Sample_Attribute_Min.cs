#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Min", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Min : ScriptableObject
    {
        // ---------- Min ----------

        [SerializeField, Min(0)]
        private int min;

        [field: SerializeField, Min(0)]
        private int Min { get; set; }

    } // class end
}

#endif