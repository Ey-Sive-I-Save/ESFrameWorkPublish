#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_DisableInPlayMode", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_DisableInPlayMode : ScriptableObject
    {
        // ---------- DisableInPlayMode ----------

        [SerializeField, DisableInPlayMode]
        private byte disableInPlayMode;

        [field: SerializeField, DisableInPlayMode]
        private byte DisableInPlayMode { get; set; }

    } // class end
}

#endif