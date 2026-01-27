using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// ESResMaster脚本形式的资源总管。
    /// 
    /// 运行时资源总管：
    /// - 作为全局单例（SingletonMono），负责初始化 ESGlobalResSetting；
    /// - 管理 AB 主清单（MainBundle / MainManifest）；
    /// - 串行调度所有实现了 IEnumeratorTask 的资源加载任务；
    /// - 对外提供若干对象池（如 Loader、ResKey、ResSource）的统一入口。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public partial class ESResMaster : SingletonMono<ESResMaster>
    {


        #region 全局设置和初始化
        [Required]
        [LabelText("全局设置")]
        public ESGlobalResSetting Settings;
        [LabelText("自动开始下载")]
        public bool AutoDownload = false;
        protected override void DoAwake()
        {
            base.DoAwake();
            if (Settings != null)
            {
                Settings.RuntimeAwake();//装载
            }

            // 初始化默认路径缓存
            DefaultPaths.InitDefaultPaths();

            if (Application.isPlaying && AutoDownload)
            {
                Debug.Log("ESResMaster: AutoDownload 启动");
                GameInit_ResCompareAndDownload();
            }
        }

        
        #endregion

        #region 准备加载数据
        public AssetBundle MainBundle;
        public AssetBundleManifest MainManifest;
        #endregion

        #region 加载任务链

#pragma warning disable CS0414
        private int mMaxCoroutineCount = 8; //最快协成大概在6到8之间
#pragma warning restore CS0414
        private LinkedList<IEnumeratorTask> ResLoadTasks = new LinkedList<IEnumeratorTask>();
        private bool isLoading = false;
        private void TryStartLoadTask()
        {
            Debug.Log($"[ESResMaster.TryStartLoadTask] 尝试启动加载任务。任务队列长度: {ResLoadTasks.Count}, 正在加载: {isLoading}");

            if (ResLoadTasks.Count == 0)
            {
                Debug.Log("[ESResMaster.TryStartLoadTask] 任务队列为空，无需启动。");
                return;
            }

            if (isLoading)
            {
                Debug.Log("[ESResMaster.TryStartLoadTask] 正在加载中，跳过启动。");
                return;
            }

            Debug.Log("[ESResMaster.TryStartLoadTask] 启动加载任务协程。");
            StartCoroutine(LoadResTask());
        }
        public void PushResLoadTask(IEnumeratorTask task)
        {
            if (task == null)
            {
                return;
            }
            ResLoadTasks.AddLast(task);
            TryStartLoadTask();
        }
        private void OnLoadTaskOK()
        {

        }
        private IEnumerator LoadResTask()
        {
            Debug.Log("[ESResMaster.LoadResTask] 开始加载任务协程，设置isLoading为true，支持并发处理。");
            isLoading = true;
            int runningTasks = 0;
            int taskIndex = 0;

            while (ResLoadTasks.Count > 0 || runningTasks > 0)
            {
                // 启动新任务，如果有空闲槽且队列不空
                while (ResLoadTasks.Count > 0 && runningTasks < mMaxCoroutineCount)
                {
                    Debug.Log($"[ESResMaster.LoadResTask] 启动并发任务 {taskIndex + 1}，当前运行任务数: {runningTasks + 1}/{mMaxCoroutineCount}");
                    var task = ResLoadTasks.First;
                    ResLoadTasks.RemoveFirst();
                    runningTasks++;
                    StartCoroutine(ExecuteTask(task.Value, taskIndex, () =>
                    {
                        runningTasks--;
                        Debug.Log($"[ESResMaster.LoadResTask] 任务 {taskIndex + 1} 执行完成，剩余运行任务数: {runningTasks}");
                    }));
                    taskIndex++;
                }

                yield return null;
            }

            Debug.Log("[ESResMaster.LoadResTask] 所有任务加载完成，设置isLoading为false。");
            isLoading = false;
            yield return null;
        }

        private IEnumerator ExecuteTask(IEnumeratorTask task, int index, Action onComplete)
        {
            Debug.Log($"[ESResMaster.LoadResTask] 开始执行任务 {index + 1}: {task?.ToString() ?? "Unknown"}");
            yield return StartCoroutine(task.DoTaskAsync(OnLoadTaskOK));
            onComplete();
        }
        #endregion

        #region 池化
        public ESSimplePool<ESResKey> PoolForESResKey = new ESSimplePool<ESResKey>(() => new ESResKey(),
(f) =>
{

}
, 30);

        public ESSimplePool<ESResLoader> PoolForESLoader = new ESSimplePool<ESResLoader>(() => new ESResLoader(),
       (f) =>
       {

       }
       , 30);

        public ESSimplePool<ESABSource> PoolForESABSource = new ESSimplePool<ESABSource>(() => new ESABSource(),
(f) =>
{

}
, 30);

        public ESSimplePool<ESAssetSource> PoolForESAsset = new ESSimplePool<ESAssetSource>(() => new ESAssetSource(),
(f) =>
{

}
, 30);
        #region 池操作在这里
        public ESResKey GetInPool_ResKey(string resName, string ownerBundleName = null, Type assetType = null)
        {
            var resSearchRule = ESResMaster.Instance.PoolForESResKey.GetInPool();
            resSearchRule.ResName = resName;
            resSearchRule.ABName = ownerBundleName == null ? null : ownerBundleName;
            resSearchRule.TargetType = assetType;
            return resSearchRule;
        }
        public ESResLoader GetInPool_ESLoader()
        {
            return PoolForESLoader.GetInPool();
        }
        #endregion

        #endregion

        #region 资源源查询
        public ESResSource CreateNewResSourceByKey(object key, ESResSourceLoadType loadType)
        {
            ESResSource retRes = null;

            if (loadType == ESResSourceLoadType.AssetBundle)
            {
                var abKey = (ESResKey)key;
                retRes = CreateResSource_AssetBundle(abKey);
            }
            else if (loadType == ESResSourceLoadType.ABAsset)
            {
                var assetKey = (ESResKey)key;
                retRes = CreateResSource_ABAsset(assetKey);
            }
            /*.Where(creator => creator.Match(resSearchKeys))
            .Select(creator => creator.Create(resSearchKeys))
            .FirstOrDefault();*/

            if (retRes == null)
            {
                Debug.LogError("创建资源源失败了. 找不到这个查找键" + key);
                return null;
            }

            return retRes;
        }

        public ESResSource GetResSourceByKey(object key, ESResSourceLoadType loadType, bool ifNullCreateNew = true)
        {
            ESResSource res = null;
            if (loadType == ESResSourceLoadType.ABAsset)
            {
                res = ResTable.GetAssetResByKey(key);
            }
            else if (loadType == ESResSourceLoadType.AssetBundle)
            {
                res = ResTable.GetABResByKey(key);
            }
            if (res != null)
            {
                AcquireResHandle(key, loadType);
                return res;
            }

            if (!ifNullCreateNew)
            {
                Debug.LogErrorFormat("没找到资源，并且这里也不允许创建:{0}", key);
                return null;
            }

            res = CreateNewResSourceByKey(key, loadType);

            if (res != null)
            {
                bool registered = false;
                if (loadType == ESResSourceLoadType.ABAsset)
                {
                    registered = ResTable.TryRegisterAssetRes(key, res);
                }
                else if (loadType == ESResSourceLoadType.AssetBundle)
                {
                    registered = ResTable.TryRegisterABRes(key, res);
                }

                if (!registered)
                {
                    Debug.LogWarning($"资源键重复注册: {key}");
                }
                AcquireResHandle(key, loadType);
            }

            return res;
        }

        #endregion

        #region 资源源创建方式
        internal ESResSource CreateResSource_AssetBundle(ESResKey abKey)
        {
            var use = PoolForESABSource.GetInPool();
            use.IsNet = true;//还没实装
            use.Set(abKey.ABName, abKey.ABName, ESResSourceLoadType.AssetBundle); // Assuming ABName is used for both
            use.TargetType = typeof(AssetBundle);
            return use;
        }
        internal ESResSource CreateResSource_ABAsset(ESResKey key)
        {
            var use = PoolForESAsset.GetInPool();
            use.Set(key.ABName, key.ResName, ESResSourceLoadType.ABAsset);
            use.TargetType = key.TargetType;
            return use;
        }


        #endregion

        private void AcquireResHandle(object key, ESResSourceLoadType loadType)
        {
            switch (loadType)
            {
                case ESResSourceLoadType.ABAsset:
                    ResTable.AcquireAssetRes(key);
                    break;
                case ESResSourceLoadType.AssetBundle:
                    ResTable.AcquireABRes(key);
                    break;
                default:
                    break;
            }
        }

        internal void ReleaseResHandle(object key, ESResSourceLoadType loadType, bool unloadWhenZero)
        {
            switch (loadType)
            {
                case ESResSourceLoadType.ABAsset:
                    ResTable.ReleaseAssetRes(key, unloadWhenZero);
                    break;
                case ESResSourceLoadType.AssetBundle:
                    ResTable.ReleaseABRes(key, unloadWhenZero);
                    break;
                default:
                    break;
            }
        }

    }
}
