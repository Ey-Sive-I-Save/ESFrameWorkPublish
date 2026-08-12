using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    [Flags]
    internal enum ESZoneProfileExtensionLifecycleState : byte
    {
        None = 0,
        Awake = 1 << 0,
        Enable = 1 << 1,
        Pool = 1 << 2,
        EverEntered = 1 << 3
    }

    public sealed class ESZoneProfileRuntimeContext : ESProfileRuntimeContextBase
    {
        public const int MaxExtensionCount = 64;
        private const int InitialMemberCapacity = 4;

        internal sealed class ExtensionBinding
        {
            public ESZoneProfileExtensionSettings Settings;
            public ESZoneProfileExtensionRuntime Runtime;
            public ESZoneProfileExtensionLifecycleState State;
        }

        internal struct MemberState
        {
            public ESZoneMember Member;
            public ulong EnteredExtensionMask;
        }

        private List<ExtensionBinding> extensions;
        private Dictionary<UnityEngine.Object, MemberState> members;
        private List<UnityEngine.Object> memberKeyBuffer;

        public bool AwakeLifecycleCompleted { get; internal set; }
        public bool EnableLifecycleActive { get; internal set; }
        public bool PoolLifecycleActive { get; internal set; }
        public bool DestroyLifecycleCompleted { get; internal set; }
        public int ActiveMemberCount => members?.Count ?? 0;

        internal IReadOnlyList<ExtensionBinding> Extensions =>
            extensions ?? (IReadOnlyList<ExtensionBinding>)Array.Empty<ExtensionBinding>();

        internal bool ContainsMember(UnityEngine.Object key)
        {
            return members != null && members.ContainsKey(key);
        }

        internal bool TryGetMember(UnityEngine.Object key, out MemberState state)
        {
            if (members != null)
                return members.TryGetValue(key, out state);
            state = default;
            return false;
        }

        internal void SetMember(UnityEngine.Object key, MemberState state)
        {
            members ??= new Dictionary<UnityEngine.Object, MemberState>(InitialMemberCapacity);
            members[key] = state;
        }

        internal bool RemoveMember(UnityEngine.Object key)
        {
            return members != null && members.Remove(key);
        }

        internal static MemberState CreateMemberState(ESZoneMember member, ulong enteredMask)
        {
            return new MemberState
            {
                Member = member,
                EnteredExtensionMask = enteredMask
            };
        }

        internal List<UnityEngine.Object> PrepareMemberKeyBuffer()
        {
            memberKeyBuffer ??= new List<UnityEngine.Object>(Mathf.Max(InitialMemberCapacity, ActiveMemberCount));
            memberKeyBuffer.Clear();
            if (members == null)
                return memberKeyBuffer;
            foreach (UnityEngine.Object key in members.Keys)
                memberKeyBuffer.Add(key);
            return memberKeyBuffer;
        }

        internal void EnsureMemberCapacity(int capacity)
        {
            if (capacity <= 0)
                return;

            members ??= new Dictionary<UnityEngine.Object, MemberState>(capacity);
            members.EnsureCapacity(capacity);
            memberKeyBuffer ??= new List<UnityEngine.Object>(capacity);
            if (memberKeyBuffer.Capacity < capacity)
                memberKeyBuffer.Capacity = capacity;
        }

        internal void SetExtensions(List<ExtensionBinding> bindings)
        {
            extensions = bindings;
        }

        internal bool HasAnyState(ESZoneProfileExtensionLifecycleState state)
        {
            if (extensions == null)
                return false;
            for (int i = 0; i < extensions.Count; i++)
            {
                if ((extensions[i].State & state) != 0)
                    return true;
            }

            return false;
        }

        internal void BeginPoolSpawn(int generation)
        {
            BeginPoolGeneration(generation);
        }

        internal void ClearPoolGeneration()
        {
            EndPoolGeneration();
        }

        protected override void ClearTransientState()
        {
            members?.Clear();
            memberKeyBuffer?.Clear();
        }
    }
}
