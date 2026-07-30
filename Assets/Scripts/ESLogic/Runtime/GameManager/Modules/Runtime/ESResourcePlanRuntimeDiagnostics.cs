using System.Collections.Generic;

namespace ES
{
    public readonly struct ESResourcePlanDiagnosticEntry
    {
        public readonly string PlanName;
        public readonly ESResourcePlanState State;
        public readonly int TotalCount;
        public readonly int SuccessCount;
        public readonly int FailureCount;
        public readonly int RequiredFailureCount;
        public readonly int OptionalPendingCount;
        public readonly int RetainCount;
        public readonly bool IsReleasing;
        public readonly int LifetimeScopeOwnerCount;
        public readonly int UnownedRetainCount;
        public readonly bool InternalScopeDisposed;

        internal ESResourcePlanDiagnosticEntry(
            ESResourcePlanReport report,
            bool isReleasing,
            int lifetimeScopeOwnerCount,
            int unownedRetainCount,
            bool internalScopeDisposed)
        {
            PlanName = report?.Plan != null ? report.Plan.name : "<Missing Plan>";
            State = report?.State ?? ESResourcePlanState.Idle;
            TotalCount = report?.TotalCount ?? 0;
            SuccessCount = report?.SuccessCount ?? 0;
            FailureCount = report?.FailureCount ?? 0;
            RequiredFailureCount = report?.RequiredFailureCount ?? 0;
            OptionalPendingCount = report?.OptionalPendingCount ?? 0;
            RetainCount = report?.RetainCount ?? 0;
            IsReleasing = isReleasing;
            LifetimeScopeOwnerCount = lifetimeScopeOwnerCount;
            UnownedRetainCount = unownedRetainCount;
            InternalScopeDisposed = internalScopeDisposed;
        }
    }

    public readonly struct ESResourcePlanRuntimeDiagnostics
    {
        public readonly int ActiveCount;
        public readonly int ReleasingCount;
        public readonly IReadOnlyList<ESResourcePlanDiagnosticEntry> Entries;

        public ESResourcePlanRuntimeDiagnostics(int activeCount, int releasingCount, IReadOnlyList<ESResourcePlanDiagnosticEntry> entries)
        {
            ActiveCount = activeCount;
            ReleasingCount = releasingCount;
            Entries = entries;
        }
    }

    public sealed partial class ESResourcePlanRuntimeService
    {
        public ESResourcePlanRuntimeDiagnostics GetRuntimeDiagnostics()
        {
            var entries = new List<ESResourcePlanDiagnosticEntry>(contexts.Count + releasingContexts.Count);
            foreach (Context context in contexts.Values)
                entries.Add(new ESResourcePlanDiagnosticEntry(
                    context.report,
                    false,
                    context.lifetimeScopeRetains.Count,
                    context.unownedRetainCount,
                    context.scope.IsDisposed));
            foreach (Context context in releasingContexts)
                entries.Add(new ESResourcePlanDiagnosticEntry(
                    context.report,
                    true,
                    context.lifetimeScopeRetains.Count,
                    context.unownedRetainCount,
                    context.scope.IsDisposed));
            return new ESResourcePlanRuntimeDiagnostics(contexts.Count, releasingContexts.Count, entries);
        }
    }
}
