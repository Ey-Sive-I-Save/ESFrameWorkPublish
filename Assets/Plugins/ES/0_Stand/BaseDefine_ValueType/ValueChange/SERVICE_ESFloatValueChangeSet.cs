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

        private int nextTokenId = 1;
        private int nextOrder = 1;
        private float baseValue;
        private float cachedValue;
        private bool dirty = true;

        public ESFloatValueChangeSet(float baseValue = 0f, int capacity = 4)
        {
            if (capacity < 0)
                capacity = 0;

            changes = new List<ESFloatValueChange>(capacity);
            indexByTokenId = new Dictionary<int, int>(capacity);
            tokensByOwnerId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            tokensBySourceId = new Dictionary<int, List<ESValueChangeToken>>(capacity);
            this.baseValue = baseValue;
            cachedValue = baseValue;
        }

        public int Count
        {
            get { return changes.Count; }
        }

        public float BaseValue
        {
            get { return baseValue; }
            set
            {
                if (baseValue == value)
                    return;

                baseValue = value;
                dirty = true;
            }
        }

        public bool IsDirty
        {
            get { return dirty; }
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

        public ESValueChangeToken Add(
            ESFloatValueChangeOp op,
            float value,
            int ownerId = 0,
            int sourceId = 0,
            int priority = 0,
            bool enabled = true)
        {
            EnsureOrderCapacity();

            ESValueChangeToken token = NewToken();
            ESFloatValueChange change = new ESFloatValueChange
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
                op = op,
                value = value
            };
            change.ownerListIndex = AddOwnerToken(ownerId, token);
            change.sourceListIndex = AddSourceToken(sourceId, token);

            int index = changes.Count;
            changes.Add(change);
            indexByTokenId[token.tokenId] = index;
            dirty = true;
            return token;
        }

        public bool Update(ESValueChangeToken token, float value)
        {
            if (!TryGetIndex(token, out int index))
                return false;

            ESFloatValueChange change = changes[index];
            if (change.value == value)
                return true;

            change.value = value;
            changes[index] = change;
            dirty = true;
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
            tokensByOwnerId.Clear();
            tokensBySourceId.Clear();
            nextTokenId = 1;
            nextOrder = 1;
            cachedValue = baseValue;
            dirty = true;
        }

        public void ForceRecalculate()
        {
            dirty = true;
            Recalculate();
        }

        private void Recalculate()
        {
            float addSum = 0f;
            float addPercentSum = 0f;
            float multiplyProduct = 1f;
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
            value += addSum;
            value *= 1f + addPercentSum;
            value *= multiplyProduct;

            if (hasMin && value < minValue)
                value = minValue;

            if (hasMax && value > maxValue)
                value = maxValue;

            cachedValue = value;
            dirty = false;
        }

        private bool TryGetIndex(ESValueChangeToken token, out int index)
        {
            if (!token.IsValid || !indexByTokenId.TryGetValue(token.tokenId, out index))
            {
                index = -1;
                return false;
            }

            ESFloatValueChange change = changes[index];
            return change.tokenId == token.tokenId && change.tokenVersion == token.tokenVersion;
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

        private void RemoveOwnerToken(ESFloatValueChange change)
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
                    ESFloatValueChange movedChange = changes[movedEntryIndex];
                    movedChange.ownerListIndex = index;
                    changes[movedEntryIndex] = movedChange;
                }
            }

            tokens.RemoveAt(last);
            if (tokens.Count == 0)
                tokensByOwnerId.Remove(change.ownerId);
        }

        private void RemoveSourceToken(ESFloatValueChange change)
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
                    ESFloatValueChange movedChange = changes[movedEntryIndex];
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
