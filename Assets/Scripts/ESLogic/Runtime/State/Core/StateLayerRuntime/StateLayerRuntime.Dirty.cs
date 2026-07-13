using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public partial class StateLayerRuntime
    {
        [LabelText("Dirty标记"), ShowInInspector, ReadOnly]
        [NonSerialized] public PipelineDirtyFlags dirtyFlags = PipelineDirtyFlags.None;

        [NonSerialized] private float lastDirtyTime;

        public bool IsDirty => dirtyFlags != PipelineDirtyFlags.None; 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkDirty(PipelineDirtyFlags flags)
        {
            if (flags == PipelineDirtyFlags.None) return;

            var decayFlags = PipelineDirtyFlags.MediumPriority | PipelineDirtyFlags.HighPriority;
            bool touchesDecayTimer = (flags & decayFlags) != 0;

            var merged = dirtyFlags | flags;
            if (merged == dirtyFlags && !touchesDecayTimer) return;

            dirtyFlags = merged;
            if (touchesDecayTimer) lastDirtyTime = Time.time;

#if UNITY_EDITOR
            if ((flags & (PipelineDirtyFlags.MixerWeights | PipelineDirtyFlags.HotPlug)) != 0)
            {
                _debugMixerSlotWeightsDirty = true;
                _debugPlayableInputsDirty = true;
            }
#endif
        }

        public void ClearDirty(PipelineDirtyFlags flags = PipelineDirtyFlags.None)
        {
            if (flags == PipelineDirtyFlags.None)
            {
                dirtyFlags = PipelineDirtyFlags.None;
                return;
            }
            dirtyFlags &= ~flags;
        }

        public bool HasDirtyFlag(PipelineDirtyFlags flags) => (dirtyFlags & flags) != 0;

        public void UpdateDirtyDecay()
        {
            if (dirtyFlags == PipelineDirtyFlags.None) return;
            var decayFlags = PipelineDirtyFlags.MediumPriority | PipelineDirtyFlags.HighPriority;
            if ((dirtyFlags & decayFlags) == 0) return;

            if (Time.time - lastDirtyTime >= 1.0f)
            {
                dirtyFlags &= ~decayFlags;
                dirtyFlags |= PipelineDirtyFlags.FallbackCheck;
            }
        }
    }
}
