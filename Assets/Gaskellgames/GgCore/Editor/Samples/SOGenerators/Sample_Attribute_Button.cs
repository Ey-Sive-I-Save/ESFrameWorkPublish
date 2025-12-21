#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_Button", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_Button : ScriptableObject
    {
        // ---------- Button ----------

        [Button]
        private void MethodRow1()
        {
            Debug.Log("Method 1: Button Pressed");
        }

        [Button("Row2")]
        private void MethodRow2a()
        {
            Debug.Log("Method 2a: Button Pressed");
        }

        [Button("Row2")]
        private void MethodRow2b()
        {
            Debug.Log("Method 2b: Button Pressed");
        }

    } // class end
}

#endif