using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// One permit law candidate used by the framework-level resolver.
    /// Higher priority wins; when priority is equal, higher stackIndex wins.
    /// </summary>
    public struct ESPermitLawEntry
    {
        public ESPermitLaw decision;
        public int priority;
        public int stackIndex;

        public ESPermitLawEntry(ESPermitLaw decision, int priority, int stackIndex)
        {
            this.decision = decision;
            this.priority = priority;
            this.stackIndex = stackIndex;
        }
    }

    /// <summary>
    /// Resolved bool value plus the winning decision metadata.
    /// </summary>
    public struct ESPermitLawResult
    {
        public bool value;
        public bool hasExplicitDecision;
        public bool usedFallback;
        public ESPermitLaw decision;
        public int priority;
        public int stackIndex;
        public int sourceIndex;

        public static ESPermitLawResult Fallback(bool fallback)
        {
            return new ESPermitLawResult
            {
                value = fallback,
                hasExplicitDecision = false,
                usedFallback = true,
                decision = ESPermitLaw.Ignore,
                priority = int.MinValue,
                stackIndex = int.MinValue,
                sourceIndex = -1
            };
        }
    }

    public static class ESPermitLawResolver
    {
        public static bool Resolve(IList<ESPermitLawEntry> entries, bool fallback)
        {
            return Resolve(entries, fallback, out ESPermitLawResult result)
                ? result.value
                : fallback;
        }

        public static bool Resolve(IList<ESPermitLawEntry> entries, bool fallback, out ESPermitLawResult result)
        {
            if (entries == null || entries.Count == 0)
            {
                result = ESPermitLawResult.Fallback(fallback);
                return false;
            }

            bool found = false;
            int bestIndex = -1;
            ESPermitLawEntry best = new ESPermitLawEntry();

            for (int i = 0; i < entries.Count; i++)
            {
                ESPermitLawEntry current = entries[i];
                if (!ESPermitLawUtility.IsExplicit(current.decision))
                    continue;

                if (!found || IsHigherAuthority(current, best))
                {
                    found = true;
                    best = current;
                    bestIndex = i;
                }
            }

            if (!found)
            {
                result = ESPermitLawResult.Fallback(fallback);
                return false;
            }

            result = new ESPermitLawResult
            {
                value = ESPermitLawUtility.Apply(best.decision, fallback),
                hasExplicitDecision = true,
                usedFallback = false,
                decision = best.decision,
                priority = best.priority,
                stackIndex = best.stackIndex,
                sourceIndex = bestIndex
            };
            return true;
        }

        public static bool Resolve(ESPermitLawEntry[] entries, int count, bool fallback)
        {
            return Resolve(entries, count, fallback, out ESPermitLawResult result)
                ? result.value
                : fallback;
        }

        public static bool Resolve(ESPermitLawEntry[] entries, int count, bool fallback, out ESPermitLawResult result)
        {
            if (entries == null || count <= 0)
            {
                result = ESPermitLawResult.Fallback(fallback);
                return false;
            }

            if (count > entries.Length)
                count = entries.Length;

            bool found = false;
            int bestIndex = -1;
            ESPermitLawEntry best = new ESPermitLawEntry();

            for (int i = 0; i < count; i++)
            {
                ESPermitLawEntry current = entries[i];
                if (!ESPermitLawUtility.IsExplicit(current.decision))
                    continue;

                if (!found || IsHigherAuthority(current, best))
                {
                    found = true;
                    best = current;
                    bestIndex = i;
                }
            }

            if (!found)
            {
                result = ESPermitLawResult.Fallback(fallback);
                return false;
            }

            result = new ESPermitLawResult
            {
                value = ESPermitLawUtility.Apply(best.decision, fallback),
                hasExplicitDecision = true,
                usedFallback = false,
                decision = best.decision,
                priority = best.priority,
                stackIndex = best.stackIndex,
                sourceIndex = bestIndex
            };
            return true;
        }

        public static bool IsHigherAuthority(ESPermitLawEntry candidate, ESPermitLawEntry currentBest)
        {
            if (candidate.priority != currentBest.priority)
                return candidate.priority > currentBest.priority;

            return candidate.stackIndex > currentBest.stackIndex;
        }
    }
}

