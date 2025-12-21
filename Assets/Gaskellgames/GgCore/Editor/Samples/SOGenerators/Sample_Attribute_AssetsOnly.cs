#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_AssetsOnly", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_AssetsOnly : ScriptableObject
    {
        // ---------- AssetsOnly ----------

        [SerializeField, AssetsOnly]
        private GameObject prefab;

        [field: SerializeField, AssetsOnly]
        private GameObject Prefab { get; set; }

    } // class end
}

#endif