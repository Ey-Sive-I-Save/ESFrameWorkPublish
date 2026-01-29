using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES资源源工厂
    /// 
    /// 职责：
    /// 1. 根据ESResSourceLoadType创建对应的ESResSourceBase实例
    /// 2. 管理资源源类型注册表
    /// 3. 提供可扩展的类型注册机制
    /// 
    /// 设计模式：
    /// - 工厂模式：根据类型创建实例
    /// - 策略模式：每种类型对应不同的加载策略
    /// - 注册模式：支持运行时动态注册新类型
    /// 
    /// 扩展性：
    /// - 添加新类型只需：1) 枚举中添加值 2) 创建子类 3) 注册到工厂
    /// - 无需修改ESResMaster等核心代码
    /// </summary>
    public static class ESResSourceFactory
    {
        /// <summary>
        /// 资源源类型注册表
        /// Key: ESResSourceLoadType
        /// Value: 创建函数（返回从对象池获取的实例）
        /// </summary>
        private static readonly Dictionary<ESResSourceLoadType, Func<ESResSourceBase>> _typeRegistry =
            new Dictionary<ESResSourceLoadType, Func<ESResSourceBase>>();

        /// <summary>
        /// 静态构造函数，注册所有内置类型
        /// </summary>
        static ESResSourceFactory()
        {
            RegisterBuiltInTypes();
        }

        /// <summary>
        /// 注册所有内置资源类型
        /// </summary>
        private static void RegisterBuiltInTypes()
        {
            // AB包类型
            RegisterType(ESResSourceLoadType.AssetBundle, () => 
            {
                var source = ESResMaster.Instance.PoolForESABSource.GetInPool();
                source.IsNet = true;
                return source;
            });

            // AB资源类型
            RegisterType(ESResSourceLoadType.ABAsset, () => 
                ESResMaster.Instance.PoolForESAsset.GetInPool());

            // AB场景类型（从对象池获取）
            RegisterType(ESResSourceLoadType.ABScene, () => 
                ESResMaster.Instance.PoolForESABScene.GetInPool());

            // Shader变体类型（从对象池获取）
            RegisterType(ESResSourceLoadType.ShaderVariant, () => 
                ESResMaster.Instance.PoolForESShaderVariant.GetInPool());

            // RawFile类型（从对象池获取）
            RegisterType(ESResSourceLoadType.RawFile, () => 
                ESResMaster.Instance.PoolForESRawFile.GetInPool());

            // InternalResource类型（从对象池获取）
            RegisterType(ESResSourceLoadType.InternalResource, () => 
                ESResMaster.Instance.PoolForESInternalResource.GetInPool());

            // NetImage类型（从对象池获取）
            RegisterType(ESResSourceLoadType.NetImageRes, () => 
                ESResMaster.Instance.PoolForESNetImage.GetInPool());

            // TODO: 其他类型
            // RegisterType(ESResSourceLoadType.LocalImageRes, () => ...);
        }

        /// <summary>
        /// 注册资源类型
        /// </summary>
        /// <param name="loadType">加载类型</param>
        /// <param name="creator">创建函数（从对象池获取或直接new）</param>
        public static void RegisterType(ESResSourceLoadType loadType, Func<ESResSourceBase> creator)
        {
            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }

            if (_typeRegistry.ContainsKey(loadType))
            {
                Debug.LogWarning($"[ESResSourceFactory] 类型 {loadType} 已注册，将被覆盖");
            }

            _typeRegistry[loadType] = creator;
        }

        /// <summary>
        /// 创建资源源实例
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="loadType">加载类型</param>
        /// <returns>资源源实例</returns>
        public static ESResSourceBase CreateResSource(ESResKey key, ESResSourceLoadType loadType)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!_typeRegistry.TryGetValue(loadType, out var creator))
            {
                throw new NotSupportedException($"不支持的资源加载类型: {loadType}");
            }

            var source = creator();
            if (source == null)
            {
                throw new InvalidOperationException($"创建资源源失败: {loadType}");
            }

            // 初始化资源源
            source.Set(key, loadType);
            source.TargetType = key.TargetType;

            return source;
        }

        /// <summary>
        /// 检查类型是否已注册
        /// </summary>
        public static bool IsTypeRegistered(ESResSourceLoadType loadType)
        {
            return _typeRegistry.ContainsKey(loadType);
        }

        /// <summary>
        /// 获取所有已注册的类型
        /// </summary>
        public static ESResSourceLoadType[] GetRegisteredTypes()
        {
            var types = new ESResSourceLoadType[_typeRegistry.Count];
            _typeRegistry.Keys.CopyTo(types, 0);
            return types;
        }

        /// <summary>
        /// 取消注册类型（谨慎使用）
        /// </summary>
        public static bool UnregisterType(ESResSourceLoadType loadType)
        {
            return _typeRegistry.Remove(loadType);
        }
    }
}
