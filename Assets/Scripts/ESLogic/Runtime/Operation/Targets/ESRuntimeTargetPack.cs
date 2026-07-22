using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Operation 运行目标包。
    /// 这里不叫 Context：项目内已经有 ContextPool/ContextOperation，本类只保存一次 Skill/Track/Clip 执行时的实体目标信息。
    /// 临时键值数据、缓存和运行期存储由 ESOpSupport 提供。
    /// </summary>
    public sealed class ESRuntimeTargetPack : IPoolableAuto
    {
        private const int InitialExtraCapacity = 4;
        private const int MaxPoolCount = 1000;
        private const int InitPoolCount = 20;

        public static readonly ESSimplePool<ESRuntimeTargetPack> Pool = new ESSimplePool<ESRuntimeTargetPack>(
            factoryMethod: () => new ESRuntimeTargetPack(),
            resetMethod: null,
            initCount: InitPoolCount,
            maxCount: MaxPoolCount,
            poolDisplayName: "ESRuntimeTargetPack Pool"
        );

        private object[] extras;
        private int extraCount;
        private bool extrasEnabled;
        private object recycleToken;
        private bool recycleRequested;

        public bool IsRecycled { get; set; }

        public int Version { get; private set; }

        /// <summary>使用者/发起者，一般是释放技能或触发效果的 Entity。</summary>
        public Entity userEntity;

        /// <summary>主目标/被施加目标。按当前命名约定，Entity 字段使用 entity 前缀。</summary>
        public Entity entityMainTarget;

        /// <summary>多目标列表。保留 targetEntities 命名，因为它表达的是目标 Entity 列表，不是上下文容器。</summary>
        public readonly List<Entity> targetEntities = new List<Entity>(8);

        public Item userItem;

        public Item itemMainTarget;

        public readonly List<Item> targetItems = new List<Item>(8);

        /// <summary>运行时轻量数值槽位。高频 Op 优先读取原生值，必要时再由表达式写入。</summary>
        public float runtimeFloat = 1f;

        /// <summary>运行时轻量布尔槽位。</summary>
        public bool runtimeBool = true;

        [Obsolete("Use entityMainTarget instead.")]
        public Entity applierEntity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return entityMainTarget; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { entityMainTarget = value; }
        }

        public int ExtraCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return extraCount; }
        }

        public bool IsRecycleRequested
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return recycleRequested; }
        }

        public object RecycleToken
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return recycleToken; }
        }

        public bool ExtrasEnabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return extrasEnabled; }
        }

        public ESRuntimeTargetPack EnsureListCapacity(int entityTargetCapacity = 8, int itemTargetCapacity = 8)
        {
            if (targetEntities.Capacity < entityTargetCapacity)
                targetEntities.Capacity = entityTargetCapacity;

            if (targetItems.Capacity < itemTargetCapacity)
                targetItems.Capacity = itemTargetCapacity;

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetEntity(Entity entity)
        {
            userEntity = entity;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetItem(Item item)
        {
            userItem = item;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetUser(Entity entity)
        {
            userEntity = entity;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetUser(Item item)
        {
            userItem = item;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetApplier(Entity entity)
        {
            entityMainTarget = entity;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetEntityMainTarget(Entity entity)
        {
            entityMainTarget = entity;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetItemMainTarget(Item item)
        {
            itemMainTarget = item;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetMainTarget(Entity entity)
        {
            entityMainTarget = entity;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetMainTarget(Item item)
        {
            itemMainTarget = item;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetUserEntity()
        {
            return userEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Item GetUserItem()
        {
            return userItem;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetApplierEntity()
        {
            return entityMainTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetEntityMainTarget()
        {
            return entityMainTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Item GetItemMainTarget()
        {
            return itemMainTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetMainTarget()
        {
            return entityMainTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Item GetMainItemTarget()
        {
            return itemMainTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearTargets()
        {
            targetEntities.Clear();
            targetItems.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearEntityTargets()
        {
            targetEntities.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearItemTargets()
        {
            targetItems.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTarget(Entity entity)
        {
            TryAddTarget(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTarget(Item item)
        {
            TryAddTarget(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddTarget(Entity entity)
        {
            if (entity == null || targetEntities.Count >= targetEntities.Capacity)
                return false;

            targetEntities.Add(entity);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddTarget(Item item)
        {
            if (item == null || targetItems.Count >= targetItems.Capacity)
                return false;

            targetItems.Add(item);
            return true;
        }

        public ESRuntimeTargetPack CopyFrom(ESRuntimeTargetPack source, bool copyTargets = true, bool copyExtras = false)
        {
            if (source == null)
                return this;

            userEntity = source.userEntity;
            entityMainTarget = source.entityMainTarget;
            userItem = source.userItem;
            itemMainTarget = source.itemMainTarget;

            if (copyTargets)
            {
                targetEntities.Clear();
                targetItems.Clear();

                int entityCount = source.targetEntities.Count;
                int maxEntityCount = targetEntities.Capacity;
                for (int i = 0; i < entityCount && i < maxEntityCount; i++)
                    targetEntities.Add(source.targetEntities[i]);

                int itemCount = source.targetItems.Count;
                int maxItemCount = targetItems.Capacity;
                for (int i = 0; i < itemCount && i < maxItemCount; i++)
                    targetItems.Add(source.targetItems[i]);
            }

            if (copyExtras && source.extrasEnabled && source.extraCount > 0)
            {
                EnableExtras(source.extras != null ? source.extras.Length : InitialExtraCapacity);
                ResetAllExtras();
                for (int i = 0; i < source.extraCount; i++)
                    AddExtra(source.extras[i]);
            }
            else if (!copyExtras)
            {
                ResetAllExtras();
            }

            return this;
        }

        public ESRuntimeTargetPack CopyTo(ESRuntimeTargetPack target, bool copyTargets = true, bool copyExtras = false)
        {
            if (target != null)
                target.CopyFrom(this, copyTargets, copyExtras);

            return target;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameObject GetGameObject()
        {
            if (userEntity != null)
                return userEntity.gameObject;

            return userItem != null ? userItem.gameObject : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Transform GetTransform()
        {
            if (userEntity != null)
                return userEntity.transform;

            return userItem != null ? userItem.transform : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Animator GetAnimator()
        {
            return userEntity != null ? userEntity.animator : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameObject GetMainTargetGameObject()
        {
            if (entityMainTarget != null)
                return entityMainTarget.gameObject;

            return itemMainTarget != null ? itemMainTarget.gameObject : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Transform GetMainTargetTransform()
        {
            if (entityMainTarget != null)
                return entityMainTarget.transform;

            return itemMainTarget != null ? itemMainTarget.transform : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddExtra(object extra)
        {
            TryAddExtra(extra);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddExtra(object extra)
        {
            if (extra == null || !extrasEnabled)
                return false;

            EnsureExtraCapacity(extraCount + 1);
            extras[extraCount++] = extra;
            return true;
        }

        public ESRuntimeTargetPack EnableExtras(int capacity = InitialExtraCapacity)
        {
            extrasEnabled = true;
            EnsureExtraCapacity(Mathf.Max(InitialExtraCapacity, capacity));
            return this;
        }

        public ESRuntimeTargetPack DisableExtras()
        {
            ResetAllExtras();
            extrasEnabled = false;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveExtra(object extra)
        {
            if (extra == null || extras == null)
                return false;

            for (int i = 0; i < extraCount; i++)
            {
                if (!ReferenceEquals(extras[i], extra))
                    continue;

                int lastIndex = extraCount - 1;
                extras[i] = extras[lastIndex];
                extras[lastIndex] = null;
                extraCount = lastIndex;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetExtra<T>() where T : class
        {
            if (extras == null)
                return null;

            for (int i = 0; i < extraCount; i++)
            {
                T typed = extras[i] as T;
                if (typed != null)
                    return typed;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetExtra<T>(out T extra) where T : class
        {
            extra = GetExtra<T>();
            return extra != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasExtra<T>() where T : class
        {
            return GetExtra<T>() != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetExtraAt(int index)
        {
            return extras != null && index >= 0 && index < extraCount ? extras[index] : null;
        }

        public void OnResetAsPoolable()
        {
            ResetAllFields();
            ResetAllExtras();
            extrasEnabled = false;
            recycleToken = null;
            recycleRequested = false;
            Version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAutoPushedToPool()
        {
            ForcePushToPool();
        }

        public void HoldRecycle(object newRecycleToken)
        {
            if (IsRecycled)
                return;

            recycleRequested = true;
            recycleToken = newRecycleToken;
        }

        public bool CompleteRecycle(object requestToken)
        {
            if (IsRecycled)
                return false;

            if (!recycleRequested || !ReferenceEquals(requestToken, recycleToken))
                return false;

            return PushToPoolDirectly();
        }

        public bool ForcePushToPool()
        {
            if (IsRecycled)
                return false;

            recycleRequested = true;
            return PushToPoolDirectly();
        }

        private bool PushToPoolDirectly()
        {
            return Pool.PushToPool(this);
        }

        private void ResetAllFields()
        {
            userEntity = null;
            entityMainTarget = null;
            userItem = null;
            itemMainTarget = null;
            targetEntities.Clear();
            targetItems.Clear();
            runtimeFloat = 1f;
            runtimeBool = true;
        }

        private void ResetAllExtras()
        {
            if (extras == null)
            {
                extraCount = 0;
                return;
            }

            for (int i = 0; i < extraCount; i++)
            {
                extras[i] = null;
            }

            extraCount = 0;
        }

        private void EnsureExtraCapacity(int requiredCapacity)
        {
            if (extras == null)
            {
                extras = new object[Math.Max(InitialExtraCapacity, requiredCapacity)];
                return;
            }

            if (requiredCapacity <= extras.Length)
                return;

            int newCapacity = extras.Length << 1;
            while (newCapacity < requiredCapacity)
                newCapacity <<= 1;

            Array.Resize(ref extras, newCapacity);
        }
    }
}
