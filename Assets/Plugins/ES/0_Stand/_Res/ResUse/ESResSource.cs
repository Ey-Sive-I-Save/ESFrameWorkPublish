using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// IResSource
    /// 
    /// 单个资源加载源抽象：
    /// - 既是一个可枚举任务（IEnumeratorTask），用于被 ESResMaster 调度；
    /// - 又暴露 ResName / ABName / Asset / Progress 等查询信息；
    /// - 通过 OnLoadOKAction_Submit / WithDraw 维护回调列表，支持多处监听加载完成。
    /// </summary>
    public interface IResSource : IEnumeratorTask
    {
        string ResName { get; }

        string ABName { get; }
        Type TargetType { get; set; }

        ResSourceState State { get; }

        UnityEngine.Object Asset { get; }

        float Progress { get; }



        void OnLoadOKAction_Submit(Action<bool, IResSource> listener);
        void OnLoadOKAction_WithDraw(Action<bool, IResSource> listener);

        bool LoadSync();

        void LoadAsync();

        string[] GetDependResSourceAllAssetBundles(out bool withHash);

        bool IsDependResLoadFinish();

        bool ReleaseTheResSource();

        void TryAutoPushedToPool();

    }
    /// <summary>
    /// ESResSource 抽象基类
    /// 
    /// 实现了 IResSource 的大部分通用逻辑：
    /// - 维护状态机（Waiting/Loading/Ready）；
    /// - 负责记录 ABName / ResName / Asset；
    /// - 在状态切换到 Ready 时触发已注册的回调；
    /// - 同时实现 IPoolablebAuto，便于通过对象池反复复用。
    /// 具体加载细节交给子类（如从本地 AB 或远程流加载）。
    /// </summary>
    public abstract class ESResSource : IResSource, IPoolablebAuto
    {
        #region 私有保护
        protected string m_ResName;
        protected string m_ABName;
        private ResSourceState m_ResSourceState = ResSourceState.Waiting;
        protected UnityEngine.Object m_Asset;
        private event Action<bool, IResSource> m_OnLoadOKAction;
        public ESResSourceLoadType m_LoadType;
        public void Set(string ABName, string ResName, ESResSourceLoadType loadType)
        {
            m_ABName = ABName;
            m_ResName = ResName;
            m_LoadType = loadType;
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
                        return CalculateProgress();
                    case ResSourceState.Ready:
                        return 1;
                }
                return 0;
            }
        }

        public bool IsRecycled { get; set; }

        protected virtual float CalculateProgress()
        {
            return 0;
        }
        public void OnLoadOKAction_Submit(Action<bool, IResSource> listener)
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

        public void OnLoadOKAction_WithDraw(Action<bool, IResSource> listener)
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
        protected void OnResLoadFaild()
        {
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
            //必须处于无状态
            if (State != ResSourceState.Waiting)
            {
                return;
            }
            //资源名有效
            if (string.IsNullOrEmpty(ResName))
            {
                return;
            }
            //开始推送加载
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
            var dependsAB = GetDependResSourceAllAssetBundles(out var withHash);
            if (dependsAB == null || dependsAB.Length == 0)
            {
                return true;
            }
            //倒着测试
            for (var i = dependsAB.Length - 1; i >= 0; --i)
            {
                //抓AB
                string pre = withHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                if (ESResMaster.ESResData_ABKeys.NameToABKeys.TryGetValue(pre, out int index))
                {
                    var res = ESResMaster.Instance.GetResSourceByKey(index, ESResSourceLoadType.AssetBundle);
                    if (res == null || res.State != ResSourceState.Ready)
                    {
                        return false;
                    }
                }
            }

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
                return true;
            }

            TryReleaseRes();

            State = ResSourceState.Waiting;
            m_OnLoadOKAction = null;
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

            //自己完成
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
            if (State == ResSourceState.Ready) return true;
            m_Asset = ESResMaster.HasLoadedAB(m_ResName);
            if (m_Asset != null)
            {
                State = ResSourceState.Ready;
                return true;
            }
            var dependsAB = GetDependResSourceAllAssetBundles(out bool withHash);
            if (dependsAB.Length > 0)
            {
                for (int i = 0; i < dependsAB.Length; i++)
                {
                    string pre = withHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                    if (ESResMaster.ESResData_ABKeys.NameToABKeys.TryGetValue(pre, out int index))
                    {
                        var res = ESResMaster.Instance.GetResSourceByKey(index, ESResSourceLoadType.AssetBundle);
                        if (res == null || res.State != ResSourceState.Ready)
                        {
                            ESResMaster.MainLoader.AddAB2LoadByABPreNameSourcer(pre);

                        }
                    }
                }
                ESResMaster.MainLoader.LoadAll_Sync();
            }
            m_Asset = AssetBundle.LoadFromFile(IsNet ? ESResMaster.Instance.GetDownloadLocalPath() + "/" + ResName : ESResMaster.Instance.GetDownloadLocalPath() + "/" + ResName) ?? m_Asset;
            if (m_Asset != null)
            {
                State = ResSourceState.Ready;
            }
            return true;
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready) yield break;
            m_Asset = ESResMaster.HasLoadedAB(m_ResName);
            if (m_Asset != null)
            {
                State = ResSourceState.Ready;
                yield break;
            }
            var dependsAB = GetDependResSourceAllAssetBundles(out bool withHash);
            if (dependsAB.Length > 0)
            {
                for (int i = 0; i < dependsAB.Length; i++)
                {
                    string pre = withHash ? ESResMaster.PathAndNameTool_GetPreName(dependsAB[i]) : dependsAB[i];
                    if (ESResMaster.ESResData_ABKeys.NameToABKeys.TryGetValue(pre, out int index))
                    {
                        var res = ESResMaster.Instance.GetResSourceByKey(index, ESResSourceLoadType.AssetBundle);
                        if (res == null || res.State != ResSourceState.Ready)
                        {
                            ESResMaster.MainLoader.AddAB2LoadByABPreNameSourcer(pre);

                        }
                    }
                }
                ESResMaster.MainLoader.LoadAllAsync(() => ESResMaster.Instance.StartCoroutine(LoadSelf(finishCallback)));
            }
            else
            {
                ESResMaster.Instance.StartCoroutine(LoadSelf(finishCallback));
            }

        }
        private IEnumerator LoadSelf(System.Action finishCallback)
        {
            if (m_Asset == null)
            {
                var request = AssetBundle.LoadFromFileAsync(IsNet ? ESResMaster.Instance.GetDownloadLocalPath() + "/" + ResName : ESResMaster.Instance.GetDownloadLocalPath() + "/" + ResName);
                yield return request;
                m_Asset = request.assetBundle;
                if (m_Asset == null)
                {
                    OnResLoadFaild();
                    yield break;
                }
            }
            State = ResSourceState.Ready;
            finishCallback();
        }
        protected override float CalculateProgress()
        {
            return 0;
        }
        public override string ToString()
        {
            return $"T:AB包\t {base.ToString()}";
        }
    }
    public class ESAssetSource : ESResSource
    {
        public override bool LoadSync()
        {
            if (ESResMaster.ESResData_ABKeys.NameToABKeys.TryGetValue(ABName, out int index))
            {
                var abResSou = ESResMaster.Instance.GetResSourceByKey(index, ESResSourceLoadType.AssetBundle);
                if (abResSou == null || abResSou.State != ResSourceState.Ready)
                {
                    abResSou.LoadSync();
                }
                if (abResSou.Asset is AssetBundle ab)
                {
                    m_Asset = ab.LoadAsset(ResName) ?? m_Asset;
                    if (m_Asset != null)
                    {
                        State = ResSourceState.Ready;
                        return true;
                    }
                }
            };

            return false;
        }
        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready) yield break;
            if (ESResMaster.ESResData_ABKeys.NameToABKeys.TryGetValue(ABName, out int index))
            {
                var abResSou = ESResMaster.Instance.GetResSourceByKey(index, ESResSourceLoadType.AssetBundle);
                abResSou.OnLoadOKAction_Submit((b, res) =>
                { if (b) { Debug.Log("资产自动加载依赖AB" + abResSou.ResName); ESResMaster.Instance.StartCoroutine(LoadSelf(res.Asset as AssetBundle, finishCallback)); } });
                abResSou.LoadAsync();
            }
        }
        private IEnumerator LoadSelf(AssetBundle ab, System.Action finishCallback)
        {
            if (ab != null)
            {
                var request = ab.LoadAssetAsync(m_ResName);
                yield return request;
                m_Asset = request.asset ?? m_Asset;

            }
            if (m_Asset == null)
            {
                OnResLoadFaild();
                yield break;
            }
            State = ResSourceState.Ready;
            finishCallback();
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
