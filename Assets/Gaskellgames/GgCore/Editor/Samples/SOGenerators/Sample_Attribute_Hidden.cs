#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Hidden", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Hidden : ScriptableObject
    {
        // ---------- Hidden ----------

        [SerializeField]
        private byte above;

        [SerializeField, Hidden]
        private byte hidden;

        [field: SerializeField, Hidden]
        private byte Hidden { get; set; }

        [SerializeField]
        private byte below;

    } // class end
}

#endif