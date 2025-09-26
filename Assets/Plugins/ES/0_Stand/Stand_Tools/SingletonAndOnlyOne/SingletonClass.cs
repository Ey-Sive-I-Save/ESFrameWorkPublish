using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class SingletonClass<This> where This : new()
    {
        private static readonly Lazy<This> _lazyInstance =
            new Lazy<This>(() => new This(), isThreadSafe: true);

        public static This Instance
        {
            get
            {
                if (!_lazyInstance.IsValueCreated)
                {
                    Debug.Log($"获取单例普通类{typeof(This).Name}的实例");
                }
                return _lazyInstance.Value;
            }
        }

    }
}