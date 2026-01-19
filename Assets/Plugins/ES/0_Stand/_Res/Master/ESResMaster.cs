using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// ESResMaster
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
        public bool AutoDownload = true;
        protected override void DoAwake()
        {
            base.DoAwake();
            if (Settings != null)
            {
                Settings.RunTimeAwake();//装载
            }

            StartResCompareAndDownload();
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
            if (ResLoadTasks.Count == 0) return;
            if (isLoading) return;
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
            isLoading = true;
            while (ResLoadTasks.Count > 0)
            {
                var task = ResLoadTasks.First;
                ResLoadTasks.RemoveFirst();
                yield return StartCoroutine(task.Value.DoTaskAsync(OnLoadTaskOK));
                yield return null;
            }
            isLoading = false;
            yield return null;
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
        public static ESResTable ResTable = new ESResTable();

        public ESResSource CreateNewResSourceByKey(int keyIndex, ESResSourceLoadType loadType)
        {
            ESResSource retRes = null;

            if (loadType == ESResSourceLoadType.AssetBundle)
            {
                var key = ESResData_ABKeys.ABKeys[keyIndex];
                retRes = CreateResSource_AssetBundle(key);
            }
            else if (loadType == ESResSourceLoadType.ABAsset)
            {
                var key = ESResData_AssetKeys.AssetKeys[keyIndex];
                retRes = CreateResSource_ABAsset(key);
            }
            /*.Where(creator => creator.Match(resSearchKeys))
            .Select(creator => creator.Create(resSearchKeys))
            .FirstOrDefault();*/

            if (retRes == null)
            {
                Debug.LogError("创建资源源失败了. 找不到这个查找键" + keyIndex);
                return null;
            }

            return retRes;
        }

        public ESResSource GetResSourceByKey(int keyIndex, ESResSourceLoadType loadType, bool ifNullCreateNew = true)
        {
            ESResSource res = null;
            if (loadType == ESResSourceLoadType.ABAsset) ResTable.GetAssetResByIndex(keyIndex);
            else if (loadType == ESResSourceLoadType.ABAsset) ResTable.GetABResByIndex(keyIndex);
            if (res != null)
            {
                return res;
            }

            if (!ifNullCreateNew)
            {
                Debug.LogErrorFormat("没找到资源，并且这里也不允许创建:{0}", keyIndex);
                return null;
            }

            res = CreateNewResSourceByKey(keyIndex, loadType);

            if (res != null)
            {
                if (loadType == ESResSourceLoadType.ABAsset) ResTable.AssetsSources.TryAdd(keyIndex, res);
                else if (loadType == ESResSourceLoadType.ABAsset) ResTable.ABSources.TryAdd(keyIndex, res);
            }

            return res;
        }

        #endregion

        #region 资源源创建方式
        internal ESResSource CreateResSource_AssetBundle(ESResKey key)
        {
            var use = PoolForESABSource.GetInPool();
            use.IsNet = true;//还没实装
            use.Set(key.ABName,key.ResName,ESResSourceLoadType.AssetBundle);
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

    }
}
