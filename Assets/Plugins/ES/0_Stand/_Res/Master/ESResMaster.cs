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
        #region 全局资源加载器
        /// <summary>
        /// 全局默认资源加载器 - 用于 ESResRefer 等辅助工具
        /// </summary>
        private static ESResLoader _globalResLoader;
        public static ESResLoader GlobalResLoader
        {
            get
            {
                if (_globalResLoader == null)
                {
                    _globalResLoader = new ESResLoader();
                }
                return _globalResLoader;
            }
        }
        #endregion

        #region 对象池
        public ESSimplePool<ESABSource> PoolForESABSource = new ESSimplePool<ESABSource>(
            () => new ESABSource(),
            (source) => source.OnResetAsPoolable()
        );
        
        public ESSimplePool<ESAssetSource> PoolForESAsset = new ESSimplePool<ESAssetSource>(
            () => new ESAssetSource(),
            (source) => source.OnResetAsPoolable()
        );
        
        public ESSimplePool<ESABSceneSource> PoolForESABScene = new ESSimplePool<ESABSceneSource>(
            () => new ESABSceneSource(),
            (source) => source.OnResetAsPoolable()
        );
        
        public ESSimplePool<ESShaderVariantSource> PoolForESShaderVariant = new ESSimplePool<ESShaderVariantSource>(
            () => new ESShaderVariantSource(),
            (source) => source.OnResetAsPoolable()
        );
        
        public ESSimplePool<ESRawFileSource> PoolForESRawFile = new ESSimplePool<ESRawFileSource>(
            () => new ESRawFileSource(),
            (source) => source.OnResetAsPoolable()
        );
        
        public ESSimplePool<ESInternalResourceSource> PoolForESInternalResource = new ESSimplePool<ESInternalResourceSource>(
            () => new ESInternalResourceSource(),
            (source) => source.OnResetAsPoolable()
        );
        
        public ESSimplePool<ESNetImageSource> PoolForESNetImage = new ESSimplePool<ESNetImageSource>(
            () => new ESNetImageSource(),
            (source) => source.OnResetAsPoolable()
        );
        #endregion

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
        
        #region 池操作在这里
        public ESResKey GetInPool_ResKey(string resName, string ownerBundleName = null, Type assetType = null)
        {
            var resSearchRule = ESResMaster.Instance.PoolForESResKey.GetInPool();
            resSearchRule.ResName = resName;
            resSearchRule.ABPreName = ownerBundleName == null ? null : ownerBundleName;
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
        /// <summary>
        /// 使用工厂模式创建资源源（商业级重构 - 强类型版本）
        /// 扩展性：添加新类型只需在ESResSourceFactory注册，无需修改此方法
        /// </summary>
        public ESResSourceBase CreateNewResSourceByKey(ESResKey resKey, ESResSourceLoadType loadType)
        {
            if (resKey == null)
            {
                Debug.LogError("资源键不能为null");
                return null;
            }

            try
            {
                // ✅ 使用工厂创建，完全解耦，符合开闭原则
                return ESResSourceFactory.CreateResSource(resKey, loadType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"创建资源源失败 [Type: {loadType}, Key: {resKey}]\n{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 通过ESResKey获取资源源（强类型版本）
        /// </summary>
        public ESResSourceBase GetResSourceByKey(ESResKey key, ESResSourceLoadType loadType, bool ifNullCreateNew = true)
        {
            if (key == null)
            {
                Debug.LogError("资源键不能为null");
                return null;
            }

            ESResSourceBase res = null;
            if (loadType == ESResSourceLoadType.ABAsset)
            {
                res = ResTable.GetAssetResByKey(key);
            }
            else if (loadType == ESResSourceLoadType.AssetBundle)
            {
                res = ResTable.GetABResByKey(key);
            }
            else if (loadType == ESResSourceLoadType.RawFile)
            {
                res = ResTable.GetRawFileResByKey(key);
            }
            else if (loadType == ESResSourceLoadType.InternalResource)
            {
                res = ResTable.GetInternalResourceResByKey(key);
            }
            else if (loadType == ESResSourceLoadType.NetImageRes)
            {
                res = ResTable.GetNetImageResByKey(key);
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
                else if (loadType == ESResSourceLoadType.RawFile)
                {
                    registered = ResTable.TryRegisterRawFileRes(key, res);
                }
                else if (loadType == ESResSourceLoadType.InternalResource)
                {
                    registered = ResTable.TryRegisterInternalResourceRes(key, res);
                }
                else if (loadType == ESResSourceLoadType.NetImageRes)
                {
                    registered = ResTable.TryRegisterNetImageRes(key, res);
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

        #region 遗留工厂方法（已弃用，保留兼容性）
        /// <summary>
        /// [已弃用] 使用 ESResSourceFactory.CreateResSource() 代替
        /// </summary>
        [Obsolete("请使用 ESResSourceFactory.CreateResSource()")]
        internal ESResSourceBase CreateResSource_AssetBundle(ESResKey abKey)
        {
            var use = PoolForESABSource.GetInPool();
            use.IsNet = true;//还没实装
            use.Set(abKey, ESResSourceLoadType.AssetBundle);
            use.TargetType = typeof(AssetBundle);
            return use;
        }
        /// <summary>
        /// [已弃用] 使用 ESResSourceFactory.CreateResSource() 代替
        /// </summary>
        [Obsolete("请使用 ESResSourceFactory.CreateResSource()")]
        internal ESResSourceBase CreateResSource_ABAsset(ESResKey key)
        {
            var use = PoolForESAsset.GetInPool();
            use.Set(key, ESResSourceLoadType.ABAsset);
            use.TargetType = key.TargetType;
            return use;
        }


        #endregion

        #region 资源源管理（引用计数）
        /// <summary>
        /// 获取资源句柄（引用计数+1）- 强类型版本
        /// </summary>
        private void AcquireResHandle(ESResKey key, ESResSourceLoadType loadType)
        {
            switch (loadType)
            {
                case ESResSourceLoadType.ABAsset:
                    ResTable.AcquireAssetRes(key);
                    break;
                case ESResSourceLoadType.AssetBundle:
                    ResTable.AcquireABRes(key);
                    break;
                case ESResSourceLoadType.ShaderVariant:
                    // Shader资源不需要引用计数
                    break;
                case ESResSourceLoadType.RawFile:
                    ResTable.AcquireRawFileRes(key);
                    break;
                case ESResSourceLoadType.InternalResource:
                    ResTable.AcquireInternalResourceRes(key);
                    break;
                case ESResSourceLoadType.NetImageRes:
                    ResTable.AcquireNetImageRes(key);
                    break;
                default:
                    Debug.LogWarning($"未处理的资源类型引用计数: {loadType}");
                    break;
            }
        }

        /// <summary>
        /// 释放资源句柄（引用计数-1）- 强类型版本
        /// </summary>
        internal void ReleaseResHandle(ESResKey key, ESResSourceLoadType loadType, bool unloadWhenZero)
        {
            switch (loadType)
            {
                case ESResSourceLoadType.ABAsset:
                    ResTable.ReleaseAssetRes(key, unloadWhenZero);
                    break;
                case ESResSourceLoadType.AssetBundle:
                    ResTable.ReleaseABRes(key, unloadWhenZero);
                    break;
                case ESResSourceLoadType.ShaderVariant:
                    // Shader资源永不卸载
                    Debug.LogWarning($"Shader资源不应被释放: {key}");
                    break;
                case ESResSourceLoadType.RawFile:
                    ResTable.ReleaseRawFileRes(key, unloadWhenZero);
                    break;
                case ESResSourceLoadType.InternalResource:
                    ResTable.ReleaseInternalResourceRes(key, unloadWhenZero);
                    break;
                case ESResSourceLoadType.NetImageRes:
                    ResTable.ReleaseNetImageRes(key, unloadWhenZero);
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Shader自动预热API
        
        /// <summary>
        /// 手动触发Shader预热（通常不需要，系统会自动预热）
        /// </summary>
        public static void WarmUpAllShaders(Action onComplete = null)
        {
            if (Instance == null)
            {
                Debug.LogError("[ESResMaster.WarmUpAllShaders] ESResMaster实例不存在");
                onComplete?.Invoke();
                return;
            }

            Instance.StartCoroutine(ESShaderPreloader.AutoWarmUpAllShaders(onComplete));
        }

        /// <summary>
        /// 检查Shader是否已预热完成
        /// </summary>
        public static bool IsShadersWarmedUp()
        {
            return ESShaderPreloader.IsWarmedUp;
        }

        /// <summary>
        /// 获取Shader预热统计信息
        /// </summary>
        public static string GetShaderStatistics()
        {
            return ESShaderPreloader.GetStatistics();
        }

        #endregion

    }
}
