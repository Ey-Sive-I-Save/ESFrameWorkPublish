#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_LabelText", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_LabelText : ScriptableObject
    {
        // ---------- LabelText ----------

        [SerializeField, LabelText("Label Text")]
        private float label;

        [field: SerializeField, LabelText("Label Text")]
        private float Label { get; set; }

    } // class end
}
#endif