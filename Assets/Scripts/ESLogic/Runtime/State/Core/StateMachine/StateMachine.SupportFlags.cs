using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        public void SetSupportFlags(StateSupportFlags flags)
        {
#if STATEMACHINEDEBUG
            var dbg = StateMachineDebugSettings.Instance;
            if (dbg != null && dbg.IsStateTransitionEnabled)
            {
                dbg.LogStateTransition($"设置支持标记: {flags}");
            }
#endif
            var beforeFlags = currentSupportFlags;
            currentSupportFlags = NormalizeSingleSupportFlag(flags);
            if (beforeFlags != currentSupportFlags)
            {
                RemoveUnsupportedRunningStates(currentSupportFlags);
                MarkSupportFlagsDirty();
            }
        }

        private void MarkSupportFlagsDirty()
        {
            for (int i = 0; i < _layerArray.Length; i++)
            {
                _layerArray[i].MarkDirty(PipelineDirtyFlags.FallbackCheck);
            }
            MarkDirty(StateDirtyReason.RuntimeChanged);
        }

        private void RemoveUnsupportedRunningStates(StateSupportFlags newFlag)
        {
            if (newFlag == StateSupportFlags.None)
                return;

            var allRunningStates = GetRunningStatesSnapshot();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null) continue;

                var sharedData = state.stateSharedData;
                if (sharedData == null) continue;

                var basicConfig = sharedData.basicConfig;
                if (basicConfig == null) continue;

                if (basicConfig.ignoreSupportFlag) continue;

                var stateFlag = basicConfig.stateSupportFlag;
                if (stateFlag == StateSupportFlags.None) continue;

                if (!basicConfig.deactivateOnSupportFlagSwitching) continue;

                if ((stateFlag & newFlag) == 0)
                {
                    if (stateLayerMap.TryGetValue(state, out var layerType))
                    {
                        TruelyDeactivateState(state, layerType);
                    }
                }
            }
        }
    }
}