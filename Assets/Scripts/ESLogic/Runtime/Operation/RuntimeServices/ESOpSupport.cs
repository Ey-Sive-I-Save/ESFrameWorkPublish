using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESOpSupportKind
    {
        Unknown,
        Entity,
        Item,
        Skill,
        Buff,
        Custom
    }

    /// <summary>
    /// High-weight Op runtime support.
    /// It owns the runtime resources of one Op scope and points to its related owner.
    /// Long-lived examples: Entity / Item. Short-lived examples: Skill / Buff instance.
    /// Temporary tokens, events, rented target packs and cleanup callbacks belong to scopeSupport,
    /// not to the long-lived hostSupport.
    /// </summary>
    [Serializable, TypeRegistryItem("ES Op运行支撑")]
    public class ESOpSupport : IDisposable, IPoolableAuto,
        IOpStoreDictionary<IOperation, DeleAndCount, OutputOperationDelegateFlag>
    {
        public static readonly ESSimplePool<ESOpSupport> Pool = new ESSimplePool<ESOpSupport>(
            factoryMethod: () => new ESOpSupport(),
            onCreate: support => support.MarkPoolOwned(),
            initCount: 16,
            maxCount: 1024,
            poolDisplayName: "ESOpSupport Pool"
        );

        [NonSerialized] private List<ESOpSupport> children;
        [NonSerialized] private List<OwnedTargetPack> targetPacks;
        [NonSerialized] private List<Action> cleanupCallbacks;
        [NonSerialized] private Dictionary<ESOutputOp, List<ESAudioVoiceHandle>> audioVoiceHandles;
        [NonSerialized] private Dictionary<ESOutputOp, List<ESVfxHandle>> vfxHandles;

        [NonSerialized, HideInInspector]
        public ContextPool contextPool;

        [NonSerialized, HideInInspector]
        public CacherPool cacherPool;

        [NonSerialized, HideInInspector]
        public SafeDictionary<IOperation, DeleAndCount> storeForDelegate = new SafeDictionary<IOperation, DeleAndCount>();

        [ShowInInspector, ReadOnly, LabelText("支持类型")]
        public ESOpSupportKind Kind { get; private set; } = ESOpSupportKind.Unknown;

        [ShowInInspector, ReadOnly, LabelText("Owner ID")]
        public int OwnerId { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("版本")]
        public int Version { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("池化回收")]
        public bool IsRecycled { get; set; }

        [ShowInInspector, ReadOnly, LabelText("池创建")]
        public bool IsPoolOwned { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("已释放")]
        public bool IsDisposed { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("泛型Owner")]
        public object OwnerObject { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Entity Owner")]
        public Entity OwnerEntity { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Skill Owner")]
        public EntityState_Skill OwnerSkillState { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Item Owner")]
        public Item OwnerItem { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Buff Domain Owner")]
        public EntityBuffDomain OwnerBuffDomain { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Buff Module Owner")]
        public EntityBuffModuleBase OwnerBuffModule { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Buff Runtime Owner")]
        public ESActiveBuffRuntime OwnerBuffRuntime { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("父级Support")]
        public ESOpSupport Parent { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Owner说明")]
        public string OwnerSummary => BuildOwnerSummary();

        [ShowInInspector, ReadOnly, LabelText("运行资源")]
        public string RuntimeSummary =>
            $"Children:{(children != null ? children.Count : 0)} TargetPacks:{(targetPacks != null ? targetPacks.Count : 0)} Cleanup:{(cleanupCallbacks != null ? cleanupCallbacks.Count : 0)}";

        public ContextPool Context => contextPool;

        public CacherPool Cacher => cacherPool;

        public EntityState_Skill CurrentSkillState { get; private set; }

        public Entity CurrentEntity => CurrentSkillState != null ? CurrentSkillState.HostEntity : OwnerEntity;

        public static ESOpSupport CreateStandalone()
        {
            ESOpSupport support = new ESOpSupport();
            support.IsPoolOwned = false;
            support.IsRecycled = false;
            support.IsDisposed = false;
            support.EnsureRuntimeServices();
            return support;
        }

        public static ESOpSupport Rent()
        {
            ESOpSupport support = Pool.GetInPool();
            support.MarkPoolOwned();
            support.IsDisposed = false;
            support.EnsureRuntimeServices();
            return support;
        }

        private void MarkPoolOwned()
        {
            IsPoolOwned = true;
        }

        public void EnsureRuntimeServices()
        {
            contextPool ??= new ContextPool();
            cacherPool ??= new CacherPool();
            storeForDelegate ??= new SafeDictionary<IOperation, DeleAndCount>();
        }

        public SafeDictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOperationDelegateFlag flag = OutputOperationDelegateFlag.Default)
        {
            return storeForDelegate;
        }

        public void SetCurrentSkillState(EntityState_Skill state)
        {
            CurrentSkillState = state;
        }

        public ESOpSupport Configure(
            ESOpSupportKind kind,
            object ownerObject = null,
            Entity ownerEntity = null,
            int ownerId = 0,
            ESOpSupport parent = null)
        {
            EnsureRuntimeServices();
            ClearActivationRuntime();

            Kind = kind;
            OwnerObject = ownerObject;
            OwnerEntity = ownerEntity;
            OwnerSkillState = null;
            OwnerItem = null;
            OwnerBuffDomain = null;
            OwnerBuffModule = null;
            OwnerBuffRuntime = null;
            OwnerId = ownerId;
            Parent = parent;
            IsDisposed = false;
            Version++;
            return this;
        }

        public ESOpSupport InitializeEntityOwner(Entity entity, int ownerId = 0)
        {
            return BindEntity(entity, ownerId, null);
        }

        public ESOpSupport InitializeSkillOwner(EntityState_Skill skillState, ESOpSupport hostSupport = null, int ownerId = 0)
        {
            return BindSkill(skillState, ownerId, hostSupport);
        }

        public ESOpSupport InitializeItemOwner(Item item, int ownerId = 0)
        {
            return BindItem(item, ownerId, null);
        }

        public ESOpSupport InitializeBuffOwner(EntityBuffDomain buffDomain, EntityBuffModuleBase buffModule = null, ESOpSupport hostSupport = null, int ownerId = 0, ESActiveBuffRuntime buffRuntime = null)
        {
            return BindBuff(buffDomain, buffModule, ownerId, hostSupport, buffRuntime);
        }

        public ESOpSupport BindEntity(Entity entity, int ownerId = 0, ESOpSupport parent = null)
        {
            return Configure(ESOpSupportKind.Entity, entity, entity, ownerId, parent);
        }

        public ESOpSupport BindSkill(EntityState_Skill skillState, int ownerId = 0, ESOpSupport parent = null)
        {
            Configure(ESOpSupportKind.Skill, skillState, skillState != null ? skillState.HostEntity : null, ownerId, parent);
            OwnerSkillState = skillState;
            return this;
        }

        public ESOpSupport BindItem(Item item, int ownerId = 0, ESOpSupport parent = null)
        {
            Configure(ESOpSupportKind.Item, item, null, ownerId, parent);
            OwnerItem = item;
            return this;
        }

        public ESOpSupport BindBuff(EntityBuffDomain buffDomain, EntityBuffModuleBase buffModule = null, int ownerId = 0, ESOpSupport parent = null, ESActiveBuffRuntime buffRuntime = null)
        {
            Entity entity = buffDomain != null ? buffDomain.MyCore : null;
            Configure(ESOpSupportKind.Buff, buffRuntime != null ? buffRuntime : buffModule != null ? buffModule : buffDomain, entity, ownerId, parent);
            OwnerBuffDomain = buffDomain;
            OwnerBuffModule = buffModule;
            OwnerBuffRuntime = buffRuntime;
            return this;
        }

        public ESOpSupport BindCustom(object ownerObject, Entity ownerEntity = null, int ownerId = 0, ESOpSupport parent = null)
        {
            return Configure(ESOpSupportKind.Custom, ownerObject, ownerEntity, ownerId, parent);
        }

        public ESOpSupport BindOwner(object ownerObject, int ownerId = 0, ESOpSupport parent = null)
        {
            if (ownerObject is Entity entity)
                return BindEntity(entity, ownerId, parent);

            if (ownerObject is EntityState_Skill skillState)
                return BindSkill(skillState, ownerId, parent);

            if (ownerObject is Item item)
                return BindItem(item, ownerId, parent);

            if (ownerObject is EntityBuffModuleBase buffModule)
                return BindBuff(buffModule.MyDomain, buffModule, ownerId, parent);

            if (ownerObject is EntityBuffDomain buffDomain)
                return BindBuff(buffDomain, null, ownerId, parent);

            return BindCustom(ownerObject, null, ownerId, parent);
        }

        public T GetOwner<T>() where T : class
        {
            if (OwnerObject is T typed)
                return typed;
            if (OwnerEntity is T entity)
                return entity;
            if (OwnerSkillState is T skillState)
                return skillState;
            if (OwnerItem is T item)
                return item;
            if (OwnerBuffModule is T buffModule)
                return buffModule;
            if (OwnerBuffDomain is T buffDomain)
                return buffDomain;
            return null;
        }

        public bool TryGetOwner<T>(out T owner) where T : class
        {
            owner = GetOwner<T>();
            return owner != null;
        }

        public ESOpSupport CreateChild(ESOpSupportKind kind, object ownerObject = null, Entity ownerEntity = null, int ownerId = 0)
        {
            ESOpSupport child = Pool.GetInPool();
            child.Configure(kind, ownerObject, ownerEntity, ownerId, this);

            children ??= new List<ESOpSupport>(2);
            children.Add(child);
            return child;
        }

        public ESRuntimeTargetPack RentTargetPack()
        {
            ESRuntimeTargetPack target = ESRuntimeTargetPack.Pool.GetInPool();
            targetPacks ??= new List<OwnedTargetPack>(4);
            targetPacks.Add(new OwnedTargetPack(target));
            return target;
        }

        public void AddCleanup(Action cleanup)
        {
            if (cleanup == null)
                return;

            cleanupCallbacks ??= new List<Action>(4);
            cleanupCallbacks.Add(cleanup);
        }

        /// <summary>
        /// Stores the Voice owned by one concrete Op execution. The entry is scoped to this
        /// support and is stopped automatically when the support is cleared or recycled.
        /// </summary>
        public void SetAudioVoiceHandle(ESOutputOp operation, ESAudioVoiceHandle handle)
        {
            if (operation == null)
                return;

            if (!handle.IsValid)
            {
                audioVoiceHandles?.Remove(operation);
                return;
            }

            audioVoiceHandles ??= new Dictionary<ESOutputOp, List<ESAudioVoiceHandle>>(2);
            if (!audioVoiceHandles.TryGetValue(operation, out List<ESAudioVoiceHandle> handles))
            {
                handles = new List<ESAudioVoiceHandle>(2);
                audioVoiceHandles.Add(operation, handles);
            }
            handles.Clear();
            handles.Add(handle);
        }

        /// <summary>Adds a Voice without overwriting another execution of the same Op.</summary>
        public void AddAudioVoiceHandle(ESOutputOp operation, ESAudioVoiceHandle handle)
        {
            if (operation == null || !handle.IsValid)
                return;

            audioVoiceHandles ??= new Dictionary<ESOutputOp, List<ESAudioVoiceHandle>>(2);
            if (!audioVoiceHandles.TryGetValue(operation, out List<ESAudioVoiceHandle> handles))
            {
                handles = new List<ESAudioVoiceHandle>(2);
                audioVoiceHandles.Add(operation, handles);
            }
            handles.Add(handle);
        }

        /// <summary>Retrieves and removes the Voice owned by one concrete Op execution.</summary>
        public bool TryTakeAudioVoiceHandle(ESOutputOp operation, out ESAudioVoiceHandle handle)
        {
            handle = default;
            if (operation == null || audioVoiceHandles == null
                || !audioVoiceHandles.TryGetValue(operation, out List<ESAudioVoiceHandle> handles)
                || handles.Count == 0)
                return false;

            int last = handles.Count - 1;
            handle = handles[last];
            handles.RemoveAt(last);
            if (handles.Count == 0)
                audioVoiceHandles.Remove(operation);
            return true;
        }

        /// <summary>Stops and removes every Voice owned by one Op execution group.</summary>
        public int StopAudioVoices(ESOutputOp operation)
        {
            if (operation == null || audioVoiceHandles == null
                || !audioVoiceHandles.TryGetValue(operation, out List<ESAudioVoiceHandle> handles))
                return 0;

            int stopped = 0;
            ESAudioModule audio = ESGameManager.Audio;
            if (audio != null)
            {
                for (int i = 0; i < handles.Count; i++)
                    if (audio.Stop(handles[i]))
                        stopped++;
            }
            audioVoiceHandles.Remove(operation);
            return stopped;
        }

        /// <summary>Stores the VFX handle owned by one concrete Op execution.</summary>
        public void SetVfxHandle(ESOutputOp operation, ESVfxHandle handle)
        {
            if (operation == null)
                return;
            if (!handle.IsValid)
            {
                vfxHandles?.Remove(operation);
                return;
            }
            vfxHandles ??= new Dictionary<ESOutputOp, List<ESVfxHandle>>(2);
            if (!vfxHandles.TryGetValue(operation, out List<ESVfxHandle> handles))
            {
                handles = new List<ESVfxHandle>(2);
                vfxHandles.Add(operation, handles);
            }
            handles.Clear();
            handles.Add(handle);
        }

        /// <summary>Adds a VFX handle without overwriting another execution in the same support scope.</summary>
        public void AddVfxHandle(ESOutputOp operation, ESVfxHandle handle)
        {
            if (operation == null || !handle.IsValid)
                return;

            vfxHandles ??= new Dictionary<ESOutputOp, List<ESVfxHandle>>(2);
            if (!vfxHandles.TryGetValue(operation, out List<ESVfxHandle> handles))
            {
                handles = new List<ESVfxHandle>(2);
                vfxHandles.Add(operation, handles);
            }
            handles.Add(handle);
        }

        /// <summary>Retrieves and removes the VFX handle owned by one Op execution.</summary>
        public bool TryTakeVfxHandle(ESOutputOp operation, out ESVfxHandle handle)
        {
            handle = default;
            if (operation == null || vfxHandles == null
                || !vfxHandles.TryGetValue(operation, out List<ESVfxHandle> handles)
                || handles.Count == 0)
                return false;
            int last = handles.Count - 1;
            handle = handles[last];
            handles.RemoveAt(last);
            if (handles.Count == 0)
                vfxHandles.Remove(operation);
            return true;
        }

        /// <summary>Stops and removes every VFX owned by one Op execution group.</summary>
        public int StopVfxHandles(ESOutputOp operation)
        {
            if (operation == null || vfxHandles == null
                || !vfxHandles.TryGetValue(operation, out List<ESVfxHandle> handles))
                return 0;

            int stopped = 0;
            ESVfxModule vfx = ESGameManager.Vfx;
            if (vfx != null)
            {
                for (int i = 0; i < handles.Count; i++)
                    if (vfx.Stop(handles[i]))
                        stopped++;
            }
            vfxHandles.Remove(operation);
            return stopped;
        }

        public void ClearRuntime()
        {
            ClearActivationRuntime();
        }

        public void ClearOwnerRuntime()
        {
            ClearActivationRuntime();
            contextPool?.ClearAllRuntimeValues();
        }

        public void ClearActivationRuntime()
        {
            DisposeChildren();
            ReleaseAudioVoices();
            ReleaseVfxHandles();
            RunCleanupCallbacks();
            ReleaseTargetPacks();
            contextPool?.ClearRuntimeValues();
            cacherPool?.Clear();
            storeForDelegate?.Clear();
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            ClearRuntime();
            contextPool?.ClearAllRuntimeValues();
            ClearOwner();
            SetCurrentSkillState(null);
            IsDisposed = true;
            Version++;
        }

        public void TryAutoPushedToPool()
        {
            if (IsRecycled)
                return;

            if (!IsPoolOwned)
            {
                Dispose();
                return;
            }

            Dispose();
            Pool.PushToPool(this);
        }

        public void OnResetAsPoolable()
        {
            ClearRuntime();
            contextPool?.ClearAllRuntimeValues();
            ClearOwner();
            SetCurrentSkillState(null);
            IsDisposed = true;
            Version++;
        }

        private void ClearOwner()
        {
            Kind = ESOpSupportKind.Unknown;
            OwnerId = 0;
            OwnerObject = null;
            OwnerEntity = null;
            OwnerSkillState = null;
            OwnerItem = null;
            OwnerBuffDomain = null;
            OwnerBuffModule = null;
            OwnerBuffRuntime = null;
            Parent = null;
        }

        private void DisposeChildren()
        {
            if (children == null)
                return;

            for (int i = children.Count - 1; i >= 0; i--)
                children[i]?.TryAutoPushedToPool();

            children.Clear();
        }

        private void RunCleanupCallbacks()
        {
            if (cleanupCallbacks == null)
                return;

            for (int i = cleanupCallbacks.Count - 1; i >= 0; i--)
            {
                try
                {
                    cleanupCallbacks[i]?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            cleanupCallbacks.Clear();
        }

        private void ReleaseAudioVoices()
        {
            if (audioVoiceHandles == null || audioVoiceHandles.Count == 0)
                return;

            ESAudioModule audio = ESGameManager.Audio;
            if (audio != null)
            {
                foreach (List<ESAudioVoiceHandle> handles in audioVoiceHandles.Values)
                    for (int i = 0; i < handles.Count; i++)
                        audio.Stop(handles[i]);
            }

            audioVoiceHandles.Clear();
        }

        private void ReleaseVfxHandles()
        {
            if (vfxHandles == null || vfxHandles.Count == 0)
                return;

            ESVfxModule vfx = ESGameManager.Vfx;
            if (vfx != null)
            {
                foreach (List<ESVfxHandle> handles in vfxHandles.Values)
                    for (int i = 0; i < handles.Count; i++)
                        vfx.Stop(handles[i]);
            }

            vfxHandles.Clear();
        }

        private void ReleaseTargetPacks()
        {
            if (targetPacks == null)
                return;

            for (int i = targetPacks.Count - 1; i >= 0; i--)
            {
                OwnedTargetPack owned = targetPacks[i];
                ESRuntimeTargetPack.TryReturnOwned(owned.Target, owned.Version);
            }

            targetPacks.Clear();
        }

        private readonly struct OwnedTargetPack
        {
            public readonly ESRuntimeTargetPack Target;
            public readonly long Version;

            public OwnedTargetPack(ESRuntimeTargetPack target)
            {
                Target = target;
                Version = target != null ? target.Version : 0L;
            }
        }

        private string BuildOwnerSummary()
        {
            switch (Kind)
            {
                case ESOpSupportKind.Entity:
                    return OwnerEntity != null ? $"Entity: {OwnerEntity.name}" : "Entity: null";
                case ESOpSupportKind.Skill:
                    return OwnerSkillState != null ? $"SkillState: {OwnerSkillState.GetType().Name}, Host: {(OwnerEntity != null ? OwnerEntity.name : "null")}" : "SkillState: null";
                case ESOpSupportKind.Item:
                    return OwnerItem != null ? $"Item: {OwnerItem.name}" : "Item: null";
                case ESOpSupportKind.Buff:
                    return $"Buff: {(OwnerBuffModule != null ? OwnerBuffModule.GetType().Name : OwnerBuffDomain != null ? OwnerBuffDomain.GetType().Name : "null")}, Host: {(OwnerEntity != null ? OwnerEntity.name : "null")}";
                case ESOpSupportKind.Custom:
                    return OwnerObject != null ? $"Custom: {OwnerObject.GetType().Name}" : "Custom: null";
                default:
                    return "Unknown";
            }
        }
    }

}
