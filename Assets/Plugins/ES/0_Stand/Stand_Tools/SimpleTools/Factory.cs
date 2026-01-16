using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public interface IFactory<out T>
    {
        T Create();
    }
    public class ESFactory_New<T> : IFactory<T> where T : new()
    {
        public T Create()
        {
            return new T();
        }
    }
    public class ESFactory_Custom<T> : IFactory<T>
    {
        protected Func<T> createFunc;
        public ESFactory_Custom(Func<T> factoryMethod)
        {
            createFunc = factoryMethod;
        }
        public T Create()
        {
            return createFunc();
        }
    }
}

