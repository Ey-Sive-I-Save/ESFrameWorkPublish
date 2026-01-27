using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private ResSourceState m_ResSourceState = ResSourceState.Waiting;
        protected UnityEngine.Object m_Asset;
        protected float m_LastKnownProgress;
        protected string m_LastErrorMessage;
        private event Action<bool, ESResSource> m_OnLoadOKAction;
        public ESResSourceLoadType m_LoadType;
        private int m_ReferenceCount;
        public void Set(string ABName, string ResName, ESResSourceLoadType loadType)
        {
            m_ABName = ABName;
            m_ResName = ResName;
            m_LoadType = loadType;
            IsRecycled = false;
            m_LastKnownProgress = 0f;
            m_LastErrorMessage = null;
            m_ReferenceCount = 0;
        }
        #endregion
        //路径
        public string ResName => m_ResName;
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
        public override string[] GetDependResSourceAllAssetBundles(out bool withHash)
        {
            withHash = true;//带hash
            return ESResMaster.Instance.MainManifest.GetAllDependencies(m_ResName);
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
            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            var cached = ESResMaster.HasLoadedAB(m_ResName);
            if (cached != null)
            {
                CompleteWithAsset(cached);
                finishCallback?.Invoke();
                yield break;
            }

            var dependsAB = GetDependResSourceAllAssetBundles(out bool withHash);
            var dependencyResults = new Dictionary<ESResSource, bool?>();
            if (dependsAB != null && dependsAB.Length > 0)
            {
                for (int i = 0; i < dependsAB.Length; i++)
                {
                    string pre = withHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                    if (!ESResMaster.GlobalABKeys.TryGetValue(pre, out var abKey))
                    {
                        OnResLoadFaild($"未找到依赖AssetBundle键: {pre}");
                        finishCallback?.Invoke();
                        yield break;
                    }

                    var dependencyRes = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
                    if (dependencyRes == null)
                    {
                        OnResLoadFaild($"未找到依赖AssetBundle: {pre}");
                        finishCallback?.Invoke();
                        yield break;
                    }

                    if (dependencyRes.State == ResSourceState.Ready)
                    {
                        dependencyResults[dependencyRes] = true;
                    }
                    else
                    {
                        dependencyResults[dependencyRes] = null;
                        var captured = dependencyRes;
                        captured.OnLoadOKAction_Submit((success, _) => dependencyResults[captured] = success);
                        ESResMaster.MainLoader.AddAB2LoadByABPreNameSourcer(pre);
                    }
                }

                if (dependencyResults.Values.Any(v => v == null))
                {
                    ESResMaster.MainLoader.LoadAllAsync();
                    while (dependencyResults.Values.Any(v => v == null))
                    {
                        ReportProgress(0.1f);
                        yield return null;
                    }
                }

                if (dependencyResults.Values.Any(v => v == false))
                {
                    OnResLoadFaild("依赖AssetBundle加载失败");
                    finishCallback?.Invoke();
                    yield break;
                }
            }

            yield return LoadSelf();

            finishCallback?.Invoke();
        }
        private IEnumerator LoadSelf()
        {
            if (m_Asset == null)
            {
                string localBasePath = ESResMaster.Instance.GetDownloadLocalPath();
                string bundlePath = Path.Combine(localBasePath, ResName);
                var request = AssetBundle.LoadFromFileAsync(bundlePath);

                if (request == null)
                {
                    OnResLoadFaild("无法创建AssetBundle加载请求");
                    yield break;
                }

                while (!request.isDone)
                {
                    ReportProgress(Mathf.Lerp(0.2f, 0.95f, request.progress));
                    yield return null;
                }

                if (!CompleteWithAsset(request.assetBundle))
                {
                    Debug.LogError($"异步加载AssetBundle失败: {bundlePath}");
                    yield break;
                }
            }
            else
            {
                CompleteWithAsset(m_Asset);
            }
            ReportProgress(1f);
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
        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            if (!ESResMaster.GlobalABKeys.TryGetValue(ABName, out var abKey))
            {
                OnResLoadFaild($"未找到AB键: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            var abResSou = ESResMaster.Instance.GetResSourceByKey(abKey, ESResSourceLoadType.AssetBundle);
            if (abResSou == null)
            {
                OnResLoadFaild($"未找到AssetBundle资源: {ABName}");
                finishCallback?.Invoke();
                yield break;
            }

            if (abResSou.State != ResSourceState.Ready)
            {
                bool dependencyCompleted = false;
                bool dependencySuccess = false;
                string dependencyError = null;

                abResSou.OnLoadOKAction_Submit((success, res) =>
                {
                    dependencyCompleted = true;
                    dependencySuccess = success;
                    if (!success && res is ESResSource resSource && resSource.HasError)
                    {
                        dependencyError = resSource.LastErrorMessage;
                    }
                });

                abResSou.LoadAsync();

                while (!dependencyCompleted)
                {
                    ReportProgress(0.1f);
                    yield return null;
                }

                if (!dependencySuccess)
                {
                    OnResLoadFaild(string.IsNullOrEmpty(dependencyError) ? "依赖AssetBundle加载失败" : dependencyError);
                    finishCallback?.Invoke();
                    yield break;
                }
            }

            if (abResSou.Asset is AssetBundle ab)
            {
                yield return LoadSelf(ab);
                finishCallback?.Invoke();
                yield break;
            }

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
