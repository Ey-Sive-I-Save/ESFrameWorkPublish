using System;
using System.Collections.Generic;

namespace ES
{
    public sealed class ESFloatValueChangeSet
    {
        private readonly List<ESFloatValueChange> changes;
        private readonly Dictionary<int, int> indexByTokenId;
        private readonly Dictionary<int, List<ESValueChangeToken>> tokensByOwnerId;
        private readonly Dictionary<int, List<ESValueChangeToken>> tokensBySourceId;
        private readonly Stack<List<ESValueChangeToken>> recycledTokenIndexLists;

        private readonly int setId;
        private int nextTokenId = 1;
        private int tokenVersion = 1;
        private int nextOrder = 1;
        private float baseValue;
        private float cachedValue;
        private float minimumValue = float.NegativeInfinity;
        private float maximumValue = float.PositiveInfinity;
        private bool dirty = true;
        private int mutationDepth;
        private bool notificationPending;
        private bool isNotifying;

        /// <summary>
        /// Increments whenever the set's inputs change. Consumers that cache <see cref="Value"/>
        /// can use it, or subscribe to <see cref="Changed"/>, to know when to refresh.
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>Raised after the modifier collection or its base value changes.</summary>
        public event Action<ESFloatValueChangeSet> Changed;

        public ESFloatValueChangeSet(float baseValue = 0f, int capacity = 4)
        {
            if (capacity < 0)
                capacity = 0;
            if (!IsFinite(baseValue))
                throw new ArgumentOutOfRangeException(nameof(baseValue), "ValueChange base value must be finite.");

            changes = new List<ESFloatValueChange>(capacity);
            indexByTokenId = new Dictionary<int, int>(capacity);
            tokensByOwnerId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            tokensBySourceId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            recycledTokenIndexLists = new Stack<List<ESValueChangeToken>>(capacity > 1 ? capacity * 2 : 2);
            setId = ESValueChangeSetIdentity.Allocate();
            this.baseValue = baseValue;
            cachedValue = baseValue;
        }

        /// <summary>Process-local identity used to reject tokens issued by another set.</summary>
        public int SetId => setId;

        public int Count
        {
            get { return changes.Count; }
        }

        public float BaseValue
        {
            get { return baseValue; }
            set
            {
                if (!IsFinite(value))
                    throw new ArgumentOutOfRangeException(nameof(value), "ValueChange base value must be finite.");
                if (baseValue == value)
                    return;

                baseValue = value;
                MarkChanged();
            }
        }

        public bool IsDirty
        {
            get { return dirty; }
        }

        /// <summary>
        /// Non-revocable lower bound supplied by the owning attribute definition. It applies after
        /// all runtime modifier stages, including modifier Min/Max operations.
        /// </summary>
        public float MinimumValue => minimumValue;

        /// <summary>Non-revocable upper bound supplied by the owning attribute definition.</summary>
        public float MaximumValue => maximumValue;

        /// <summary>
        /// Sets the definition-level final bounds in one mutation. Bounds belong to the attribute
        /// schema, not to a runtime Token, so a Buff cannot release or override them.
        /// </summary>
        public void SetBounds(float minimum, float maximum)
        {
            if (float.IsNaN(minimum) || float.IsNaN(maximum) || minimum > maximum)
                throw new ArgumentOutOfRangeException(nameof(minimum), "ValueChange bounds must be ordered and cannot be NaN.");

            if (minimumValue == minimum && maximumValue == maximum)
                return;

            minimumValue = minimum;
            maximumValue = maximum;
            MarkChanged();
        }

        public float Value
        {
            get
            {
                if (dirty)
                    Recalculate();

                return cachedValue;
            }
        }

        /// <summary>
        /// Defers the Changed event until the outermost scope is disposed. Mutations remain immediately
        /// visible to subsequent reads; only notification fan-out is coalesced.
        /// </summary>
        public ESFloatValueChangeBatch BeginBatch()
        {
            mutationDepth++;
            return new ESFloatValueChangeBatch(this);
        }

        public ESValueChangeToken Add(
            ESFloatValueChangeOp op,
            float value,
            int ownerId = 0,
            int sourceId = 0,
            int priority = 0,
            bool enabled = true)
        {
            if (!IsFinite(value))
                return ESValueChangeToken.Invalid;

            EnsureOrderCapacity();

            ESValueChangeToken token = NewToken();
            ESFloatValueChange change = new ESFloatValueChange
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
                op = op,
                value = value
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

        public bool Update(ESValueChangeToken token, float value)
        {
            if (!IsFinite(value))
                return false;
            if (!TryGetIndex(token, out int index))
                return false;

            ESFloatValueChange change = changes[index];
            return Update(token, change.op, value, change.priority);
        }

        /// <summary>Updates every calculation field that can be changed at runtime.</summary>
        public bool Update(ESValueChangeToken token, ESFloatValueChangeOp op, float value, int priority)
        {
            if (!IsFinite(value))
                return false;
            if (!TryGetIndex(token, out int index))
                return false;

            ESFloatValueChange change = changes[index];
            if (change.op == op && change.value == value && change.priority == priority)
                return true;

            change.op = op;
            change.value = value;
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
            ESFloatValueChange change = changes[index];
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

        public bool TryGet(ESValueChangeToken token, out ESFloatValueChange change)
        {
            if (TryGetIndex(token, out int index))
            {
                change = changes[index];
                return true;
            }

            change = default;
            return false;
        }

        public ESFloatValueChange GetChangeAt(int index)
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
            cachedValue = baseValue;
            MarkChanged();
        }

        public void ForceRecalculate()
        {
            dirty = true;
            Recalculate();
        }

        private void Recalculate()
        {
            double addSum = 0d;
            double addPercentSum = 0d;
            double multiplyProduct = 1d;
            bool hasOverride = false;
            float overrideValue = 0f;
            int overridePriority = int.MinValue;
            int overrideOrder = int.MinValue;
            bool hasMin = false;
            bool hasMax = false;
            float minValue = 0f;
            float maxValue = 0f;

            for (int i = 0; i < changes.Count; i++)
            {
                ESFloatValueChange change = changes[i];
                if (change.enabled == 0)
                    continue;

                switch (change.op)
                {
                    case ESFloatValueChangeOp.Add:
                        addSum += change.value;
                        break;
                    case ESFloatValueChangeOp.AddPercent:
                        addPercentSum += change.value;
                        break;
                    case ESFloatValueChangeOp.Multiply:
                        multiplyProduct *= change.value;
                        break;
                    case ESFloatValueChangeOp.Override:
                        if (!hasOverride || IsHigher(change.priority, change.order, overridePriority, overrideOrder))
                        {
                            hasOverride = true;
                            overrideValue = change.value;
                            overridePriority = change.priority;
                            overrideOrder = change.order;
                        }
                        break;
                    case ESFloatValueChangeOp.Min:
                        if (!hasMin || change.value > minValue)
                        {
                            hasMin = true;
                            minValue = change.value;
                        }
                        break;
                    case ESFloatValueChangeOp.Max:
                        if (!hasMax || change.value < maxValue)
                        {
                            hasMax = true;
                            maxValue = change.value;
                        }
                        break;
                }
            }

            float value = hasOverride ? overrideValue : baseValue;
            value = ClampToFinite(value + addSum);
            value = ClampToFinite(value * (1d + addPercentSum));
            value = ClampToFinite(value * multiplyProduct);

            if (hasMin && value < minValue)
                value = minValue;

            if (hasMax && value > maxValue)
                value = maxValue;

            if (value < minimumValue)
                value = minimumValue;
            if (value > maximumValue)
                value = maximumValue;

            cachedValue = value;
            dirty = false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Finite inputs can still overflow while a large group is aggregated. The resolver must
        // never publish NaN/Infinity, because a single bad result poisons every downstream stat.
        private static float ClampToFinite(double value)
        {
            if (double.IsNaN(value))
                return 0f;
            if (value >= float.MaxValue)
                return float.MaxValue;
            if (value <= float.MinValue)
                return float.MinValue;
            return (float)value;
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

            ESFloatValueChange change = changes[index];
            return change.setId == token.setId
                && change.tokenId == token.tokenId
                && change.tokenVersion == token.tokenVersion;
        }

        private void RemoveAtSwapBack(int index)
        {
            int last = changes.Count - 1;
            ESFloatValueChange removed = changes[index];
            RemoveOwnerToken(removed);
            RemoveSourceToken(removed);

            if (index != last)
            {
                ESFloatValueChange moved = changes[last];
                changes[index] = moved;
                indexByTokenId[moved.tokenId] = index;
            }

            changes.RemoveAt(last);
            indexByTokenId.Remove(removed.tokenId);
        }

        private ESValueChangeToken NewToken()
        {
            if (nextTokenId == int.MaxValue)
                throw new InvalidOperationException("ESFloatValueChangeSet token id exhausted.");

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

        private void RemoveOwnerToken(ESFloatValueChange change)
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
                    ESFloatValueChange movedChange = changes[movedEntryIndex];
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

        private void RemoveSourceToken(ESFloatValueChange change)
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
                    ESFloatValueChange movedChange = changes[movedEntryIndex];
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
                ESFloatValueChange change = changes[i];
                change.order = i + 1;
                changes[i] = change;
                indexByTokenId[change.tokenId] = i;
            }

            nextOrder = changes.Count + 1;
        }

        private static bool IsHigher(int priority, int order, int bestPriority, int bestOrder)
        {
            if (priority != bestPriority)
                return priority > bestPriority;

            return order > bestOrder;
        }
    }
}
