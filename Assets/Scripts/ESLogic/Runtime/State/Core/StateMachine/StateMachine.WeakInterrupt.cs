using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        private void ApplyWeakInterruptions(StateBase suppressor, StateLayerRuntime layer, in StateActivationResult result)
        {
            if (suppressor == null || layer == null || result.weakInterruptCount <= 0 || result.statesToWeakInterrupt == null)
                return;

            int count = Mathf.Min(result.weakInterruptCount, result.statesToWeakInterrupt.Count);
            for (int i = 0; i < count; i++)
            {
                var suppressed = result.statesToWeakInterrupt[i];
                if (suppressed == null || suppressed == suppressor)
                    continue;

                bool exists = false;
                for (int r = 0; r < _weakInterruptRecords.Count; r++)
                {
                    var record = _weakInterruptRecords[r];
                    if (record.suppressed == suppressed && record.suppressor == suppressor)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                    continue;

                var suppressedLayer = ResolveConnectedLayerRuntimeForState(suppressed, layer.layerType, out var suppressedLayerType);
                if (suppressedLayer == null)
                    continue;

                _weakInterruptRecords.Add(new WeakInterruptRecord
                {
                    suppressed = suppressed,
                    suppressor = suppressor,
                    layerType = suppressedLayerType
                });

                if (_weakInterruptSuppressionCounts.TryGetValue(suppressed, out int currentCount))
                    _weakInterruptSuppressionCounts[suppressed] = currentCount + 1;
                else
                    _weakInterruptSuppressionCounts[suppressed] = 1;

                suppressedLayer.MarkDirty(PipelineDirtyFlags.MixerWeights);
            }
        }

        private void ClearWeakInterruptionsForState(StateBase state)
        {
            if (state == null || _weakInterruptRecords.Count == 0)
                return;

            for (int i = _weakInterruptRecords.Count - 1; i >= 0; i--)
            {
                var record = _weakInterruptRecords[i];
                if (record.suppressor != state && record.suppressed != state)
                    continue;

                DecrementWeakSuppressionCount(record.suppressed);
                var layer = GetLayerByType(record.layerType);
                if (layer != null)
                    layer.MarkDirty(PipelineDirtyFlags.MixerWeights);

                _weakInterruptRecords.RemoveAt(i);
            }
        }

        private void ClearAllWeakInterruptions()
        {
            _weakInterruptRecords.Clear();
            _weakInterruptSuppressionCounts.Clear();
        }

        private void DecrementWeakSuppressionCount(StateBase suppressed)
        {
            if (suppressed == null)
                return;

            if (!_weakInterruptSuppressionCounts.TryGetValue(suppressed, out int count))
                return;

            if (count <= 1)
                _weakInterruptSuppressionCounts.Remove(suppressed);
            else
                _weakInterruptSuppressionCounts[suppressed] = count - 1;
        }

        private float ApplyWeakInterruptWeightFactor(StateBase state, float requestedWeight)
        {
            if (state == null || requestedWeight <= 0f)
                return requestedWeight;

            return _weakInterruptSuppressionCounts.ContainsKey(state)
                ? requestedWeight * Mathf.Clamp01(weakInterruptSuppressedWeightFactor)
                : requestedWeight;
        }

        public int WeakInterruptRelationCount => _weakInterruptRecords.Count;

        public bool IsStateWeakSuppressed(StateBase state)
        {
            return state != null && _weakInterruptSuppressionCounts.ContainsKey(state);
        }

        public string GetWeakInterruptSummary()
        {
            if (_weakInterruptRecords.Count == 0)
                return "无压制关系";

            var sb = _continuousStatsBuilder;
            sb.Clear();
            for (int i = 0; i < _weakInterruptRecords.Count; i++)
            {
                var record = _weakInterruptRecords[i];
                string suppressed = record.suppressed != null ? record.suppressed.strKey : "<空>";
                string suppressor = record.suppressor != null ? record.suppressor.strKey : "<空>";
                sb.Append(suppressed)
                    .Append(" <= ")
                    .Append(suppressor)
                    .Append("  层级:")
                    .Append(record.layerType)
                    .AppendLine();
            }
            return sb.ToString();
        }
    }
}
