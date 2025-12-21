#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Unit", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Unit : ScriptableObject
    {
        // ---------- Unit ----------

        [SerializeField, Unit(Units.Seconds)]
        private int unit;

        [field: SerializeField, Unit(Units.Percentage)]
        private int Unit { get; set; }

    } // class end
}

#endif