using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// ESResLoader
    /// 
    /// 负责围绕 ESResMaster 提供“单个 Loader 实例”的加载能力：
    /// - 对外暴露同步加载接口（按 KeyIndex 取资源）；
    /// - 提供按路径 / GUID / AB 名称 添加到异步加载队列的便捷方法；
    /// - 自身可池化复用，通过 ESResMaster.Instance.PoolForESLoader 管理生命周期。
    /// 
    /// 注意：此类不直接持有全局配置，仅通过 ESResMaster 访问 JsonData 和 Key 表。
    /// </summary>
    public class ESResLoader : IPoolableAuto
    {
        #region 池化
        public bool IsRecycled { get; set; }

        public void OnResetAsPoolable()
        {

        }

        public void TryAutoPushedToPool()
        {
            if (LoaderResSources != null)
            {
                LoaderResSources.Clear();
                ThisLoaderResSourcesWaitToLoad.Clear();
            }
            ESResMaster.Instance.PoolForESLoader.PushToPool(this);
        }
        #endregion

        #region ResSource相关
        public IResSource _LoadResSync(ESResKey resSearchKeys)
        {
            return null;
        }
        #endregion

        #region 同步加载
        public UnityEngine.Object LoadAssetSync(int keyIndex)
        {
            var res = ESResMaster.Instance.GetResSourceByKey(keyIndex, ESResSourceLoadType.ABAsset);
            if (res != null)
            {
                if (res.State == ResSourceState.Ready)
                {
                    return res.Asset;
                }
                else
                {
                    res.LoadSync();
                    return res.Asset;
                }
            }
            return null;
        }
        #endregion

        #region 异步队列实现

        public void AddAsset2LoadByPathSourcer(string path, Action<bool, IResSource> listener = null, bool AtLastOrFirst = true)
        {
            if(ESResMaster.MainESResData_AssetKeys.PathToAssetKeys.TryGetValue(path,out int index))
            {
                Add2LoadByKeyIndex(index, ESResSourceLoadType.ABAsset, listener,AtLastOrFirst);
            }
        }

        public void AddAsset2LoadByGUIDSourcer(string guid, Action<bool, IResSource> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.MainESResData_AssetKeys.GUIDToAssetKeys.TryGetValue(guid, out int index))
            {
                Add2LoadByKeyIndex(index, ESResSourceLoadType.ABAsset, listener, AtLastOrFirst);
            }
        }

        public void AddAB2LoadByABPreNameSourcer(string abName, Action<bool, IResSource> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.MainESResData_ABKeys.NameToABKeys.TryGetValue(abName, out int index))
            {
                Add2LoadByKeyIndex(index, ESResSourceLoadType.AssetBundle, listener, AtLastOrFirst);
            }
        }

        public void Add2LoadByKeyIndex(int keyIndex, ESResSourceLoadType loadType, Action<bool, IResSource> listener = null, bool AtLastOrFirst = true)
        {
            var key = loadType == ESResSourceLoadType.AssetBundle ? ESResMaster.MainESResData_ABKeys.ABKeys[keyIndex] : ESResMaster.MainESResData_AssetKeys.AssetKeys[keyIndex];
            var res = FindResInThisLoaderList(key);
            if (res != null)
            {
                Debug.Log("已经被加载过");
                if (listener != null) res.OnLoadOKAction_Submit(listener);
                return;
            }
            res = ESResMaster.Instance.GetResSourceByKey(keyIndex, loadType);
            if (res != null)
            {
                if (listener != null) res.OnLoadOKAction_Submit(listener);
                //添加依赖支持
                {
                    //获得依赖AB们
                    var dependsAssetBundles = res.GetDependResSourceAllAssetBundles(out bool withHash);

                    if (dependsAssetBundles != null)
                    {
                        foreach (var depend in dependsAssetBundles)
                        {
                            AddAB2LoadByABPreNameSourcer(withHash? ESResMaster.PathAndNameTool_GetPreName(depend):depend);
                        }
                    }
                }
                AddRes2ThisLoaderRes(res, AtLastOrFirst);
            }
        }
        public IResSource FindResInThisLoaderList(ESResKey key)
        {
            int index = LoaderResSources.Count;
            if (index > 0)
            {
                for (int i = 0; i < index; i++)
                {
                    var res = LoaderResSources[i];
                    if (res == null) continue;
                    {
                        if (res.ResName == key.ResName) return res;
                    }
                }
            }
            return null;
        }
        public IResSource FindResInThisLoaderList(int keyIndex,ESResSourceLoadType loadType)
        {
            var key = loadType== ESResSourceLoadType.ABAsset? ESResMaster.MainESResData_AssetKeys.AssetKeys[keyIndex] : ESResMaster.MainESResData_ABKeys.ABKeys[keyIndex];
            int index = LoaderResSources.Count;
            if (index > 0)
            {
                for (int i = 0; i < index; i++)
                {
                    var res = LoaderResSources[i];
                    if (res == null) continue;
                    {
                        if (res.ResName == key.ResName) return res;
                    }
                }
            }
            return null;
        }
        public IResSource FindResInThisLoaderList(IResSource theRes)
        {
            int index = LoaderResSources.Count;
            if (index > 0)
            {
                for (int i = 0; i < index; i++)
                {
                    var res = LoaderResSources[i];
                    if (res == null) continue;
                    {
                        if (res.ResName == theRes.ResName) return res;
                    }
                }
            }
            return null;
        }
        private void AddRes2ThisLoaderRes(IResSource res, bool AddToLastOrFirst)
        {
            //本地是否已经加载
            IResSource thisLoaderRes = FindResInThisLoaderList(res);

            if (thisLoaderRes != null)//只要新的
            {
                return;
            }
            //可以加入了
            LoaderResSources.Add(res);
            if (res.State != ResSourceState.Ready)
            {
                ++mLoadingCount;
                if (AddToLastOrFirst)
                {
                    ThisLoaderResSourcesWaitToLoad.AddLast(res);
                }
                else
                {
                    ThisLoaderResSourcesWaitToLoad.AddFirst(res);
                }
            }
        }

        private readonly List<IResSource> LoaderResSources = new List<IResSource>();
        private readonly LinkedList<IResSource> ThisLoaderResSourcesWaitToLoad = new LinkedList<IResSource>();
        #endregion

        public void LoadAllAsync(Action listener = null)
        {
            mListener_ForLoadAllOK = listener;
            DoLoadAsync();
        }
        private void OnOneResLoadFinished(bool result, IResSource res)
        {
            --mLoadingCount;
            DoLoadAsync();
            if (mLoadingCount == 0)
            {
                if (mListener_ForLoadAllOK != null)
                {
                    mListener_ForLoadAllOK();
                }
            }
        }
        private void DoLoadAsync()
        {
            if (mLoadingCount == 0)
            {
                if (mListener_ForLoadAllOK != null)
                {
                    mListener_ForLoadAllOK();
                    mListener_ForLoadAllOK = null;
                }
                return;
            }

            var nextNode = ThisLoaderResSourcesWaitToLoad.First;
            LinkedListNode<IResSource> currentNode = null;
            while (nextNode != null)
            {
                currentNode = nextNode;
                var res = currentNode.Value;
                nextNode = currentNode.Next;//循环判定

                Debug.Log("DETECT");

                if (res.IsDependResLoadFinish())
                {
                    ThisLoaderResSourcesWaitToLoad.Remove(currentNode);
                    Debug.Log("DETECT2");
                    if (res.State != ResSourceState.Ready)
                    {
                        res.OnLoadOKAction_Submit(OnOneResLoadFinished);
                        res.LoadAsync();
                        Debug.Log("DETECT3");
                    }
                    else
                    {
                        --mLoadingCount;
                    }
                }
            }
        }

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

        private System.Action mListener_ForLoadAllOK;

        private int mLoadingCount;


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
    }
}
