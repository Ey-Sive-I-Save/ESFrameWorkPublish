using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// Internal pool-order diagnostics. Kept in the runtime assembly so Entity and Combat tests
    /// can assert lifecycle sequencing without exposing serialized gameplay state.
    /// </summary>
    internal static class ESActionPoolLifecycleDiagnostics
    {
        internal static readonly List<string> Sequence = new List<string>();

        internal static int SpawnCount { get; private set; }
        internal static int DespawnCount { get; private set; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void Clear()
        {
            Sequence.Clear();
            SpawnCount = 0;
            DespawnCount = 0;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void Record(string marker)
        {
            Sequence.Add(marker);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void RecordSpawn()
        {
            SpawnCount++;
            Sequence.Add("Entity.PoolSpawned");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void RecordDespawn()
        {
            DespawnCount++;
            Sequence.Add("Entity.PoolDespawned");
        }
    }
}
