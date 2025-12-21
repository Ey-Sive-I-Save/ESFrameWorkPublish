#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_InLineButton", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_InLineButton : ScriptableObject
    {
        // ---------- InLineButton ----------

        [SerializeField, InLineButton(nameof(Method1))]
        private byte inLineButton;

        [field: SerializeField, InLineButton(nameof(Method2))]
        private byte InLineButton { get; set; }

        private void Method1()
        {
            Debug.LogFormat("{0} invoked.", nameof(Method1));
        }

        private void Method2()
        {
            Debug.LogFormat("{0} invoked.", nameof(Method2));
        }
        
    } // class end
}

#endif