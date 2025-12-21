#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Decorator_Title", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Decorator_Title : ScriptableObject
    {
        // ---------- HeaderLine ----------

        [SerializeField]
        private GameObject placeholder01;

        [SerializeField]
        private GameObject placeholder02;

        [SerializeField, Title("Heading", "SubHeading")]
        private GameObject placeholder03;

        [SerializeField]
        private GameObject placeholder04;

        [SerializeField, Title("Heading")]
        private GameObject placeholder05;

        [SerializeField]
        private GameObject placeholder06;

        [SerializeField, Title("", "SubHeading")]
        private GameObject placeholder07;

        [SerializeField]
        private GameObject placeholder08;

        [field: SerializeField, Title]
        private GameObject placeholder09 { get; set; }

        [field: SerializeField]
        private GameObject placeholder10 { get; set; }

#if UNITY_EDITOR
        private void RemoveConsoleWarnings()
        { 
            if (placeholder01 != placeholder02) { }
            if (placeholder03 != placeholder04) { }
            if (placeholder05 != placeholder06) { }
            if (placeholder07 != placeholder08) { }
            if (placeholder09 != placeholder10) { }
        }
#endif

    } // class end
}
#endif