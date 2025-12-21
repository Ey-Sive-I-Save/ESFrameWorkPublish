#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_ReadOnly", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_ReadOnly : ScriptableObject
    {
        // ---------- ReadOnly ----------

        [SerializeField, ReadOnly]
        private float readOnly;

        [field: SerializeField, ReadOnly]
        private GameObject ReadOnly { get; set; }

    } // class end
}

#endif