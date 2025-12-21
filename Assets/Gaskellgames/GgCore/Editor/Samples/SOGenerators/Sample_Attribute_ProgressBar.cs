#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "Sample_Attribute_ProgressBar", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_ProgressBar : ScriptableObject
    {
        // ---------- ProgressBar ----------

        [SerializeField, ProgressBar]
        private float progressBar1 = 10;

        [SerializeField, ProgressBar(true)]
        private float progressBar2 = 20;

        [SerializeField, ProgressBar(200)]
        private float progressBar3 = 60;

        [SerializeField, ProgressBar("Mana")]
        private int progressBar4 = 70;

        [SerializeField, ProgressBar(050, 179, 050, 255)]
        private int progressBar5 = 80;

        [field: SerializeField, ProgressBar(200, "Health", 223, 050, 050, 255)]
        private int progressBar6 { get; set; } = 180;

        private void RemoveConsoleWarning()
        {
            if (progressBar1 < progressBar2) { }
            if (progressBar3 < progressBar4) { }
            if (progressBar5 < progressBar6) { }
        }

    } // class end
}
#endif