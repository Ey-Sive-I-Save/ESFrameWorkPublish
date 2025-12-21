#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_InLineEditor", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_InLineEditor : ScriptableObject
    {
        // ---------- InLineEditor ----------

        [SerializeField, InLineEditor]
        private ScriptableObject inLineEditor;

        [field: SerializeField, InLineEditor]
        private ScriptableObject InLineEditor { get; set; }

    } // class end
}

#endif