#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_HideInPlayMode", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_HideInPlayMode : ScriptableObject
    {
        // ---------- HideInPlayMode ----------

        [SerializeField, HideInPlayMode]
        private byte hideInPlayMode;

        [field: SerializeField, HideInPlayMode]
        private byte HideInPlayMode;

    } // class end
}

#endif