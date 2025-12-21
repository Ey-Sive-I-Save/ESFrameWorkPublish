#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_DisableInEditMode", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_DisableInEditMode : ScriptableObject
    {
        // ---------- DisableInEditMode ----------

        [SerializeField, DisableInEditMode]
        private byte disableInEditMode;

        [field: SerializeField, DisableInEditMode]
        private byte DisableInEditMode { get; set; }

    } // class end
}

#endif