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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetEntity(Entity entity)
        {
            userEntity = entity;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESRuntimeTargetPack SetUser(Entity entity)
        {
            userEntity = entity;
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
        public ESRuntimeTargetPack SetMainTarget(Entity entity)
        {
            entityMainTarget = entity;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetUserEntity()
        {
            return userEntity;
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
        public Entity GetMainTarget()
        {
            return entityMainTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearTargets()
        {
            targetEntities.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTarget(Entity entity)
        {
            if (entity != null)
                targetEntities.Add(entity);
        }

        public ESRuntimeTargetPack CopyFrom(ESRuntimeTargetPack source, bool copyTargets = true, bool copyExtras = false)
        {
            if (source == null)
                return this;

            userEntity = source.userEntity;
            entityMainTarget = source.entityMainTarget;

            if (copyTargets)
            {
                targetEntities.Clear();
                targetEntities.AddRange(source.targetEntities);
            }

            if (copyExtras)
            {
                ResetAllExtras();
                for (int i = 0; i < source.extraCount; i++)
                    AddExtra(source.extras[i]);
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
            return userEntity != null ? userEntity.gameObject : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Transform GetTransform()
        {
            return userEntity != null ? userEntity.transform : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Animator GetAnimator()
        {
            return userEntity != null ? userEntity.animator : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddExtra(object extra)
        {
            if (extra == null)
                return;

            EnsureExtraCapacity(extraCount + 1);
            extras[extraCount++] = extra;
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
            targetEntities.Clear();
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
