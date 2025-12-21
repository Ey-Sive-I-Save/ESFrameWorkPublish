#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_ShowAsTag", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_ShowAsTag : ScriptableObject
    {
        // ---------- StringDropdown ----------

        [SerializeField]
        private string stringValue = "TagValue";

        [SerializeField, ShowAsTag, Space]
        private string showAsTag;

        private void OnValidate()
        {
            showAsTag = stringValue;
        }
        
    } // class end
}

#endif