#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_TagDropdown", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_TagDropdown : ScriptableObject
    {
        // ---------- TagDropdown ----------

        [SerializeField, TagDropdown]
        private string tagDropdown = "";

        [field: SerializeField, TagDropdown]
        private string TagDropdown { get; set; } = "";

        private void RemoveConsoleWarning()
        {
            if (tagDropdown == TagDropdown) { }
        }

    } // class end
}
#endif