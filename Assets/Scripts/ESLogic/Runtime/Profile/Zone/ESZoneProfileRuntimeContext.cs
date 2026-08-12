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

        private readonly List<ExtensionBinding> extensions = new List<ExtensionBinding>();
        private readonly Dictionary<UnityEngine.Object, MemberState> members =
            new Dictionary<UnityEngine.Object, MemberState>(InitialMemberCapacity);
        private readonly List<UnityEngine.Object> memberKeyBuffer =
            new List<UnityEngine.Object>(InitialMemberCapacity);

        public bool AwakeLifecycleCompleted { get; internal set; }
        public bool EnableLifecycleActive { get; internal set; }
        public bool PoolLifecycleActive { get; internal set; }
        public bool DestroyLifecycleCompleted { get; internal set; }
        public int ActiveMemberCount => members.Count;

        internal IReadOnlyList<ExtensionBinding> Extensions => extensions;
        internal Dictionary<UnityEngine.Object, MemberState> Members => members;

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
            memberKeyBuffer.Clear();
            foreach (UnityEngine.Object key in members.Keys)
                memberKeyBuffer.Add(key);
            return memberKeyBuffer;
        }

        internal void EnsureMemberCapacity(int capacity)
        {
            if (capacity <= 0)
                return;

            members.EnsureCapacity(capacity);
            if (memberKeyBuffer.Capacity < capacity)
                memberKeyBuffer.Capacity = capacity;
        }

        internal void SetExtensions(List<ExtensionBinding> bindings)
        {
            extensions.Clear();
            if (bindings != null)
                extensions.AddRange(bindings);
        }

        internal bool HasAnyState(ESZoneProfileExtensionLifecycleState state)
        {
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
            members.Clear();
            memberKeyBuffer.Clear();
        }
    }
}
