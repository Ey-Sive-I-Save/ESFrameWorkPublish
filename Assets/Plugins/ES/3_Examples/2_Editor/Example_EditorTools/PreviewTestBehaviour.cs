using UnityEngine;
using ES;

namespace ES.Samples.Editor{
    /// <summary>
    /// 娴嬭瘯棰勮闈㈡澘鐨?Mono 妗堜緥鑴氭湰銆?
    /// 鎸傝浇鍒板満鏅墿浣撳悗锛屾棤闇€浠讳綍棰濆閰嶇疆锛屽嵆鍙湪 Inspector 搴曢儴鐪嬪埌棰勮鏁堟灉銆?
    /// 姝よ剼鏈彲瀹夊叏鏀剧疆鍦ㄦ櫘閫氾紙闈?Editor锛夋枃浠跺す涓嬭繍琛屻€?
    /// </summary>
    public class PreviewTestBehaviour : MonoBehaviour, IPreviewElement
    {
        // ----- 瀹炵幇 IPreviewElement 鎺ュ彛 -----

        /// <summary> 鏄惁鍏佽棰勮 </summary>
        public bool CanPreview => true;

        /// <summary> 鏄惁鐙崰棰勮鍖哄煙锛堣涓?false锛屼娇鍏惰繘鍏ュ父瑙勬姌鍙犲尯锛?</summary>
        public bool IsSingleArea => false;

        /// <summary>
        /// 娓告垙杩愯鏃剁殑棰勮 GUI 缁樺埗
        /// </summary>
        public void DrawPreviewGUIPlaying()
        {
            // 浣跨敤 #if 鍖呰９浠呭瓨鍦ㄤ簬 Unity Editor 涓殑绫诲瀷锛堝 EditorStyles锛?
#if UNITY_EDITOR
            GUILayout.Label($"<b>[ 杩愯鏃舵祴璇?]</b>", UnityEditor.EditorStyles.boldLabel);
#else
            GUILayout.Label("[ 杩愯鏃舵祴璇?]");
#endif
            
            GUILayout.Label($"褰撳墠杩愯鏃堕棿: {Time.time:F2} 绉?);

            if (GUILayout.Button("鐐规垜瑙﹀彂娴嬭瘯鏃ュ織"))
            {
                Debug.Log("鉁?棰勮闈㈡澘鐨勮繍琛屾椂鎸夐挳琚偣鍑讳簡锛?);
            }
        }

        /// <summary>
        /// 缂栬緫鍣ㄩ潪杩愯鏃剁殑棰勮 GUI 缁樺埗
        /// </summary>
        public void EditorPreviewDrawPreviewGUINonPlay()
        {
#if UNITY_EDITOR
            GUILayout.Label($"<b>[ 缂栬緫鍣ㄦ祴璇?]</b>", UnityEditor.EditorStyles.boldLabel);
#else
            GUILayout.Label("[ 缂栬緫鍣ㄦ祴璇?]");
#endif
            GUILayout.Label("姝ゆ椂娓告垙鏈繍琛岋紝浣犲彲浠ュ湪杩欓噷灞曠ず閰嶇疆淇℃伅鎴栭潤鎬佹暟鎹€?);
        }
    }
}
