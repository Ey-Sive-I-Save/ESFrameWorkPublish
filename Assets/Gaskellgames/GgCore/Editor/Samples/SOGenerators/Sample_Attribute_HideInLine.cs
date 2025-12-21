#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_HideInLine", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_HideInLine : ScriptableObject
    {
        // ---------- HideInLine ----------

        [SerializeField]
        private byte notHidden;

        [SerializeField, HideInLine]
        private byte hideInLine;

        [field: SerializeField, HideInLine]
        private byte HideInLine { get; set; }

    } // class end
}

#endif