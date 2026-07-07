using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateAssetMenu(fileName = "TrackSequenceEditorSettings", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "Track Sequence Editor Settings")]
    public class TrackSequenceEditorSettings : ESEditorGlobalSo<TrackSequenceEditorSettings>
    {
        [TabGroup("Debug")]
        [LabelText("GameObject采样调试")]
        public bool gameObjectDebug;
    }
}
