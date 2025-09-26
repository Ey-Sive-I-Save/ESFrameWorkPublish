using ES;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public abstract class SingletonAsCore<T> : Core where T : SingletonAsCore<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        public static T Instance
        {
            get
            {
                if (ESSystem.IsQuitting)
                {
                    Debug.LogWarning($"[{typeof(T)}] 应用正在退出，单例实例已被销毁。返回 null。");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 尝试在场景中查找已存在的实例
                        _instance = FindAnyObjectByType<T>();

                        if (_instance == null)
                        {
                            // 如果没有找到，自动创建一个新的GameObject和实例
                            GameObject singletonObject = new GameObject();
                            _instance = singletonObject.AddComponent<T>();
                            singletonObject.name = $"[Singleton] {typeof(T).Name}";

                            Debug.Log($"[{typeof(T)}] 单例实例被自动创建。");
                        }

                        // 确保单例在加载新场景时不销毁
                        DontDestroyOnLoad(_instance.gameObject);
                    }
                    return _instance;
                }
            }
        }
        protected sealed override void Awake()
        {
            // 防止重复实例
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T)}] 场景中已存在一个实例，销毁新创建的实例: {gameObject.name}");
                DestroyImmediate(gameObject);
                return;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = this as T;
                    DontDestroyOnLoad(gameObject);
                    base.Awake();//DO
                                 // 可在此处添加其他初始化逻辑
                }
            }
        }
    }
}