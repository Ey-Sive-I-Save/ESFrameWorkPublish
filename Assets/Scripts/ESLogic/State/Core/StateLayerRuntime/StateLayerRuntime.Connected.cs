using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    public partial class StateLayerRuntime
    {
        [NonSerialized] internal readonly List<StateBase> connectedStates = new List<StateBase>(64);
        [NonSerialized] internal readonly List<int> connectedSlots = new List<int>(64);
        [NonSerialized] private readonly Dictionary<StateBase, int> _connectedIndexMap = new Dictionary<StateBase, int>(64);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalOnStateConnected(StateBase state, int slotIndex)
        {
            if (state == null) return;
            if (_connectedIndexMap.ContainsKey(state))
            {
                Debug.LogError($"[StateLayerRuntime] 状态已存在于 connectedStates: {state.strKey}");
                return;
            }
            int idx = connectedStates.Count;
            connectedStates.Add(state);
            connectedSlots.Add(slotIndex);
            _connectedIndexMap[state] = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalOnStateDisconnected(StateBase state)
        {
            if (state == null) return;
            if (!_connectedIndexMap.TryGetValue(state, out int idx)) return;

            int last = connectedStates.Count - 1;
            var lastState = connectedStates[last];
            var lastSlot = connectedSlots[last];

            connectedStates[idx] = lastState;
            connectedSlots[idx] = lastSlot;
            connectedStates.RemoveAt(last);
            connectedSlots.RemoveAt(last);

            _connectedIndexMap.Remove(state);
            if (idx != last)
                _connectedIndexMap[lastState] = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalClearConnectedStates()
        {
            connectedStates.Clear();
            connectedSlots.Clear();
            _connectedIndexMap.Clear();
        }
    }
}