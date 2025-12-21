#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Wrap", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Wrap : ScriptableObject
    {
        // ---------- Wrap ----------

        [SerializeField, Wrap(0, 360)]
        private int wrap;

        [field: SerializeField, Wrap(0, 360)]
        private int Wrap { get; set; }

    } // class end
}

#endif