#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Highlight", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Highlight : ScriptableObject
    {
        // ---------- Highlight ----------

        [SerializeField, Highlight]
        private GameObject highlight;

        [field: SerializeField, Highlight(000, 179, 223, 255), Space]
        private GameObject Highlight { get; set; }

    } // class end
}

#endif