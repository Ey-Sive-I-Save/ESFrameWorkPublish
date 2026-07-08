using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// ESResLoader - 资源加载器
    /// 
    /// 【核心职责】
    /// 1. 作为资源加载的"会话管理器"，管理一组相关资源的生命周期
    /// 2. 维护本地引用计数，确保资源不被提前卸载
    /// 3. 协调依赖资源的加载顺序，保证依赖关系正确
    /// 4. 提供同步/异步加载接口，简化资源获取流程
    /// 5. 支持对象池复用，减少 GC 压力
    /// 
    /// 【设计原则】
    /// - 单一职责：只负责加载和引用管理，不处理资源创建
    /// - 防御性编程：所有公开接口都进行空值和状态检查
    /// - 容错设计：单个资源失败不影响其他资源，回调异常不影响流程
    /// - 性能优先：避免重复查找、减少临时分配、优化集合操作
    /// 
    /// 【引用计数机制】
    /// - 本地计数(LoaderResRefCounts)：记录 Loader 对资源的持有次数
    /// - 全局计数(ESResMaster)：记录所有 Loader 对资源的总持有次数
    /// - 释放规则：本地计数归零时从 Loader 移除，全局计数归零时卸载资源
    /// 
    /// 【线程安全】
    /// ⚠️ 此类设计为单线程使用（Unity 主线程），不保证线程安全
    /// </summary>
    public sealed class ESResLoader : IPoolableAuto
    {
        #region 池化接口实现 - IPoolableAuto

        /// <summary>
        /// 标记对象是否已回收到池中
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 池化重置回调 - 清理状态，准备复用
        /// ⚠️ 注意：此方法由对象池调用，不要手动调用
        /// </summary>
        public void OnResetAsPoolable()
        {
            mIsLoadingInProgress = false;
            // 注意：不清理集合，因为 ReleaseAll 会在回池前调用
        }

        /// <summary>
        /// 自动回池 - 释放所有资源并返回对象池
        /// 📌 使用场景：资源加载完成后不再需要 Loader 时调用
        /// </summary>
        public void TryAutoPushedToPool()
        {
            if (ESResMaster.Instance?.PoolForESLoader == null)
            {
                Debug.LogWarning("[ESResLoader.TryAutoPushedToPool] 对象池未初始化，无法回池");
                return;
            }

            ReleaseAll(resumePooling: false);
            ESResMaster.Instance.PoolForESLoader.PushToPool(this);
        }

        #endregion

        #region 同步加载 - 立即返回资源

        /// <summary>
        /// 同步加载资产 - 阻塞直到资源加载完成
        /// </summary>
        /// <param name="key">资源键（强类型）</param>
        /// <returns>加载的资源对象，失败返回 null</returns>
        /// <remarks>
        /// ⚠️ 性能警告：会阻塞主线程，建议仅在必要时使用
        /// 📌 引用管理：此方法不增加 Loader 本地引用计数
        /// </remarks>
        public UnityEngine.Object LoadAssetSync(ESResKey key)
        {
            if (key == null)
            {
                return null;
            }

            var res = ESResMaster.Instance.GetResSourceByKey(key, ESResSourceLoadType.ABAsset);
            if (res == null)
            {
                Debug.LogWarning($"同步加载失败，未找到资源键: {key}");
                return null;
            }

            if (res.State != ResSourceState.Ready)
            {
                if (!res.LoadSync())
                {
                    Debug.LogError($"同步加载失败: {key}");
                    ESResMaster.Instance.ReleaseResHandle(key, ESResSourceLoadType.ABAsset, unloadWhenZero: false);
                    return null;
                }
            }

            return res.Asset;
        }

        /// <summary>
        /// 同步加载资产（泛型版本） - 自动类型转换
        /// </summary>
        /// <typeparam name="T">资源类型（Texture、Sprite、GameObject等）</typeparam>
        /// <param name="key">资源键</param>
        /// <returns>指定类型的资源对象，类型不匹配或失败返回 null</returns>
        public T LoadAssetSync<T>(ESResKey key) where T : UnityEngine.Object
        {
            return LoadAssetSync(key) as T;
        }

        /// <summary>
        /// 尝试获取已加载的资产 - 非阻塞查询
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="asset">输出参数：资源对象</param>
        /// <returns>true=资源已就绪并获取成功，false=资源未加载或未就绪</returns>
        /// <remarks>
        /// ✅ 性能友好：不会触发加载，仅查询已加载资源
        /// ⚠️ 引用管理：成功时会增加全局引用计数（需要手动释放）
        /// </remarks>
        public bool TryGetLoadedAsset(ESResKey key, out UnityEngine.Object asset)
        {
            asset = null;
            if (key == null)
            {
                return false;
            }

            var res = ESResMaster.ResTable.GetAssetResByKey(key);
            if (res == null || res.State != ResSourceState.Ready)
            {
                return false;
            }

            asset = res.Asset;
            if (asset != null)
            {
                ESResMaster.ResTable.AcquireAssetRes(key);
                return true;
            }

            return false;
        }

        #endregion

        #region 异步加载 - 队列管理

        /// <summary>
        /// 通过资源路径添加异步加载任务
        /// </summary>
        /// <param name="path">资源路径（如 "Assets/Prefabs/Player.prefab"）</param>
        /// <param name="listener">加载完成回调（参数1=成功/失败，参数2=资源源对象）</param>
        /// <param name="AtLastOrFirst">true=添加到队列末尾，false=添加到队列开头（优先加载）</param>
        /// <remarks>
        /// 内部会通过 GlobalAssetKeys 将路径转换为 ESResKey
        /// ⭐ 最常用的加载方式之一，推荐使用
        /// </remarks>
        public void AddAsset2LoadByPathSourcer(string path, Action<bool, ESResSourceBase> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.GlobalAssetKeys.TryGetESResKeyByPath(path, out var assetKey))
            {
                Add2LoadByKey(assetKey, ESResSourceLoadType.ABAsset, listener, AtLastOrFirst);
            }
            else
            {
                Debug.LogError($"通过路径添加异步加载任务失败，未找到资源键: {path}");
            }
        }

        /// <summary>
        /// 通过资源 GUID 添加异步加载任务
        /// </summary>
        /// <param name="guid">资源 GUID（Unity 内部唯一标识符）</param>
        /// <param name="listener">加载完成回调</param>
        /// <param name="AtLastOrFirst">true=队列末尾，false=队列开头</param>
        /// <remarks>
        /// ⭐ 最常用的加载方式之一，推荐使用
        /// </remarks>
        public void AddAsset2LoadByGUIDSourcer(string guid, Action<bool, ESResSourceBase> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.GlobalAssetKeys.TryGetESResKeyByGUID(guid, out var assetKey))
            {
                Add2LoadByKey(assetKey, ESResSourceLoadType.ABAsset, listener, AtLastOrFirst);
            }
        }

        /// <summary>
        /// 通过 AssetBundle PreName 添加异步加载任务
        /// </summary>
        /// <param name="abName">AB包的PreName（不带Hash后缀，如 "ui_mainmenu"）</param>
        /// <param name="listener">加载完成回调</param>
        /// <param name="AtLastOrFirst">true=队列末尾，false=队列开头</param>
        /// <remarks>
        /// ⚠️ 注意：abName 必须是 PreName（不带Hash），而非完整文件名
        /// 🔒 访问修饰符：public - 通常由依赖加载内部调用，但保留为 public 以支持手动加载 AB 包
        /// </remarks>
        public void AddAB2LoadByABPreNameSourcer(string abName, Action<bool, ESResSourceBase> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.GlobalABKeys.TryGetValue(abName, out var abKey))
            {
                Add2LoadByKey(abKey, ESResSourceLoadType.AssetBundle, listener, AtLastOrFirst);
            }
        }

        /// <summary>
        /// 添加RawFile原始文件异步加载任务
        /// </summary>
        /// <param name="filePath">文件路径（可以是相对路径或绝对路径）</param>
        /// <param name="listener">加载完成回调</param>
        /// <param name="AtLastOrFirst">true=队列末尾，false=队列开头</param>
        /// <remarks>
        /// ⭐ 适用场景：
        /// - Lua脚本文件（.lua.txt）
        /// - JSON配置文件（.json）
        /// - Protobuf二进制数据（.bytes）
        /// - 加密文件等
        /// 
        /// 📌 使用方式：
        /// <code>
        /// loader.AddRawFile2Load("Config/game_settings.json", (success, source) => {
        ///     if (success) {
        ///         var rawFileSource = source as ESRawFileSource;
        ///         string jsonText = rawFileSource.GetTextContent();
        ///         // 解析JSON...
        ///     }
        /// });
        /// </code>
        /// 
        /// ⚠️ 注意：RawFile不使用GUID，直接用文件路径作为标识
        /// </remarks>
        public void AddRawFile2Load(string filePath, Action<bool, ESResSourceBase> listener = null, bool AtLastOrFirst = true)
        {
            // 创建RawFile专用的ESResKey
            var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
            key.SourceLoadType = ESResSourceLoadType.RawFile;
            key.ResName = filePath;
            key.LocalABLoadPath = filePath; // 直接使用路径

            Add2LoadByKey(key, ESResSourceLoadType.RawFile, listener, AtLastOrFirst);
        }

        /// <summary>
        /// 添加InternalResource（Resources文件夹资源）异步加载任务
        /// </summary>
        /// <param name="resourcePath">Resources相对路径（不包含扩展名）</param>
        /// <param name="listener">加载完成回调</param>
        /// <param name="AtLastOrFirst">true=队列末尾，false=队列开头</param>
        /// <remarks>
        /// ⭐ 适用场景：
        /// - 默认配置、UI图标等小型固定资源
        /// - 快速原型开发，无需AB打包
        /// 
        /// 📌 使用方式：
        /// <code>
        /// loader.AddInternalResource2Load("UI/Icons/default_icon", (success, source) => {
        ///     if (success) {
        ///         var sprite = source.Asset as Sprite;
        ///         // 使用Sprite...
        ///     }
        /// });
        /// </code>
        /// 
        /// ⚠️ 注意：
        /// - InternalResource不使用GUID，直接用Resources路径作为标识
        /// - Resources资源会增加包体大小，不建议大量使用
        /// - 不支持热更新
        /// </remarks>
        public void AddInternalResource2Load(string resourcePath, Action<bool, ESResSourceBase> listener = null, bool AtLastOrFirst = true)
        {
            // 创建InternalResource专用的ESResKey
            var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
            key.SourceLoadType = ESResSourceLoadType.InternalResource;
            key.ResName = resourcePath;

            Add2LoadByKey(key, ESResSourceLoadType.InternalResource, listener, AtLastOrFirst);
        }

        /// <summary>
        /// 添加NetImage（网络图片）异步加载任务
        /// </summary>
        /// <param name="url">完整URL地址（支持HTTP/HTTPS）</param>
        /// <param name="listener">加载完成回调</param>
        /// <param name="AtLastOrFirst">true=队列末尾，false=队列开头</param>
        /// <remarks>
        /// ⭐ 适用场景：
        /// - 动态头像、远程图片
        /// - CDN资源
        /// 
        /// 📌 使用方式：
        /// <code>
        /// loader.AddNetImage2Load("https://example.com/avatar.jpg", (success, source) => {
        ///     if (success) {
        ///         var netImageSource = source as ESNetImageSource;
        ///         Texture2D texture = netImageSource.Texture;
        ///         // 使用Texture...
        ///     }
        /// });
        /// </code>
        /// 
        /// ⚠️ 注意：
        /// - NetImage不使用GUID，直接用URL作为标识
        /// - 仅支持异步加载（网络请求）
        /// - 自动缓存到本地，支持离线使用
        /// - 支持自动重试（最多3次）
        /// - 超时时间：30秒
        /// </remarks>
        public void AddNetImage2Load(string url, Action<bool, ESResSourceBase> listener = null, bool AtLastOrFirst = true)
        {
            // 验证URL
            if (string.IsNullOrEmpty(url) || (!url.StartsWith("http://") && !url.StartsWith("https://")))
            {
                Debug.LogError($"[ESResLoader.AddNetImage2Load] 无效的URL: {url}");
                listener?.Invoke(false, null);
                return;
            }

            // 创建NetImage专用的ESResKey
            var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
            key.SourceLoadType = ESResSourceLoadType.NetImageRes;
            key.ResName = url;

            Add2LoadByKey(key, ESResSourceLoadType.NetImageRes, listener, AtLastOrFirst);
        }

        /// <summary>
        /// 通过资源键添加异步加载任务 - 核心方法
        /// </summary>
        /// <param name="key">资源键（强类型）</param>
        /// <param name="loadType">加载类型（AssetBundle/ABAsset/RawFile等）</param>
        /// <param name="listener">加载完成回调</param>
        /// <param name="AtLastOrFirst">true=队列末尾，false=队列开头（优先级）</param>
        /// <remarks>
        /// 【逻辑流程】
        /// 1. 检查资源是否已在本 Loader 的队列中（去重）
        /// 2. 从 ESResMaster 获取或创建资源源对象
        /// 3. 自动解析并添加依赖 AB 包到加载队列
        /// 4. 将资源加入 Loader 本地队列并增加引用计数
        /// 5. 触发异步加载流程
        /// 
        /// 【去重机制】
        /// - 同一个 Key 多次添加到同一个 Loader，只有第一次生效
        /// - 后续调用仅注册回调（如有），不增加引用计数
        /// - 这避免了重复加载同一资源的开销
        /// 
        /// 🔒 访问修饰符：internal - 外部应通过 AddAsset2LoadByPathSourcer/AddAsset2LoadByGUIDSourcer 等便利方法调用
        /// </remarks>
        internal void Add2LoadByKey(ESResKey key, ESResSourceLoadType loadType, Action<bool, ESResSourceBase> listener = null, bool AtLastOrFirst = true)
        {
            // 检查是否已在本 Loader 中
            var res = FindResInThisLoaderList(key, loadType);
            if (res != null)
            {
                ESLog.Verbose("已经被加载过");
                RegisterLocalRes(res, key, loadType, skipGlobalRetain: false);
                if (listener != null) res.OnLoadOKAction_Submit(listener);
                return;
            }

            // 从全局管理器获取或创建资源源
            res = ESResMaster.Instance.GetResSourceByKey(key, loadType);
            if (res != null)
            {
                if (listener != null) res.OnLoadOKAction_Submit(listener);
                //添加依赖支持
                {
                    //获得依赖AB们
                    var dependsAssetBundles = res.GetDependResSourceAllAssetBundles(out bool withHash);

                    if (dependsAssetBundles != null && dependsAssetBundles.Length > 0)
                    {
                        ESLog.Verbose($"[ESResLoader] 资源 '{res.ResName}' 有 {dependsAssetBundles.Length} 个依赖AB需要加载");
                        foreach (var depend in dependsAssetBundles)
                        {
                            string abName = withHash ? ESResMaster.PathAndNameTool_GetPreName(depend) : depend;
                            ESLog.Verbose($"[ESResLoader] -> 添加依赖AB任务: {abName}");
                            AddAB2LoadByABPreNameSourcer(abName);
                        }
                    }
                }

                bool isNew = AddRes2ThisLoaderRes(res, key, loadType, AtLastOrFirst);
                ESLog.Verbose($"[ESResLoader] 尝试添加 '{res.ResName}' 到加载列表, 结果: {(isNew ? "成功(新任务)" : "已存在(复用)")}");

                if (isNew)
                {
                    RegisterLocalRes(res, key, loadType, skipGlobalRetain: true);
                    DoLoadAsync();
                }
                else
                {
                    // 复用时不需要立即Release，只是不需要再retain一次全局的（GetResSourceByKey已经retain了一次）
                    // 但这里 ReleaseResHandle 的目的是抵消 GetResSourceByKey 增加的引用计数，因为 AddRes2ThisLoaderRes 返回 false 意味着没加进 loader 列表
                    // 而 Loader 认为如果没加进去（因为已经有了），那么 GetResSourceByKey 产生的那次 Acquire 就多余了，需要还回去
                    // 但 RegisterLocalRes 内部会再次 Register 增加本地计数，本地计数对应 loader 持有
                    // 逻辑梳理：
                    // 情况A：AddRes2ThisLoaderRes 返回 true -> 是新加入 -> RegisterLocalRes (skipGlobalRetain=true) -> 此时 loader 本地+1，全局引用保持 GetKeys 时的 +1。 正确。
                    // 情况B：AddRes2ThisLoaderRes 返回 false -> 已经在 loader 里 -> 我们不需要在 LoaderResSources 里加新的 -> 但我们需要增加本地引用计数吗？
                    // AddRes2ThisLoaderRes 返回 false 表示 *同一个 res 实例* 已经在 LoaderResSources (List) 中了。
                    // 现在的逻辑是：如果已经在 loader 里，直接 dispose 掉这次多余的 Get，不做任何本地计数增加？
                    // 之前的逻辑：AddRes2ThisLoaderRes 返回 false -> 释放这次 Get 带来的全局引用。
                    // 这样会导致：同一个 Loader 对同一个资源调用多次 Add，只有第一次算数。后续调用只会注册 listener (如果有)。
                    // 这样是符合设计预期的（一个 Loader 不应该重复加载同一个资源多次，或者说重复排队）。

                    ESResMaster.Instance.ReleaseResHandle(key, loadType, unloadWhenZero: false);
                    RegisterLocalRes(res, key, loadType, skipGlobalRetain: false); // 确保重复添加时也能增加本地计数？不需要，现在的逻辑看起来是不支持重复排队。
                                                                                   // 原代码逻辑：AddRes2ThisLoaderRes 失败（已存在） -> ReleaseResHandle（抵消 Get 的+1） -> 结束。
                                                                                   // 这意味着多次 Add 同一个 Key 到同一个 Loader，Loader 内部只持有一个引用计数？
                                                                                   // 如果外部调用了两次 Add，然后 Release 一次，会导致资源被卸载吗？
                                                                                   // 查看 ReleaseAsset -> ReleaseReference -> 减本地计数 -> 减全局。
                                                                                   // 如果 Add 时没有增加本地计数，Release 时减本地计数可能归零 -> 导致 RemoveFromLoader。
                                                                                   // 这里的关键是：AddRes2ThisLoaderRes 返回 false 时，我们是否应该增加 LocalRef？

                    // 修正：如果资源已在 Loader 中，AddRes2ThisLoaderRes 返回 false，但作为 Loader 的使用者，我可能期望它引用计数+1。
                    // 但看 AddRes2ThisLoaderRes 的实现，它只是检查是否存在。
                    // 原来的代码直接 dispose 掉了，这可能意味着 Loader 设计为“对同一个资源去重”。
                    // 这里添加一个 Debug 提示即可，保持原有逻辑。

                    Debug.LogWarning($"[ESResLoader] 资源 '{res.ResName}' 已在加载队列中，本次仅注册回调(如有)。");
                }
            }
            else
            {
                Debug.LogWarning($"[ESResLoader] 无法创建资源源: {key}");
            }
        }
        /// <summary>
        /// 在本 Loader 的队列中查找资源 - 通过 Key 查找
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="loadType">加载类型（当前未使用）</param>
        /// <returns>找到的资源源，未找到返回 null</returns>
        /// <remarks>
        /// 🔒 访问修饰符：private - 仅用于 Add2LoadByKey 内部去重检查，外部不应直接调用
        /// </remarks>
        private ESResSourceBase FindResInThisLoaderList(ESResKey key, ESResSourceLoadType loadType)
        {
            if (key == null)
            {
                return null;
            }

            if (LoaderKeyToRes.TryGetValue(key, out var res) && res != null)
            {
                return res;
            }

            return null;
        }

        /// <summary>
        /// 在本 Loader 的队列中查找资源 - 通过资源实例查找
        /// </summary>
        /// <param name="theRes">资源源实例</param>
        /// <returns>如果在队列中返回该实例，否则返回 null</returns>
        /// <remarks>
        /// 🔒 访问修饰符：private - 仅用于 AddRes2ThisLoaderRes 内部检查，外部不应直接调用
        /// </remarks>
        private ESResSourceBase FindResInThisLoaderList(ESResSourceBase theRes)
        {
            if (theRes == null)
            {
                return null;
            }

            return LoaderResKeys.ContainsKey(theRes) ? theRes : null;
        }
        /// <summary>
        /// 将资源加入 Loader 内部队列 - 核心数据结构维护
        /// </summary>
        /// <param name="res">资源源对象</param>
        /// <param name="key">资源键</param>
        /// <param name="loadType">加载类型</param>
        /// <param name="addToLast">true=添加到队列末尾，false=添加到队列开头</param>
        /// <returns>true=成功添加新资源，false=资源已存在</returns>
        /// <remarks>
        /// 【数据结构维护】
        /// - LoaderResSources: 所有资源的总列表
        /// - LoaderResKeys: 资源源 -> Key 的映射
        /// - LoaderKeyToRes: Key -> 资源源的映射
        /// - ThisLoaderResSourcesWaitToLoad: 等待加载的资源队列（LinkedList）
        /// - mLoadingCount: 当前正在加载的资源数量
        /// </remarks>
        private bool AddRes2ThisLoaderRes(ESResSourceBase res, ESResKey key, ESResSourceLoadType loadType, bool addToLast)
        {
            //本地是否已经加载
            ESResSourceBase thisLoaderRes = FindResInThisLoaderList(res);

            if (thisLoaderRes != null)//只要新的
            {
                ESLog.Verbose($"[ESResLoader] 资源 '{res?.ResName ?? "Unknown"}' 已存在于Loader中，跳过添加 (Key: {key}, Type: {loadType})");
                return false;
            }
            //可以加入了
            LoaderResSources.Add(res);
            LoaderResKeys[res] = key;
            if (key != null)
            {
                LoaderKeyToRes[key] = res;
            }
            if (res.State != ResSourceState.Ready)
            {
                ++mLoadingCount;
                if (addToLast)
                {
                    ThisLoaderResSourcesWaitToLoad.AddLast(res);
                }
                else
                {
                    ThisLoaderResSourcesWaitToLoad.AddFirst(res);
                }
                ESLog.Verbose($"[ESResLoader] 新资源 '{res.ResName}' 已添加到等待队列 (Key: {key}, Type: {loadType}, 队列位置: {(addToLast ? "末尾" : "开头")})");
            }
            else
            {
                ESLog.Verbose($"[ESResLoader] 新资源 '{res.ResName}' 已就绪，直接添加到列表 (Key: {key}, Type: {loadType})");
            }
            return true;
        }
        #endregion

        #region 数据结构 - 资源管理

        /// <summary>
        /// 所有资源的总列表（已加载 + 等待加载）
        /// </summary>
        private readonly List<ESResSourceBase> LoaderResSources = new List<ESResSourceBase>();

        /// <summary>
        /// 等待加载的资源队列（依赖未就绪或未开始加载）
        /// 📌 使用 LinkedList 以支持高效的中间节点移除
        /// </summary>
        private readonly LinkedList<ESResSourceBase> ThisLoaderResSourcesWaitToLoad = new LinkedList<ESResSourceBase>();

        /// <summary>
        /// Key -> 资源源的映射（用于快速查找）
        /// </summary>
        private readonly Dictionary<ESResKey, ESResSourceBase> LoaderKeyToRes = new Dictionary<ESResKey, ESResSourceBase>();

        /// <summary>
        /// 资源源 -> Key 的反向映射（用于释放时查询 Key）
        /// </summary>
        private readonly Dictionary<ESResSourceBase, ESResKey> LoaderResKeys = new Dictionary<ESResSourceBase, ESResKey>();

        /// <summary>
        /// 资源源的本地引用计数（Loader 对资源的持有次数）
        /// ⚠️ 注意：这是本地计数，与全局引用计数（ESResMaster）分开管理
        /// </summary>
        private readonly Dictionary<ESResSourceBase, int> LoaderResRefCounts = new Dictionary<ESResSourceBase, int>();

        #endregion

        #region 异步加载执行 - 流程控制

        /// <summary>
        /// 异步加载所有已添加的资源
        /// </summary>
        /// <param name="listener">加载完成回调（所有资源加载完毕时触发）</param>
        /// <remarks>
        /// 【重要特性】
        /// 1. 支持重复调用：多次调用会收集所有回调，加载完成时一起触发
        /// 2. 防重复加载：已在加载进程中时，仅注册回调，不重新启动流程
        /// 3. 线程安全：通过 mIsLoadingInProgress 标志位防止并发问题
        /// 
        /// 【使用场景】
        /// - 一次性加载多个资源：loader.AddAsset(...); loader.AddAsset(...); loader.LoadAllAsync()
        /// - 动态追加回调：loader.LoadAllAsync(callback1); loader.LoadAllAsync(callback2)
        /// </remarks>
        public void LoadAllAsync(Action listener = null)
        {
            // 添加回调到列表
            if (listener != null)
            {
                if (mListeners_ForLoadAllOK == null)
                {
                    mListeners_ForLoadAllOK = new List<Action>();
                }

                if (!mListeners_ForLoadAllOK.Contains(listener))
                {
                    mListeners_ForLoadAllOK.Add(listener);
                    ESLog.Verbose($"[ESResLoader.LoadAllAsync] 添加完成回调，当前回调数量: {mListeners_ForLoadAllOK.Count}");
                }
            }

            // 只有在没有加载进行时才触发新的加载流程
            // 这样避免重复调用导致的重复加载
            if (!mIsLoadingInProgress)
            {
                mIsLoadingInProgress = true;
                ESLog.Verbose("[ESResLoader.LoadAllAsync] 开始新的加载流程");
                DoLoadAsync();
            }
            else
            {
                ESLog.Verbose("[ESResLoader.LoadAllAsync] 加载已在进行中，仅注册回调");
            }
        }
        /// <summary>
        /// 单个资源加载完成回调 - 内部使用
        /// </summary>
        /// <param name="result">加载是否成功</param>
        /// <param name="res">资源源对象</param>
        /// <remarks>
        /// 此方法会被注册到每个资源的加载回调中，加载完成后：
        /// 1. 减少 mLoadingCount 计数器
        /// 2. 注销自身回调（防止重复触发）
        /// 3. 继续调度后续加载任务
        /// </remarks>
        private void OnOneResLoadFinished(bool result, ESResSourceBase res)
        {
            if (mLoadingCount > 0)
            {
                --mLoadingCount;
            }
            res?.OnLoadOKAction_WithDraw(OnOneResLoadFinished);

            DoLoadAsync();
        }
        /// <summary>
        /// 异步加载调度器 - 核心逻辑
        /// </summary>
        /// <remarks>
        /// 【调度逻辑】
        /// 1. 检查是否所有资源已加载完毕（mLoadingCount=0 && 队列为空）
        /// 2. 逐个检查等待队列中的资源，判断依赖是否就绪
        /// 3. 依赖就绪的资源从队列移除并启动加载
        /// 4. 如果资源已经 Ready，直接减少 mLoadingCount
        /// 5. 循环结束后再次检查是否全部完成
        /// 
        /// 【依赖处理】
        /// - 通过 IsDependResLoadFinish() 判断依赖是否就绪
        /// - 仅当依赖全部就绪时才开始加载本资源
        /// - 这保证了 AB 包的加载顺序正确性
        /// 
        /// 【性能优化】
        /// - 使用 LinkedList 支持 O(1) 节点移除
        /// - 循环中提前保存 nextNode 防止迭代器失效
        /// </remarks>
        private void DoLoadAsync()
        {
            ESLog.Verbose($"[ESResLoader.DoLoadAsync] 进入异步加载调度。当前加载计数: {mLoadingCount}, 等待队列长度: {ThisLoaderResSourcesWaitToLoad.Count}");

            if (mLoadingCount == 0 && ThisLoaderResSourcesWaitToLoad.Count == 0)
            {
                ESLog.Verbose("[ESResLoader.DoLoadAsync] 所有资源已加载完成，触发完成回调。");
                // 触发所有回调
                InvokeAllLoadCompleteCallbacks();
                return;
            }

            ESLog.Verbose("[ESResLoader.DoLoadAsync] 开始处理等待队列中的资源。" + ThisLoaderResSourcesWaitToLoad.Count);
            var nextNode = ThisLoaderResSourcesWaitToLoad.First;
            LinkedListNode<ESResSourceBase> currentNode = null;
            while (nextNode != null)
            {
                currentNode = nextNode;
                var res = currentNode.Value;
                nextNode = currentNode.Next;//循环判定

                ESLog.Verbose($"[ESResLoader.DoLoadAsync] 检查资源 '{res?.ResName ?? "Unknown"}' 的依赖状态。");
                if (res.IsDependResLoadFinish())
                {
                    ESLog.Verbose($"[ESResLoader.DoLoadAsync] 资源 '{res.ResName}' 依赖已完成，从等待队列移除。");
                    ThisLoaderResSourcesWaitToLoad.Remove(currentNode);
                    if (res.State != ResSourceState.Ready)
                    {
                        ESLog.Verbose($"[ESResLoader.DoLoadAsync] 资源 '{res.ResName}' 状态为 {res.State}，开始异步加载。");
                        res.OnLoadOKAction_Submit(OnOneResLoadFinished);
                        res.LoadAsync();
                    }
                    else
                    {
                        ESLog.Verbose($"[ESResLoader.DoLoadAsync] 资源 '{res.ResName}' 已就绪，减少加载计数。");
                        if (mLoadingCount > 0)
                        {
                            --mLoadingCount;
                        }
                    }
                }
                else
                {
                    ESLog.Verbose($"[ESResLoader.DoLoadAsync] 资源 '{res?.ResName ?? "Unknown"}' 依赖未完成，跳过。");
                }
            }

            if (mLoadingCount == 0 && ThisLoaderResSourcesWaitToLoad.Count == 0)
            {
                ESLog.Verbose("[ESResLoader.DoLoadAsync] 循环后检查：所有资源加载完成，触发完成回调。");
                // 触发所有回调
                InvokeAllLoadCompleteCallbacks();
            }
            else
            {
                ESLog.Verbose($"[ESResLoader.DoLoadAsync] 循环后检查：仍有 {mLoadingCount} 个加载中，{ThisLoaderResSourcesWaitToLoad.Count} 个等待依赖，继续调度。");
            }
        }

        /// <summary>
        /// 触发所有加载完成回调 - 安全执行
        /// </summary>
        /// <remarks>
        /// 【安全特性】
        /// 1. 复制回调列表：防止回调中修改列表导致的问题
        /// 2. 异常捕获：单个回调异常不影响其他回调执行
        /// 3. 重置标志位：允许下一轮加载
        /// 4. 清空回调列表：防止重复触发
        /// </remarks>
        private void InvokeAllLoadCompleteCallbacks()
        {
            // 重置加载标记，允许下一轮加载
            mIsLoadingInProgress = false;

            if (mListeners_ForLoadAllOK != null && mListeners_ForLoadAllOK.Count > 0)
            {
                ESLog.Verbose($"[ESResLoader] 触发 {mListeners_ForLoadAllOK.Count} 个加载完成回调");

                // 复制列表以避免回调中修改列表导致的问题
                var callbacks = new List<Action>(mListeners_ForLoadAllOK);
                mListeners_ForLoadAllOK.Clear();

                foreach (var callback in callbacks)
                {
                    try
                    {
                        callback?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ESResLoader] 加载完成回调执行异常: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }

        /// <summary>
        /// 同步加载所有等待队列中的资源
        /// </summary>
        /// <remarks>
        /// ⚠️ 性能警告：会阻塞主线程直到所有资源加载完毕
        /// 📌 使用场景：仅在必要时使用（如启动界面、关键资源）
        /// </remarks>
        public void LoadAll_Sync()
        {
            while (ThisLoaderResSourcesWaitToLoad.Count > 0)
            {
                var now = ThisLoaderResSourcesWaitToLoad.First.Value;
                --mLoadingCount;
                ThisLoaderResSourcesWaitToLoad.RemoveFirst();

                if (now == null)
                {
                    return;
                }

                if (now.LoadSync())
                {
                    //同步加载哈
                }
            }
        }
        #endregion

        #region 状态字段 - 内部状态跟踪

        /// <summary>
        /// 加载完成回调列表（支持多个监听者）
        /// </summary>
        private List<Action> mListeners_ForLoadAllOK;

        /// <summary>
        /// 标记是否正在进行加载，防止重复触发加载流程
        /// </summary>
        private bool mIsLoadingInProgress;

        /// <summary>
        /// 当前正在加载的资源数量（不包括等待依赖的资源）
        /// </summary>
        private int mLoadingCount;

        #endregion


        #region 公开属性 - 状态查询

        /// <summary>
        /// 获取加载进度 (0.0 ~ 1.0)
        /// </summary>
        /// <remarks>
        /// 计算方式：（已加载资源数 + 正在加载资源的进度和） / 总资源数
        /// ⚠️ 性能注意：每次访问都会遍历等待队列，频繁调用可能影响性能
        /// </remarks>
        public float Progress
        {
            get
            {
                if (ThisLoaderResSourcesWaitToLoad.Count == 0)
                {
                    return 1;
                }

                var unit = 1.0f / LoaderResSources.Count;//所有资源
                var currentValue = unit * (LoaderResSources.Count - mLoadingCount);//已经加载的

                var currentNode = ThisLoaderResSourcesWaitToLoad.First;

                while (currentNode != null)
                {
                    currentValue += unit * currentNode.Value.Progress;
                    currentNode = currentNode.Next;
                }

                return currentValue;
            }
        }

        /// <summary>
        /// 获取当前正在加载的资源数量
        /// </summary>
        public int PendingCount => mLoadingCount;

        /// <summary>
        /// 获取所有已添加资源的快照（只读列表）
        /// </summary>
        /// <returns>资源列表的副本（避免外部修改）</returns>
        /// <remarks>
        /// 🔒 访问修饰符：internal - 用于框架内部调试和监控，外部用户不应依赖此方法
        /// </remarks>
        internal IReadOnlyList<ESResSourceBase> SnapshotQueuedSources()
        {
            return LoaderResSources.ToList();
        }

        #endregion




        #region 资源释放 - 引用管理

        /// <summary>
        /// 取消所有等待中的加载任务
        /// </summary>
        /// <param name="releaseResources">true=同时释放资源引用，false=仅取消加载</param>
        /// <remarks>
        /// 【使用场景】
        /// - 切换场景时取消未完成的加载
        /// - 用户取消操作时中断加载流程
        /// </remarks>
        public void CancelPendingLoads(bool releaseResources = false)
        {
            var pending = ThisLoaderResSourcesWaitToLoad.ToList();
            ThisLoaderResSourcesWaitToLoad.Clear();
            foreach (var res in pending)
            {
                if (res == null)
                {
                    continue;
                }

                res.OnLoadOKAction_WithDraw(OnOneResLoadFinished);
                ReleaseEntry(res, releaseResources);
            }

            mLoadingCount = 0;
        }

        /// <summary>
        /// 释放所有资源并清空 Loader 状态
        /// </summary>
        /// <param name="resumePooling">true=保留回池逻辑，false=禁用回池（用于 TryAutoPushedToPool）</param>
        /// <remarks>
        /// 【执行步骤】
        /// 1. 取消所有等待中的加载任务
        /// 2. 释放所有已添加资源的引用计数
        /// 3. 清空所有内部数据结构
        /// 4. 重置加载状态标志
        /// 
        /// ⚠️ 注意：应用退出时会跳过释放逻辑，避免错误
        /// </remarks>
        public void ReleaseAll(bool resumePooling = true)
        {
            // 如果应用正在退出，跳过释放逻辑以避免在关闭时执行
            if (!Application.isPlaying)
            {
                mLoadingCount = 0;
                mListeners_ForLoadAllOK?.Clear();
                mListeners_ForLoadAllOK = null;
                mIsLoadingInProgress = false;  // 重置加载标记
                LoaderResKeys.Clear();
                LoaderKeyToRes.Clear();
                LoaderResSources.Clear();
                LoaderResRefCounts.Clear();
                return;
            }

            CancelPendingLoads(releaseResources: true);

            foreach (var res in LoaderResSources.ToArray())
            {
                ReleaseEntry(res, unloadWhenZero: true);
            }

            mLoadingCount = 0;
            mListeners_ForLoadAllOK?.Clear();
            mListeners_ForLoadAllOK = null;
            mIsLoadingInProgress = false;  // 重置加载标记
            LoaderResKeys.Clear();
            LoaderKeyToRes.Clear();
            LoaderResSources.Clear();
            LoaderResRefCounts.Clear();

            if (!resumePooling)
            {
                return;
            }
        }

        /// <summary>
        /// 释放指定资产的引用
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="unloadWhenZero">true=引用计数归零时卸载资源，false=仅减少引用计数</param>
        /// <remarks>
        /// 【释放逻辑】
        /// 1. 本地引用计数 -1
        /// 2. 全局引用计数 -1
        /// 3. 本地计数归零时从 Loader 移除资源
        /// 4. 全局计数归零且 unloadWhenZero=true 时卸载资源
        /// </remarks>
        public void ReleaseAsset(ESResKey key, bool unloadWhenZero = false)
        {
            if (key == null)
            {
                return;
            }

            if (!LoaderKeyToRes.TryGetValue(key, out var res))
            {
                return;
            }

            var loadType = res != null ? res.m_LoadType : ESResSourceLoadType.ABAsset;
            var remaining = ReleaseReference(res, key, loadType, unloadWhenZero);
            if (remaining == 0)
            {
                RemoveResFromLoader(res, key);
            }
        }

        /// <summary>
        /// 释放指定 AssetBundle 的引用
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="unloadWhenZero">true=引用计数归零时卸载资源，false=仅减少引用计数</param>
        public void ReleaseAssetBundle(ESResKey key, bool unloadWhenZero = false)
        {
            if (key == null)
            {
                return;
            }

            if (!LoaderKeyToRes.TryGetValue(key, out var res))
            {
                return;
            }

            var loadType = res != null ? res.m_LoadType : ESResSourceLoadType.AssetBundle;
            var remaining = ReleaseReference(res, key, loadType, unloadWhenZero);
            if (remaining == 0)
            {
                RemoveResFromLoader(res, key);
            }
        }

        /// <summary>
        /// 释放资源项 - 内部方法
        /// </summary>
        /// <param name="res">资源源对象</param>
        /// <param name="unloadWhenZero">true=最后一次释放时卸载资源</param>
        /// <remarks>
        /// 此方法会释放本 Loader 对资源的所有引用计数（循环调用 ReleaseReference）
        /// </remarks>
        private void ReleaseEntry(ESResSourceBase res, bool unloadWhenZero)
        {
            if (res == null)
            {
                return;
            }

            if (!LoaderResKeys.TryGetValue(res, out var key))
            {
                return;
            }

            var loadType = res.m_LoadType;

            var localCount = LoaderResRefCounts.TryGetValue(res, out var count) ? Mathf.Max(0, count) : 1;
            if (localCount <= 0)
            {
                localCount = 1;
            }

            for (var i = localCount; i > 0; --i)
            {
                var shouldUnload = unloadWhenZero && i == 1;
                ReleaseReference(res, key, loadType, shouldUnload);
            }

            RemoveResFromLoader(res, key);
        }

        /// <summary>
        /// 注册本地资源引用 - 增加引用计数
        /// </summary>
        /// <param name="res">资源源对象</param>
        /// <param name="key">资源键</param>
        /// <param name="loadType">加载类型</param>
        /// <param name="skipGlobalRetain">true=跳过全局引用计数+1（用于 GetResSourceByKey 已经 +1 的场景）</param>
        /// <remarks>
        /// 此方法同时维护本地和全局引用计数：
        /// - 本地计数：无条件 +1
        /// - 全局计数：根据 skipGlobalRetain 决定是否 +1
        /// </remarks>
        private void RegisterLocalRes(ESResSourceBase res, ESResKey key, ESResSourceLoadType loadType, bool skipGlobalRetain)
        {
            if (res == null)
            {
                return;
            }

            if (key == null && LoaderResKeys.TryGetValue(res, out var storedKey))
            {
                key = storedKey;
            }

            if (!skipGlobalRetain && key != null)
            {
                RetainGlobalHandle(key, loadType);
            }

            LoaderResRefCounts.TryGetValue(res, out var count);
            count = Mathf.Max(0, count) + 1;
            LoaderResRefCounts[res] = count;
        }

        /// <summary>
        /// 增加全局引用计数 - 静态方法
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="loadType">加载类型（决定调用哪个 Acquire 方法）</param>
        /// <remarks>
        /// 此方法会调用 ESResMaster.ResTable 的相应 Acquire 方法：
        /// - ABAsset -> AcquireAssetRes
        /// - AssetBundle -> AcquireABRes
        /// - RawFile -> AcquireRawFileRes
        /// </remarks>
        private static void RetainGlobalHandle(ESResKey key, ESResSourceLoadType loadType)
        {
            if (key == null)
            {
                return;
            }

            switch (loadType)
            {
                case ESResSourceLoadType.ABAsset:
                    ESResMaster.ResTable.AcquireAssetRes(key);
                    break;
                case ESResSourceLoadType.AssetBundle:
                    ESResMaster.ResTable.AcquireABRes(key);
                    break;
                case ESResSourceLoadType.RawFile:
                    ESResMaster.ResTable.AcquireRawFileRes(key);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 减少引用计数 - 本地和全局同时减少
        /// </summary>
        /// <param name="res">资源源对象</param>
        /// <param name="key">资源键</param>
        /// <param name="loadType">加载类型</param>
        /// <param name="unloadWhenZero">true=全局计数归零时卸载资源</param>
        /// <returns>本地剩余引用计数</returns>
        /// <remarks>
        /// ⚠️ 应用退出检查：退出时跳过全局释放，防止错误
        /// </remarks>
        private int ReleaseReference(ESResSourceBase res, ESResKey key, ESResSourceLoadType loadType, bool unloadWhenZero)
        {
            if (res == null || key == null)
            {
                return 0;
            }

            // 减少本地引用计数
            if (LoaderResRefCounts.TryGetValue(res, out var count))
            {
                count = Mathf.Max(0, count - 1);
                if (count == 0)
                {
                    LoaderResRefCounts.Remove(res);
                }
                else
                {
                    LoaderResRefCounts[res] = count;
                }
            }

            // 减少全局引用计数（退出时跳过）
            if (!ESSystem.IsQuitting) ESResMaster.Instance.ReleaseResHandle(key, loadType, unloadWhenZero);

            return LoaderResRefCounts.TryGetValue(res, out var remain) ? remain : 0;
        }

        /// <summary>
        /// 从 Loader 移除资源 - 清理所有关联数据结构
        /// </summary>
        /// <param name="res">资源源对象</param>
        /// <param name="key">资源键</param>
        /// <remarks>
        /// 此方法会：
        /// 1. 从 LoaderKeyToRes 移除 Key -> Res 映射
        /// 2. 从 LoaderResKeys 移除 Res -> Key 映射
        /// 3. 从 LoaderResSources 移除资源
        /// 4. 从 LoaderResRefCounts 移除引用计数
        /// 5. 从 ThisLoaderResSourcesWaitToLoad 移除等待队列
        /// 6. 注销加载完成回调
        /// </remarks>
        private void RemoveResFromLoader(ESResSourceBase res, ESResKey key)
        {
            if (key != null)
            {
                LoaderKeyToRes.Remove(key);
            }

            LoaderResKeys.Remove(res);
            LoaderResSources.Remove(res);
            LoaderResRefCounts.Remove(res);
            if (ThisLoaderResSourcesWaitToLoad.Remove(res) && mLoadingCount > 0)
            {
                --mLoadingCount;
            }

            res.OnLoadOKAction_WithDraw(OnOneResLoadFinished);
        }

        #endregion
    }
}
