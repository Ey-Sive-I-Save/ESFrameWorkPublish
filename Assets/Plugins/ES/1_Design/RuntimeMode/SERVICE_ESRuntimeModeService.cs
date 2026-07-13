using System.Collections.Generic;

namespace ES
{
    public struct ESRuntimeModeHandle
    {
        public int id;

        public bool IsValid
        {
            get { return id > 0; }
        }
    }

    public struct ESRuntimeModeEntry
    {
        public ESRuntimeMode mode;
        public ESRuntimeModeHandle handle;
        public int priority;
        public int stackIndex;
        public object owner;
    }

    public struct ESRuntimeModeTagHandle
    {
        public int id;

        public bool IsValid
        {
            get { return id > 0; }
        }
    }

    public struct ESRuntimeModeTagEntry
    {
        public ESRuntimeModeTag tag;
        public ESRuntimeModeTagHandle handle;
        public int priority;
        public int stackIndex;
        public object owner;
    }

    public sealed class ESRuntimeModeService
    {
        public event System.Action<ESRuntimeModePolicy> OnPolicyChanged;

        private readonly List<ESRuntimeModeEntry> modeStack = new List<ESRuntimeModeEntry>(8);
        private readonly List<ESRuntimeModeTagEntry> tags = new List<ESRuntimeModeTagEntry>(8);
        private readonly List<ESPermitLawEntry> tempEntries = new List<ESPermitLawEntry>(16);

        private int nextHandleId = 1;
        private int nextStackIndex = 1;
        private ESRuntimeModePolicy currentPolicy;
        private ESRuntimeModePolicyTrace currentTrace;

        public ESRuntimeModeService()
        {
            currentPolicy = ESRuntimeModePolicy.Default;
        }

        public ESRuntimeModePolicy CurrentPolicy
        {
            get { return currentPolicy; }
        }

        public ESRuntimeModePolicyTrace CurrentTrace
        {
            get { return currentTrace; }
        }

        public int ModeCount
        {
            get { return modeStack.Count; }
        }

        public int TagCount
        {
            get { return tags.Count; }
        }

        public ESRuntimeMode CurrentMode
        {
            get
            {
                return modeStack.Count > 0
                    ? modeStack[modeStack.Count - 1].mode
                    : ESRuntimeMode.Gameplay;
            }
        }

        public ESRuntimeModeHandle PushMode(ESRuntimeMode mode, object owner = null, int? priorityOverride = null)
        {
            ESRuntimeModeHandle handle = NewModeHandle();
            modeStack.Add(new ESRuntimeModeEntry
            {
                mode = mode,
                handle = handle,
                priority = priorityOverride.HasValue ? priorityOverride.Value : ESRuntimeModeDefaults.GetModePriority(mode),
                stackIndex = nextStackIndex++,
                owner = owner
            });

            RebuildPolicy();
            return handle;
        }

        public bool PopMode(ESRuntimeModeHandle handle)
        {
            return RemoveMode(handle);
        }

        public bool RemoveMode(ESRuntimeModeHandle handle)
        {
            if (!handle.IsValid)
                return false;

            for (int i = modeStack.Count - 1; i >= 0; i--)
            {
                if (modeStack[i].handle.id != handle.id)
                    continue;

                modeStack.RemoveAt(i);
                RebuildPolicy();
                return true;
            }

            return false;
        }

        public bool RemoveModeWithAbove(ESRuntimeModeHandle handle)
        {
            if (!handle.IsValid)
                return false;

            for (int i = modeStack.Count - 1; i >= 0; i--)
            {
                if (modeStack[i].handle.id != handle.id)
                    continue;

                int removeCount = modeStack.Count - i;
                modeStack.RemoveRange(i, removeCount);
                RebuildPolicy();
                return true;
            }

            return false;
        }

        public bool PopTopMode()
        {
            if (modeStack.Count == 0)
                return false;

            modeStack.RemoveAt(modeStack.Count - 1);
            RebuildPolicy();
            return true;
        }

        public ESRuntimeModeTagHandle AddTag(ESRuntimeModeTag tag, object owner = null, int? priorityOverride = null)
        {
            ESRuntimeModeTagHandle handle = NewTagHandle();
            tags.Add(new ESRuntimeModeTagEntry
            {
                tag = tag,
                handle = handle,
                priority = priorityOverride.HasValue ? priorityOverride.Value : ESRuntimeModeDefaults.GetTagPriority(tag),
                stackIndex = nextStackIndex++,
                owner = owner
            });

            RebuildPolicy();
            return handle;
        }

        public bool RemoveTag(ESRuntimeModeTagHandle handle)
        {
            if (!handle.IsValid)
                return false;

            for (int i = tags.Count - 1; i >= 0; i--)
            {
                if (tags[i].handle.id != handle.id)
                    continue;

                tags.RemoveAt(i);
                RebuildPolicy();
                return true;
            }

            return false;
        }

        public bool ContainsMode(ESRuntimeMode mode)
        {
            for (int i = 0; i < modeStack.Count; i++)
            {
                if (modeStack[i].mode == mode)
                    return true;
            }

            return false;
        }

        public bool ContainsTag(ESRuntimeModeTag tag)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].tag == tag)
                    return true;
            }

            return false;
        }

        public ESRuntimeModeEntry GetModeEntryAt(int index)
        {
            return modeStack[index];
        }

        public ESRuntimeModeTagEntry GetTagEntryAt(int index)
        {
            return tags[index];
        }

        public void Clear()
        {
            modeStack.Clear();
            tags.Clear();
            RebuildPolicy();
        }

        private ESRuntimeModeHandle NewModeHandle()
        {
            return new ESRuntimeModeHandle { id = nextHandleId++ };
        }

        private ESRuntimeModeTagHandle NewTagHandle()
        {
            return new ESRuntimeModeTagHandle { id = nextHandleId++ };
        }

        private void RebuildPolicy()
        {
            ESRuntimeModePolicy fallback = ESRuntimeModePolicy.Default;
            ESRuntimeModePolicy next = fallback;
            ESRuntimeModePolicyTrace trace = new ESRuntimeModePolicyTrace();

            next.allowPlayerInput = Resolve(ESRuntimeModePolicyField.PlayerInput, fallback.allowPlayerInput, out trace.playerInput);
            next.allowMoveInput = Resolve(ESRuntimeModePolicyField.MoveInput, fallback.allowMoveInput, out trace.moveInput);
            next.allowCameraLook = Resolve(ESRuntimeModePolicyField.CameraLook, fallback.allowCameraLook, out trace.cameraLook);
            next.allowCombatInput = Resolve(ESRuntimeModePolicyField.CombatInput, fallback.allowCombatInput, out trace.combatInput);
            next.allowInteractionInput = Resolve(ESRuntimeModePolicyField.InteractionInput, fallback.allowInteractionInput, out trace.interactionInput);
            next.allowUIInput = Resolve(ESRuntimeModePolicyField.UIInput, fallback.allowUIInput, out trace.uiInput);
            next.showCursor = Resolve(ESRuntimeModePolicyField.CursorVisible, fallback.showCursor, out trace.cursorVisible);
            next.lockCursor = Resolve(ESRuntimeModePolicyField.CursorLocked, fallback.lockCursor, out trace.cursorLocked);
            next.pauseWorld = Resolve(ESRuntimeModePolicyField.WorldPause, fallback.pauseWorld, out trace.worldPause);
            next.showGameplayHud = Resolve(ESRuntimeModePolicyField.GameplayHud, fallback.showGameplayHud, out trace.gameplayHud);

            currentTrace = trace;

            if (PolicyEquals(currentPolicy, next))
            {
                currentPolicy = next;
                return;
            }

            currentPolicy = next;
            System.Action<ESRuntimeModePolicy> callback = OnPolicyChanged;
            if (callback != null)
                callback(currentPolicy);
        }

        private bool Resolve(ESRuntimeModePolicyField field, bool fallback, out ESPermitLawResult result)
        {
            tempEntries.Clear();

            for (int i = 0; i < modeStack.Count; i++)
            {
                ESRuntimeModeEntry entry = modeStack[i];
                ESRuntimeModePolicyPatch patch = ESRuntimeModeDefaults.GetModePatch(entry.mode);
                tempEntries.Add(new ESPermitLawEntry(GetLaw(patch, field), entry.priority, entry.stackIndex));
            }

            for (int i = 0; i < tags.Count; i++)
            {
                ESRuntimeModeTagEntry entry = tags[i];
                ESRuntimeModePolicyPatch patch = ESRuntimeModeDefaults.GetTagPatch(entry.tag);
                tempEntries.Add(new ESPermitLawEntry(GetLaw(patch, field), entry.priority, entry.stackIndex));
            }

            return ESPermitLawResolver.Resolve(tempEntries, fallback, out result)
                ? result.value
                : fallback;
        }

        private static ESPermitLaw GetLaw(ESRuntimeModePolicyPatch patch, ESRuntimeModePolicyField field)
        {
            switch (field)
            {
                case ESRuntimeModePolicyField.PlayerInput:
                    return patch.playerInput;
                case ESRuntimeModePolicyField.MoveInput:
                    return patch.moveInput;
                case ESRuntimeModePolicyField.CameraLook:
                    return patch.cameraLook;
                case ESRuntimeModePolicyField.CombatInput:
                    return patch.combatInput;
                case ESRuntimeModePolicyField.InteractionInput:
                    return patch.interactionInput;
                case ESRuntimeModePolicyField.UIInput:
                    return patch.uiInput;
                case ESRuntimeModePolicyField.CursorVisible:
                    return patch.cursorVisible;
                case ESRuntimeModePolicyField.CursorLocked:
                    return patch.cursorLocked;
                case ESRuntimeModePolicyField.WorldPause:
                    return patch.worldPause;
                case ESRuntimeModePolicyField.GameplayHud:
                    return patch.gameplayHud;
                default:
                    return ESPermitLaw.Ignore;
            }
        }

        private static bool PolicyEquals(ESRuntimeModePolicy a, ESRuntimeModePolicy b)
        {
            return a.allowPlayerInput == b.allowPlayerInput
                   && a.allowMoveInput == b.allowMoveInput
                   && a.allowCameraLook == b.allowCameraLook
                   && a.allowCombatInput == b.allowCombatInput
                   && a.allowInteractionInput == b.allowInteractionInput
                   && a.allowUIInput == b.allowUIInput
                   && a.showCursor == b.showCursor
                   && a.lockCursor == b.lockCursor
                   && a.pauseWorld == b.pauseWorld
                   && a.showGameplayHud == b.showGameplayHud;
        }
    }
}
