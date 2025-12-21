#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_ContainsType", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_ContainsType : ScriptableObject
    {
        // ---------- ContainsType ----------

        [SerializeField, ContainsType(typeof(Sample_Attribute_ContainsType))]
        private GameObject containsType;

        [field: SerializeField, ContainsType(typeof(Sample_Attribute_ContainsType))]
        private GameObject ContainsType { get; set; }

    } // class end
}

#endif