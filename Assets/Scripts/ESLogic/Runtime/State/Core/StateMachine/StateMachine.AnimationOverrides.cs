using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        private enum AnimationOverrideScope
        {
            AnyState = 0,
            StateKey = 1,
            StateId = 2
        }

        private enum AnimationOverrideSelector
        {
            Marker = 0,
            SourceClip = 1,
            ClipIndex = 2
        }

        private sealed class PendingAnimationClipOverride
        {
            public AnimationOverrideScope scope;
            public AnimationOverrideSelector selector;
            public string stateKey;
            public int stateId;
            public string marker;
            public AnimationClip sourceClip;
            public int clipIndex;
            public AnimationClip targetClip;
        }

        [NonSerialized]
        private List<PendingAnimationClipOverride> _pendingAnimationClipOverrides;

        public bool SetAnimationClipOverride(string marker, AnimationClip newClip)
        {
            if (string.IsNullOrWhiteSpace(marker) || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.AnyState,
                selector = AnimationOverrideSelector.Marker,
                marker = marker,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            ApplyPendingAnimationClipOverrideToCachedStates(rule);
            return true;
        }

        public bool SetAnimationClipOverride(AnimationClip sourceClip, AnimationClip newClip)
        {
            if (sourceClip == null || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.AnyState,
                selector = AnimationOverrideSelector.SourceClip,
                sourceClip = sourceClip,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            ApplyPendingAnimationClipOverrideToCachedStates(rule);
            return true;
        }

        public bool SetStateAnimationClipOverride(string stateKey, string marker, AnimationClip newClip)
        {
            if (string.IsNullOrEmpty(stateKey) || string.IsNullOrWhiteSpace(marker) || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.StateKey,
                selector = AnimationOverrideSelector.Marker,
                stateKey = stateKey,
                marker = marker,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            TryApplyPendingAnimationClipOverrideToCachedState(GetStateByString(stateKey), rule);
            return true;
        }

        public bool SetStateAnimationClipOverride(int stateId, string marker, AnimationClip newClip)
        {
            if (stateId == 0 || string.IsNullOrWhiteSpace(marker) || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.StateId,
                selector = AnimationOverrideSelector.Marker,
                stateId = stateId,
                marker = marker,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            TryApplyPendingAnimationClipOverrideToCachedState(GetStateByInt(stateId), rule);
            return true;
        }

        public bool SetStateAnimationClipOverride(string stateKey, AnimationClip sourceClip, AnimationClip newClip)
        {
            if (string.IsNullOrEmpty(stateKey) || sourceClip == null || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.StateKey,
                selector = AnimationOverrideSelector.SourceClip,
                stateKey = stateKey,
                sourceClip = sourceClip,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            TryApplyPendingAnimationClipOverrideToCachedState(GetStateByString(stateKey), rule);
            return true;
        }

        public bool SetStateAnimationClipOverride(string stateKey, int clipIndex, AnimationClip newClip)
        {
            if (string.IsNullOrEmpty(stateKey) || clipIndex < 0 || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.StateKey,
                selector = AnimationOverrideSelector.ClipIndex,
                stateKey = stateKey,
                clipIndex = clipIndex,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            TryApplyPendingAnimationClipOverrideToCachedState(GetStateByString(stateKey), rule);
            return true;
        }

        public bool SetStateAnimationClipOverride(int stateId, AnimationClip sourceClip, AnimationClip newClip)
        {
            if (stateId == 0 || sourceClip == null || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.StateId,
                selector = AnimationOverrideSelector.SourceClip,
                stateId = stateId,
                sourceClip = sourceClip,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            TryApplyPendingAnimationClipOverrideToCachedState(GetStateByInt(stateId), rule);
            return true;
        }

        public bool SetStateAnimationClipOverride(int stateId, int clipIndex, AnimationClip newClip)
        {
            if (stateId == 0 || clipIndex < 0 || newClip == null)
                return false;

            var rule = new PendingAnimationClipOverride
            {
                scope = AnimationOverrideScope.StateId,
                selector = AnimationOverrideSelector.ClipIndex,
                stateId = stateId,
                clipIndex = clipIndex,
                targetClip = newClip
            };

            UpsertPendingAnimationClipOverride(rule);
            TryApplyPendingAnimationClipOverrideToCachedState(GetStateByInt(stateId), rule);
            return true;
        }

        public bool ClearAnimationClipOverride(string marker, bool restoreCachedStates = true)
        {
            if (string.IsNullOrWhiteSpace(marker))
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.AnyState, AnimationOverrideSelector.Marker, null, 0, marker, null, -1);
            if (restoreCachedStates)
            {
                RestoreCachedStateAnimationClipOverride(marker);
            }
            return removed;
        }

        public bool ClearAnimationClipOverride(AnimationClip sourceClip, bool restoreCachedStates = true)
        {
            if (sourceClip == null)
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.AnyState, AnimationOverrideSelector.SourceClip, null, 0, null, sourceClip, -1);
            if (restoreCachedStates)
            {
                RestoreCachedStateAnimationClipOverride(sourceClip);
            }
            return removed;
        }

        public bool ClearStateAnimationClipOverride(string stateKey, string marker, bool restoreIfRunning = true)
        {
            if (string.IsNullOrEmpty(stateKey) || string.IsNullOrWhiteSpace(marker))
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.StateKey, AnimationOverrideSelector.Marker, stateKey, 0, marker, null, -1);
            if (restoreIfRunning)
            {
                var state = GetStateByString(stateKey);
                if (state != null)
                    state.RestoreAnimationClipOverride(marker);
            }
            return removed;
        }

        public bool ClearStateAnimationClipOverride(string stateKey, AnimationClip sourceClip, bool restoreIfRunning = true)
        {
            if (string.IsNullOrEmpty(stateKey) || sourceClip == null)
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.StateKey, AnimationOverrideSelector.SourceClip, stateKey, 0, null, sourceClip, -1);
            if (restoreIfRunning)
            {
                var state = GetStateByString(stateKey);
                if (state != null)
                    state.RestoreAnimationClipOverride(sourceClip);
            }
            return removed;
        }

        public bool ClearStateAnimationClipOverride(string stateKey, int clipIndex, bool restoreIfRunning = true)
        {
            if (string.IsNullOrEmpty(stateKey) || clipIndex < 0)
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.StateKey, AnimationOverrideSelector.ClipIndex, stateKey, 0, null, null, clipIndex);
            if (restoreIfRunning)
            {
                var state = GetStateByString(stateKey);
                if (state != null)
                    state.RestoreAnimationClipOverride(clipIndex);
            }
            return removed;
        }

        public bool ClearStateAnimationClipOverride(int stateId, AnimationClip sourceClip, bool restoreIfRunning = true)
        {
            if (stateId == 0 || sourceClip == null)
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.StateId, AnimationOverrideSelector.SourceClip, null, stateId, null, sourceClip, -1);
            if (restoreIfRunning)
            {
                var state = GetStateByInt(stateId);
                if (state != null)
                    state.RestoreAnimationClipOverride(sourceClip);
            }
            return removed;
        }

        public bool ClearStateAnimationClipOverride(int stateId, string marker, bool restoreIfRunning = true)
        {
            if (stateId == 0 || string.IsNullOrWhiteSpace(marker))
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.StateId, AnimationOverrideSelector.Marker, null, stateId, marker, null, -1);
            if (restoreIfRunning)
            {
                var state = GetStateByInt(stateId);
                if (state != null)
                    state.RestoreAnimationClipOverride(marker);
            }
            return removed;
        }

        public bool ClearStateAnimationClipOverride(int stateId, int clipIndex, bool restoreIfRunning = true)
        {
            if (stateId == 0 || clipIndex < 0)
                return false;

            bool removed = RemovePendingAnimationClipOverride(AnimationOverrideScope.StateId, AnimationOverrideSelector.ClipIndex, null, stateId, null, null, clipIndex);
            if (restoreIfRunning)
            {
                var state = GetStateByInt(stateId);
                if (state != null)
                    state.RestoreAnimationClipOverride(clipIndex);
            }
            return removed;
        }

        public int ClearAllAnimationClipOverrides(bool restoreCachedStates = true)
        {
            int removed = _pendingAnimationClipOverrides != null ? _pendingAnimationClipOverrides.Count : 0;
            if (_pendingAnimationClipOverrides != null)
            {
                _pendingAnimationClipOverrides.Clear();
            }

            if (restoreCachedStates)
            {
                RestoreCachedStateAnimationClipOverrides();
            }

            return removed;
        }

        private void UpsertPendingAnimationClipOverride(PendingAnimationClipOverride rule)
        {
            if (_pendingAnimationClipOverrides == null)
                _pendingAnimationClipOverrides = new List<PendingAnimationClipOverride>(4);

            for (int i = 0; i < _pendingAnimationClipOverrides.Count; i++)
            {
                var existing = _pendingAnimationClipOverrides[i];
                if (IsSamePendingAnimationClipOverride(existing, rule))
                {
                    existing.targetClip = rule.targetClip;
                    return;
                }
            }

            _pendingAnimationClipOverrides.Add(rule);
        }

        private bool RemovePendingAnimationClipOverride(
            AnimationOverrideScope scope,
            AnimationOverrideSelector selector,
            string stateKey,
            int stateId,
            string marker,
            AnimationClip sourceClip,
            int clipIndex)
        {
            var rules = _pendingAnimationClipOverrides;
            if (rules == null || rules.Count == 0)
                return false;

            bool removed = false;
            for (int i = rules.Count - 1; i >= 0; i--)
            {
                var rule = rules[i];
                if (rule.scope != scope || rule.selector != selector)
                    continue;

                if (scope == AnimationOverrideScope.StateKey && !string.Equals(rule.stateKey, stateKey, StringComparison.Ordinal))
                    continue;

                if (scope == AnimationOverrideScope.StateId && rule.stateId != stateId)
                    continue;

                if (selector == AnimationOverrideSelector.Marker && !string.Equals(rule.marker, marker, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (selector == AnimationOverrideSelector.SourceClip && rule.sourceClip != sourceClip)
                    continue;

                if (selector == AnimationOverrideSelector.ClipIndex && rule.clipIndex != clipIndex)
                    continue;

                rules.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private bool IsSamePendingAnimationClipOverride(PendingAnimationClipOverride a, PendingAnimationClipOverride b)
        {
            if (a == null || b == null)
                return false;

            if (a.scope != b.scope || a.selector != b.selector)
                return false;

            if (a.scope == AnimationOverrideScope.StateKey && !string.Equals(a.stateKey, b.stateKey, StringComparison.Ordinal))
                return false;

            if (a.scope == AnimationOverrideScope.StateId && a.stateId != b.stateId)
                return false;

            switch (a.selector)
            {
                case AnimationOverrideSelector.Marker:
                    return string.Equals(a.marker, b.marker, StringComparison.OrdinalIgnoreCase);
                case AnimationOverrideSelector.SourceClip:
                    return a.sourceClip == b.sourceClip;
                case AnimationOverrideSelector.ClipIndex:
                    return a.clipIndex == b.clipIndex;
                default:
                    return false;
            }
        }

        private int ApplyPendingAnimationClipOverrides(StateBase state)
        {
            var rules = _pendingAnimationClipOverrides;
            if (state == null || rules == null || rules.Count == 0)
                return 0;

            int changed = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!ShouldApplyPendingAnimationClipOverride(state, rule))
                    continue;

                if (ApplyPendingAnimationClipOverride(state, rule))
                    changed++;
            }

            return changed;
        }

        private bool ShouldApplyPendingAnimationClipOverride(StateBase state, PendingAnimationClipOverride rule)
        {
            if (state == null || rule == null || rule.targetClip == null)
                return false;

            switch (rule.scope)
            {
                case AnimationOverrideScope.AnyState:
                    return true;
                case AnimationOverrideScope.StateKey:
                    return string.Equals(state.strKey, rule.stateKey, StringComparison.Ordinal);
                case AnimationOverrideScope.StateId:
                    return state.intKey == rule.stateId;
                default:
                    return false;
            }
        }

        private bool ApplyPendingAnimationClipOverride(StateBase state, PendingAnimationClipOverride rule)
        {
            switch (rule.selector)
            {
                case AnimationOverrideSelector.Marker:
                    return state.TryOverrideAnimationClip(rule.marker, rule.targetClip);
                case AnimationOverrideSelector.SourceClip:
                    return state.TryOverrideAnimationClip(rule.sourceClip, rule.targetClip);
                case AnimationOverrideSelector.ClipIndex:
                    return state.TryOverrideAnimationClip(rule.clipIndex, rule.targetClip);
                default:
                    return false;
            }
        }

        private int ApplyPendingAnimationClipOverrideToCachedStates(PendingAnimationClipOverride rule)
        {
            if (rule == null)
                return 0;

            int changed = 0;
            var states = _registeredStatesList;
            for (int i = 0; i < states.Count; i++)
            {
                if (TryApplyPendingAnimationClipOverrideToCachedState(states[i], rule))
                {
                    changed++;
                }
            }
            return changed;
        }

        private bool TryApplyPendingAnimationClipOverrideToCachedState(StateBase state, PendingAnimationClipOverride rule)
        {
            if (state == null || rule == null || !ShouldApplyPendingAnimationClipOverride(state, rule))
                return false;

            var runtime = state.AnimationRuntime;
            if (runtime == null || !runtime.IsInitialized)
                return false;

            return ApplyPendingAnimationClipOverride(state, rule);
        }

        private int RestoreCachedStateAnimationClipOverride(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
                return 0;

            int changed = 0;
            var states = _registeredStatesList;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null && state.RestoreAnimationClipOverride(marker))
                {
                    changed++;
                }
            }
            return changed;
        }

        private int RestoreCachedStateAnimationClipOverride(AnimationClip sourceClip)
        {
            if (sourceClip == null)
                return 0;

            int changed = 0;
            var states = _registeredStatesList;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null && state.RestoreAnimationClipOverride(sourceClip))
                {
                    changed++;
                }
            }
            return changed;
        }

        private int RestoreCachedStateAnimationClipOverrides()
        {
            int changed = 0;
            var states = _registeredStatesList;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null)
                {
                    changed += state.RestoreAllAnimationClipOverrides();
                }
            }
            return changed;
        }
    }
}
