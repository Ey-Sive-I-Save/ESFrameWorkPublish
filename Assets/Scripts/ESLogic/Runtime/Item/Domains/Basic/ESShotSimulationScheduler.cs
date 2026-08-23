using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Shot 的单帧集中调度权威。MonoBehaviour Update 只负责触发一次批次，实际 Shot
    /// 模拟由本表顺序执行；注册、移除和容量变化只发生在发射/回收边界。
    /// </summary>
    public static class ESShotSimulationBatch
    {
        public const int Capacity = 4096;
        private static readonly ProfilerMarker TickMarker =
            new ProfilerMarker("ES.Shot.Simulation.Batch");
        private static readonly List<ItemShotModule> ActiveShots =
            new List<ItemShotModule>(Capacity);
        private static int lastFrame = -1;
        private static int highWatermark;
        private static int capacityRejectCount;
        private static long hitQueryOverflowCount;
        private static long hitOverflowStopCount;
        private static long resolvedColliderCapacityRejectCount;
        private static long impactQueryOverflowCount;

        public static int ActiveCount => ActiveShots.Count;
        public static int HighWatermark => highWatermark;
        public static int CapacityRejectCount => capacityRejectCount;
        public static long HitQueryOverflowCount => hitQueryOverflowCount;
        public static long HitOverflowStopCount => hitOverflowStopCount;
        public static long ResolvedColliderCapacityRejectCount => resolvedColliderCapacityRejectCount;
        public static long ImpactQueryOverflowCount => impactQueryOverflowCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int index = 0; index < ActiveShots.Count; index++)
            {
                ItemShotModule shot = ActiveShots[index];
                if (shot != null)
                    shot.Internal_SimulationIndex = -1;
            }
            ActiveShots.Clear();
            lastFrame = -1;
            highWatermark = 0;
            capacityRejectCount = 0;
            hitQueryOverflowCount = 0;
            hitOverflowStopCount = 0;
            resolvedColliderCapacityRejectCount = 0;
            impactQueryOverflowCount = 0;
        }

        internal static bool Internal_Register(ItemShotModule shot)
        {
            if (shot == null)
                return false;
            if (shot.Internal_SimulationIndex >= 0)
                return true;
            if (ActiveShots.Count >= Capacity)
            {
                capacityRejectCount++;
                return false;
            }

            shot.Internal_SimulationIndex = ActiveShots.Count;
            ActiveShots.Add(shot);
            if (ActiveShots.Count > highWatermark)
                highWatermark = ActiveShots.Count;
            return true;
        }

        internal static void Internal_Unregister(ItemShotModule shot)
        {
            if (shot == null)
                return;
            int index = shot.Internal_SimulationIndex;
            if ((uint)index >= (uint)ActiveShots.Count || ActiveShots[index] != shot)
            {
                shot.Internal_SimulationIndex = -1;
                return;
            }

            int last = ActiveShots.Count - 1;
            ItemShotModule moved = ActiveShots[last];
            hitQueryOverflowCount += shot.hitOverflowCount;
            hitOverflowStopCount += shot.hitOverflowStopCount;
            resolvedColliderCapacityRejectCount += shot.resolvedColliderOverflowCount;
            impactQueryOverflowCount += shot.impactOverflowCount;
            ActiveShots.RemoveAt(last);
            if (index != last)
            {
                ActiveShots[index] = moved;
                moved.Internal_SimulationIndex = index;
            }
            shot.Internal_SimulationIndex = -1;
        }

        [ESHotPath]
        internal static void Internal_Tick(float deltaTime)
        {
            int frame = Time.frameCount;
            if (lastFrame == frame)
                return;

            lastFrame = frame;
            using (TickMarker.Auto())
            {
                int remaining = ActiveShots.Count;
                int index = 0;
                while (remaining-- > 0 && index < ActiveShots.Count)
                {
                    ItemShotModule shot = ActiveShots[index];
                    if (shot != null)
                        shot.Internal_TickCentralized(deltaTime);
                    if (index < ActiveShots.Count && ActiveShots[index] == shot)
                        index++;
                }
            }
        }

        internal static void Internal_ResetDiagnostics()
        {
            highWatermark = ActiveShots.Count;
            capacityRejectCount = 0;
            hitQueryOverflowCount = 0;
            hitOverflowStopCount = 0;
            resolvedColliderCapacityRejectCount = 0;
            impactQueryOverflowCount = 0;
            for (int index = 0; index < ActiveShots.Count; index++)
                ActiveShots[index]?.Internal_ResetOverflowDiagnostics();
        }
    }
}
