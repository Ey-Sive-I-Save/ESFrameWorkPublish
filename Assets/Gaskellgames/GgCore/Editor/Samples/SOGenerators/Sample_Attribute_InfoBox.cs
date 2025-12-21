#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_InfoBox", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_InfoBox : ScriptableObject
    {
        // ---------- InfoBox ----------

        [Title("InfoBox Types")]
        [SerializeField, InfoBox("Example InfoBox: Type None.", InfoMessageType.None)]
        private byte infoBoxNone;

        [SerializeField, InfoBox("Example InfoBox: Type Info.", InfoMessageType.Info)]
        private byte infoBoxInfo;

        [SerializeField, InfoBox("Example InfoBox: Type Warning.", InfoMessageType.Warning)]
        private byte infoBoxWarning;

        [SerializeField, InfoBox("Example InfoBox: Type Error.", InfoMessageType.Error)]
        private byte infoBoxError;

        [Title("InfoBox Conditional", "Info box will show the chosen type if toggle is true.")] [SerializeField]
        private bool showInfoBox;

        [SerializeField, Range(0, 3, false)]
        private int type;

        [SerializeField]
        [InfoBox("Conditional InfoBox: Type None", InfoMessageType.None, nameof(showInfoBox0))]
        [InfoBox("Conditional InfoBox: Type Info", InfoMessageType.Info, nameof(showInfoBox1))]
        [InfoBox("Conditional InfoBox: Type Warning", InfoMessageType.Warning, nameof(showInfoBox2))]
        [InfoBox("Conditional InfoBox: Type Error", InfoMessageType.Error, nameof(showInfoBox3))]
        private byte infoBoxConditional;

        [SerializeField, Hidden] private bool showInfoBox0;
        [SerializeField, Hidden] private bool showInfoBox1;
        [SerializeField, Hidden] private bool showInfoBox2;
        [SerializeField, Hidden] private bool showInfoBox3;

        private void OnValidate()
        {
            showInfoBox0 = showInfoBox && type == 0;
            showInfoBox1 = showInfoBox && type == 1;
            showInfoBox2 = showInfoBox && type == 2;
            showInfoBox3 = showInfoBox && type == 3;
        }

    } // class end
}

#endif