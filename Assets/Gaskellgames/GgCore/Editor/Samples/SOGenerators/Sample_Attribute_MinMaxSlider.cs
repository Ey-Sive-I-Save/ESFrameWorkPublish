#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_MinMaxSlider", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_MinMaxSlider : ScriptableObject
    {
        // ---------- MinMaxSlider ----------

        [SerializeField, MinMaxSlider(0, 1)]
        private Vector2 rangeMinMax = new Vector2(0.25f, 0.5f);

        [SerializeField, MinMaxSlider(0, 1, "Label 1", "Label 2")]
        private Vector2 rangeMinMax2 = new Vector2(0.25f, 0.75f);

        [field: SerializeField, MinMaxSlider(0, 1, true)]
        private Vector2 rangeMinMax3 { get; set; } = new Vector2(0.5f, 0.75f);

    } // class end
}

#endif