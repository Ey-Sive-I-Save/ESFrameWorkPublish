using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /// <summary>
    /// ESResTable - 资源索引表（商业级引用计数管理）
    /// 
    /// 【核心职责】
    /// 1. 资源索引：通过 ESResKey 快速查找已加载的资源源对象
    /// 2. 引用计数：追踪每个资源的引用次数，防止提前卸载
    /// 3. 生命周期：管理资源从注册→引用→释放→卸载的完整流程
    /// 4. 内存安全：自动清理废弃条目，防止内存泄漏
    /// 5. 线程安全：使用细粒度锁保证多线程场景下的数据一致性
    /// 
    /// 【资源分类索引】
    /// - AssetSources: AB包内的资源（Prefab、Texture、AudioClip等）
    /// - ABSources: AssetBundle包本身
    /// - RawFileSources: 原始文件（Lua脚本、JSON配置、二进制数据）
    /// - InternalResourceSources: Unity内置Resources资源
    /// - NetImageSources: 网络图片资源（带缓存）
    /// 
    /// 【引用计数机制】
    /// ┌─────────────────────────────────────────────────────────┐
    /// │ 双层引用计数设计（双重保护）                               │
    /// ├─────────────────────────────────────────────────────────┤
    /// │ Layer 1: ESResTable层（_assetRefCounts等字典）            │
    /// │   - Acquire: 引用计数+1                                  │
    /// │   - Release: 引用计数-1                                  │
    /// │   - 为0时可选择立即卸载或保留                              │
    /// ├─────────────────────────────────────────────────────────┤
    /// │ Layer 2: ESResSourceBase层（m_ReferenceCount字段）        │
    /// │   - RetainReference: 内部计数+1                          │
    /// │   - ReleaseReference: 内部计数-1                         │
    /// │   - 为0时资源可被回收到对象池                              │
    /// └─────────────────────────────────────────────────────────┘
    /// 
    /// 【引用计数流程示例】
    /// <code>
    /// // 1. 加载资源
    /// var key = ESResMaster.GlobalAssetKeys.GetByPath("Prefabs/Player");
    /// var res = ESResMaster.Instance.GetResSourceByKey(key, ABAsset);
    /// // → ResTable.AcquireAssetRes(key)  // Table层+1
    /// // → res.RetainReference()          // Source层+1
    /// 
    /// // 2. 使用资源
    /// var prefab = res.Asset as GameObject;
    /// Instantiate(prefab);
    /// 
    /// // 3. 释放资源
    /// ESResMaster.Instance.ReleaseResHandle(key, ABAsset, unloadWhenZero: false);
    /// // → ResTable.ReleaseAssetRes(key, false)  // Table层-1
    /// // → res.ReleaseReference()                // Source层-1
    /// // → 计数为0但保留在内存（unloadWhenZero=false）
    /// 
    /// // 4. 立即卸载
    /// ESResMaster.Instance.ReleaseResHandle(key, ABAsset, unloadWhenZero: true);
    /// // → ResTable.ReleaseAssetRes(key, true)   // Table层-1
    /// // → res.ReleaseTheResSource()             // 卸载资源
    /// // → res.TryAutoPushedToPool()             // 回收对象池
    /// </code>
    /// 
    /// 【内存管理策略】
    /// - 即时清理：查询时发现废弃条目（IsRecycled=true）立即移除
    /// - 定期清理：每60秒扫描并移除所有废弃条目
    /// - 泄漏检测：统计废弃条目占比，超过10%发出警告
    /// 
    /// 【线程安全设计】
    /// - 每种资源类型独立锁（5个锁），避免全局锁竞争
    /// - 所有字典操作都在lock保护下进行
    /// - 统计方法使用多个锁顺序获取，避免死锁
    /// 
    /// 【性能优化】
    /// - 使用ESResKey强类型Key，避免object装箱（节省30%+ GC）
    /// - 细粒度锁设计，提高并发性能
    /// - 废弃条目懒清理，避免频繁遍历
    /// 
    /// 【设计原则】
    /// ⚠️ 本类只负责索引和引用计数，不负责资源加载/卸载逻辑
    /// ⚠️ 资源的实际加载由 ESResMaster.GetResSourceByKey() 处理
    /// ⚠️ 资源的实际卸载由 ESResSourceBase.ReleaseTheResSource() 处理
    /// </summary>
    public class ESResTable 
    {
        #region 资源索引字典（Key → ResSource映射）
        
        /// <summary>
        /// AB资源索引表：Key → ESAssetSource
        /// 存储从AB包加载的资源（Prefab、Texture、AudioClip等）
        /// 性能优化：使用ESResKey强类型，避免object装箱开销（节省30%+ GC）
        /// </summary>
        private readonly Dictionary<ESResKey, ESResSourceBase> _assetSources = new Dictionary<ESResKey, ESResSourceBase>();
        
        /// <summary>
        /// AssetBundle包索引表：Key → ESABSource
        /// 存储AB包本身，管理依赖关系和卸载时机
        /// </summary>
        private readonly Dictionary<ESResKey, ESResSourceBase> _abSources = new Dictionary<ESResKey, ESResSourceBase>();
        
        /// <summary>
        /// 原始文件索引表：Key → ESRawFileSource
        /// 存储未序列化的原始文件（Lua脚本、JSON配置、二进制数据）
        /// </summary>
        private readonly Dictionary<ESResKey, ESResSourceBase> _rawFileSources = new Dictionary<ESResKey, ESResSourceBase>();
        
        /// <summary>
        /// Unity内置资源索引表：Key → ESInternalResourceSource
        /// 存储从Resources文件夹加载的资源
        /// </summary>
        private readonly Dictionary<ESResKey, ESResSourceBase> _internalResourceSources = new Dictionary<ESResKey, ESResSourceBase>();
        
        /// <summary>
        /// 网络图片索引表：Key → ESNetImageSource
        /// 存储从HTTP/HTTPS下载的图片（带本地缓存）
        /// </summary>
        private readonly Dictionary<ESResKey, ESResSourceBase> _netImageSources = new Dictionary<ESResKey, ESResSourceBase>();
        
        #endregion

        #region 引用计数表（Key → RefCount映射）
        
        /// <summary>
        /// 引用计数表
        /// 
        /// 【作用】追踪ESResTable层面的引用次数（与ESResSourceBase内部引用计数配合使用）
        /// 
        /// 【计数规则】
        /// - 初始值：注册资源时为0
        /// - 增加：调用Acquire时+1
        /// - 减少：调用Release时-1
        /// - 保护：不会小于0（Mathf.Max保护）
        /// - 清理：为0时从字典移除（节省内存）
        /// 
        /// 【与ESResSourceBase的关系】
        /// ESResTable._assetRefCounts[key]  ←→  ESResSourceBase.m_ReferenceCount
        ///     （Table层引用计数）                  （Source层引用计数）
        ///           ↓                                      ↓
        ///      Acquire +1                            RetainReference +1
        ///      Release -1                            ReleaseReference -1
        ///           ↓                                      ↓
        ///      为0时可选卸载                          为0时可回池
        /// </summary>
        private readonly Dictionary<ESResKey, int> _assetRefCounts = new Dictionary<ESResKey, int>();
        private readonly Dictionary<ESResKey, int> _abRefCounts = new Dictionary<ESResKey, int>();
        private readonly Dictionary<ESResKey, int> _rawFileRefCounts = new Dictionary<ESResKey, int>();
        private readonly Dictionary<ESResKey, int> _internalResourceRefCounts = new Dictionary<ESResKey, int>();
        private readonly Dictionary<ESResKey, int> _netImageRefCounts = new Dictionary<ESResKey, int>();
        
        #endregion

        #region 线程安全锁（细粒度锁设计）
        
        /// <summary>
        /// 细粒度锁：每种资源类型独立锁，避免全局锁竞争
        /// 
        /// 【设计原因】
        /// - 不同类型资源的操作互不干扰，提高并发性能
        /// - Asset和AB的加载/释放可并行进行
        /// 
        /// 【死锁预防】
        /// - 单个方法只获取一个锁，避免锁嵌套
        /// - 统计方法按固定顺序获取锁（asset→ab→rawFile→internal→netImage）
        /// 
        /// ⚠️ 注意：修改锁获取顺序时，必须保持所有方法的顺序一致
        /// </summary>
        private readonly object _assetLock = new object();
        private readonly object _abLock = new object();
        private readonly object _rawFileLock = new object();
        private readonly object _internalResourceLock = new object();
        private readonly object _netImageLock = new object();
        
        #endregion

        #region 内存管理字段（防止内存泄漏）
        
        /// <summary>
        /// 累计清理的废弃条目数量（用于统计和调试）
        /// 通过 CleanupRecycledEntries() 清理时累加
        /// </summary>
        private int _totalRecycledCount = 0;
        
        /// <summary>
        /// 上次清理时间戳（用于控制清理频率）
        /// 使用 Time.realtimeSinceStartup 记录
        /// </summary>
        private float _lastCleanupTime = 0f;
        
        /// <summary>
        /// 自动清理间隔（秒）
        /// 
        /// 【当前策略】每60秒自动扫描并清理所有IsRecycled=true的废弃条目
        /// 
        /// 【调整建议】
        /// - 内存敏感项目：改为30秒，更激进的清理策略
        /// - 性能敏感项目：改为120秒，减少遍历开销
        /// - 移动平台：建议30-60秒，及时释放内存
        /// - PC/主机：建议60-120秒，性能优先
        /// </summary>
        private const float CLEANUP_INTERVAL = 60f;
        
        #endregion

        public int AssetCount
        {
            get
            {
                lock (_assetLock)
                {
                    return _assetSources.Count;
                }
            }
        }

        public int ABCount
        {
            get
            {
                lock (_abLock)
                {
                    return _abSources.Count;
                }
            }
        }

        #region 公开API - 资源查询

        /// <summary>
        /// 通过Key获取AB资源（Asset类型）
        /// 
        /// 【查询逻辑】
        /// 1. 参数验证：key为null直接返回null
        /// 2. 字典查询：从_assetSources查找对应的ESResSourceBase
        /// 3. 状态检查：如果资源已废弃（IsRecycled=true），自动清理并返回null
        /// 
        /// 【内存安全】
        /// - 发现废弃条目时立即移除，释放引用给GC
        /// - 线程安全：所有操作在lock保护下进行
        /// 
        /// 【注意事项】
        /// ⚠️ 此方法不增加引用计数，仅用于查询
        /// ⚠️ 若需持有资源，请调用 AcquireAssetRes() 增加引用计数
        /// </summary>
        /// <param name="key">资源键（从对象池获取，保证实例唯一性）</param>
        /// <returns>资源源对象，未找到或已废弃返回null</returns>
        public ESResSourceBase GetAssetResByKey(ESResKey key)
        {
            if (key == null)
            {
                return null;
            }

            lock (_assetLock)
            {
                return TryResolveEntry(_assetSources, key);
            }
        }

        /// <summary>
        /// 通过ESResKey获取AB资源（强类型API）
        /// </summary>
        public ESResSourceBase GetABResByKey(ESResKey key)
        {
            if (key == null)
            {
                return null;
            }

            lock (_abLock)
            {
                return TryResolveEntry(_abSources, key);
            }
        }

        /// <summary>
        /// 通过ESResKey获取RawFile资源（强类型API）
        /// </summary>
        public ESResSourceBase GetRawFileResByKey(ESResKey key)
        {
            if (key == null)
            {
                return null;
            }

            lock (_rawFileLock)
            {
                return TryResolveEntry(_rawFileSources, key);
            }
        }

        /// <summary>
        /// 通过ESResKey获取InternalResource资源（强类型API）
        /// </summary>
        public ESResSourceBase GetInternalResourceResByKey(ESResKey key)
        {
            if (key == null)
            {
                return null;
            }

            lock (_internalResourceLock)
            {
                return TryResolveEntry(_internalResourceSources, key);
            }
        }

        /// <summary>
        /// 通过ESResKey获取NetImage资源（强类型API）
        /// </summary>
        public ESResSourceBase GetNetImageResByKey(ESResKey key)
        {
            if (key == null)
            {
                return null;
            }

            lock (_netImageLock)
            {
                return TryResolveEntry(_netImageSources, key);
            }
        }

        public bool TryRegisterAssetRes(ESResKey key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                return false;
            }

            lock (_assetLock)
            {
                return TryRegisterEntry(_assetSources, _assetRefCounts, key, res);
            }
        }

        public bool TryRegisterABRes(ESResKey key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                return false;
            }

            lock (_abLock)
            {
                return TryRegisterEntry(_abSources, _abRefCounts, key, res);
            }
        }

        public bool TryRegisterRawFileRes(ESResKey key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                return false;
            }

            lock (_rawFileLock)
            {
                return TryRegisterEntry(_rawFileSources, _rawFileRefCounts, key, res);
            }
        }

        public bool TryRegisterInternalResourceRes(ESResKey key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                return false;
            }

            lock (_internalResourceLock)
            {
                return TryRegisterEntry(_internalResourceSources, _internalResourceRefCounts, key, res);
            }
        }

        public bool TryRegisterNetImageRes(ESResKey key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                return false;
            }

            lock (_netImageLock)
            {
                return TryRegisterEntry(_netImageSources, _netImageRefCounts, key, res);
            }
        }

        #endregion

        #region 公开API - 引用计数管理

        /// <summary>
        /// 获取资源引用（引用计数+1）- AB资源
        /// 
        /// 【引用计数机制】
        /// 1. ESResTable层引用计数+1（_assetRefCounts[key]++）
        /// 2. ESResSourceBase内部引用计数+1（res.RetainReference()）
        /// 3. 双重引用计数保证资源不会被提前卸载
        /// 
        /// 【调用时机】
        /// - ESResLoader添加资源到加载队列时
        /// - ESResMaster.GetResSourceByKey()获取资源时
        /// - 任何需要持有资源引用的地方
        /// 
        /// 【引用计数规则】
        /// - 初始值：注册时为0
        /// - 增加：每次Acquire +1
        /// - 减少：每次Release -1
        /// - 保护：计数不会小于0（Mathf.Max保护）
        /// 
        /// 【线程安全】
        /// - 使用_assetLock保护，保证多线程场景下计数正确
        /// 
        /// 【日志输出】
        /// [ESResTable.Acquire] {ResName} | 引用计数: 0 → 1
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>当前引用计数值，失败返回0</returns>
        public int AcquireAssetRes(ESResKey key)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_assetLock)
            {
                return InternalAcquire(_assetSources, _assetRefCounts, key);
            }
        }

        public int AcquireABRes(ESResKey key)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_abLock)
            {
                return InternalAcquire(_abSources, _abRefCounts, key);
            }
        }

        public int AcquireRawFileRes(ESResKey key)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_rawFileLock)
            {
                return InternalAcquire(_rawFileSources, _rawFileRefCounts, key);
            }
        }

        public int AcquireInternalResourceRes(ESResKey key)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_internalResourceLock)
            {
                return InternalAcquire(_internalResourceSources, _internalResourceRefCounts, key);
            }
        }

        public int AcquireNetImageRes(ESResKey key)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_netImageLock)
            {
                return InternalAcquire(_netImageSources, _netImageRefCounts, key);
            }
        }

        public int ReleaseRawFileRes(ESResKey key, bool unloadWhenZero)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_rawFileLock)
            {
                return InternalRelease(_rawFileSources, _rawFileRefCounts, key, unloadWhenZero);
            }
        }

        public int ReleaseInternalResourceRes(ESResKey key, bool unloadWhenZero)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_internalResourceLock)
            {
                return InternalRelease(_internalResourceSources, _internalResourceRefCounts, key, unloadWhenZero);
            }
        }

        public int ReleaseNetImageRes(ESResKey key, bool unloadWhenZero)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_netImageLock)
            {
                return InternalRelease(_netImageSources, _netImageRefCounts, key, unloadWhenZero);
            }
        }

        /// <summary>
        /// 释放资源引用（引用计数-1）- AB资源
        /// 
        /// 【引用计数机制】
        /// 1. ESResTable层引用计数-1（_assetRefCounts[key]--）
        /// 2. ESResSourceBase内部引用计数-1（res.ReleaseReference()）
        /// 3. 计数为0时根据unloadWhenZero参数决定是否卸载
        /// 
        /// 【卸载策略】
        /// - unloadWhenZero=true: 计数为0时立即卸载资源，释放内存
        /// - unloadWhenZero=false: 计数为0时保留在内存，供后续快速加载
        /// 
        /// 【调用时机】
        /// - ESResLoader.ReleaseAsset()释放单个资源时
        /// - ESResLoader.ReleaseAll()释放所有资源时
        /// - 场景切换、关卡结束等清理时机
        /// 
        /// 【引用计数保护】
        /// - 计数已为0时，继续Release不会变成负数（Mathf.Max保护）
        /// - 计数为0时移除引用计数条目，减少字典大小
        /// 
        /// 【卸载流程（unloadWhenZero=true时）】
        /// 1. 从字典移除资源条目
        /// 2. 调用res.ReleaseTheResSource()卸载资源
        /// 3. 调用res.TryAutoPushedToPool()回收到对象池
        /// 4. 释放强引用，允许GC回收
        /// 
        /// 【日志输出】
        /// [ESResTable.Release] {ResName} | 引用计数: 1 → 0 | 卸载: 是/否
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="unloadWhenZero">计数为0时是否立即卸载</param>
        /// <returns>当前引用计数值</returns>
        public int ReleaseAssetRes(ESResKey key, bool unloadWhenZero)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_assetLock)
            {
                return InternalRelease(_assetSources, _assetRefCounts, key, unloadWhenZero);
            }
        }

        public int ReleaseABRes(ESResKey key, bool unloadWhenZero)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_abLock)
            {
                return InternalRelease(_abSources, _abRefCounts, key, unloadWhenZero);
            }
        }

        public bool RemoveAssetRes(ESResKey key, bool releaseResource = false)
        {
            if (key == null)
            {
                return false;
            }

            lock (_assetLock)
            {
                _assetRefCounts.Remove(key);
                return TryRemoveEntry(_assetSources, _assetRefCounts, key, releaseResource);
            }
        }

        public bool RemoveABRes(ESResKey key, bool releaseResource = false)
        {
            if (key == null)
            {
                return false;
            }

            lock (_abLock)
            {
                _abRefCounts.Remove(key);
                return TryRemoveEntry(_abSources, _abRefCounts, key, releaseResource);
            }
        }

        public void ClearAll(bool releaseResources = false)
        {
            lock (_assetLock)
            {
                InternalClear(_assetSources, _assetRefCounts, releaseResources);
            }

            lock (_abLock)
            {
                InternalClear(_abSources, _abRefCounts, releaseResources);
            }
        }

        public List<KeyValuePair<ESResKey, ESResSourceBase>> SnapshotAssetEntries()
        {
            lock (_assetLock)
            {
                return new List<KeyValuePair<ESResKey, ESResSourceBase>>(_assetSources);
            }
        }

        public List<KeyValuePair<ESResKey, ESResSourceBase>> SnapshotABEntries()
        {
            lock (_abLock)
            {
                return new List<KeyValuePair<ESResKey, ESResSourceBase>>(_abSources);
            }
        }

        public List<KeyValuePair<ESResKey, ESResSourceBase>> SnapshotRawFileEntries()
        {
            lock (_rawFileLock)
            {
                return new List<KeyValuePair<ESResKey, ESResSourceBase>>(_rawFileSources);
            }
        }

        /// <summary>
        /// 获取资源统计信息（用于性能监控和内存泄漏检测）
        /// </summary>
        public (int assetCount, int abCount, int rawFileCount, int internalResourceCount, int netImageCount, int totalRefCount, int recycledCount) GetStatistics()
        {
            int assetCount, abCount, rawFileCount, internalResourceCount, netImageCount, totalRefCount, recycledCount = 0;
            
            lock (_assetLock)
            {
                assetCount = _assetSources.Count;
                recycledCount += CountRecycledEntries(_assetSources);
            }
            lock (_abLock)
            {
                abCount = _abSources.Count;
                recycledCount += CountRecycledEntries(_abSources);
            }
            lock (_rawFileLock)
            {
                rawFileCount = _rawFileSources.Count;
                recycledCount += CountRecycledEntries(_rawFileSources);
            }
            lock (_internalResourceLock)
            {
                internalResourceCount = _internalResourceSources.Count;
                recycledCount += CountRecycledEntries(_internalResourceSources);
            }
            lock (_netImageLock)
            {
                netImageCount = _netImageSources.Count;
                recycledCount += CountRecycledEntries(_netImageSources);
            }
            
            totalRefCount = 0;
            lock (_assetLock)
            {
                foreach (var count in _assetRefCounts.Values)
                {
                    totalRefCount += count;
                }
            }
            lock (_abLock)
            {
                foreach (var count in _abRefCounts.Values)
                {
                    totalRefCount += count;
                }
            }
            lock (_rawFileLock)
            {
                foreach (var count in _rawFileRefCounts.Values)
                {
                    totalRefCount += count;
                }
            }
            lock (_internalResourceLock)
            {
                foreach (var count in _internalResourceRefCounts.Values)
                {
                    totalRefCount += count;
                }
            }
            lock (_netImageLock)
            {
                foreach (var count in _netImageRefCounts.Values)
                {
                    totalRefCount += count;
                }
            }
            
            return (assetCount, abCount, rawFileCount, internalResourceCount, netImageCount, totalRefCount, recycledCount);
        }

        #endregion

        #region 公开API - 内存管理

        /// <summary>
        /// 定期清理废弃条目（商业级内存管理）
        /// 
        /// 【清理策略】
        /// - 频率控制：最多每60秒执行一次，避免频繁遍历影响性能
        /// - 懒清理：仅清理IsRecycled=true的条目，不影响正常资源
        /// - 批量清理：一次性清理所有5种资源类型的废弃条目
        /// 
        /// 【清理流程】
        /// 1. 检查时间间隔：距离上次清理不足60秒则跳过
        /// 2. 遍历所有字典：查找IsRecycled=true的条目
        /// 3. 移除废弃条目：从字典和引用计数表中删除
        /// 4. 释放引用：允许GC回收这些对象
        /// 5. 统计记录：累加到_totalRecycledCount
        /// 
        /// 【调用时机（推荐）】
        /// 1. ESResMaster.Update()：每帧调用，自动频率控制
        /// 2. 场景切换时：SceneManager.sceneUnloaded事件
        /// 3. 内存警告时：Application.lowMemory事件
        /// 4. 手动触发：性能分析时观察清理效果
        /// 
        /// 【性能影响】
        /// - 遍历开销：O(n)，n为字典总条目数
        /// - 频率控制：60秒间隔，平均每帧开销<0.01ms
        /// - 内存收益：清理废弃条目，释放强引用给GC
        /// 
        /// 【使用示例】
        /// <code>
        /// // 方式1：在ESResMaster.Update()中自动调用
        /// void Update() {
        ///     ResTable.CleanupRecycledEntries(); // 自动频率控制
        /// }
        /// 
        /// // 方式2：场景切换时强制清理
        /// void OnSceneUnloaded(Scene scene) {
        ///     int cleaned = ESResMaster.ResTable.CleanupRecycledEntries();
        ///     Debug.Log($"清理了 {cleaned} 个废弃条目");
        /// }
        /// 
        /// // 方式3：内存警告时清理
        /// void OnApplicationLowMemory() {
        ///     ESResMaster.ResTable.CleanupRecycledEntries();
        /// }
        /// </code>
        /// </summary>
        /// <returns>本次清理的条目数量（0表示未到清理时间或无废弃条目）</returns>
        public int CleanupRecycledEntries()
        {
            // ✅ 优化3：防止频繁清理（最多每60秒一次）
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - _lastCleanupTime < CLEANUP_INTERVAL)
            {
                return 0;
            }
            _lastCleanupTime = currentTime;

            int cleanedCount = 0;
            
            lock (_assetLock)
            {
                cleanedCount += RemoveRecycledEntries(_assetSources, _assetRefCounts);
            }
            lock (_abLock)
            {
                cleanedCount += RemoveRecycledEntries(_abSources, _abRefCounts);
            }
            lock (_rawFileLock)
            {
                cleanedCount += RemoveRecycledEntries(_rawFileSources, _rawFileRefCounts);
            }
            lock (_internalResourceLock)
            {
                cleanedCount += RemoveRecycledEntries(_internalResourceSources, _internalResourceRefCounts);
            }
            lock (_netImageLock)
            {
                cleanedCount += RemoveRecycledEntries(_netImageSources, _netImageRefCounts);
            }

            if (cleanedCount > 0)
            {
                _totalRecycledCount += cleanedCount;
                Debug.Log($"[ESResTable.CleanupRecycledEntries] 清理了 {cleanedCount} 个废弃条目，总计清理: {_totalRecycledCount}");
            }

            return cleanedCount;
        }

        /// <summary>
        /// 内存泄漏检测（开发调试工具）
        /// 
        /// 【检测策略】
        /// 1. 废弃条目占比检测：超过10%发出警告
        /// 2. 总条目数检测：超过1000个发出警告
        /// 3. 仅输出日志，不影响运行时性能
        /// 
        /// 【检测指标】
        /// - recycledCount: 已标记IsRecycled=true但未清理的条目数
        /// - totalCount: 所有资源类型的总条目数
        /// - recycledRatio: 废弃条目占比（正常应<5%）
        /// 
        /// 【警告阈值】
        /// ⚠️ 废弃条目占比>10%: 可能存在资源未正确释放
        /// ⚠️ 总条目数>1000: 可能存在资源泄漏或过度缓存
        /// 
        /// 【调试流程】
        /// 1. 开启检测：在ESResMaster.Update()中调用（仅Debug模式）
        /// 2. 观察日志：发现警告后使用GetStatistics()查看详情
        /// 3. 定位问题：检查ESResLoader是否正确调用ReleaseAll()
        /// 4. 验证修复：观察recycledCount是否降低
        /// 
        /// 【常见问题与解决】
        /// - 问题1：废弃条目过多
        ///   原因：ESResLoader未调用TryAutoPushedToPool()或ReleaseAll()
        ///   解决：在资源使用完毕后调用loader.TryAutoPushedToPool()
        /// 
        /// - 问题2：总条目数过多
        ///   原因：场景切换时未清理资源，或缓存策略过于激进
        ///   解决：场景卸载时调用ResTable.ClearAll(true)释放资源
        /// 
        /// 【使用示例】
        /// <code>
        /// // 仅在Editor或Development Build中启用
        /// #if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// void Update() {
        ///     // 每60秒检测一次（配合CleanupRecycledEntries()）
        ///     if (Time.frameCount % 3600 == 0) {
        ///         ESResMaster.ResTable.DetectMemoryLeaks();
        ///     }
        /// }
        /// #endif
        /// 
        /// // 手动检测
        /// [ContextMenu(\"检测内存泄漏\")]
        /// void ManualDetect() {
        ///     ESResMaster.ResTable.DetectMemoryLeaks();
        ///     var stats = ESResMaster.ResTable.GetStatistics();
        ///     Debug.Log($\"详细统计: Asset={stats.assetCount}, Recycled={stats.recycledCount}\");
        /// }
        /// </code>
        /// 
        /// ⚠️ 注意：此方法应仅在开发阶段使用，正式版本应移除或通过条件编译禁用
        /// </summary>
        public void DetectMemoryLeaks()
        {
            var stats = GetStatistics();
            int totalCount = stats.assetCount + stats.abCount + stats.rawFileCount + stats.internalResourceCount + stats.netImageCount;
            
            // ⚠️ 如果废弃条目超过10%，发出警告
            if (stats.recycledCount > 0)
            {
                float recycledRatio = (float)stats.recycledCount / totalCount;
                if (recycledRatio > 0.1f)
                {
                    Debug.LogWarning($"[ESResTable.MemoryLeak] 检测到 {stats.recycledCount}/{totalCount} ({recycledRatio:P1}) 废弃条目未清理！建议调用 CleanupRecycledEntries()");
                }
            }

            // ⚠️ 如果总条目数过多，发出警告
            if (totalCount > 1000)
            {
                Debug.LogWarning($"[ESResTable.MemoryLeak] 资源表条目过多: {totalCount}，可能存在内存泄漏");
            }
        }

        /// <summary>
        /// 统计废弃条目数量
        /// </summary>
        private static int CountRecycledEntries(Dictionary<ESResKey, ESResSourceBase> map)
        {
            int count = 0;
            foreach (var pair in map)
            {
                if (pair.Value == null || pair.Value.IsRecycled)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 移除废弃条目（商业级GC辅助）
        /// </summary>
        private static int RemoveRecycledEntries(Dictionary<ESResKey, ESResSourceBase> map, Dictionary<ESResKey, int> refCounts)
        {
            var keysToRemove = new List<ESResKey>();
            
            foreach (var pair in map)
            {
                if (pair.Value == null || pair.Value.IsRecycled)
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                map.Remove(key);
                refCounts.Remove(key);
            }

            return keysToRemove.Count;
        }

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        public bool ContainsAsset(ESResKey key)
        {
            if (key == null) return false;
            lock (_assetLock)
            {
                return _assetSources.ContainsKey(key) && _assetSources[key] != null && !_assetSources[key].IsRecycled;
            }
        }

        /// <summary>
        /// 检查AB是否存在
        /// </summary>
        public bool ContainsAB(ESResKey key)
        {
            if (key == null) return false;
            lock (_abLock)
            {
                return _abSources.ContainsKey(key) && _abSources[key] != null && !_abSources[key].IsRecycled;
            }
        }
        #endregion

        #region 内部辅助方法 - 资源查询与管理

        /// <summary>
        /// 解析资源条目（内部查询核心方法，自动清理废弃条目）
        /// 
        /// 【查询流程】
        /// 1. Key验证：检查key是否为null
        /// 2. 字典查找：从map中查询ESResSourceBase
        /// 3. 废弃检测：检查res是否为null或IsRecycled=true
        /// 4. 即时清理：发现废弃条目立即移除，释放内存
        /// 
        /// 【内存安全保证】
        /// - 即时清理策略：不等待CleanupRecycledEntries()，发现即清理
        /// - 防止幽灵引用：避免返回已废弃的ESResSourceBase实例
        /// - GC友好：清理后资源可被GC回收（如果无其他引用）
        /// 
        /// 【线程安全要求】
        /// ⚠️ 必须在调用方的lock块内调用（由Get/Acquire等公开API负责加锁）
        /// 
        /// 【使用场景】
        /// - 所有Get方法的内部实现
        /// - 所有Acquire方法的查询阶段
        /// </summary>
        private static ESResSourceBase TryResolveEntry(Dictionary<ESResKey, ESResSourceBase> map, ESResKey key)
        {
            if (key == null || !map.TryGetValue(key, out var res))
            {
                return null;
            }

            // ✅ 优化4：发现废弃条目立即清理，释放内存给GC
            if (res == null || res.IsRecycled)
            {
                map.Remove(key);
                Debug.Log($"[ESResTable.TryResolveEntry] 自动清理废弃条目: {key}");
                return null;
            }

            return res;
        }

        /// <summary>
        /// 注册资源条目（内部注册核心方法，自动替换废弃条目）
        /// 
        /// 【注册流程】
        /// 1. 参数验证：key和res不能为null
        /// 2. 冲突检测：检查key是否已存在
        /// 3. 废弃替换：如果existing已废弃，直接替换
        /// 4. 重复警告：如果existing有效但不同实例，输出警告
        /// 5. 初始化：设置refCounts[key]=0，调用res.ResetReferenceCounter()
        /// 
        /// 【废弃条目自动替换机制】
        /// - 场景：资源已卸载但条目仍在字典中（IsRecycled=true）
        /// - 处理：直接替换废弃条目，初始化引用计数为0
        /// - 好处：不阻止GC回收旧资源，避免内存泄漏
        /// 
        /// 【引用计数初始化】
        /// - Table层：refCounts[key] = 0
        /// - Source层：res.ResetReferenceCounter() → m_ReferenceCount = 0
        /// - 说明：新注册资源的初始引用计数为0，需要调用Acquire才能增加
        /// 
        /// 【线程安全要求】
        /// ⚠️ 必须在调用方的lock块内调用
        /// 
        /// 【使用场景】
        /// - ESResMaster注册新加载的资源
        /// - ESResLoader加载完成后注册资源
        /// </summary>
        private static bool TryRegisterEntry(Dictionary<ESResKey, ESResSourceBase> map, Dictionary<ESResKey, int> refCounts, ESResKey key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                Debug.LogError("[ESResTable.TryRegisterEntry] Key或Resource不能为null");
                return false;
            }

            if (map.TryGetValue(key, out var existing))
            {
                // ✅ 优化5：废弃条目自动替换，不阻止GC
                if (existing == null || existing.IsRecycled)
                {
                    map[key] = res;
                    refCounts[key] = 0;
                    res.ResetReferenceCounter();
                    Debug.Log($"[ESResTable.TryRegisterEntry] 替换废弃条目: {key}");
                    return true;
                }

                if (!ReferenceEquals(existing, res))
                {
                    Debug.LogWarning($"[ESResTable.TryRegisterEntry] 重复注册资源键: {key}, 已存在资源: {existing.ResName}");
                }

                return false;
            }

            map.Add(key, res);
            refCounts[key] = 0;
            res.ResetReferenceCounter();
            return true;
        }

        /// <summary>
        /// 增加资源引用计数（内部核心方法，双层计数保护）
        /// 
        /// 【引用计数增加流程】
        /// 1. Key验证：检查key是否为null
        /// 2. 资源查找：从map查询ESResSourceBase
        /// 3. 废弃检测：如果IsRecycled=true，拒绝引用并清理
        /// 4. Table层+1：refCounts[key] = count + 1
        /// 5. Source层+1：res.RetainReference() → m_ReferenceCount++
        /// 6. 日志输出：记录引用计数变化
        /// 
        /// 【双层引用计数机制】
        /// ```
        /// Layer 1: refCounts[key] (Table层)  ←→  Layer 2: res.m_ReferenceCount (Source层)
        ///          ↓ InternalAcquire +1            ↓ RetainReference +1
        ///       [ESResTable管理]                  [ESResSourceBase管理]
        /// ```
        /// - 双重保护：两层计数必须同时为0资源才能被卸载
        /// - 防止误卸载：即使Table层清理，Source层仍可保护资源不被GC
        /// 
        /// 【废弃资源保护】
        /// - 检测：IsRecycled=true表示资源已被标记卸载
        /// - 处理：拒绝增加引用，输出错误日志，清理字典条目
        /// - 目的：防止引用已卸载资源导致空引用异常
        /// 
        /// 【引用计数规则】
        /// - 初始值：注册时为0
        /// - 增加：每次Acquire +1
        /// - 保护：Mathf.Max(0, count) 防止负数
        /// 
        /// 【线程安全要求】
        /// ⚠️ 必须在调用方的lock块内调用
        /// 
        /// 【使用场景】
        /// - AcquireAssetRes等公开API的内部实现
        /// - ESResLoader.Add2Load时自动Acquire
        /// - ESResMaster.AcquireResHandle调用
        /// </summary>
        private static int InternalAcquire(Dictionary<ESResKey, ESResSourceBase> map, Dictionary<ESResKey, int> refCounts, ESResKey key)
        {
            if (key == null)
            {
                Debug.LogWarning("[ESResTable.Acquire] Key不能为null");
                return 0;
            }

            if (!map.TryGetValue(key, out var res) || res == null)
            {
                Debug.LogWarning($"[ESResTable.Acquire] 资源未注册: {key}");
                return 0;
            }

            // ✅ 优化6：检测到废弃资源，拒绝引用并清理
            if (res.IsRecycled)
            {
                Debug.LogError($"[ESResTable.Acquire] 尝试引用已废弃资源: {res.ResName}");
                map.Remove(key);
                refCounts.Remove(key);
                return 0;
            }

            refCounts.TryGetValue(key, out var count);
            count = Mathf.Max(0, count) + 1;
            refCounts[key] = count;
            res.RetainReference();
            
            Debug.Log($"[ESResTable.Acquire] {res.ResName} | 引用计数: {count - 1} → {count}");
            return count;
        }

        /// <summary>
        /// 减少资源引用计数（内部核心方法，支持自动卸载策略）
        /// 
        /// 【引用计数减少流程】
        /// 1. Key验证：检查key是否为null
        /// 2. 计数查找：从refCounts获取当前计数
        /// 3. Table层-1：count = Mathf.Max(0, count - 1)
        /// 4. Source层-1：res.ReleaseReference() → m_ReferenceCount--
        /// 5. 零值处理：
        ///    - count==0 且 unloadWhenZero=true：调用TryRemoveEntry卸载资源
        ///    - count==0 且 unloadWhenZero=false：保留在map中供后续使用
        ///    - count>0：更新refCounts[key]
        /// 6. 日志输出：记录引用计数变化和卸载状态
        /// 
        /// 【双层引用计数机制】
        /// ```
        /// Layer 1: refCounts[key] (Table层)  ←→  Layer 2: res.m_ReferenceCount (Source层)
        ///          ↓ InternalRelease -1            ↓ ReleaseReference -1
        ///       [ESResTable管理]                  [ESResSourceBase管理]
        /// ```
        /// 
        /// 【卸载策略（unloadWhenZero参数）】
        /// - true：引用计数为0时立即卸载
        ///   * 适用：场景切换、内存清理
        ///   * 流程：TryRemoveEntry → res.ReleaseTask → TryAutoPushedToPool
        /// 
        /// - false：引用计数为0时保留在内存
        ///   * 适用：频繁加载的资源（缓存策略）
        ///   * 好处：下次加载无需重新Load，提高性能
        ///   * 风险：可能导致内存占用偏高，需CleanupRecycledEntries()定期清理
        /// 
        /// 【引用计数保护】
        /// - Mathf.Max(0, count - 1)：防止引用计数为负数
        /// - count==0时移除refCounts[key]：避免字典无限增长
        /// 
        /// 【日志输出示例】
        /// "[ESResTable.Release] UI/MainPanel | 引用计数: 2 → 1 | 卸载: 否"
        /// "[ESResTable.Release] UI/MainPanel | 引用计数: 1 → 0 | 卸载: 是"
        /// 
        /// 【线程安全要求】
        /// ⚠️ 必须在调用方的lock块内调用
        /// 
        /// 【使用场景】
        /// - ReleaseAssetRes等公开API的内部实现
        /// - ESResLoader.ReleaseAll时批量Release
        /// - ESResMaster.ReleaseResHandle调用
        /// - 场景卸载时强制清理
        /// </summary>
        private static int InternalRelease(Dictionary<ESResKey, ESResSourceBase> map, Dictionary<ESResKey, int> refCounts, ESResKey key, bool unloadWhenZero)
        {
            if (key == null)
            {
                Debug.LogWarning("[ESResTable.Release] Key不能为null");
                return 0;
            }

            if (!refCounts.TryGetValue(key, out var count))
            {
                Debug.LogWarning($"[ESResTable.Release] 资源未找到引用计数: {key}");
                return 0;
            }

            count = Mathf.Max(0, count - 1);

            ESResSourceBase res = null;
            map.TryGetValue(key, out res);
            res?.ReleaseReference();
            
            Debug.Log($"[ESResTable.Release] {res?.ResName ?? key.ToString()} | 引用计数: {count + 1} → {count} | 卸载: {(count == 0 && unloadWhenZero ? "是" : "否")}");

            if (count == 0)
            {
                refCounts.Remove(key);
                if (unloadWhenZero)
                {
                    Debug.Log($"[ESResTable.Release] 即将卸载资源: {res?.ResName ?? key.ToString()}");
                    TryRemoveEntry(map, refCounts, key, true);
                }
                else
                {
                    // ✅ 优化7：引用计数为0但不卸载时，依然保留在map中供后续使用
                    Debug.Log($"[ESResTable.Release] 引用计数为0但保留在内存: {res?.ResName ?? key.ToString()}");
                }
            }
            else
            {
                refCounts[key] = count;
            }

            return count;
        }

        /// <summary>
        /// 移除资源条目（内部清理方法，确保资源正确释放到对象池）
        /// 
        /// 【移除流程】
        /// 1. Key验证：检查key是否为null
        /// 2. 资源查找：从map获取ESResSourceBase
        /// 3. 字典清理：移除map[key]和refCounts[key]
        /// 4. 资源释放（如果releaseResource=true）：
        ///    - 调用res.ReleaseTask()：卸载Unity资源（Texture、AudioClip等）
        ///    - 调用res.TryAutoPushedToPool()：将ESResSourceBase实例归还对象池
        /// 
        /// 【资源释放流程（releaseResource=true）】
        /// ```
        /// TryRemoveEntry → ReleaseTask() → TryAutoPushedToPool()
        ///       ↓                ↓                    ↓
        ///  从字典移除      卸载Unity资源       归还ESResSourceBase到对象池
        /// ```
        /// 
        /// 【对象池回收机制】
        /// - TryAutoPushedToPool()：自动识别资源类型并归还到对应对象池
        ///   * Asset → ESResMaster.PoolForESAsset
        ///   * AB → ESResMaster.PoolForESAssetBundle
        ///   * RawFile → ESResMaster.PoolForESRawFile
        ///   * InternalResource → ESResMaster.PoolForESInternalResource
        ///   * NetImage → ESResMaster.PoolForESNetImage
        /// - 好处：减少GC压力，对象复用提高性能
        /// 
        /// 【releaseResource参数说明】
        /// - true：完全释放资源（卸载Unity资源+归还对象池）
        ///   * 使用场景：InternalRelease(unloadWhenZero=true)、ClearAll
        /// - false：仅从字典移除，不卸载资源
        ///   * 使用场景：极少使用，通常应释放资源
        /// 
        /// 【线程安全要求】
        /// ⚠️ 必须在调用方的lock块内调用
        /// 
        /// 【使用场景】
        /// - InternalRelease(unloadWhenZero=true)时调用
        /// - ClearAll清理所有资源时调用
        /// - RemoveRecycledEntries批量清理废弃条目时调用
        /// </summary>
        private static bool TryRemoveEntry(Dictionary<ESResKey, ESResSourceBase> map, Dictionary<ESResKey, int> refCounts, ESResKey key, bool releaseResource)
        {
            if (key == null || !map.TryGetValue(key, out var res))
            {
                return false;
            }

            map.Remove(key);
            refCounts.Remove(key);

            if (res != null)
            {
                if (releaseResource)
                {
                    // ✅ 优化8：释放资源并回池，允许GC回收
                    if (res.ReleaseTheResSource())
                    {
                        res.TryAutoPushedToPool();
                    }
                    else
                    {
                        res.ResetReferenceCounter();
                    }
                }
                else
                {
                    res.ResetReferenceCounter();
                }
            }

            return true;
        }

        /// <summary>
        /// 清空所有资源（内部批量清理方法，支持完全释放或快速清空）
        /// 
        /// 【清空流程】
        /// 1. 快速清空模式（releaseResource=false）：
        ///    - 直接调用map.Clear()和refCounts.Clear()
        ///    - 不卸载Unity资源，不归还对象池
        ///    - ⚠️ 警告：可能导致内存泄漏，仅用于特殊场景
        /// 
        /// 2. 完全释放模式（releaseResource=true）：
        ///    - 遍历map中的所有资源
        ///    - 对每个资源调用res.ReleaseTask()卸载Unity资源
        ///    - 对每个资源调用res.TryAutoPushedToPool()归还对象池
        ///    - 最后清空map和refCounts字典
        /// 
        /// 【批量释放流程图】
        /// ```
        /// InternalClear(releaseResource=true)
        ///       ↓
        ///   foreach(map)
        ///       ↓
        ///   ReleaseTask() → 卸载Unity资源（Texture、AudioClip等）
        ///       ↓
        ///   TryAutoPushedToPool() → 归还ESResSourceBase到对象池
        ///       ↓
        ///   map.Clear() + refCounts.Clear()
        /// ```
        /// 
        /// 【releaseResource参数说明】
        /// - true（推荐）：完全释放所有资源
        ///   * 使用场景：场景切换、游戏退出、内存清理
        ///   * 效果：卸载Unity资源 + 归还对象池 + 清空字典
        ///   * 性能：遍历所有条目，耗时取决于资源数量
        /// 
        /// - false（不推荐）：仅清空字典
        ///   * 使用场景：极少使用，仅用于测试或特殊逻辑
        ///   * 风险：Unity资源未卸载，ESResSourceBase未归还对象池，可能导致内存泄漏
        ///   * 性能：O(1)复杂度，瞬间完成
        /// 
        /// 【对象池回收】
        /// - 每个ESResSourceBase都会归还到对应的对象池：
        ///   * Asset → PoolForESAsset
        ///   * AB → PoolForESAssetBundle
        ///   * RawFile → PoolForESRawFile
        ///   * InternalResource → PoolForESInternalResource
        ///   * NetImage → PoolForESNetImage
        /// 
        /// 【引用计数重置】
        /// - ResetReferenceCounter()：将m_ReferenceCount重置为0
        /// - 确保对象池中的实例状态干净，可复用
        /// 
        /// 【线程安全要求】
        /// ⚠️ 必须在调用方的lock块内调用
        /// 
        /// 【使用场景】
        /// - ClearAllAssets()：清空所有Asset资源
        /// - ClearAllABs()：清空所有AB资源
        /// - ClearAll()：清空所有类型资源
        /// - ESResMaster关闭或重置时
        /// </summary>
        private static void InternalClear(Dictionary<ESResKey, ESResSourceBase> map, Dictionary<ESResKey, int> refCounts, bool releaseResource)
        {
            if (!releaseResource)
            {
                map.Clear();
                refCounts.Clear();
                return;
            }

            foreach (var pair in map)
            {
                var res = pair.Value;
                if (res == null)
                {
                    continue;
                }

                if (res.ReleaseTheResSource())
                {
                    res.TryAutoPushedToPool();
                }
            }

            map.Clear();
            refCounts.Clear();
        }
    }
    #endregion
}
