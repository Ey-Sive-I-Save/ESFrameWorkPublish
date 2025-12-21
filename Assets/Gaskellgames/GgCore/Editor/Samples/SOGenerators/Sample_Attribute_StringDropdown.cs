#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_StringDropdown", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_StringDropdown : ScriptableObject
    {
        // ---------- StringDropdown ----------

        [SerializeField, StringDropdown("Option1", "Option2", "Option3")]
        private string stringDropdown;

        [field: SerializeField, StringDropdown("Option1", "Option2", "Option3")]
        private string StringDropdown { get; set; }

    } // class end
}

#endif