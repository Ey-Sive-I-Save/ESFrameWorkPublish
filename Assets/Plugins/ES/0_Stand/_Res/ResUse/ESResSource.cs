using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// ESResSource 抽象基类
    /// 
    /// 单个资源加载源抽象：
    /// - 既是一个可枚举任务（IEnumeratorTask），用于被 ESResMaster 调度；
    /// - 又暴露 ResName / ABName / Asset / Progress 等查询信息；
    /// - 通过 OnLoadOKAction_Submit / WithDraw 维护回调列表，支持多处监听加载完成。
    /// - 维护状态机（Waiting/Loading/Ready）；
    /// - 负责记录 ABName / ResName / Asset；
    /// - 在状态切换到 Ready 时触发已注册的回调；
    /// - 同时实现 IPoolableAuto，便于通过对象池反复复用。
    /// 具体加载细节交给子类（如从本地 AB 或远程流加载）。
    /// </summary>
    public abstract class ESResSource : IEnumeratorTask, IPoolableAuto
    {
        #region 私有保护
        protected string m_ResName;
        protected string m_ABName;
        public string libFolderName;
        private ResSourceState m_ResSourceState = ResSourceState.Waiting;
        protected UnityEngine.Object m_Asset;
        protected float m_LastKnownProgress;
        protected string m_LastErrorMessage;
        private event Action<bool, ESResSource> m_OnLoadOKAction;
        public ESResSourceLoadType m_LoadType;
        private int m_ReferenceCount;
        public void Set(string ABName, string _ResName, string libFolderName, ESResSourceLoadType loadType)
        {
            m_ABName = ABName;
            m_ResName = _ResName;
            this.libFolderName = libFolderName;
            m_LoadType = loadType;
            IsRecycled = false;
            m_LastKnownProgress = 0f;
            m_LastErrorMessage = null;
            m_ReferenceCount = 0;
        }
        #endregion
        //路径

        public string ResName { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => m_ResName; }
        public ResSourceState State
        {
            get { return m_ResSourceState; }
            set
            {
                if (m_ResSourceState != value)
                {
                    m_ResSourceState = value;
                    if (m_ResSourceState == ResSourceState.Ready)
                    {
                        Method_ResLoadOK(true);
                    }
                }
            }
        }
        public string ABName => m_ABName;

        public Type TargetType { get; set; }

        public UnityEngine.Object Asset => m_Asset;

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

        public bool IsRecycled { get; set; }

        public int ReferenceCount => m_ReferenceCount;

        public bool HasError => !string.IsNullOrEmpty(m_LastErrorMessage);

        public string LastErrorMessage => m_LastErrorMessage;

        public bool IsLoading => m_ResSourceState == ResSourceState.Loading;

        protected virtual float CalculateProgress()
        {
            return 0;
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

        internal int RetainReference()
        {
            if (m_ReferenceCount < 0)
            {
                m_ReferenceCount = 0;
            }

            m_ReferenceCount++;
            return m_ReferenceCount;
        }

        internal int ReleaseReference()
        {
            if (m_ReferenceCount <= 0)
            {
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
        public void OnLoadOKAction_Submit(Action<bool, ESResSource> listener)
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

        public void OnLoadOKAction_WithDraw(Action<bool, ESResSource> listener)
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
        protected void OnResLoadFaild(string message = null)
        {
            m_LastErrorMessage = message;
            m_LastKnownProgress = 0f;
            m_ResSourceState = ResSourceState.Waiting;
            Method_ResLoadOK(false);
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
            Debug.Log($"[ESResSource.LoadAsync] 开始异步加载资源: {ResName}, 当前状态: {State}");

            //必须处于无状态
            if (State == ResSourceState.Loading || State == ResSourceState.Ready)
            {
                Debug.Log($"[ESResSource.LoadAsync] 资源 '{ResName}' 已在加载或已就绪状态 ({State})，跳过异步加载。");
                return;
            }

            //资源名有效
            if (string.IsNullOrEmpty(ResName))
            {
                Debug.LogError($"[ESResSource.LoadAsync] 资源名为空，无法开始异步加载。");
                return;
            }

            Debug.Log($"[ESResSource.LoadAsync] 资源 '{ResName}' 通过状态和名称检查，开始加载。");
            BeginLoad();
            //开始推送加载
            Debug.Log($"[ESResSource.LoadAsync] 推送资源 '{ResName}' 到加载任务队列。");
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
            Debug.Log($"[ESResSource.IsDependResLoadFinish] 检查资源 '{ResName}' 的依赖加载状态。");

            var dependsAB = GetDependResSourceAllAssetBundles(out var withHash);
            Debug.Log($"[ESResSource.IsDependResLoadFinish] 获取到 {dependsAB?.Length ?? 0} 个依赖AB，withHash: {withHash}。");

            if (dependsAB == null || dependsAB.Length == 0)
            {
                Debug.Log($"[ESResSource.IsDependResLoadFinish] 资源 '{ResName}' 无依赖，返回true。");
                return true;
            }

            //倒着测试
            for (var i = dependsAB.Length - 1; i >= 0; --i)
            {
                //抓AB
                string pre = withHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                Debug.Log($"[ESResSource.IsDependResLoadFinish] 检查依赖AB: {pre} (原始: {dependsAB[i]})");

                if (ESResMaster.GlobalABKeys.TryGetValue(pre, out var abKey))
                {
                    var res = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
                    if (res == null)
                    {
                        Debug.LogWarning($"[ESResSource.IsDependResLoadFinish] 依赖AB '{pre}' 未找到资源源，返回false。");
                        return false;
                    }
                    if (res.State != ResSourceState.Ready)
                    {
                        Debug.Log($"[ESResSource.IsDependResLoadFinish] 依赖AB '{pre}' 状态为 {res.State}，未就绪，返回false。");
                        return false;
                    }
                    Debug.Log($"[ESResSource.IsDependResLoadFinish] 依赖AB '{pre}' 已就绪。");
                }
                else
                {
                    Debug.LogWarning($"[ESResSource.IsDependResLoadFinish] 未找到依赖AB键: {pre}，返回false。");
                    return false;
                }
            }

            Debug.Log($"[ESResSource.IsDependResLoadFinish] 资源 '{ResName}' 所有依赖已就绪，返回true。");
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
            m_ResName = null;
            m_OnLoadOKAction = null;
            m_ABName = null;
            m_Asset = null;
            m_LastKnownProgress = 0f;
            m_LastErrorMessage = null;
            m_ResSourceState = ResSourceState.Waiting;
            IsRecycled = false;
            m_ReferenceCount = 0;
        }
    }
    public class ESABSource : ESResSource
    {
        public bool IsNet = true;
        public readonly string[] Nulldeps = new string[0];
        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = false;//Dependences是不带hash
            if (!ESResMaster.GlobalDependencies.TryGetValue(ABName, out var deps))
            {
                return Nulldeps;
            }
            return deps;
        }

        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
            {
                return true;
            }

            BeginLoad();

            var cached = ESResMaster.HasLoadedAB(m_ResName);
            if (cached != null)
            {
                return CompleteWithAsset(cached);
            }

            var dependsAB = GetDependResSourceAllAssetBundles(out bool withHash);
            if (dependsAB != null && dependsAB.Length > 0)
            {
                for (int i = 0; i < dependsAB.Length; i++)
                {
                    string pre = withHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                    if (ESResMaster.GlobalABKeys.TryGetValue(pre, out var abKey))
                    {
                        var res = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
                        if (res == null || res.State != ResSourceState.Ready)
                        {
                            ESResMaster.MainLoader.AddAB2LoadByABPreNameSourcer(pre);
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

            string localBasePath = ESResMaster.Instance.GetDownloadLocalPath();
            string bundlePath = Path.Combine(localBasePath, ResName);
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
            Debug.Log($"[ESABSource.DoTaskAsync] 开始异步加载AssetBundle任务: {ResName}");

            if (State == ResSourceState.Ready)
            {
                Debug.Log($"[ESABSource.DoTaskAsync] AssetBundle '{ResName}' 已就绪，直接调用完成回调。");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.Log($"[ESABSource.DoTaskAsync] 初始化加载状态: {ResName}");
            BeginLoad();

            var cached = ESResMaster.HasLoadedAB(m_ResName);
            if (cached != null)
            {
                Debug.Log($"[ESABSource.DoTaskAsync] 使用缓存的AssetBundle: {ResName}");
                CompleteWithAsset(cached);
                finishCallback?.Invoke();
                yield break;
            }

            var dependsAB = GetDependResSourceAllAssetBundles(out bool withHash);
            Debug.Log($"[ESABSource.DoTaskAsync] 获取到 {dependsAB?.Length ?? 0} 个依赖AB，withHash: {withHash}");

            var dependencyResults = new Dictionary<ESResSource, bool?>();
            if (dependsAB != null && dependsAB.Length > 0)
            {
                for (int i = 0; i < dependsAB.Length; i++)
                {
                    string pre = withHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                    Debug.Log($"[ESABSource.DoTaskAsync] 处理依赖AB: {pre} (原始: {dependsAB[i]})");

                    if (!ESResMaster.GlobalABKeys.TryGetValue(pre, out var abKey))
                    {
                        Debug.LogError($"[ESABSource.DoTaskAsync] 未找到依赖AssetBundle键: {pre}，加载失败。");
                        OnResLoadFaild($"未找到依赖AssetBundle键: {pre}");
                        finishCallback?.Invoke();
                        yield break;
                    }

                    var dependencyRes = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
                    if (dependencyRes == null)
                    {
                        Debug.LogError($"[ESABSource.DoTaskAsync] 未找到依赖AssetBundle资源源: {pre}，加载失败。");
                        OnResLoadFaild($"未找到依赖AssetBundle: {pre}");
                        finishCallback?.Invoke();
                        yield break;
                    }

                    if (dependencyRes.State == ResSourceState.Ready)
                    {
                        Debug.Log($"[ESABSource.DoTaskAsync] 依赖AB '{pre}' 已就绪。");
                        dependencyResults[dependencyRes] = true;
                    }
                    else
                    {
                        Debug.Log($"[ESABSource.DoTaskAsync] 依赖AB '{pre}' 未就绪，添加到加载队列。");
                        dependencyResults[dependencyRes] = null;
                        var captured = dependencyRes;
                        captured.OnLoadOKAction_Submit((success, _) => dependencyResults[captured] = success);
                        ESResMaster.MainLoader.AddAB2LoadByABPreNameSourcer(pre);
                    }
                }

                if (dependencyResults.Values.Any(v => v == null))
                {
                    Debug.Log($"[ESABSource.DoTaskAsync] 开始异步加载所有依赖AB。");
                    ESResMaster.MainLoader.LoadAllAsync();
                    while (dependencyResults.Values.Any(v => v == null))
                    {
                        ReportProgress(0.1f);
                        yield return null;
                    }
                }

                if (dependencyResults.Values.Any(v => v == false))
                {
                    Debug.LogError($"[ESABSource.DoTaskAsync] 依赖AssetBundle加载失败。");
                    OnResLoadFaild("依赖AssetBundle加载失败");
                    finishCallback?.Invoke();
                    yield break;
                }

                Debug.Log($"[ESABSource.DoTaskAsync] 所有依赖AB加载成功。");
            }
            else
            {
                Debug.Log($"[ESABSource.DoTaskAsync] AssetBundle '{ResName}' 无依赖。");
            }

            Debug.Log($"[ESABSource.DoTaskAsync] 开始加载自身AssetBundle: {ResName}");
            yield return LoadSelf();

            Debug.Log($"[ESABSource.DoTaskAsync] AssetBundle '{ResName}' 加载完成。");
            finishCallback?.Invoke();
        }
        private IEnumerator LoadSelf()
        {
            Debug.Log($"[ESABSource.LoadSelf] 开始加载AssetBundle: {ResName}");

            if (m_Asset == null)
            {
                var bundlePath = Path.Combine(ESResMaster.Instance.GetDownloadLocalPath(), libFolderName, "AB", ResName);
                Debug.Log($"[ESABSource.LoadSelf] 创建异步加载请求（含Hash）: {ResName})");
                var request = AssetBundle.LoadFromFileAsync(bundlePath);
                if (request == null)
                {
                    Debug.LogError($"[ESABSource.LoadSelf] 无法创建带Hash的AssetBundle加载请求: {ResName}");
                    OnResLoadFaild("无法创建AssetBundle加载请求");
                    yield break;
                }

                Debug.Log($"[ESABSource.LoadSelf] 开始等待带Hash的AssetBundle加载完成: {ResName}");

                Debug.Log($"[ESABSource.LoadSelf] 开始等待AssetBundle加载完成: {bundlePath}");
                while (!request.isDone)
                {
                    float progress = Mathf.Lerp(0.2f, 0.95f, request.progress);
                    ReportProgress(progress);
                    Debug.Log($"[ESABSource.LoadSelf] 加载进度: {progress:F2} for {bundlePath}");
                    yield return null;
                }

                if (!CompleteWithAsset(request.assetBundle))
                {
                    Debug.LogError($"[ESABSource.LoadSelf] 异步加载AssetBundle失败: {bundlePath}");
                    yield break;
                }
                else
                {
                    Debug.Log($"[ESABSource.LoadSelf] AssetBundle加载成功: {bundlePath}");
                }
                Debug.Log($"[ESABSource.LoadSelf] AssetBundle '{ResName}' 加载完成，设置进度为1。");
                ReportProgress(1f);
            }
            else
            {
                Debug.Log($"[ESABSource.LoadSelf] AssetBundle '{ResName}' 已加载，跳过加载步骤。");
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
    }
    public class ESAssetSource : ESResSource
    {
        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
            {
                return true;
            }

            BeginLoad();

            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
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
            Debug.Log($"[ESAssetSource.DoTaskAsync] 开始异步加载任务: {ResName}");

            // 如果资源已就绪，直接调用完成回调并退出协程
            if (State == ResSourceState.Ready)
            {
                Debug.Log($"[ESAssetSource.DoTaskAsync] 资源 '{ResName}' 已就绪，直接调用完成回调。");
                finishCallback?.Invoke();
                yield break;
            }

            // 初始化加载状态
            Debug.Log($"[ESAssetSource.DoTaskAsync] 初始化加载状态: {ResName}");
            BeginLoad();

            // 检查全局AB键字典中是否存在对应的AssetBundle键
            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
                // 如果未找到键，记录错误并调用完成回调
                Debug.LogError($"[ESAssetSource.DoTaskAsync] 未找到AB键: {ABName}，加载失败。");
                OnResLoadFaild($"未找到AB键: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.Log($"[ESAssetSource.DoTaskAsync] 找到AB键: {ABName} -> {abKey}");

            // 获取AssetBundle资源源
            var abResSou = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
            if (abResSou == null)
            {
                // 如果未找到AssetBundle资源源，记录错误并调用完成回调
                Debug.LogError($"[ESAssetSource.DoTaskAsync] 未找到AssetBundle资源源: {ABName}，加载失败。");
                OnResLoadFaild($"未找到AssetBundle资源: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            Debug.Log($"[ESAssetSource.DoTaskAsync] 获取到AssetBundle资源源: {abResSou.ResName}，状态: {abResSou.State}");

            // 如果AssetBundle未就绪，等待其加载完成
            if (abResSou.State != ResSourceState.Ready)
            {
                Debug.Log($"[ESAssetSource.DoTaskAsync] AssetBundle '{ABName}' 未就绪，开始等待依赖加载。");

                // 初始化依赖加载状态变量
                bool dependencyCompleted = false;
                bool dependencySuccess = false;
                string dependencyError = null;

                // 注册AssetBundle加载完成回调
                abResSou.OnLoadOKAction_Submit((success, res) =>
                {
                    dependencyCompleted = true;
                    dependencySuccess = success;
                    // 如果加载失败，记录错误信息
                    if (!success && res is ESResSource resSource && resSource.HasError)
                    {
                        dependencyError = resSource.LastErrorMessage;
                    }
                    Debug.Log($"[ESAssetSource.DoTaskAsync] 依赖AssetBundle '{ABName}' 加载完成，结果: {success}");
                });

                // 开始异步加载AssetBundle
                Debug.Log($"[ESAssetSource.DoTaskAsync] 开始异步加载AssetBundle: {ABName}");
                abResSou.LoadAsync();

                // 等待AssetBundle加载完成
                while (!dependencyCompleted)
                {
                    // 报告进度（依赖加载阶段）
                    ReportProgress(0.1f);
                    yield return null;
                }

                // 检查依赖加载结果
                if (!dependencySuccess)
                {
                    // 如果依赖加载失败，记录错误并调用完成回调
                    Debug.LogError($"[ESAssetSource.DoTaskAsync] 依赖AssetBundle '{ABName}' 加载失败: {dependencyError}");
                    OnResLoadFaild(string.IsNullOrEmpty(dependencyError) ? "依赖AssetBundle加载失败" : dependencyError);
                    finishCallback?.Invoke();
                    yield break;
                }

                Debug.Log($"[ESAssetSource.DoTaskAsync] 依赖AssetBundle '{ABName}' 加载成功。");
            }
            else
            {
                Debug.Log($"[ESAssetSource.DoTaskAsync] AssetBundle '{ABName}' 已就绪，跳过依赖加载。");
            }

            // 如果AssetBundle已就绪且是AssetBundle类型，开始加载自身资源
            if (abResSou.Asset is AssetBundle ab)
            {
                Debug.Log($"[ESAssetSource.DoTaskAsync] 开始加载自身资源: {ResName} 从AssetBundle: {ABName}");
                // 调用子协程加载自身资源
                yield return LoadSelf(ab);
                // 加载完成后调用完成回调
                Debug.Log($"[ESAssetSource.DoTaskAsync] 自身资源 '{ResName}' 加载完成。");
                finishCallback?.Invoke();
                yield break;
            }

            // 如果AssetBundle未就绪，记录错误并调用完成回调
            Debug.LogError($"[ESAssetSource.DoTaskAsync] AssetBundle '{ABName}' 未就绪，加载失败。");
            OnResLoadFaild($"AssetBundle未就绪: {ABName}");
            finishCallback?.Invoke();
        }
        private IEnumerator LoadSelf(AssetBundle ab)
        {
            if (ab != null)
            {
                AssetBundleRequest request = TargetType != null ? ab.LoadAssetAsync(m_ResName, TargetType) : ab.LoadAssetAsync(m_ResName);
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
    public enum ESResSourceLoadType
    {
        [InspectorName("AB包")] AssetBundle = 0,
        [InspectorName("AB资源")] ABAsset = 1,
        [InspectorName("AB场景")] ABScene = 2,
        [InspectorName("内置的Res")] InternalResource = 3,
        [InspectorName("网络图片")] NetImageRes = 4,
        [InspectorName("本地图片")] LocalImageRes = 5,
    }
}
