#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_OnValueChanged", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_OnValueChanged : ScriptableObject
    {
        // ---------- OnValueChanged ----------

        [SerializeField, OnValueChanged(nameof(ExampleMethod))]
        private int onValueChanged;

        [field: SerializeField, OnValueChanged(nameof(ExampleMethod))]
        private int OnValueChanged { get; set; }

        private void ExampleMethod()
        {
            Debug.Log($"ExampleMethod called by {name}");
        }

    } // class end
}

#endif