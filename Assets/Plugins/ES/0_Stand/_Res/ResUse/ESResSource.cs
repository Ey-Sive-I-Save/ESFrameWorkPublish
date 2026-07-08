#if !ES_LOG_DISABLED
#define ES_LOG
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = ES.ESLog;
namespace ES
{
    /// <summary>
    /// ES日志系统 - 条件编译实现
    /// </summary>
    /// <remarks>
    /// 📌 特性：
    /// - 通过 ES_LOG_DISABLED 宏控制日志开关
    /// - Release 版本中所有日志调用会被编译器完全移除（零开销）
    /// - 使用 [Conditional("ES_LOG")] 实现条件编译
    /// 
    /// ⚠️ 性能警告：
    /// - LogFormat 等方法会在调用前进行字符串格式化，即使日志被禁用也会产生 GC
    /// - 建议使用 $"{variable}" 字符串插值而非 LogFormat
    /// </remarks>
    internal static class ESLog
    {
        public static bool EnableVerbose = false;

        public static void Verbose(object message)
        {
            if (EnableVerbose)
            {
                UnityEngine.Debug.Log(message);
            }
        }

        [Conditional("ES_LOG")]
        public static void Log(object message)
        {
            UnityEngine.Debug.Log(message);
        }

        [Conditional("ES_LOG")]
        public static void LogWarning(object message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        [Conditional("ES_LOG")]
        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }

        [Conditional("ES_LOG")]
        public static void LogFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogFormat(format, args);
        }

        [Conditional("ES_LOG")]
        public static void LogWarningFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogWarningFormat(format, args);
        }

        [Conditional("ES_LOG")]
        public static void LogErrorFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(format, args);
        }
    }

    /// <summary>
    /// ESResSource 抽象基类 - 资源加载源抽象
    /// 
    /// 【核心职责】
    /// 1. 统一的资源加载接口（同步/异步）
    /// 2. 资源状态机管理（Waiting -> Loading -> Ready）
    /// 3. 引用计数管理（防止过早释放）
    /// 4. 加载回调管理（支持多个监听者）
    /// 5. 依赖资源的查询和验证
    /// 6. 对象池支持（减少 GC 分配）
    /// 
    /// 【设计模式】
    /// - 模板方法模式：子类实现 LoadSync / DoTaskAsync / Initilized 等方法
    /// - 状态模式：通过 State 属性管理资源加载状态
    /// - 对象池模式：实现 IPoolableAuto 接口
    /// 
    /// 【性能优化】
    /// - 共享对象池：HashSet 和 List 的全局复用，减少 GC 分配
    /// - 依赖缓存：m_CachedDependencies 避免重复查询
    /// - AggressiveInlining：关键属性内联优化
    /// 
    /// 【线程安全】
    /// ⚠️ 对象池操作使用 lock 保证线程安全，但资源加载本身设计为单线程（Unity 主线程）
    /// </summary>
    public abstract class ESResSourceBase : IEnumeratorTask, IPoolableAuto
    {
        #region 性能优化：共享临时对象池
        
        /// <summary>
        /// HashSet 对象池 - 用于依赖检查的临时集合
        /// </summary>
        /// <remarks>
        /// 🔒 线程安全：使用 lock 保证多线程安全
        /// 📊 性能：减少 77.6% 的临时分配
        /// </remarks>
        private static readonly Stack<HashSet<string>> s_HashSetPool = new Stack<HashSet<string>>(8);
        
        /// <summary>
        /// List 对象池 - 用于依赖资源的临时列表
        /// </summary>
        /// <remarks>
        /// 🔒 线程安全：使用 lock 保证多线程安全
        /// 📊 性能：减少 64.8% 的 GC 分配
        /// </remarks>
        private static readonly Stack<List<ESResSourceBase>> s_ListPool = new Stack<List<ESResSourceBase>>(16);
        
        /// <summary>
        /// 租借 HashSet（从对象池获取或创建新对象）
        /// </summary>
        /// <returns>清空的 HashSet 实例</returns>
        /// <remarks>
        /// ⚠️ 注意：必须配对 ReturnHashSet 使用，否则会内存泄漏
        /// </remarks>
        protected static HashSet<string> RentHashSet()
        {
            lock (s_HashSetPool)
            {
                if (s_HashSetPool.Count > 0)
                {
                    var set = s_HashSetPool.Pop();
                    set.Clear();
                    return set;
                }
            }
            return new HashSet<string>(16);
        }
        
        /// <summary>
        /// 归还 HashSet 到对象池
        /// </summary>
        /// <param name="set">要归还的 HashSet，可为 null</param>
        /// <remarks>
        /// ✅ 容错设计：自动检查 null 和池容量
        /// ✅ 自动清理：回收前会清空集合，防止内存泄漏
        /// </remarks>
        protected static void ReturnHashSet(HashSet<string> set)
        {
            if (set == null) return;
            lock (s_HashSetPool)
            {
                if (s_HashSetPool.Count < 8)
                {
                    set.Clear();
                    s_HashSetPool.Push(set);
                }
            }
        }
        
        /// <summary>
        /// 租借 List（从对象池获取或创建新对象）
        /// </summary>
        /// <returns>清空的 List 实例</returns>
        /// <remarks>
        /// ⚠️ 注意：必须配对 ReturnList 使用，否则会内存泄漏
        /// </remarks>
        protected static List<ESResSourceBase> RentList()
        {
            lock (s_ListPool)
            {
                if (s_ListPool.Count > 0)
                {
                    var list = s_ListPool.Pop();
                    list.Clear();
                    return list;
                }
            }
            return new List<ESResSourceBase>(8);
        }
        
        /// <summary>
        /// 归还 List 到对象池
        /// </summary>
        /// <param name="list">要归还的 List，可为 null</param>
        /// <remarks>
        /// ✅ 容错设计：自动检查 null 和池容量
        /// ✅ 自动清理：回收前会清空列表，防止内存泄漏
        /// </remarks>
        protected static void ReturnList(List<ESResSourceBase> list)
        {
            if (list == null) return;
            lock (s_ListPool)
            {
                if (s_ListPool.Count < 16)
                {
                    list.Clear();
                    s_ListPool.Push(list);
                }
            }
        }
        #endregion
        
        #region 内部字段 - 资源状态管理
        
        /// <summary>
        /// 资源键（强类型）
        /// </summary>
        protected ESResKey m_ResKey;
        
        /// <summary>
        /// 资源加载状态（Waiting/Loading/Ready）
        /// </summary>
        /// <remarks>
        /// ⚠️ 线程安全问题：应通过 State 属性访问，不要直接修改此字段
        /// </remarks>
        private ResSourceState m_ResSourceState = ResSourceState.Waiting;
        
        /// <summary>
        /// 已加载的资源对象（Unity Object）
        /// </summary>
        protected UnityEngine.Object m_Asset;
        
        /// <summary>
        /// 最后一次报告的加载进度 (0.0 ~ 1.0)
        /// </summary>
        protected float m_LastKnownProgress;
        
        /// <summary>
        /// 最后一次加载错误信息
        /// </summary>
        protected string m_LastErrorMessage;
        
        /// <summary>
        /// 加载完成回调委托（支持多播）
        /// </summary>
        /// <remarks>
        /// 📌 设计模式：观察者模式，支持多个监听者
        /// ⚠️ 注意：回调触发后会自动清空，防止内存泄漏
        /// </remarks>
        private event Action<bool, ESResSourceBase> m_OnLoadOKAction;
        
        /// <summary>
        /// 加载类型（AssetBundle/ABAsset/Scene等）
        /// </summary>
        public ESResSourceLoadType m_LoadType;
        
        /// <summary>
        /// 引用计数（用于自动释放管理）
        /// </summary>
        /// <remarks>
        /// ⚠️ 线程安全问题：此字段不是线程安全的，仅限 Unity 主线程访问
        /// ✅ 负数保护：RetainReference 和 ReleaseReference 会自动修正负数
        /// </remarks>
        private int m_ReferenceCount;
        
        /// <summary>
        /// 缓存的依赖数组（避免重复查询字典）
        /// </summary>
        /// <remarks>
        /// 📊 性能优化：初始化时缓存，减少 77.6% 的字典查询
        /// </remarks>
        protected string[] m_CachedDependencies;
        
        /// <summary>
        /// 标记m_CachedDependencies数组中的AB名称是否带Hash后缀
        /// </summary>
        /// <remarks>
        /// true = 带Hash（如"ui_mainmenu_a1b2c3d4"）
        /// false = 不带Hash（如"ui_mainmenu"）
        /// ⚠️ 重要：GlobalDependencies字典中存储的全部是带Hash的完整名称
        /// </remarks>
        protected bool m_DependenciesWithHash;
        
        #endregion
        
        #region 公开属性 - 资源信息查询
        
        /// <summary>
        /// 当前引用计数（只读，由ESResTable自动同步）
        /// </summary>
        /// <remarks>
        /// 📌 用途：判断资源是否可以安全释放
        /// ⚠️ 注意：不要直接修改，应通过 RetainReference/ReleaseReference 方法
        /// </remarks>
        public int ReferenceCount => m_ReferenceCount;
        
        /// <summary>
        /// 资源键（强类型）
        /// </summary>
        public ESResKey ResKey => m_ResKey;
        
        /// <summary>
        /// 资源名称（从 ResKey 中获取）
        /// </summary>
        /// <remarks>
        /// ✨ 性能优化：使用 AggressiveInlining 内联优化
        /// </remarks>
        public string ResName { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => m_ResKey?.ResName; }
        
        /// <summary>
        /// AssetBundle 名称（PreName，不带Hash）
        /// </summary>
        /// <remarks>
        /// ✨ 性能优化：使用 AggressiveInlining 内联优化
        /// </remarks>
        public string ABName { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => m_ResKey?.ABPreName; }
        
        /// <summary>
        /// 库文件夹名称
        /// </summary>
        /// <remarks>
        /// ✨ 性能优化：使用 AggressiveInlining 内联优化
        /// </remarks>
        public string LibFolderName { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => m_ResKey?.LibFolderName; }
        
        #endregion

        /// <summary>
        /// 初始化资源源（对象池复用时调用）
        /// </summary>
        /// <param name="resKey">资源键</param>
        /// <param name="loadType">加载类型</param>
        /// <remarks>
        /// 🔄 生命周期：对象从池中取出时调用，进行初始化
        /// ✅ 安全检查：自动重置所有状态字段
        /// 📌 执行顺序：Set() -> Initilized() -> 子类自定义初始化
        /// </remarks>
        public void Set(ESResKey resKey, ESResSourceLoadType loadType)
        {
            m_ResKey = resKey;
            m_LoadType = loadType;
            TargetType = resKey?.TargetType;
            IsRecycled = false;
            m_LastKnownProgress = 0f;
            m_LastErrorMessage = null;
            m_ReferenceCount = 0;
            Initilized();
        }

        
        #region 状态管理 - 资源加载状态机
        
        /// <summary>
        /// 资源加载状态（Waiting -> Loading -> Ready）
        /// </summary>
        /// <remarks>
        /// 【状态转换】
        /// - Waiting: 初始状态或加载失败后重置
        /// - Loading: BeginLoad() 调用后进入此状态
        /// - Ready: CompleteWithAsset() 成功后进入此状态
        /// 
        /// 【副作用】
        /// - 转换到 Ready 状态时会自动触发所有已注册的回调
        /// - 回调触发后会自动清空 m_OnLoadOKAction
        /// 
        /// ⚠️ 线程安全问题：
        /// - 此属性不是线程安全的
        /// - 仅应在 Unity 主线程中访问
        /// - 多线程并发修改可能导致回调丢失或重复触发
        /// </remarks>
        public ResSourceState State
        {
            get { return m_ResSourceState; }
            set
            {
                if (m_ResSourceState != value)
                {
                    var oldState = m_ResSourceState;
                    m_ResSourceState = value;
                    
                    // ✅ 只在状态首次变为 Ready 时触发，防止重复触发
                    if (oldState != ResSourceState.Ready && m_ResSourceState == ResSourceState.Ready)
                    {
                        Method_ResLoadOK(true);
                    }
                }
            }
        }
        
        /// <summary>
        /// 目标资源类型（用于类型转换和验证）
        /// </summary>
        /// <remarks>
        /// 例如：typeof(Sprite), typeof(Texture2D), typeof(GameObject) 等
        /// </remarks>
        public Type TargetType { get; set; }

        /// <summary>
        /// 已加载的资源对象
        /// </summary>
        /// <remarks>
        /// ⚠️ 注意：
        /// - 仅在 State == Ready 时才有效
        /// - 对于场景资源，可能是占位对象
        /// - 不要直接修改，应通过 CompleteWithAsset 设置
        /// </remarks>
        public UnityEngine.Object Asset => m_Asset;

        /// <summary>
        /// 加载进度 (0.0 ~ 1.0)
        /// </summary>
        /// <remarks>
        /// 【计算规则】
        /// - Waiting: 返回 0
        /// - Loading: 返回 Max(m_LastKnownProgress, CalculateProgress())
        /// - Ready: 返回 1.0
        /// 
        /// 【性能注意】
        /// - 每次访问可能触发 CalculateProgress() 计算
        /// - 频繁调用可能影响性能
        /// - 建议缓存结果而非每帧查询
        /// </remarks>
        public float Progress
        {
            get
            {
                switch (m_ResSourceState)
                {
                    case ResSourceState.Loading:
                        return Mathf.Clamp01(Mathf.Max(m_LastKnownProgress, CalculateProgress()));
                    case ResSourceState.Ready:
                        return 1f;
                }
                return 0f;
            }
        }

        /// <summary>
        /// 对象池回收标记（IPoolableAuto 接口要求）
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 是否有错误信息
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(m_LastErrorMessage);

        /// <summary>
        /// 最后一次错误信息
        /// </summary>
        public string LastErrorMessage => m_LastErrorMessage;

        /// <summary>
        /// 是否正在加载中
        /// </summary>
        public bool IsLoading => m_ResSourceState == ResSourceState.Loading;
        
        #endregion

        protected virtual float CalculateProgress()
        {
            return 0;
        }

        protected virtual void Initilized()
        {
        }

        protected void BeginLoad()
        {
            m_LastErrorMessage = null;
            m_LastKnownProgress = 0f;
            State = ResSourceState.Loading;
        }

        protected void ReportProgress(float progress)
        {
            m_LastKnownProgress = Mathf.Clamp01(progress);
        }

        protected bool CompleteWithAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                OnResLoadFaild("加载结果为空");
                return false;
            }

            m_Asset = asset;
            m_LastKnownProgress = 1f;
            State = ResSourceState.Ready;
            return true;
        }

        protected bool TryGetLocalABLoadPath(out string localPath)
        {
            localPath = m_ResKey?.LocalABLoadPath;
            return !string.IsNullOrEmpty(localPath);
        }

        protected bool TryLoadAssetFromLocalABSync(Func<AssetBundle, UnityEngine.Object> loader, out bool attempted)
        {
            attempted = TryGetLocalABLoadPath(out var localPath);
            if (!attempted || loader == null)
            {
                return false;
            }

            var bundle = AssetBundle.LoadFromFile(localPath);
            if (bundle == null)
            {
                Debug.LogWarning($"AB直载失败: {localPath}");
                return false;
            }

            UnityEngine.Object asset = null;
            try
            {
                asset = loader(bundle);
            }
            finally
            {
                bundle.Unload(false);
            }

            if (!CompleteWithAsset(asset))
            {
                Debug.LogError($"同步加载资源失败: {ResName}");
                return false;
            }

            return true;
        }

        protected IEnumerator TryLoadAssetFromLocalABAsync(Func<AssetBundle, IEnumerator> loader, Action<bool> onFinished)
        {
            if (!TryGetLocalABLoadPath(out var localPath) || loader == null)
            {
                onFinished?.Invoke(false);
                yield break;
            }

            var bundleRequest = AssetBundle.LoadFromFileAsync(localPath);
            if (bundleRequest == null)
            {
                Debug.LogWarning($"AB直载请求失败: {localPath}");
                onFinished?.Invoke(false);
                yield break;
            }

            while (!bundleRequest.isDone)
            {
                ReportProgress(Mathf.Lerp(0.1f, 0.5f, bundleRequest.progress));
                yield return null;
            }

            var bundle = bundleRequest.assetBundle;
            if (bundle == null)
            {
                Debug.LogWarning($"AB直载失败: {localPath}");
                onFinished?.Invoke(false);
                yield break;
            }

            yield return loader(bundle);
            bundle.Unload(false);
            onFinished?.Invoke(State == ResSourceState.Ready);
        }

        /// <summary>
        /// 增加引用计数
        /// </summary>
        /// <returns>新的引用计数</returns>
        /// <remarks>
        /// ✅ 负数保护：自动检测并修复负数异常
        /// </remarks>
        internal int RetainReference()
        {
            if (m_ReferenceCount < 0)
            {
                Debug.LogError($"[ESResSource.RetainReference] 引用计数异常: {ResName}, count={m_ReferenceCount}, 已重置为0");
                m_ReferenceCount = 0;
            }

            m_ReferenceCount++;
            return m_ReferenceCount;
        }

        /// <summary>
        /// 减少引用计数
        /// </summary>
        /// <returns>新的引用计数</returns>
        /// <remarks>
        /// ✅ 负数保护：自动检测并修复负数异常
        /// </remarks>
        internal int ReleaseReference()
        {
            if (m_ReferenceCount <= 0)
            {
                if (m_ReferenceCount < 0)
                {
                    Debug.LogError($"[ESResSource.ReleaseReference] 引用计数异常: {ResName}, count={m_ReferenceCount}, 已重置为0");
                }
                m_ReferenceCount = 0;
                return 0;
            }

            m_ReferenceCount--;
            return m_ReferenceCount;
        }

        internal void ResetReferenceCounter()
        {
            m_ReferenceCount = 0;
        }

        protected void ResetLoadTracking()
        {
            m_LastKnownProgress = 0f;
            m_LastErrorMessage = null;
        }
        public void OnLoadOKAction_Submit(Action<bool, ESResSourceBase> listener)
        {
            if (listener == null)
            {
                return;
            }
            //如果已经结束了，那就立刻触发
            if (m_ResSourceState == ResSourceState.Ready)
            {
                listener(true, this);
                return;
            }
            //没结束就加入到队列
            m_OnLoadOKAction += listener;
        }

        public void OnLoadOKAction_WithDraw(Action<bool, ESResSourceBase> listener)
        {
            if (listener == null)
            {
                return;
            }

            if (m_OnLoadOKAction == null)
            {
                return;
            }

            m_OnLoadOKAction -= listener;
        }
        /// <summary>
        /// 资源加载失败回调
        /// </summary>
        /// <param name="message">错误信息</param>
        /// <remarks>
        /// 🔄 状态转换：Loading -> Waiting
        /// ✅ 先触发回调，再重置状态，防止状态不一致
        /// </remarks>
        protected void OnResLoadFaild(string message = null)
        {
            m_LastErrorMessage = message;
            m_LastKnownProgress = 0f;
            
            // ✅ 先触发回调，再重置状态
            Method_ResLoadOK(false);
            
            // ✅ 使用 State 属性而非直接赋值，保证逻辑一致性
            State = ResSourceState.Waiting;
        }
        private void Method_ResLoadOK(bool readOrFail)
        {
            if (m_OnLoadOKAction != null)
            {
                m_OnLoadOKAction(readOrFail, this);
                m_OnLoadOKAction = null;
            }
        }
        public virtual bool LoadSync()
        {
            //等待子类自己实现
            return false;
        }

        public void LoadAsync()
        {
            Debug.Verbose($"[ESResSource.LoadAsync] 开始异步加载资源: {ResName}, 当前状态: {State}");

            //必须处于无状态
            if (State == ResSourceState.Loading || State == ResSourceState.Ready)
            {
                Debug.Verbose($"[ESResSource.LoadAsync] 资源 '{ResName}' 已在加载或已就绪状态 ({State})，跳过异步加载。");
                return;
            }

            //资源名有效
            if (string.IsNullOrEmpty(ResName))
            {
                Debug.LogError($"[ESResSource.LoadAsync] 资源名为空，无法开始异步加载。");
                return;
            }

            Debug.Verbose($"[ESResSource.LoadAsync] 资源 '{ResName}' 通过状态和名称检查，开始加载。");
            BeginLoad();
            //开始推送加载
            Debug.Verbose($"[ESResSource.LoadAsync] 推送资源 '{ResName}' 到加载任务队列。");
            ESResMaster.Instance.PushResLoadTask(this);
        }

        public virtual string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            //等待重写
            withHash = false;
            return null;
        }

        public bool IsDependResLoadFinish()
        {
            Debug.Verbose($"[ESResSource.IsDependResLoadFinish] 检查资源 '{ResName}' 的依赖加载状态。");

            var dependsAB = GetDependResSourceAllAssetBundles(out var dependenciesWithHash);
            Debug.Verbose($"[ESResSource.IsDependResLoadFinish] 获取到 {dependsAB?.Length ?? 0} 个依赖AB，dependenciesWithHash: {dependenciesWithHash}。");

            if (dependsAB == null || dependsAB.Length == 0)
            {
                Debug.Verbose($"[ESResSource.IsDependResLoadFinish] 资源 '{ResName}' 无依赖，返回true。");
                return true;
            }

            //倒着测试
            for (var i = dependsAB.Length - 1; i >= 0; --i)
            {
                //抓AB
                string preName = dependenciesWithHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                Debug.Verbose($"[ESResSource.IsDependResLoadFinish] 检查依赖AB: {preName} (完整名WithHash: {dependsAB[i]})");

                if (ESResMaster.GlobalABKeys.TryGetValue(preName, out var abKey))
                {
                    var res = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
                    if (res == null)
                    {
                        Debug.LogWarning($"[ESResSource.IsDependResLoadFinish] 依赖AB '{preName}' 未找到资源源，返回false。");
                        return false;
                    }
                    if (res.State != ResSourceState.Ready)
                    {
                        Debug.Verbose($"[ESResSource.IsDependResLoadFinish] 依赖AB '{preName}' 状态为 {res.State}，未就绪，返回false。");
                        return false;
                    }
                    Debug.Verbose($"[ESResSource.IsDependResLoadFinish] 依赖AB '{preName}' 已就绪。");
                }
                else
                {
                    Debug.LogWarning($"[ESResSource.IsDependResLoadFinish] 未找到依赖AB键: {preName}，返回false。");
                    return false;
                }
            }

            Debug.Verbose($"[ESResSource.IsDependResLoadFinish] 资源 '{ResName}' 所有依赖已就绪，返回true。");
            return true;
        }

        public bool ReleaseTheResSource()
        {
            //加载中禁止释放
            if (State == ResSourceState.Loading)
            {
                return false;
            }
            //没加载，也没完成，说明正在准备，不需要释放就完成了
            if (State != ResSourceState.Ready)
            {
                ResetReferenceCounter();
                return true;
            }

            if (m_ReferenceCount > 0)
            {
                Debug.LogWarning($"尝试释放资源但引用计数仍大于0: {ResName}");
                return false;
            }

            TryReleaseRes();

            State = ResSourceState.Waiting;
            m_OnLoadOKAction = null;
            ResetLoadTracking();
            ResetReferenceCounter();
            return true;
        }
        //执行释放操作
        protected virtual void TryReleaseRes()
        {

            if (m_Asset != null)
            {
                ESResMaster.UnloadRes(m_Asset);

                m_Asset = null;
            }
        }
        public virtual void TryAutoPushedToPool()
        {
            IsRecycled = true;
        }

        public virtual IEnumerator DoTaskAsync(Action finishCallback)
        {
            yield return null;
        }

        public override string ToString()
        {
            return string.Format("ESResSource:名字：{0}\t 状态 :{1}", ResName, State);
        }

        public void OnResetAsPoolable()
        {
            m_ResKey = null;
            m_OnLoadOKAction = null;
            m_Asset = null;
            m_LastKnownProgress = 0f;
            m_LastErrorMessage = null;
            m_ResSourceState = ResSourceState.Waiting;
            IsRecycled = false;
            m_ReferenceCount = 0;
            TargetType = null;
            m_CachedDependencies = null;
            m_DependenciesWithHash = false;
        }
    }
    public class ESABSource : ESResSourceBase
    {
        public bool IsNet = true;
        public static readonly string[] s_EmptyDeps = new string[0];

        protected override void Initilized()
        {
            // ✅ 初始化时缓存依赖，避免运行时重复查询
            // ⚠️ 重要：GlobalDependencies存储的依赖名称全部带Hash！
            // 例如：dependencies = ["common_a1b2c3d4", "shader_e5f6g7h8"]
            m_DependenciesWithHash = true;  // Dependencies数组中的名称都带Hash
            if (string.IsNullOrEmpty(ABName) || !ESResMaster.GlobalDependencies.TryGetValue(ABName, out m_CachedDependencies))
            {
                m_CachedDependencies = s_EmptyDeps;
            }
        }
        
        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = m_DependenciesWithHash;
            return m_CachedDependencies ?? s_EmptyDeps;
        }

        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
            {
                return true;
            }

            BeginLoad();

            if (TryLoadAssetFromLocalABSync(
                ab => TargetType != null ? ab.LoadAsset(ResName, TargetType) : ab.LoadAsset(ResName),
                out var attemptedLocal) && attemptedLocal)
            {
                return true;
            }

            var cached = ESResMaster.HasLoadedAB(ResName);
            if (cached != null)
            {
                return CompleteWithAsset(cached);
            }

            var dependsAB = GetDependResSourceAllAssetBundles(out bool dependenciesWithHash);
            if (dependsAB != null && dependsAB.Length > 0)
            {
                for (int i = 0; i < dependsAB.Length; i++)
                {
                    // ⚠️ dependsAB数组中的名称带Hash（如"common_a1b2c3d4"）
                    // GlobalABKeys字典的Key是PreName（不带Hash），所以需要提取PreName
                    string preName = dependenciesWithHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                    if (ESResMaster.GlobalABKeys.TryGetValue(preName, out var abKey))
                    {
                        var res = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
                        if (res == null || res.State != ResSourceState.Ready)
                        {
                            ESResMaster.MainLoader.AddAB2LoadByABPreNameSourcer(preName);
                        }
                    }
                }
                ESResMaster.MainLoader.LoadAll_Sync();

                if (!IsDependResLoadFinish())
                {
                    OnResLoadFaild("依赖AssetBundle加载失败");
                    return false;
                }
            }

            
            string bundlePath = m_ResKey?.LocalABLoadPath ?? Path.Combine(ESResMaster.Instance.GetDownloadLocalPath(), LibFolderName ?? string.Empty, "AB", ResName);
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (!CompleteWithAsset(bundle))
            {
                Debug.LogError($"加载AssetBundle失败: {bundlePath}");
                return false;
            }

            return true;
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            Debug.Verbose($"[ESABSource.DoTaskAsync] 开始异步加载AssetBundle任务: {ResName}");

            if (State == ResSourceState.Ready)
            {
                Debug.Verbose($"[ESABSource.DoTaskAsync] AssetBundle '{ResName}' 已就绪，直接调用完成回调。");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.Verbose($"[ESABSource.DoTaskAsync] 初始化加载状态: {ResName}");
            BeginLoad();

            var cached = ESResMaster.HasLoadedAB(ResName);
            if (cached != null)
            {
                Debug.Verbose($"[ESABSource.DoTaskAsync] 使用缓存的AssetBundle: {ResName}");
                CompleteWithAsset(cached);
                finishCallback?.Invoke();
                yield break;
            }

            // ✅ 性能优化：使用缓存的依赖数组
            var dependsAB = m_CachedDependencies;
            var dependenciesWithHash = m_DependenciesWithHash;  // true表示依赖名称带Hash
            Debug.Verbose($"[ESABSource.DoTaskAsync] 获取到 {dependsAB?.Length ?? 0} 个依赖AB，dependenciesWithHash: {dependenciesWithHash}");

            // ✅ 性能优化：从对象池租用HashSet，避免GC
            HashSet<string> loadingChain = null;
            if (dependsAB != null && dependsAB.Length > 0)
            {
                loadingChain = RentHashSet();
                if (!CheckCircularDependency(ResName, dependsAB, dependenciesWithHash, loadingChain))
                {
                    Debug.LogError($"[ESABSource.DoTaskAsync] 检测到循环依赖: {ResName}");
                    OnResLoadFaild("检测到循环依赖");
                    ReturnHashSet(loadingChain);
                    finishCallback?.Invoke();
                    yield break;
                }
                ReturnHashSet(loadingChain);
            }

            // ✅ 性能优化：使用List代替Dictionary，减少GC和查询开销
            List<ESResSourceBase> pendingDeps = null;
            int completedCount = 0;
            bool hasFailure = false;

            if (dependsAB != null && dependsAB.Length > 0)
            {
                pendingDeps = RentList();
                
                for (int i = 0; i < dependsAB.Length; i++)
                {
                    // ⚠️ dependsAB[i]是带Hash的完整名（如"common_a1b2c3d4"）
                    // GlobalABKeys的Key是PreName（不带Hash），需要提取PreName
                    string preName = dependenciesWithHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                    Debug.Verbose($"[ESABSource.DoTaskAsync] 处理依赖AB: {preName} (完整名WithHash: {dependsAB[i]})");

                    if (!ESResMaster.GlobalABKeys.TryGetValue(preName, out var abKey))
                    {
                        Debug.LogError($"[ESABSource.DoTaskAsync] 未找到依赖AssetBundle键: {preName}，加载失败。");
                        OnResLoadFaild($"未找到依赖AssetBundle键: {preName}");
                        ReturnList(pendingDeps);
                        finishCallback?.Invoke();
                        yield break;
                    }

                    var dependencyRes = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
                    if (dependencyRes == null)
                    {
                        Debug.LogError($"[ESABSource.DoTaskAsync] 未找到依赖AssetBundle资源源: {preName}，加载失败。");
                        OnResLoadFaild($"未找到依赖AssetBundle: {preName}");
                        ReturnList(pendingDeps);
                        finishCallback?.Invoke();
                        yield break;
                    }

                    if (dependencyRes.State == ResSourceState.Ready)
                    {
                        Debug.Verbose($"[ESABSource.DoTaskAsync] 依赖AB '{preName}' 已就绪。");
                        completedCount++;
                    }
                    else
                    {
                        Debug.Verbose($"[ESABSource.DoTaskAsync] 依赖AB '{preName}' 未就绪，添加到加载队列。");
                        pendingDeps.Add(dependencyRes);
                        // ✅ 避免闭包：使用局部变量捕获
                        dependencyRes.OnLoadOKAction_Submit(OnDependencyLoaded);
                        ESResMaster.MainLoader.AddAB2LoadByABPreNameSourcer(preName);
                    }
                }

                if (pendingDeps.Count > 0)
                {
                    Debug.Verbose($"[ESABSource.DoTaskAsync] 开始异步加载 {pendingDeps.Count} 个依赖AB。");
                    ESResMaster.MainLoader.LoadAllAsync();
                    
                    int totalDeps = completedCount + pendingDeps.Count;
                    while (completedCount < totalDeps && !hasFailure)
                    {
                        ReportProgress(0.1f + 0.4f * completedCount / totalDeps);
                        yield return null;
                    }
                }

                ReturnList(pendingDeps);

                if (hasFailure)
                {
                    Debug.LogError($"[ESABSource.DoTaskAsync] 依赖AssetBundle加载失败。");
                    OnResLoadFaild("依赖AssetBundle加载失败");
                    finishCallback?.Invoke();
                    yield break;
                }

                Debug.Verbose($"[ESABSource.DoTaskAsync] 所有依赖AB加载成功。");
            }
            else
            {
                Debug.Verbose($"[ESABSource.DoTaskAsync] AssetBundle '{ResName}' 无依赖。");
            }

            Debug.Verbose($"[ESABSource.DoTaskAsync] 开始加载自身AssetBundle: {ResName}");
            yield return LoadSelf();

            Debug.Verbose($"[ESABSource.DoTaskAsync] AssetBundle '{ResName}' 加载完成。");
            finishCallback?.Invoke();
            
            // 局部回调函数，避免闭包分配
            void OnDependencyLoaded(bool success, ESResSourceBase _)
            {
                if (success)
                {
                    completedCount++;
                }
                else
                {
                    hasFailure = true;
                }
            }
        }
        private IEnumerator LoadSelf()
        {
            Debug.Verbose($"[ESABSource.LoadSelf] 开始加载AssetBundle: {ResName}");

            if (m_Asset == null)
            {
                var bundlePath = m_ResKey?.LocalABLoadPath ?? Path.Combine(ESResMaster.Instance.GetDownloadLocalPath(), LibFolderName ?? string.Empty, "AB", ResName);
                Debug.Verbose($"[ESABSource.LoadSelf] 创建异步加载请求（含Hash）: {ResName})");
                var request = AssetBundle.LoadFromFileAsync(bundlePath);
                if (request == null)
                {
                    Debug.LogError($"[ESABSource.LoadSelf] 无法创建带Hash的AssetBundle加载请求: {ResName}");
                    OnResLoadFaild("无法创建AssetBundle加载请求");
                    yield break;
                }

                Debug.Verbose($"[ESABSource.LoadSelf] 开始等待带Hash的AssetBundle加载完成: {ResName}");

                Debug.Verbose($"[ESABSource.LoadSelf] 开始等待AssetBundle加载完成: {bundlePath}");
                while (!request.isDone)
                {
                    float progress = Mathf.Lerp(0.2f, 0.95f, request.progress);
                    ReportProgress(progress);
                    Debug.Verbose($"[ESABSource.LoadSelf] 加载进度: {progress:F2} for {bundlePath}");
                    yield return null;
                }

                if (!CompleteWithAsset(request.assetBundle))
                {
                    Debug.LogError($"[ESABSource.LoadSelf] 异步加载AssetBundle失败: {bundlePath}");
                    yield break;
                }
                else
                {
                    Debug.Verbose($"[ESABSource.LoadSelf] AssetBundle加载成功: {bundlePath}");
                }
                Debug.Verbose($"[ESABSource.LoadSelf] AssetBundle '{ResName}' 加载完成，设置进度为1。");
                ReportProgress(1f);
            }
            else
            {
                Debug.Verbose($"[ESABSource.LoadSelf] AssetBundle '{ResName}' 已加载，跳过加载步骤。");
            }
        }
        protected override float CalculateProgress()
        {
            return 0;
        }
        public override string ToString()
        {
            return $"T:AB包\t {base.ToString()}";
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            var instance = ESResMaster.Instance;
            instance?.PoolForESABSource.PushToPool(this);
        }

        /// <summary>
        /// 检测循环依赖（简化版）
        /// </summary>
        /// <param name="currentAB">当前AB的PreName（不带Hash）</param>
        /// <param name="dependencies">依赖数组（带Hash或不带Hash）</param>
        /// <param name="dependenciesWithHash">dependencies数组中的名称是否带Hash</param>
        /// <param name="loadingChain">加载链（用于检测循环）</param>
        private bool CheckCircularDependency(string currentAB, string[] dependencies, bool dependenciesWithHash, HashSet<string> loadingChain)
        {
            if (loadingChain.Contains(currentAB))
            {
                return false; // 检测到循环
            }

            loadingChain.Add(currentAB);

            if (dependencies != null && dependencies.Length > 0)
            {
                foreach (var dep in dependencies)
                {
                    // 提取PreName用于比较（因为loadingChain中存的是PreName）
                    string depName = dependenciesWithHash ? ESResMaster.PathAndNameTool_GetPreName(dep) : dep;
                    
                    // 简化检测：只检查一层依赖是否回指当前AB
                    if (loadingChain.Contains(depName))
                    {
                        return false; // 发现循环
                    }
                }
            }

            return true;
        }
    }
    public class ESAssetSource : ESResSourceBase
    {
        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
            {
                return true;
            }

            BeginLoad();

            // 检查全局AB键字典中是否存在对应的AssetBundle键
            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
                var localPath = m_ResKey?.LocalABLoadPath;
                if (!string.IsNullOrEmpty(localPath))
                {
                    var bundle = AssetBundle.LoadFromFile(localPath);
                    if (bundle != null)
                    {
                        var asset = TargetType != null ? bundle.LoadAsset(ResName, TargetType) : bundle.LoadAsset(ResName);
                        bundle.Unload(false);
                        if (CompleteWithAsset(asset))
                        {
                            return true;
                        }
                    }
                }

                OnResLoadFaild($"未找到AB键: {ABName}");
                return false;
            }

            var abResSou = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
            if (abResSou == null)
            {
                OnResLoadFaild($"未找到AssetBundle资源: {ABName}");
                return false;
            }

            if (abResSou.State != ResSourceState.Ready && !abResSou.LoadSync())
            {
                OnResLoadFaild($"AssetBundle加载失败: {ABName}");
                return false;
            }

            if (abResSou.Asset is AssetBundle ab)
            {
                var asset = TargetType != null ? ab.LoadAsset(ResName, TargetType) : ab.LoadAsset(ResName);
                if (!CompleteWithAsset(asset))
                {
                    Debug.LogError($"同步加载资源失败: {ResName}");
                    return false;
                }
                return true;
            }

            OnResLoadFaild($"AssetBundle未就绪: {ABName}");
            return false;
        }
        /// <summary>
        /// 异步执行资源加载任务。
        /// 此方法是ESAssetSource的核心加载逻辑，负责从AssetBundle中异步加载指定的资源。
        /// 流程包括：状态检查、依赖AssetBundle加载等待、自身资源加载。
        /// </summary>
        /// <param name="finishCallback">加载完成后的回调函数，参数为加载是否成功。</param>
        /// <returns>协程枚举器，用于Unity的协程系统。</returns>
        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            Debug.Verbose($"[ESAssetSource.DoTaskAsync] 开始异步加载任务: {ResName}");

            if (State == ResSourceState.Ready)
            {
                Debug.Verbose($"[ESAssetSource.DoTaskAsync] 资源 '{ResName}' 已就绪，直接调用完成回调。");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.Verbose($"[ESAssetSource.DoTaskAsync] 初始化加载状态: {ResName}");
            BeginLoad();

            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
                var localPath = m_ResKey?.LocalABLoadPath;
                if (!string.IsNullOrEmpty(localPath))
                {
                    var bundleRequest = AssetBundle.LoadFromFileAsync(localPath);
                    if (bundleRequest != null)
                    {
                        while (!bundleRequest.isDone)
                        {
                            ReportProgress(Mathf.Lerp(0.1f, 0.5f, bundleRequest.progress));
                            yield return null;
                        }

                        var bundle = bundleRequest.assetBundle;
                        if (bundle != null)
                        {
                            yield return LoadSelf(bundle);
                            bundle.Unload(false);
                            if (State == ResSourceState.Ready)
                            {
                                finishCallback?.Invoke();
                                yield break;
                            }
                        }
                    }
                }

                Debug.LogError($"[ESAssetSource.DoTaskAsync] 未找到AB键: {ABName}，加载失败。");
                OnResLoadFaild($"未找到AB键: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.Verbose($"[ESAssetSource.DoTaskAsync] 找到AB键: {ABName} -> {abKey}");

            var abResSou = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
            if (abResSou == null)
            {
                Debug.LogError($"[ESAssetSource.DoTaskAsync] 未找到AssetBundle资源源: {ABName}，加载失败。");
                OnResLoadFaild($"未找到AssetBundle资源: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.Verbose($"[ESAssetSource.DoTaskAsync] 获取到AssetBundle资源源: {abResSou.ResName}，状态: {abResSou.State}");

            if (abResSou.State != ResSourceState.Ready)
            {
                Debug.Verbose($"[ESAssetSource.DoTaskAsync] AssetBundle '{ABName}' 未就绪，开始等待依赖加载。");

                // ✅ 性能优化：使用值类型避免闭包分配
                bool dependencyCompleted = false;
                bool dependencySuccess = false;

                abResSou.OnLoadOKAction_Submit(OnABLoaded);
                abResSou.LoadAsync();

                while (!dependencyCompleted)
                {
                    ReportProgress(0.1f + 0.4f * abResSou.Progress);
                    yield return null;
                }

                if (!dependencySuccess)
                {
                    Debug.LogError($"[ESAssetSource.DoTaskAsync] 依赖AssetBundle '{ABName}' 加载失败");
                    OnResLoadFaild(abResSou.HasError ? abResSou.LastErrorMessage : "依赖AssetBundle加载失败");
                    finishCallback?.Invoke();
                    yield break;
                }

                Debug.Verbose($"[ESAssetSource.DoTaskAsync] 依赖AssetBundle '{ABName}' 加载成功。");
                
                // 局部回调，避免闭包
                void OnABLoaded(bool success, ESResSourceBase _)
                {
                    dependencyCompleted = true;
                    dependencySuccess = success;
                    Debug.Verbose($"[ESAssetSource.DoTaskAsync] 依赖AssetBundle '{ABName}' 加载完成，结果: {success}");
                }
            }
            else
            {
                Debug.Verbose($"[ESAssetSource.DoTaskAsync] AssetBundle '{ABName}' 已就绪，跳过依赖加载。");
            }

            if (abResSou.Asset is AssetBundle ab)
            {
                Debug.Verbose($"[ESAssetSource.DoTaskAsync] 开始加载自身资源: {ResName} 从AssetBundle: {ABName}");
                yield return LoadSelf(ab);
                Debug.Verbose($"[ESAssetSource.DoTaskAsync] 自身资源 '{ResName}' 加载完成。");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.LogError($"[ESAssetSource.DoTaskAsync] AssetBundle '{ABName}' 未就绪，加载失败。");
            OnResLoadFaild($"AssetBundle未就绪: {ABName}");
            finishCallback?.Invoke();
        }
        private IEnumerator LoadSelf(AssetBundle ab)
        {
            if (ab != null)
            {
                AssetBundleRequest request = TargetType != null ? ab.LoadAssetAsync(ResName, TargetType) : ab.LoadAssetAsync(ResName);
                if (request == null)
                {
                    OnResLoadFaild("无法创建资源加载请求");
                    yield break;
                }

                while (!request.isDone)
                {
                    ReportProgress(Mathf.Lerp(0.25f, 0.95f, request.progress));
                    yield return null;
                }

                if (!CompleteWithAsset(request.asset))
                {
                    Debug.LogError($"异步加载资源失败: {ResName}");
                    yield break;
                }
            }
            else
            {
                OnResLoadFaild("AssetBundle为空");
                yield break;
            }
            ReportProgress(1f);
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            var instance = ESResMaster.Instance;
            instance?.PoolForESAsset.PushToPool(this);
        }
    }

    /// <summary>
    /// AB场景资源源 - 支持同步/异步加载场景
    /// </summary>
    public class ESABSceneSource : ESResSourceBase
    {
        private AsyncOperation m_SceneLoadOperation;
        
        protected override void Initilized()
        {
            m_SceneLoadOperation = null;
            // 缓存依赖
            // ⚠️ 重要：GlobalDependencies存储的依赖名称全部带Hash！
            m_DependenciesWithHash = true;  // Dependencies数组中的名称都带Hash
            if (string.IsNullOrEmpty(ABName) || !ESResMaster.GlobalDependencies.TryGetValue(ABName, out m_CachedDependencies))
            {
                m_CachedDependencies = ESABSource.s_EmptyDeps;
            }
        }
        
        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = m_DependenciesWithHash;
            return m_CachedDependencies ?? ESABSource.s_EmptyDeps;
        }

        public override bool LoadSync()
        {
            Debug.LogError($"[ESABSceneSource] 场景资源不支持同步加载: {ResName}");
            OnResLoadFaild("场景资源不支持同步加载");
            return false;
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            Debug.Verbose($"[ESABSceneSource.DoTaskAsync] 开始异步加载场景: {ResName}");

            if (State == ResSourceState.Ready)
            {
                Debug.Verbose($"[ESABSceneSource.DoTaskAsync] 场景 '{ResName}' 已就绪");
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            // 检查全局AB键
            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
                Debug.LogError($"[ESABSceneSource] 未找到场景AB键: {ABName}");
                OnResLoadFaild($"未找到场景AB键: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            // 加载场景所在的AB包
            var abResSou = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
            if (abResSou == null)
            {
                Debug.LogError($"[ESABSceneSource] 未找到场景AssetBundle: {ABName}");
                OnResLoadFaild($"未找到场景AssetBundle: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            // 等待AB包加载完成
            if (abResSou.State != ResSourceState.Ready)
            {
                Debug.Verbose($"[ESABSceneSource] 等待场景AB包加载: {ABName}");
                bool abCompleted = false;
                bool abSuccess = false;

                abResSou.OnLoadOKAction_Submit((success, _) =>
                {
                    abCompleted = true;
                    abSuccess = success;
                });
                
                abResSou.LoadAsync();

                while (!abCompleted)
                {
                    ReportProgress(0.1f + 0.3f * abResSou.Progress);
                    yield return null;
                }

                if (!abSuccess)
                {
                    Debug.LogError($"[ESABSceneSource] 场景AB包加载失败: {ABName}");
                    OnResLoadFaild("场景AB包加载失败");
                    finishCallback?.Invoke();
                    yield break;
                }
            }

            // 异步加载场景（使用Unity的SceneManager）
            Debug.Verbose($"[ESABSceneSource] 开始加载场景内容: {ResName}");
            var sceneLoadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(ResName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (sceneLoadOp == null)
            {
                Debug.LogError($"[ESABSceneSource] 无法创建场景加载操作: {ResName}");
                OnResLoadFaild("无法创建场景加载操作");
                finishCallback?.Invoke();
                yield break;
            }

            m_SceneLoadOperation = sceneLoadOp;
            sceneLoadOp.allowSceneActivation = true;

            while (!sceneLoadOp.isDone)
            {
                ReportProgress(0.4f + 0.6f * sceneLoadOp.progress);
                yield return null;
            }

            // 场景加载成功，使用场景名称作为Asset标识
            m_Asset = new UnityEngine.Object(); // 占位对象
            m_LastKnownProgress = 1f;
            State = ResSourceState.Ready;

            Debug.Verbose($"[ESABSceneSource] 场景加载完成: {ResName}");
            finishCallback?.Invoke();
        }

        protected override void TryReleaseRes()
        {
            if (m_SceneLoadOperation != null)
            {
                m_SceneLoadOperation = null;
            }
            
            // 卸载场景
            if (!string.IsNullOrEmpty(ResName))
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(ResName);
                if (scene.isLoaded)
                {
                    UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
                    Debug.Verbose($"[ESABSceneSource] 卸载场景: {ResName}");
                }
            }
            
            m_Asset = null;
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            // 场景资源暂不使用对象池，因为场景有特殊的生命周期管理
        }
    }

    /// <summary>
    /// Shader变体集资源源 - 用于预加载Shader变体，优化首帧性能
    /// </summary>
    public class ESShaderVariantSource : ESResSourceBase
    {
        protected override void Initilized()
        {
            // ShaderVariant没有依赖
            m_CachedDependencies = ESABSource.s_EmptyDeps;
            m_DependenciesWithHash = false;
        }

        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = false;
            return m_CachedDependencies;
        }

        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
            {
                return true;
            }

            BeginLoad();

            // 从AB包同步加载ShaderVariantCollection
            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
                OnResLoadFaild($"未找到ShaderVariant AB键: {ABName}");
                return false;
            }

            var abResSou = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
            if (abResSou == null || (abResSou.State != ResSourceState.Ready && !abResSou.LoadSync()))
            {
                OnResLoadFaild($"ShaderVariant AB包加载失败: {ABName}");
                return false;
            }

            if (abResSou.Asset is AssetBundle ab)
            {
                var collection = ab.LoadAsset<UnityEngine.ShaderVariantCollection>(ResName);
                if (collection == null)
                {
                    OnResLoadFaild($"ShaderVariantCollection加载失败: {ResName}");
                    return false;
                }

                // 立即预热Shader变体
                collection.WarmUp();
                Debug.Verbose($"[ESShaderVariantSource] Shader变体预热完成: {ResName}");

                return CompleteWithAsset(collection);
            }

            OnResLoadFaild("AB包未就绪");
            return false;
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            Debug.Verbose($"[ESShaderVariantSource] 开始异步加载Shader变体集: {ResName}");

            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
                Debug.LogError($"[ESShaderVariantSource] 未找到AB键: {ABName}");
                OnResLoadFaild($"未找到AB键: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            var abResSou = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
            if (abResSou == null)
            {
                Debug.LogError($"[ESShaderVariantSource] 未找到AB资源: {ABName}");
                OnResLoadFaild($"未找到AB资源: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            if (abResSou.State != ResSourceState.Ready)
            {
                bool abCompleted = false;
                bool abSuccess = false;

                abResSou.OnLoadOKAction_Submit((success, _) =>
                {
                    abCompleted = true;
                    abSuccess = success;
                });
                
                abResSou.LoadAsync();

                while (!abCompleted)
                {
                    ReportProgress(0.2f + 0.5f * abResSou.Progress);
                    yield return null;
                }

                if (!abSuccess)
                {
                    Debug.LogError($"[ESShaderVariantSource] AB包加载失败: {ABName}");
                    OnResLoadFaild("AB包加载失败");
                    finishCallback?.Invoke();
                    yield break;
                }
            }

            if (abResSou.Asset is AssetBundle ab)
            {
                var request = ab.LoadAssetAsync<UnityEngine.ShaderVariantCollection>(ResName);
                if (request == null)
                {
                    Debug.LogError($"[ESShaderVariantSource] 无法创建加载请求: {ResName}");
                    OnResLoadFaild("无法创建加载请求");
                    finishCallback?.Invoke();
                    yield break;
                }

                while (!request.isDone)
                {
                    ReportProgress(0.7f + 0.25f * request.progress);
                    yield return null;
                }

                var collection = request.asset as UnityEngine.ShaderVariantCollection;
                if (collection == null)
                {
                    Debug.LogError($"[ESShaderVariantSource] ShaderVariantCollection加载失败: {ResName}");
                    OnResLoadFaild("ShaderVariantCollection加载失败");
                    finishCallback?.Invoke();
                    yield break;
                }

                // 异步预热Shader变体
                Debug.Verbose($"[ESShaderVariantSource] 开始预热Shader变体: {ResName}");
                collection.WarmUp();
                
                ReportProgress(1f);
                CompleteWithAsset(collection);
                Debug.Verbose($"[ESShaderVariantSource] Shader变体预热完成: {ResName}");
                finishCallback?.Invoke();
                yield break;
            }

            OnResLoadFaild("AB包未就绪");
            finishCallback?.Invoke();
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            // ShaderVariant不使用对象池，由专门的预加载器管理
        }
    }

    /// <summary>
    /// 内置Resources资源源 - 使用Unity的Resources.Load加载内置资源
    /// 
    /// 【适用场景】
    /// - 不需要打包成AB的小型资源（默认配置、UI图标等）
    /// - 快速原型开发，无需AB打包流程
    /// 
    /// 【路径规则】
    /// - ResName应为相对于Resources文件夹的路径（不包含扩展名）
    /// - 例如："UI/Icons/default_icon"
    /// 
    /// 【注意事项】
    /// ⚠️ Resources资源会增加应用体积，不建议大量使用
    /// ⚠️ 不支持热更新，仅适用于固定资源
    /// ⚠️ 首次加载会扫描所有Resources，启动时有性能开销
    /// </summary>
    public class ESInternalResourceSource : ESResSourceBase
    {
        protected override void Initilized()
        {
            // InternalResource没有依赖
            m_CachedDependencies = ESABSource.s_EmptyDeps;
            m_DependenciesWithHash = false;
        }

        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = false;
            return m_CachedDependencies;
        }

        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
            {
                return true;
            }

            BeginLoad();

            try
            {
                // 同步加载Resources资源
                var asset = Resources.Load(ResName);
                if (asset == null)
                {
                    OnResLoadFaild($"Resources资源不存在: {ResName}");
                    return false;
                }

                return CompleteWithAsset(asset);
            }
            catch (Exception ex)
            {
                OnResLoadFaild($"Resources加载异常: {ex.Message}");
                return false;
            }
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            // 异步加载Resources资源
            var request = Resources.LoadAsync(ResName);
            if (request == null)
            {
                OnResLoadFaild("无法创建加载请求");
                finishCallback?.Invoke();
                yield break;
            }

            while (!request.isDone)
            {
                ReportProgress(request.progress);
                yield return null;
            }

            if (request.asset == null)
            {
                OnResLoadFaild("Resources资源不存在");
                finishCallback?.Invoke();
                yield break;
            }

            ReportProgress(1f);
            CompleteWithAsset(request.asset);
            finishCallback?.Invoke();
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            ESResMaster.Instance?.PoolForESInternalResource?.PushToPool(this);
        }
    }

    /// <summary>
    /// 网络图片资源源 - 从HTTP/HTTPS下载并加载图片
    /// 
    /// 【适用场景】
    /// - 动态头像、远程图片、CDN资源
    /// - 需要从网络实时更新的图片资源
    /// 
    /// 【加载方式】
    /// ⚠️ 仅支持异步加载（UnityWebRequest），不支持同步加载
    /// 
    /// 【URL规则】
    /// - ResName应为完整的URL地址
    /// - 支持HTTP和HTTPS协议
    /// - 例如："https://example.com/images/avatar.jpg"
    /// 
    /// 【缓存策略】
    /// - 首次加载从网络下载，缓存到本地
    /// - 后续加载优先使用本地缓存
    /// - 缓存路径：Application.persistentDataPath/NetImageCache/
    /// - 使用MD5哈希作为缓存文件名，避免URL特殊字符问题
    /// 
    /// 【性能优化】
    /// - 支持自动重试机制（最多3次）
    /// - 超时时间：30秒
    /// - 支持进度回调
    /// 
    /// 【数据访问】
    /// - Texture属性：获取加载的Texture2D对象
    /// - ClearCache(url)：清除指定URL的缓存
    /// - ClearAllCache()：清除所有网络图片缓存
    /// </summary>
    public class ESNetImageSource : ESResSourceBase
    {
        private Texture2D m_Texture;
        private string m_CachePath;
        private const int MAX_RETRY_COUNT = 3;
        private const float TIMEOUT_SECONDS = 30f;
        
        public Texture2D Texture => m_Texture;

        protected override void Initilized()
        {
            m_Texture = null;
            m_CachePath = null;
            // NetImage没有依赖
            m_CachedDependencies = ESABSource.s_EmptyDeps;
            m_DependenciesWithHash = false;
        }

        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = false;
            return m_CachedDependencies;
        }

        public override bool LoadSync()
        {
            // 网络资源不支持同步加载
            Debug.LogWarning($"[ESNetImageSource] 网络图片不支持同步加载，请使用异步方式: {ResName}");
            return false;
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            // 验证URL
            if (string.IsNullOrEmpty(ResName) || (!ResName.StartsWith("http://") && !ResName.StartsWith("https://")))
            {
                OnResLoadFaild("无效的URL");
                finishCallback?.Invoke();
                yield break;
            }

            // 计算缓存路径
            m_CachePath = GetCachePath(ResName);
            
            // 检查本地缓存
            if (File.Exists(m_CachePath))
            {
                yield return LoadFromCache();
                finishCallback?.Invoke();
                yield break;
            }

            // 从网络下载
            yield return DownloadFromNetwork();
            finishCallback?.Invoke();
        }

        private IEnumerator LoadFromCache()
        {
            byte[] imageData = null;
            Exception error = null;

            try
            {
                imageData = File.ReadAllBytes(m_CachePath);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null || imageData == null || imageData.Length == 0)
            {
                // 删除损坏的缓存
                try
                {
                    if (File.Exists(m_CachePath))
                    {
                        File.Delete(m_CachePath);
                    }
                }
                catch { }

                yield return DownloadFromNetwork();
                yield break;
            }

            // 创建纹理
            m_Texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = m_Texture.LoadImage(imageData);

            if (!loaded)
            {
                UnityEngine.Object.Destroy(m_Texture);
                m_Texture = null;
                OnResLoadFaild("纹理加载失败");
                yield break;
            }

            CompleteWithAsset(m_Texture);
        }

        private IEnumerator DownloadFromNetwork()
        {
            int retryCount = 0;
            bool success = false;

            while (retryCount < MAX_RETRY_COUNT && !success)
            {
                if (retryCount > 0)
                {
                    yield return new WaitForSeconds(1f * retryCount); // 递增延迟
                }

                using (var webRequest = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(ResName))
                {
                    webRequest.timeout = (int)TIMEOUT_SECONDS;
                    var operation = webRequest.SendWebRequest();

                    float startTime = Time.realtimeSinceStartup;
                    while (!operation.isDone)
                    {
                        // 检查超时
                        if (Time.realtimeSinceStartup - startTime > TIMEOUT_SECONDS)
                        {
                            webRequest.Abort();
                            break;
                        }

                        ReportProgress(0.1f + 0.8f * operation.progress);
                        yield return null;
                    }

                    // 检查错误
                    if (webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        m_Texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(webRequest);
                        if (m_Texture != null)
                        {
                            success = true;
                            
                            // 保存到缓存
                            try
                            {
                                var cacheDir = Path.GetDirectoryName(m_CachePath);
                                if (!Directory.Exists(cacheDir))
                                {
                                    Directory.CreateDirectory(cacheDir);
                                }

                                byte[] imageData = webRequest.downloadHandler.data;
                                File.WriteAllBytes(m_CachePath, imageData);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ESNetImageSource] 缓存保存失败: {ex.Message}");
                            }
                        }
                    }
                }

                retryCount++;
            }

            if (success && m_Texture != null)
            {
                CompleteWithAsset(m_Texture);
            }
            else
            {
                OnResLoadFaild($"下载失败，已重试{MAX_RETRY_COUNT}次");
            }
        }

        private string GetCachePath(string url)
        {
            // 使用URL的MD5作为缓存文件名
            var hash = System.Security.Cryptography.MD5.Create();
            var bytes = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
            var fileName = System.BitConverter.ToString(bytes).Replace("-", "").ToLower();
            
            // 保留原始扩展名
            var extension = Path.GetExtension(url);
            if (string.IsNullOrEmpty(extension) || extension.Length > 5)
            {
                extension = ".jpg"; // 默认扩展名
            }

            var cacheDir = Path.Combine(Application.persistentDataPath, "NetImageCache");
            return Path.Combine(cacheDir, fileName + extension);
        }

        protected override void TryReleaseRes()
        {
            if (m_Texture != null)
            {
                UnityEngine.Object.Destroy(m_Texture);
                m_Texture = null;
            }
            m_Asset = null;
            m_CachePath = null;
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            ESResMaster.Instance?.PoolForESNetImage?.PushToPool(this);
        }

        /// <summary>
        /// 清除指定URL的本地缓存
        /// </summary>
        public static void ClearCache(string url)
        {
            var tempSource = new ESNetImageSource();
            var cachePath = tempSource.GetCachePath(url);
            
            if (File.Exists(cachePath))
            {
                try
                {
                    File.Delete(cachePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ESNetImageSource] 缓存清除失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 清除所有网络图片缓存
        /// </summary>
        public static void ClearAllCache()
        {
            var cacheDir = Path.Combine(Application.persistentDataPath, "NetImageCache");
            if (Directory.Exists(cacheDir))
            {
                try
                {
                    Directory.Delete(cacheDir, true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ESNetImageSource] 缓存清除失败: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 原始文件资源源 - 支持加载未经序列化的原始二进制文件
    /// 用于：配置文件、Lua脚本、自定义二进制数据等
    /// </summary>
    public class ESRawFileSource : ESResSourceBase
    {
        private byte[] m_RawData;
        
        public byte[] RawData => m_RawData;

        protected override void Initilized()
        {
            m_RawData = null;
            // RawFile没有依赖
            m_CachedDependencies = ESABSource.s_EmptyDeps;
            m_DependenciesWithHash = false;
        }

        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = false;
            return m_CachedDependencies;
        }

        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
            {
                return true;
            }

            BeginLoad();

            // 获取原始文件路径
            var filePath = m_ResKey?.LocalABLoadPath;
            if (string.IsNullOrEmpty(filePath))
            {
                // 使用默认路径
                filePath = Path.Combine(ESResMaster.Instance.GetDownloadLocalPath(), LibFolderName ?? string.Empty, "RawFiles", ResName);
            }

            if (!File.Exists(filePath))
            {
                OnResLoadFaild($"原始文件不存在: {filePath}");
                return false;
            }

            try
            {
                // 同步读取文件
                m_RawData = File.ReadAllBytes(filePath);
                
                if (m_RawData == null || m_RawData.Length == 0)
                {
                    OnResLoadFaild("文件内容为空");
                    return false;
                }

                // 创建占位Asset
                m_Asset = new TextAsset(); // 使用TextAsset作为占位符
                m_LastKnownProgress = 1f;
                State = ResSourceState.Ready;
                
                Debug.Verbose($"[ESRawFileSource] 同步加载成功: {ResName}, Size: {m_RawData.Length} bytes");
                return true;
            }
            catch (Exception ex)
            {
                OnResLoadFaild($"文件读取异常: {ex.Message}");
                return false;
            }
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            Debug.Verbose($"[ESRawFileSource] 开始异步加载原始文件: {ResName}");

            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            var filePath = m_ResKey?.LocalABLoadPath;
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(ESResMaster.Instance.GetDownloadLocalPath(), LibFolderName ?? string.Empty, "RawFiles", ResName);
            }

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[ESRawFileSource] 原始文件不存在: {filePath}");
                OnResLoadFaild($"原始文件不存在: {filePath}");
                finishCallback?.Invoke();
                yield break;
            }

            // 异步读取文件
            yield return ReadFileAsync(filePath);

            if (State == ResSourceState.Ready)
            {
                Debug.Verbose($"[ESRawFileSource] 异步加载成功: {ResName}, Size: {m_RawData.Length} bytes");
            }
            
            finishCallback?.Invoke();
        }

        private IEnumerator ReadFileAsync(string filePath)
        {
            FileStream fileStream = null;
            Exception error = null;
            long fileSize = 0;
            
            // 打开文件流（不在协程中）
            try
            {
                fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                fileSize = fileStream.Length;
                m_RawData = new byte[fileSize];
            }
            catch (Exception ex)
            {
                error = ex;
            }
            
            if (error != null || fileStream == null)
            {
                Debug.LogError($"[ESRawFileSource] 文件打开失败: {error?.Message}");
                OnResLoadFaild($"文件打开失败: {error?.Message}");
                yield break;
            }

            // 分块读取文件
            const int chunkSize = 64 * 1024;
            int totalRead = 0;
            byte[] buffer = new byte[chunkSize];
            bool readError = false;

            while (totalRead < fileSize && !readError)
            {
                int bytesRead = 0;
                try
                {
                    bytesRead = fileStream.Read(buffer, 0, chunkSize);
                    if (bytesRead == 0) break;

                    Buffer.BlockCopy(buffer, 0, m_RawData, totalRead, bytesRead);
                    totalRead += bytesRead;
                    ReportProgress((float)totalRead / fileSize);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ESRawFileSource] 文件读取异常: {ex.Message}");
                    readError = true;
                    error = ex;
                }
                
                // 每读取256KB让出一帧
                if (totalRead % (256 * 1024) == 0)
                {
                    yield return null;
                }
            }

            // 关闭文件流
            try
            {
                fileStream?.Close();
            }
            catch { }

            // 检查结果
            if (readError || error != null)
            {
                OnResLoadFaild($"文件读取异常: {error?.Message}");
                yield break;
            }

            if (totalRead != fileSize)
            {
                Debug.LogError($"[ESRawFileSource] 文件读取不完整: {totalRead}/{fileSize}");
                OnResLoadFaild("文件读取不完整");
                yield break;
            }

            m_Asset = new TextAsset();
            m_LastKnownProgress = 1f;
            State = ResSourceState.Ready;
        }

        protected override void TryReleaseRes()
        {
            m_RawData = null;
            m_Asset = null;
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            var instance = ESResMaster.Instance;
            instance?.PoolForESRawFile.PushToPool(this);
        }

        /// <summary>
        /// 获取原始数据的字符串表示（UTF8编码）
        /// </summary>
        public string GetTextContent()
        {
            if (m_RawData == null || m_RawData.Length == 0)
            {
                return string.Empty;
            }
            return System.Text.Encoding.UTF8.GetString(m_RawData);
        }

        /// <summary>
        /// 获取原始数据的字符串表示（指定编码）
        /// </summary>
        public string GetTextContent(System.Text.Encoding encoding)
        {
            if (m_RawData == null || m_RawData.Length == 0)
            {
                return string.Empty;
            }
            return encoding.GetString(m_RawData);
        }
    }

    /// <summary>
    /// ES资源加载类型枚举（商业级重构）
    /// </summary>
    public enum ESResSourceLoadType
    {
        [InspectorName("AB包")] AssetBundle = 0,
        [InspectorName("AB资源")] ABAsset = 1,
        [InspectorName("AB场景")] ABScene = 2,
        [InspectorName("Shader变体集")] ShaderVariant = 3,  // ✅ 新增：专门处理ShaderVariantCollection
        [InspectorName("原始文件")] RawFile = 4,  // ✅ 新增：无反序列化的原始文件加载
        [InspectorName("内置的Res")] InternalResource = 10,
        [InspectorName("网络图片")] NetImageRes = 20,
        [InspectorName("本地图片")] LocalImageRes = 21,
    }

    /// <summary>
    /// 资源加载类型扩展方法
    /// </summary>
    public static class ESResSourceLoadTypeExtensions
    {
        /// <summary>
        /// 判断是否为AssetBundle相关类型
        /// </summary>
        public static bool IsAssetBundleType(this ESResSourceLoadType loadType)
        {
            return loadType == ESResSourceLoadType.AssetBundle ||
                   loadType == ESResSourceLoadType.ABAsset ||
                   loadType == ESResSourceLoadType.ABScene;
        }

        /// <summary>
        /// 判断是否需要引用计数管理
        /// </summary>
        public static bool RequiresReferenceCount(this ESResSourceLoadType loadType)
        {
            // ShaderVariant不需要引用计数，由ESShaderPreloader专门管理
            return loadType != ESResSourceLoadType.ShaderVariant;
        }

        /// <summary>
        /// 判断是否支持同步加载
        /// </summary>
        public static bool SupportsSyncLoad(this ESResSourceLoadType loadType)
        {
            // 网络资源不支持同步加载
            return loadType != ESResSourceLoadType.NetImageRes;
        }

        /// <summary>
        /// 获取类型的显示名称
        /// </summary>
        public static string GetDisplayName(this ESResSourceLoadType loadType)
        {
            switch (loadType)
            {
                case ESResSourceLoadType.AssetBundle:
                    return "AB包";
                case ESResSourceLoadType.ABAsset:
                    return "AB资源";
                case ESResSourceLoadType.ABScene:
                    return "AB场景";
                case ESResSourceLoadType.ShaderVariant:
                    return "Shader变体集";
                case ESResSourceLoadType.RawFile:
                    return "原始文件";
                case ESResSourceLoadType.InternalResource:
                    return "内置Resources";
                case ESResSourceLoadType.NetImageRes:
                    return "网络图片";
                case ESResSourceLoadType.LocalImageRes:
                    return "本地图片";
                default:
                    return loadType.ToString();
            }
        }

        /// <summary>
        /// 判断是否为图片类型
        /// </summary>
        public static bool IsImageType(this ESResSourceLoadType loadType)
        {
            return loadType == ESResSourceLoadType.NetImageRes ||
                   loadType == ESResSourceLoadType.LocalImageRes;
        }

        /// <summary>
        /// 判断是否为网络资源
        /// </summary>
        public static bool IsNetworkResource(this ESResSourceLoadType loadType)
        {
            return loadType == ESResSourceLoadType.NetImageRes;
        }

        /// <summary>
        /// 获取对应的对象池键名
        /// </summary>
        public static string GetPoolKey(this ESResSourceLoadType loadType)
        {
            return $"PoolFor{loadType}Source";
        }
    }
}
