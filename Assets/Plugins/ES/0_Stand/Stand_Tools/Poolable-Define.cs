using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{
    #region 常用对象池
    public interface IPoolable
    {
        void OnResetAsPoolable();
        bool IsRecycled { get; set; }
    }
    public interface IPoolablebAuto : IPoolable
    {
        void TryAutoPushedToPool();
    }
    public interface IPool<T> where T : IPoolable
    {
        T GetInPool();
        bool PushToPool(T obj);
    }

    public abstract class Pool<T> : IPool<T> where T : IPoolable
    {
        public int CurCount
        {
            get { return mObjectStack.Count; }
        }


        protected IFactory<T> mFactory;

        public void SetFactoryDirectly(IFactory<T> factory)
        {
            mFactory = factory;
        }

        public void SetFactoryByFunc(Func<T> factoryMethod)
        {
            mFactory = new ESFactory_Custom<T>(factoryMethod);
        }

        /// <summary>
        /// 存储相关数据的栈
        /// </summary>
        protected readonly Stack<T> mObjectStack = new Stack<T>();

        public void Clear(Action<T> onClearItem = null)
        {
            if (onClearItem != null)
            {
                foreach (var poolObject in mObjectStack)
                {
                    onClearItem(poolObject);
                }
            }

            mObjectStack.Clear();
        }

        /// <summary>
        /// default is 5
        /// </summary>
        protected int mMaxCount = 12;

        public virtual T GetInPool()
        {
            var use = mObjectStack.Count == 0
                ? mFactory.Create()
                : mObjectStack.Pop();
            use.IsRecycled = false;
            return use;
        }

        public abstract bool PushToPool(T obj);
    }

    public class ESSimplePool<T> : Pool<T> where T : IPoolable
    {
        protected readonly Action<T> mResetMethod;

        public ESSimplePool(Func<T> factoryMethod, Action<T> resetMethod = null, int initCount = 0)
        {
            mFactory = new ESFactory_Custom<T>(factoryMethod);
            mResetMethod = resetMethod;

            for (var i = 0; i < initCount; i++)
            {
                mObjectStack.Push(mFactory.Create());
            }
        }

        public override bool PushToPool(T obj)
        {
            mResetMethod?.Invoke(obj);
            obj.OnResetAsPoolable();
            obj.IsRecycled = true;
            mObjectStack.Push(obj);
            return true;
        }
    }
    public class ESSimplePoolSingleton<T> : ESSimplePool<T> where T : IPoolable, new()
    {
        private static ESSimplePoolSingleton<T> pool;
        public static ESSimplePoolSingleton<T> Pool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (pool != null) return pool;
                return CreatePool();
            }
            set { }
        }
        public ESSimplePoolSingleton(Func<T> factoryMethod, Action<T> resetMethod, int initCount)
        : base(factoryMethod, resetMethod, initCount)
        {
            pool = this;
        }
        public static ESSimplePoolSingleton<T> CreatePool()
        {
            return pool = new ESSimplePoolSingleton<T>(() => new T(), null, 10);
        }
        public override bool PushToPool(T obj)
        {
            obj.OnResetAsPoolable();
            return base.PushToPool(obj);
        }
    }
    #endregion
}
