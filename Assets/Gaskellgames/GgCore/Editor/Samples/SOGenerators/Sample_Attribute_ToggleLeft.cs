#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_ToggleLeft", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_ToggleLeft : ScriptableObject
    {
        // ---------- ToggleLeft ----------

        [SerializeField, ToggleLeft]
        private bool toggleLeft;

        [field: SerializeField, ToggleLeft]
        private bool ToggleLeft { get; set; }

    } // class end
}

#endif