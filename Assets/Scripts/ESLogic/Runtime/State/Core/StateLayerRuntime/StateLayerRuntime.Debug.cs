#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    public partial class StateLayerRuntime
    {
        // 调试开关
        [NonSerialized] private bool _debugEnableMixerSlotWeights;
#pragma warning disable 0414
        [NonSerialized] private bool _debugMixerSlotWeightsDirty = true;
#pragma warning restore 0414
        [NonSerialized] private bool _debugEnablePlayableInputs;
#pragma warning disable 0414
        [NonSerialized] private bool _debugPlayableInputsDirty = true;
#pragma warning restore 0414
        [NonSerialized] private readonly List<string> _debugMixerSlotWeightsCache = new List<string>(64);
        [NonSerialized] private readonly List<string> _debugPlayableInputsCache = new List<string>(64);
        [NonSerialized] private StateBase[] _debugSlotToState;
        [NonSerialized] private bool[] _debugSlotFadingIn;
        [NonSerialized] private bool[] _debugSlotFadingOut;
        private struct ClipWeightInfo { public string clipName; public float weight; }
        [NonSerialized] private readonly List<ClipWeightInfo> _debugClipWeightsCache = new List<ClipWeightInfo>(16);
        [NonSerialized] private readonly Dictionary<string, int> _debugClipIndexCache = new Dictionary<string, int>(16);
        [NonSerialized] private readonly System.Text.StringBuilder _debugClipSummarySb = new System.Text.StringBuilder(256);

        [ShowInInspector, FoldoutGroup("Mixer权重调试"), LabelText("启用槽位权重列表(耗时)")]
        private bool DebugEnableMixerSlotWeights
        {
            get => _debugEnableMixerSlotWeights;
            set
            {
                if (_debugEnableMixerSlotWeights == value) return;
                _debugEnableMixerSlotWeights = value;
                _debugMixerSlotWeightsDirty = true;
            }
        }

        [ShowInInspector, FoldoutGroup("Playable调试"), LabelText("启用Playable输入列表(耗时)")]
        private bool DebugEnablePlayableInputs
        {
            get => _debugEnablePlayableInputs;
            set
            {
                if (_debugEnablePlayableInputs == value) return;
                _debugEnablePlayableInputs = value;
                _debugPlayableInputsDirty = true;
            }
        }

      
    }
}
#endif
