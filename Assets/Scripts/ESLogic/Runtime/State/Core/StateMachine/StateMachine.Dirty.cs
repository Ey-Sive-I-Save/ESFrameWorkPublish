using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        public void MarkDirty(StateDirtyReason reason = StateDirtyReason.Unknown)
        {
            _dirtyVersion++;
            isDirty = true;
            _lastDirtyReason = reason;
        }

        public void ClearDirty()
        {
            isDirty = false;
        }

        private void ProcessDirtyTasks(StateLayerRuntime layerData, StateLayerType layer)
        {
            if (!layerData.IsDirty) return;

            if (layerData.HasDirtyFlag(PipelineDirtyFlags.HighPriority))
            {
                // 可在此添加高优先级任务
            }

            if (layerData.HasDirtyFlag(PipelineDirtyFlags.MediumPriority))
            {
                // 可在此添加中等优先级任务
            }

            if (layerData.HasDirtyFlag(PipelineDirtyFlags.FallbackCheck))
            {
                if (layerData.runningStates.Count == 0)
                {
                    int fallbackStateId = layerData.GetFallBack(currentSupportFlags);

                    if (fallbackStateId >= 0)
                    {
                        var fallbackState = GetStateByInt(fallbackStateId);
                        StateMachineDebug.Log($"[FallbackActivate] Layer={layer} | Support={currentSupportFlags} | FallbackStateId={fallbackStateId} | FallbackState={(fallbackState != null ? fallbackState.strKey : "<null>")}");

                        if (fallbackState == null)
                        {
                            StateMachineDebug.LogWarning($"[FallbackActivate] Skip: fallback state not found. Layer={layer} | StateId={fallbackStateId}");
                            layerData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
                            return;
                        }

                        bool activated = TryActivateState(fallbackState, layer);
                        if (!activated)
                            StateMachineDebug.LogWarning($"[FallbackActivate] Failed. Layer={layer} | State={fallbackState.strKey} | StateId={fallbackStateId}");

                        layerData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
                    }
                    else
                    {
                        layerData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
                    }
                }
                else
                {
                    layerData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
                }
            }
        }
    }
}
