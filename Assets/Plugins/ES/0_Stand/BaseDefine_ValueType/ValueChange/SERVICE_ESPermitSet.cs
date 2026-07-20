using System;
using System.Collections.Generic;

namespace ES
{
    public sealed class ESPermitSet
    {
        private readonly List<ESPermitValueChange> changes;
        private readonly Dictionary<int, int> indexByTokenId;
        private readonly Dictionary<int, List<ESValueChangeToken>> tokensByOwnerId;
        private readonly Dictionary<int, List<ESValueChangeToken>> tokensBySourceId;

        private int nextTokenId = 1;
        private int nextOrder = 1;
        private bool fallbackValue;
        private bool cachedValue;
        private ESPermitLawResult cachedResult;
        private bool dirty = true;

        public ESPermitSet(bool fallbackValue = true, int capacity = 4)
        {
            if (capacity < 0)
                capacity = 0;

            changes = new List<ESPermitValueChange>(capacity);
            indexByTokenId = new Dictionary<int, int>(capacity);
            tokensByOwnerId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            tokensBySourceId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            this.fallbackValue = fallbackValue;
            cachedValue = fallbackValue;
            cachedResult = ESPermitLawResult.Fallback(fallbackValue);
        }

        public int Count
        {
            get { return changes.Count; }
        }

        public bool FallbackValue
        {
            get { return fallbackValue; }
            set
            {
                if (fallbackValue == value)
                    return;

                fallbackValue = value;
                dirty = true;
            }
        }

        public bool IsDirty
        {
            get { return dirty; }
        }

        public bool Value
        {
            get
            {
                if (dirty)
                    Recalculate();

                return cachedValue;
            }
        }

        public ESPermitLawResult Result
        {
            get
            {
                if (dirty)
                    Recalculate();

                return cachedResult;
            }
        }

        public ESValueChangeToken Add(
            ESPermitLaw law,
            int ownerId = 0,
            int sourceId = 0,
            int priority = 0,
            bool enabled = true)
        {
            EnsureOrderCapacity();

            ESValueChangeToken token = NewToken();
            ESPermitValueChange change = new ESPermitValueChange
            {
                tokenId = token.tokenId,
                tokenVersion = token.tokenVersion,
                ownerId = ownerId,
                sourceId = sourceId,
                ownerListIndex = -1,
                sourceListIndex = -1,
                priority = priority,
                order = nextOrder++,
                enabled = enabled ? (byte)1 : (byte)0,
                law = law
            };
            change.ownerListIndex = AddOwnerToken(ownerId, token);
            change.sourceListIndex = AddSourceToken(sourceId, token);

            int index = changes.Count;
            changes.Add(change);
            indexByTokenId[token.tokenId] = index;
            dirty = true;
            return token;
        }

        public bool Update(ESValueChangeToken token, ESPermitLaw law)
        {
            if (!TryGetIndex(token, out int index))
                return false;

            ESPermitValueChange change = changes[index];
            if (change.law == law)
                return true;

            change.law = law;
            changes[index] = change;
            dirty = true;
            return true;
        }

        public bool SetEnabled(ESValueChangeToken token, bool enabled)
        {
            if (!TryGetIndex(token, out int index))
                return false;

            byte next = enabled ? (byte)1 : (byte)0;
            ESPermitValueChange change = changes[index];
            if (change.enabled == next)
                return true;

            change.enabled = next;
            changes[index] = change;
            dirty = true;
            return true;
        }

        public bool Release(ESValueChangeToken token)
        {
            if (!TryGetIndex(token, out int index))
                return false;

            RemoveAtSwapBack(index);
            dirty = true;
            return true;
        }

        public int ReleaseAllByOwner(int ownerId)
        {
            if (!tokensByOwnerId.TryGetValue(ownerId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int removed = 0;
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (Release(tokens[i]))
                    removed++;
            }

            tokens.Clear();
            tokensByOwnerId.Remove(ownerId);
            return removed;
        }

        public int SetOwnerEnabled(int ownerId, bool enabled)
        {
            if (!tokensByOwnerId.TryGetValue(ownerId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int changed = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (SetEnabled(tokens[i], enabled))
                    changed++;
            }

            return changed;
        }

        public int ReleaseAllBySource(int sourceId)
        {
            if (!tokensBySourceId.TryGetValue(sourceId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int removed = 0;
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (Release(tokens[i]))
                    removed++;
            }

            tokens.Clear();
            tokensBySourceId.Remove(sourceId);
            return removed;
        }

        public int SetSourceEnabled(int sourceId, bool enabled)
        {
            if (!tokensBySourceId.TryGetValue(sourceId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int changed = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (SetEnabled(tokens[i], enabled))
                    changed++;
            }

            return changed;
        }

        public bool Contains(ESValueChangeToken token)
        {
            return TryGetIndex(token, out _);
        }

        public bool TryGet(ESValueChangeToken token, out ESPermitValueChange change)
        {
            if (TryGetIndex(token, out int index))
            {
                change = changes[index];
                return true;
            }

            change = default;
            return false;
        }

        public ESPermitValueChange GetChangeAt(int index)
        {
            return changes[index];
        }

        public void Clear()
        {
            changes.Clear();
            indexByTokenId.Clear();
            tokensByOwnerId.Clear();
            tokensBySourceId.Clear();
            nextTokenId = 1;
            nextOrder = 1;
            cachedValue = fallbackValue;
            cachedResult = ESPermitLawResult.Fallback(fallbackValue);
            dirty = true;
        }

        public void ForceRecalculate()
        {
            dirty = true;
            Recalculate();
        }

        private void Recalculate()
        {
            bool found = false;
            int bestIndex = -1;
            ESPermitValueChange best = default;

            for (int i = 0; i < changes.Count; i++)
            {
                ESPermitValueChange change = changes[i];
                if (change.enabled == 0 || !ESPermitLawUtility.IsExplicit(change.law))
                    continue;

                if (!found || IsHigherAuthority(change, best))
                {
                    found = true;
                    best = change;
                    bestIndex = i;
                }
            }

            if (!found)
            {
                cachedResult = ESPermitLawResult.Fallback(fallbackValue);
                cachedValue = fallbackValue;
                dirty = false;
                return;
            }

            cachedResult = new ESPermitLawResult
            {
                value = ESPermitLawUtility.Apply(best.law, fallbackValue),
                hasExplicitDecision = true,
                usedFallback = false,
                decision = best.law,
                priority = best.priority,
                stackIndex = best.order,
                sourceIndex = bestIndex
            };
            cachedValue = cachedResult.value;
            dirty = false;
        }

        private bool TryGetIndex(ESValueChangeToken token, out int index)
        {
            if (!token.IsValid || !indexByTokenId.TryGetValue(token.tokenId, out index))
            {
                index = -1;
                return false;
            }

            ESPermitValueChange change = changes[index];
            return change.tokenId == token.tokenId && change.tokenVersion == token.tokenVersion;
        }

        private void RemoveAtSwapBack(int index)
        {
            int last = changes.Count - 1;
            ESPermitValueChange removed = changes[index];
            RemoveOwnerToken(removed);
            RemoveSourceToken(removed);

            if (index != last)
            {
                ESPermitValueChange moved = changes[last];
                changes[index] = moved;
                indexByTokenId[moved.tokenId] = index;
            }

            changes.RemoveAt(last);
            indexByTokenId.Remove(removed.tokenId);
        }

        private ESValueChangeToken NewToken()
        {
            if (nextTokenId == int.MaxValue)
                throw new InvalidOperationException("ESPermitSet token id exhausted.");

            return new ESValueChangeToken(nextTokenId++, 1);
        }

        private int AddOwnerToken(int ownerId, ESValueChangeToken token)
        {
            if (!tokensByOwnerId.TryGetValue(ownerId, out List<ESValueChangeToken> tokens))
            {
                tokens = new List<ESValueChangeToken>(2);
                tokensByOwnerId.Add(ownerId, tokens);
            }

            int index = tokens.Count;
            tokens.Add(token);
            return index;
        }

        private int AddSourceToken(int sourceId, ESValueChangeToken token)
        {
            if (!tokensBySourceId.TryGetValue(sourceId, out List<ESValueChangeToken> tokens))
            {
                tokens = new List<ESValueChangeToken>(2);
                tokensBySourceId.Add(sourceId, tokens);
            }

            int index = tokens.Count;
            tokens.Add(token);
            return index;
        }

        private void RemoveOwnerToken(ESPermitValueChange change)
        {
            if (!tokensByOwnerId.TryGetValue(change.ownerId, out List<ESValueChangeToken> tokens) || tokens == null)
                return;

            int index = change.ownerListIndex;
            int last = tokens.Count - 1;
            if (index < 0 || index > last)
                return;

            ESValueChangeToken stored = tokens[index];
            if (stored.tokenId != change.tokenId || stored.tokenVersion != change.tokenVersion)
                return;

            if (index != last)
            {
                ESValueChangeToken movedToken = tokens[last];
                tokens[index] = movedToken;
                if (indexByTokenId.TryGetValue(movedToken.tokenId, out int movedEntryIndex))
                {
                    ESPermitValueChange movedChange = changes[movedEntryIndex];
                    movedChange.ownerListIndex = index;
                    changes[movedEntryIndex] = movedChange;
                }
            }

            tokens.RemoveAt(last);
            if (tokens.Count == 0)
                tokensByOwnerId.Remove(change.ownerId);
        }

        private void RemoveSourceToken(ESPermitValueChange change)
        {
            if (!tokensBySourceId.TryGetValue(change.sourceId, out List<ESValueChangeToken> tokens) || tokens == null)
                return;

            int index = change.sourceListIndex;
            int last = tokens.Count - 1;
            if (index < 0 || index > last)
                return;

            ESValueChangeToken stored = tokens[index];
            if (stored.tokenId != change.tokenId || stored.tokenVersion != change.tokenVersion)
                return;

            if (index != last)
            {
                ESValueChangeToken movedToken = tokens[last];
                tokens[index] = movedToken;
                if (indexByTokenId.TryGetValue(movedToken.tokenId, out int movedEntryIndex))
                {
                    ESPermitValueChange movedChange = changes[movedEntryIndex];
                    movedChange.sourceListIndex = index;
                    changes[movedEntryIndex] = movedChange;
                }
            }

            tokens.RemoveAt(last);
            if (tokens.Count == 0)
                tokensBySourceId.Remove(change.sourceId);
        }

        private void EnsureOrderCapacity()
        {
            if (nextOrder != int.MaxValue)
                return;

            changes.Sort((a, b) => a.order.CompareTo(b.order));
            for (int i = 0; i < changes.Count; i++)
            {
                ESPermitValueChange change = changes[i];
                change.order = i + 1;
                changes[i] = change;
                indexByTokenId[change.tokenId] = i;
            }

            nextOrder = changes.Count + 1;
        }

        private static bool IsHigherAuthority(ESPermitValueChange candidate, ESPermitValueChange currentBest)
        {
            bool candidateHard = ESPermitLawUtility.IsHard(candidate.law);
            bool currentHard = ESPermitLawUtility.IsHard(currentBest.law);
            if (candidateHard != currentHard)
                return candidateHard;

            if (candidate.priority != currentBest.priority)
                return candidate.priority > currentBest.priority;

            return candidate.order > currentBest.order;
        }
    }
}
