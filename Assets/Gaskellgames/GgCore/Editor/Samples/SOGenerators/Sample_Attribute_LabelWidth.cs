#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_LabelWidth", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_LabelWidth : ScriptableObject
    {
        // ---------- LabelWidth ----------

        [SerializeField, LabelWidth(250)]
        private float overrideWidth;

        [field: SerializeField, LabelWidth(250)]
        private float OverrideWidth { get; set; }

    } // class end
}
#endif