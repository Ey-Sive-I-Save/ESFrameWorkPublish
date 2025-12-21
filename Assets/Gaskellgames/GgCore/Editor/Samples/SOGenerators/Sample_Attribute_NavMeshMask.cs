#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_NavMeshMask", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_NavMeshMask : ScriptableObject
    {
        // ---------- NavMeshMask ----------

        [SerializeField, NavMeshMask]
        private int navMeshMask;

        [field: SerializeField, NavMeshMask]
        private int NavMeshMask { get; set; }

    } // class end
}

#endif