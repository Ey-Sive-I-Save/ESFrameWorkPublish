#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_FilePath", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_FilePath : ScriptableObject
    {
        // ---------- FilePath ----------

        [SerializeField, FilePath]
        private string filePath;

        [field: SerializeField, FilePath]
        private string FilePath { get; set; }

    } // class end
}

#endif