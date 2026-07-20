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
        IOpStoreDictionary<IOperation, DeleAndCount, OutputOperationDelegateFlag>,
        IOpStoreKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTargetPack, ESOpSupport, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat, OutputOperationBufferFlag>
    {
        public static readonly ESSimplePool<ESOpSupport> Pool = new ESSimplePool<ESOpSupport>(
            factoryMethod: () => new ESOpSupport(),
            initCount: 16,
            maxCount: 1024,
            poolDisplayName: "ESOpSupport Pool"
        );

        [NonSerialized] private List<ESOpSupport> children;
        [NonSerialized] private List<ESRuntimeTargetPack> targetPacks;
        [NonSerialized] private List<Action> cleanupCallbacks;

        [NonSerialized, HideInInspector]
        public ContextPool contextPool;

        [NonSerialized, HideInInspector]
        public CacherPool cacherPool;

        [NonSerialized, HideInInspector]
        public SafeDictionary<IOperation, DeleAndCount> storeForDelegate = new SafeDictionary<IOperation, DeleAndCount>();

        [NonSerialized, HideInInspector]
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
            <ESRuntimeTargetPack, ESOpSupport, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> storeForBuffer = new();

        [ShowInInspector, ReadOnly, LabelText("支持类型")]
        public ESOpSupportKind Kind { get; private set; } = ESOpSupportKind.Unknown;

        [ShowInInspector, ReadOnly, LabelText("Owner ID")]
        public int OwnerId { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("版本")]
        public int Version { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("池化回收")]
        public bool IsRecycled { get; set; }

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

        public void EnsureRuntimeServices()
        {
            contextPool ??= new ContextPool();
            cacherPool ??= new CacherPool();
            storeForDelegate ??= new SafeDictionary<IOperation, DeleAndCount>();
            storeForBuffer ??= new SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
                <ESRuntimeTargetPack, ESOpSupport, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat>();
        }

        public SafeDictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOperationDelegateFlag flag = OutputOperationDelegateFlag.Default)
        {
            return storeForDelegate;
        }

        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTargetPack, ESOpSupport, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> GetFromOpStore(OutputOperationBufferFlag flag = OutputOperationBufferFlag.Default)
        {
            return storeForBuffer;
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

        public ESOpSupport InitializeBuffOwner(EntityBuffDomain buffDomain, EntityBuffModuleBase buffModule = null, ESOpSupport hostSupport = null, int ownerId = 0)
        {
            return BindBuff(buffDomain, buffModule, ownerId, hostSupport);
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

        public ESOpSupport BindBuff(EntityBuffDomain buffDomain, EntityBuffModuleBase buffModule = null, int ownerId = 0, ESOpSupport parent = null)
        {
            Entity entity = buffDomain != null ? buffDomain.MyCore : null;
            Configure(ESOpSupportKind.Buff, buffModule != null ? buffModule : buffDomain, entity, ownerId, parent);
            OwnerBuffDomain = buffDomain;
            OwnerBuffModule = buffModule;
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
            TrackTargetPack(target);
            return target;
        }

        public void TrackTargetPack(ESRuntimeTargetPack target)
        {
            if (target == null || target.IsRecycled)
                return;

            targetPacks ??= new List<ESRuntimeTargetPack>(4);
            targetPacks.Add(target);
        }

        public void AddCleanup(Action cleanup)
        {
            if (cleanup == null)
                return;

            cleanupCallbacks ??= new List<Action>(4);
            cleanupCallbacks.Add(cleanup);
        }

        public void ClearRuntime()
        {
            ClearActivationRuntime();
        }

        public void ClearActivationRuntime()
        {
            DisposeChildren();
            RunCleanupCallbacks();
            ReleaseTargetPacks();
            contextPool?.ClearRuntimeValues();
            cacherPool?.Clear();
            storeForDelegate?.Clear();
            storeForBuffer?.Clear();
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

        private void ReleaseTargetPacks()
        {
            if (targetPacks == null)
                return;

            for (int i = targetPacks.Count - 1; i >= 0; i--)
            {
                ESRuntimeTargetPack target = targetPacks[i];
                if (target != null && !target.IsRecycled)
                    target.ForcePushToPool();
            }

            targetPacks.Clear();
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
