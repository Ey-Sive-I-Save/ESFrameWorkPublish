using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Runtime target for Operation.
    /// Keep target data small; temporary data belongs to IOpSupporter.Cacher.
    /// Recycle is guarded by one high-frequency object token.
    /// </summary>
    public sealed class ESRuntimeTarget : IPoolableAuto
    {
        private const int InitialExtraCapacity = 4;
        private const int MaxPoolCount = 1000;
        private const int InitPoolCount = 20;

        public static readonly ESSimplePool<ESRuntimeTarget> Pool = new ESSimplePool<ESRuntimeTarget>(
            factoryMethod: () => new ESRuntimeTarget(),
            resetMethod: null,
            initCount: InitPoolCount,
            maxCount: MaxPoolCount,
            poolDisplayName: "ESRuntimeTarget Pool"
        );

        private object[] extras;
        private int extraCount;
        private object recycleToken;
        private bool recycleRequested;

        public bool IsRecycled { get; set; }

        public int Version { get; private set; }

        public Entity userEntity;

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
        public ESRuntimeTarget SetEntity(Entity entity)
        {
            userEntity = entity;
            return this;
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
