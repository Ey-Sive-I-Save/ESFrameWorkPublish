#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Max", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Max : ScriptableObject
    {
        // ---------- Max ----------

        [SerializeField, Max(10)]
        private int max;

        [field: SerializeField, Max(10)]
        private int Max { get; set; }

    } // class end
}

#endif