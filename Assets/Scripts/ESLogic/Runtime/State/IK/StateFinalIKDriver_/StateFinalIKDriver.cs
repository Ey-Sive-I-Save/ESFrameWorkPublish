using UnityEngine;

namespace ES
{
    // ============================================================================
    // 文件：StateFinalIKDriver.cs
    //
    // 主 part：仅保留 MonoBehaviour 主文件入口与总览注释。
    // 具体实现按职责拆分到同目录下的 partial 文件：
    // - RuntimeCore: RuntimeState / RuntimeAndInit
    // - InspectorConfig: FeatureToggles / AutoAdd / PresetRefs / BoneBinding /
    //                    InitParams / GoalHints / HitReaction / Recoil
    // - Tooling: EditorAndSetup / Diagnostics / BoneBoxExtensions
    // ============================================================================

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1)]
    public sealed partial class StateFinalIKDriver : MonoBehaviour
    {
        
    }
}