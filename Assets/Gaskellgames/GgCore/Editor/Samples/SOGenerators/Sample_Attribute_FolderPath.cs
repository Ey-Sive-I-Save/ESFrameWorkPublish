#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_FolderPath", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_FolderPath : ScriptableObject
    {
        // ---------- FolderPath ----------

        [SerializeField, FolderPath]
        private string folderPath;

        [field: SerializeField, FolderPath]
        private string FolderPath { get; set; }

    } // class end
}
#endif
