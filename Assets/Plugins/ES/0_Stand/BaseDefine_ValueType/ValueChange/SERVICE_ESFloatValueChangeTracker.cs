using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ES
{
    [Serializable]
    public sealed class ESFloatValueChangeTracker
    {
        private readonly List<ESValueChangeToken> tokens;
        private readonly Dictionary<int, int> indexByTokenId;
        private ESFloatValueChangeSet set;

        public int ownerId;
        public int sourceId;

        public ESFloatValueChangeTracker(ESFloatValueChangeSet set = null, int ownerId = 0, int sourceId = 0, int capacity = 4)
        {
            if (capacity < 0)
                capacity = 0;

            tokens = new List<ESValueChangeToken>(capacity);
            indexByTokenId = new Dictionary<int, int>(capacity);
            this.set = set;
            this.ownerId = ownerId;
            this.sourceId = sourceId;
        }

        public ESFloatValueChangeSet Set
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return set; }
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return tokens.Count; }
        }

        public bool HasToken
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return tokens.Count > 0; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Bind(ESFloatValueChangeSet targetSet)
        {
            set = targetSet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSource(int ownerId, int sourceId)
        {
            this.ownerId = ownerId;
            this.sourceId = sourceId;
        }

        public ESValueChangeToken Add(ESFloatValueChangeOp op, float value, int priority = 0, bool enabled = true)
        {
            ESValueChangeToken token = set.Add(op, value, ownerId, sourceId, priority, enabled);
            Track(token);
            return token;
        }

        public bool Update(ESValueChangeToken token, float value)
        {
            return set.Update(token, value);
        }

        public bool SetEnabled(ESValueChangeToken token, bool enabled)
        {
            return set.SetEnabled(token, enabled);
        }

        public bool Release(ESValueChangeToken token)
        {
            if (!TryGetLocalIndex(token, out int index))
                return false;

            RemoveLocalAt(index);
            return set.Release(token);
        }

        public int SetAllEnabled(bool enabled)
        {
            int changed = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (set.SetEnabled(tokens[i], enabled))
                    changed++;
            }

            return changed;
        }

        public int ReleaseAll()
        {
            int released = 0;
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (set.Release(tokens[i]))
                    released++;
            }

            tokens.Clear();
            indexByTokenId.Clear();
            return released;
        }

        public ESValueChangeToken GetTokenAt(int index)
        {
            return tokens[index];
        }

        public void ClearLocalOnly()
        {
            tokens.Clear();
            indexByTokenId.Clear();
        }

        private void Track(ESValueChangeToken token)
        {
            indexByTokenId[token.tokenId] = tokens.Count;
            tokens.Add(token);
        }

        private bool TryGetLocalIndex(ESValueChangeToken token, out int index)
        {
            if (!token.IsValid || !indexByTokenId.TryGetValue(token.tokenId, out index))
            {
                index = -1;
                return false;
            }

            ESValueChangeToken current = tokens[index];
            return current.tokenId == token.tokenId && current.tokenVersion == token.tokenVersion;
        }

        private void RemoveLocalAt(int index)
        {
            int last = tokens.Count - 1;
            ESValueChangeToken removed = tokens[index];
            if (index != last)
            {
                ESValueChangeToken moved = tokens[last];
                tokens[index] = moved;
                indexByTokenId[moved.tokenId] = index;
            }

            tokens.RemoveAt(last);
            indexByTokenId.Remove(removed.tokenId);
        }
    }
}
