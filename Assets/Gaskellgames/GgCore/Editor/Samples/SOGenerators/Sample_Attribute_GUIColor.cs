#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "Sample_Attribute_GUIColor", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_GUIColor : ScriptableObject
    {
        // ---------- GUIColor ----------

        [SerializeField, GUIColor(223, 050, 050, 255, GUIColorTarget.All)]
        private GameObject objectField1;

        [SerializeField, GUIColor(223, 050, 050, 255, GUIColorTarget.Background)]
        private GameObject objectField2;

        [field: SerializeField, GUIColor(223, 050, 050, 255, GUIColorTarget.Content)]
        private GameObject ObjectProperty { get; set; }


        [SerializeField, GUIColor(050, 179, 050, 255, GUIColorTarget.All), Space]
        private LayerMask dropdownField1;

        [SerializeField, GUIColor(050, 179, 050, 255, GUIColorTarget.Background)]
        private LayerMask dropdownField2;

        [field: SerializeField, GUIColor(050, 179, 050, 255, GUIColorTarget.Content)]
        private LayerMask DropdownProperty { get; set; }


        [SerializeField, GUIColor(000, 179, 223, 255, GUIColorTarget.All), Space]
        private string stringField1;

        [SerializeField, GUIColor(000, 179, 223, 255, GUIColorTarget.Background)]
        private string stringField2;

        [field: SerializeField, GUIColor(000, 179, 223, 255, GUIColorTarget.Content)]
        private string StringProperty { get; set; }

    } // class end
}
#endif