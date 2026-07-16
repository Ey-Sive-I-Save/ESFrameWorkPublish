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

        [ShowInInspector, FoldoutGroup("高级调试（按需开启）", expanded: false), LabelText("显示槽位权重列表")]
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

        [ShowInInspector, FoldoutGroup("高级调试（按需开启）", expanded: false), LabelText("显示动画节点输入列表")]
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

