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
        private readonly Stack<List<ESValueChangeToken>> recycledTokenIndexLists;

        private readonly int setId;
        private int nextTokenId = 1;
        private int tokenVersion = 1;
        private int nextOrder = 1;
        private bool fallbackValue;
        private bool cachedValue;
        private ESPermitLawResult cachedResult;
        private bool dirty = true;
        private int mutationDepth;
        private bool notificationPending;
        private bool isNotifying;

        /// <summary>Increments whenever the permit inputs or fallback change.</summary>
        public int Revision { get; private set; }

        /// <summary>Raised after the permit inputs or fallback value change.</summary>
        public event Action<ESPermitSet> Changed;

        public ESPermitSet(bool fallbackValue = true, int capacity = 4)
        {
            if (capacity < 0)
                capacity = 0;

            changes = new List<ESPermitValueChange>(capacity);
            indexByTokenId = new Dictionary<int, int>(capacity);
            tokensByOwnerId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            tokensBySourceId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            recycledTokenIndexLists = new Stack<List<ESValueChangeToken>>(capacity > 1 ? capacity * 2 : 2);
            setId = ESValueChangeSetIdentity.Allocate();
            this.fallbackValue = fallbackValue;
            cachedValue = fallbackValue;
            cachedResult = ESPermitLawResult.Fallback(fallbackValue);
        }

        /// <summary>Process-local identity used to reject tokens issued by another set.</summary>
        public int SetId => setId;

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
                MarkChanged();
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

        /// <summary>Defers Changed notification until the outermost scope is disposed.</summary>
        public ESPermitValueChangeBatch BeginBatch()
        {
            mutationDepth++;
            return new ESPermitValueChangeBatch(this);
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
                setId = setId,
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
            if (ownerId != 0)
                change.ownerListIndex = AddOwnerToken(ownerId, token);
            if (sourceId != 0)
                change.sourceListIndex = AddSourceToken(sourceId, token);

            int index = changes.Count;
            changes.Add(change);
            indexByTokenId[token.tokenId] = index;
            MarkChanged();
            return token;
        }

        public bool Update(ESValueChangeToken token, ESPermitLaw law)
        {
            if (!TryGetIndex(token, out int index))
                return false;

            ESPermitValueChange change = changes[index];
            return Update(token, law, change.priority);
        }

        /// <summary>Updates every resolver field that can be changed at runtime.</summary>
        public bool Update(ESValueChangeToken token, ESPermitLaw law, int priority)
        {
            if (!TryGetIndex(token, out int index))
                return false;

            ESPermitValueChange change = changes[index];
            if (change.law == law && change.priority == priority)
                return true;

            change.law = law;
            change.priority = priority;
            changes[index] = change;
            MarkChanged();
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
            MarkChanged();
            return true;
        }

        public bool Release(ESValueChangeToken token)
        {
            if (!TryGetIndex(token, out int index))
                return false;

            RemoveAtSwapBack(index);
            MarkChanged();
            return true;
        }

        public int ReleaseAllByOwner(int ownerId)
        {
            if (ownerId == 0)
                return 0;

            if (!tokensByOwnerId.TryGetValue(ownerId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int removed = 0;
            using (BeginBatch())
            {
                for (int i = tokens.Count - 1; i >= 0; i--)
                {
                    if (Release(tokens[i]))
                        removed++;
                }

                tokens.Clear();
                tokensByOwnerId.Remove(ownerId);
            }
            return removed;
        }

        public int SetOwnerEnabled(int ownerId, bool enabled)
        {
            if (ownerId == 0)
                return 0;

            if (!tokensByOwnerId.TryGetValue(ownerId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int changed = 0;
            using (BeginBatch())
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (SetEnabled(tokens[i], enabled))
                        changed++;
                }
            }
            return changed;
        }

        public int ReleaseAllBySource(int sourceId)
        {
            if (sourceId == 0)
                return 0;

            if (!tokensBySourceId.TryGetValue(sourceId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int removed = 0;
            using (BeginBatch())
            {
                for (int i = tokens.Count - 1; i >= 0; i--)
                {
                    if (Release(tokens[i]))
                        removed++;
                }

                tokens.Clear();
                tokensBySourceId.Remove(sourceId);
            }
            return removed;
        }

        public int SetSourceEnabled(int sourceId, bool enabled)
        {
            if (sourceId == 0)
                return 0;

            if (!tokensBySourceId.TryGetValue(sourceId, out List<ESValueChangeToken> tokens) || tokens == null)
                return 0;

            int changed = 0;
            using (BeginBatch())
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (SetEnabled(tokens[i], enabled))
                        changed++;
                }
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
            RecycleTokenIndexLists(tokensByOwnerId);
            RecycleTokenIndexLists(tokensBySourceId);
            tokensByOwnerId.Clear();
            tokensBySourceId.Clear();
            nextTokenId = 1;
            AdvanceTokenVersion();
            nextOrder = 1;
            cachedValue = fallbackValue;
            cachedResult = ESPermitLawResult.Fallback(fallbackValue);
            MarkChanged();
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
            if (!token.IsValid
                || token.setId != setId
                || !indexByTokenId.TryGetValue(token.tokenId, out index))
            {
                index = -1;
                return false;
            }

            ESPermitValueChange change = changes[index];
            return change.setId == token.setId
                && change.tokenId == token.tokenId
                && change.tokenVersion == token.tokenVersion;
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

            return new ESValueChangeToken(setId, nextTokenId++, tokenVersion);
        }

        private void AdvanceTokenVersion()
        {
            if (tokenVersion == int.MaxValue)
                tokenVersion = 1;
            else
                tokenVersion++;
        }

        private void MarkChanged()
        {
            dirty = true;
            Revision++;
            if (mutationDepth != 0 || isNotifying)
            {
                notificationPending = true;
                return;
            }

            NotifyChanged();
        }

        internal void EndBatch()
        {
            if (mutationDepth == 0)
                return;

            mutationDepth--;
            if (mutationDepth == 0 && notificationPending)
            {
                notificationPending = false;
                NotifyChanged();
            }
        }

        private void NotifyChanged()
        {
            if (Changed == null)
                return;

            isNotifying = true;
            try
            {
                Changed.Invoke(this);
            }
            finally
            {
                isNotifying = false;
            }

            if (mutationDepth == 0 && notificationPending)
            {
                notificationPending = false;
                NotifyChanged();
            }
        }

        private int AddOwnerToken(int ownerId, ESValueChangeToken token)
        {
            if (!tokensByOwnerId.TryGetValue(ownerId, out List<ESValueChangeToken> tokens))
            {
                tokens = RentTokenIndexList();
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
                tokens = RentTokenIndexList();
                tokensBySourceId.Add(sourceId, tokens);
            }

            int index = tokens.Count;
            tokens.Add(token);
            return index;
        }

        private void RemoveOwnerToken(ESPermitValueChange change)
        {
            if (change.ownerId == 0 || change.ownerListIndex < 0)
                return;

            if (!tokensByOwnerId.TryGetValue(change.ownerId, out List<ESValueChangeToken> tokens) || tokens == null)
                return;

            int index = change.ownerListIndex;
            int last = tokens.Count - 1;
            if (index < 0 || index > last)
                return;

            ESValueChangeToken stored = tokens[index];
            if (stored.setId != change.setId
                || stored.tokenId != change.tokenId
                || stored.tokenVersion != change.tokenVersion)
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
            {
                tokensByOwnerId.Remove(change.ownerId);
                RecycleTokenIndexList(tokens);
            }
        }

        private void RemoveSourceToken(ESPermitValueChange change)
        {
            if (change.sourceId == 0 || change.sourceListIndex < 0)
                return;

            if (!tokensBySourceId.TryGetValue(change.sourceId, out List<ESValueChangeToken> tokens) || tokens == null)
                return;

            int index = change.sourceListIndex;
            int last = tokens.Count - 1;
            if (index < 0 || index > last)
                return;

            ESValueChangeToken stored = tokens[index];
            if (stored.setId != change.setId
                || stored.tokenId != change.tokenId
                || stored.tokenVersion != change.tokenVersion)
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
            {
                tokensBySourceId.Remove(change.sourceId);
                RecycleTokenIndexList(tokens);
            }
        }

        private List<ESValueChangeToken> RentTokenIndexList()
        {
            return recycledTokenIndexLists.Count != 0
                ? recycledTokenIndexLists.Pop()
                : new List<ESValueChangeToken>(2);
        }

        private void RecycleTokenIndexList(List<ESValueChangeToken> tokens)
        {
            if (tokens == null)
                return;

            tokens.Clear();
            recycledTokenIndexLists.Push(tokens);
        }

        private void RecycleTokenIndexLists(Dictionary<int, List<ESValueChangeToken>> index)
        {
            foreach (List<ESValueChangeToken> tokens in index.Values)
                RecycleTokenIndexList(tokens);
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
            return ESPermitLawResolver.IsHigherAuthority(
                candidate.law,
                candidate.priority,
                candidate.order,
                currentBest.law,
                currentBest.priority,
                currentBest.order);
        }
    }
}
