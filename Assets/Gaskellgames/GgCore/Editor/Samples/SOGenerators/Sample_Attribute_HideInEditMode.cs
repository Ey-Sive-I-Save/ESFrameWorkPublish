#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_HideInEditMode", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_HideInEditMode : ScriptableObject
    {
        // ---------- HideInEditMode ----------

        [SerializeField, HideInEditMode]
        private byte hideInEditMode;

        [field: SerializeField, HideInEditMode]
        private byte HideInEditMode { get; set; }

    } // class end
}

#endif