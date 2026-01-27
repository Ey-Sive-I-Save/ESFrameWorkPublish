using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            ReleaseAll(resumePooling: false);
            ESResMaster.Instance.PoolForESLoader.PushToPool(this);
        }
        #endregion

        #region ResSource相关
        public ESResSource _LoadResSync(ESResKey resSearchKeys)
        {
            return null;
        }
        #endregion

        #region 同步加载
        // TODO: Update LoadAssetSync to use object key instead of int keyIndex, as GetResSourceByKey API has changed.
        public UnityEngine.Object LoadAssetSync(object key)
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

        public T LoadAssetSync<T>(object key) where T : UnityEngine.Object
        {
            return LoadAssetSync(key) as T;
        }

        public bool TryGetLoadedAsset(object key, out UnityEngine.Object asset)
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

        #region 异步队列实现

        public void AddAsset2LoadByPathSourcer(string path, Action<bool, ESResSource> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.GlobalAssetKeys.TryGetESResKeyByPath(path, out var assetKey))
            {
                Add2LoadByKey(assetKey, ESResSourceLoadType.ABAsset, listener, AtLastOrFirst);
            }else
            {
                Debug.LogError($"通过路径添加异步加载任务失败，未找到资源键: {path}");
            }
        }

        public void AddAsset2LoadByGUIDSourcer(string guid, Action<bool, ESResSource> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.GlobalAssetKeys.TryGetESResKeyByGUID(guid, out var assetKey))
            {
                Add2LoadByKey(assetKey, ESResSourceLoadType.ABAsset, listener, AtLastOrFirst);
            }
        }

        public void AddAB2LoadByABPreNameSourcer(string abName, Action<bool, ESResSource> listener = null, bool AtLastOrFirst = true)
        {
            if (ESResMaster.GlobalABKeys.TryGetValue(abName, out var abKey))
            {
                Add2LoadByKey(abKey, ESResSourceLoadType.AssetBundle, listener, AtLastOrFirst);
            }
        }

        public void Add2LoadByKey(object key, ESResSourceLoadType loadType, Action<bool, ESResSource> listener = null, bool AtLastOrFirst = true)
        {
            ESResKey assetKey = null;
            ESResKey abKey = null;
            if (loadType == ESResSourceLoadType.AssetBundle)
            {
                abKey = (ESResKey)key;
            }
            else if (loadType == ESResSourceLoadType.ABAsset)
            {
                assetKey = (ESResKey)key;
            }
            var res = FindResInThisLoaderList(key, loadType);
            if (res != null)
            {
                Debug.Log("已经被加载过");
                RegisterLocalRes(res, key, loadType, skipGlobalRetain: false);
                if (listener != null) res.OnLoadOKAction_Submit(listener);
                return;
            }
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
                        Debug.Log($"[ESResLoader] 资源 '{res.ResName}' 有 {dependsAssetBundles.Length} 个依赖AB需要加载");
                        foreach (var depend in dependsAssetBundles)
                        {
                            string abName = withHash ? ESResMaster.PathAndNameTool_GetPreName(depend) : depend;
                             Debug.Log($"[ESResLoader] -> 添加依赖AB任务: {abName}");
                            AddAB2LoadByABPreNameSourcer(abName);
                        }
                    }
                }
                
                bool isNew = AddRes2ThisLoaderRes(res, key, loadType, AtLastOrFirst);
                 Debug.Log($"[ESResLoader] 尝试添加 '{res.ResName}' 到加载列表, 结果: {(isNew ? "成功(新任务)" : "已存在(复用)")}");

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
        public ESResSource FindResInThisLoaderList(object key, ESResSourceLoadType loadType)
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
        public ESResSource FindResInThisLoaderList(ESResSource theRes)
        {
            if (theRes == null)
            {
                return null;
            }

            return LoaderResKeys.ContainsKey(theRes) ? theRes : null;
        }
        private bool AddRes2ThisLoaderRes(ESResSource res, object key, ESResSourceLoadType loadType, bool addToLast)
        {
            //本地是否已经加载
            ESResSource thisLoaderRes = FindResInThisLoaderList(res);

            if (thisLoaderRes != null)//只要新的
            {
                Debug.Log($"[ESResLoader] 资源 '{res?.ResName ?? "Unknown"}' 已存在于Loader中，跳过添加 (Key: {key}, Type: {loadType})");
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
                Debug.Log($"[ESResLoader] 新资源 '{res.ResName}' 已添加到等待队列 (Key: {key}, Type: {loadType}, 队列位置: {(addToLast ? "末尾" : "开头")})");
            }
            else
            {
                Debug.Log($"[ESResLoader] 新资源 '{res.ResName}' 已就绪，直接添加到列表 (Key: {key}, Type: {loadType})");
            }
            return true;
        }

        private readonly List<ESResSource> LoaderResSources = new List<ESResSource>();
        private readonly LinkedList<ESResSource> ThisLoaderResSourcesWaitToLoad = new LinkedList<ESResSource>();
        private readonly Dictionary<object, ESResSource> LoaderKeyToRes = new Dictionary<object, ESResSource>();
        private readonly Dictionary<ESResSource, object> LoaderResKeys = new Dictionary<ESResSource, object>();
        private readonly Dictionary<ESResSource, int> LoaderResRefCounts = new Dictionary<ESResSource, int>();
        #endregion

        public void LoadAllAsync(Action listener = null)
        {
            mListener_ForLoadAllOK = listener;
            DoLoadAsync();
        }
        private void OnOneResLoadFinished(bool result, ESResSource res)
        {
            if (mLoadingCount > 0)
            {
                --mLoadingCount;
            }
            res?.OnLoadOKAction_WithDraw(OnOneResLoadFinished);

            DoLoadAsync();
        }
        private void DoLoadAsync()
        {
            Debug.Log($"[ESResLoader.DoLoadAsync] 进入异步加载调度。当前加载计数: {mLoadingCount}, 等待队列长度: {ThisLoaderResSourcesWaitToLoad.Count}");

            if (mLoadingCount == 0 && ThisLoaderResSourcesWaitToLoad.Count == 0)
            {
                Debug.Log("[ESResLoader.DoLoadAsync] 所有资源已加载完成，触发完成回调。");
                var listener = mListener_ForLoadAllOK;
                mListener_ForLoadAllOK = null;
                listener?.Invoke();
                return;
            }

            Debug.Log("[ESResLoader.DoLoadAsync] 开始处理等待队列中的资源。"+ThisLoaderResSourcesWaitToLoad.Count);
            var nextNode = ThisLoaderResSourcesWaitToLoad.First;
            LinkedListNode<ESResSource> currentNode = null;
            while (nextNode != null)
            {
                currentNode = nextNode;
                var res = currentNode.Value;
                nextNode = currentNode.Next;//循环判定

                Debug.Log($"[ESResLoader.DoLoadAsync] 检查资源 '{res?.ResName ?? "Unknown"}' 的依赖状态。");
                if (res.IsDependResLoadFinish())
                {
                    Debug.Log($"[ESResLoader.DoLoadAsync] 资源 '{res.ResName}' 依赖已完成，从等待队列移除。");
                    ThisLoaderResSourcesWaitToLoad.Remove(currentNode);
                    if (res.State != ResSourceState.Ready)
                    {
                        Debug.Log($"[ESResLoader.DoLoadAsync] 资源 '{res.ResName}' 状态为 {res.State}，开始异步加载。");
                        res.OnLoadOKAction_Submit(OnOneResLoadFinished);
                        res.LoadAsync();
                    }
                    else
                    {
                        Debug.Log($"[ESResLoader.DoLoadAsync] 资源 '{res.ResName}' 已就绪，减少加载计数。");
                        if (mLoadingCount > 0)
                        {
                            --mLoadingCount;
                        }
                    }
                }
                else
                {
                    Debug.Log($"[ESResLoader.DoLoadAsync] 资源 '{res?.ResName ?? "Unknown"}' 依赖未完成，跳过。");
                }
            }
            
            if (mLoadingCount == 0 && ThisLoaderResSourcesWaitToLoad.Count == 0)
            {
                Debug.Log("[ESResLoader.DoLoadAsync] 循环后检查：所有资源加载完成，触发完成回调。");
                var listener = mListener_ForLoadAllOK;
                mListener_ForLoadAllOK = null;
                listener?.Invoke();
            }
            else
            {
                Debug.Log($"[ESResLoader.DoLoadAsync] 循环后检查：仍有 {mLoadingCount} 个加载中，{ThisLoaderResSourcesWaitToLoad.Count} 个等待依赖，继续调度。");
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

        public int PendingCount => mLoadingCount;

        public IReadOnlyList<ESResSource> SnapshotQueuedSources()
        {
            return LoaderResSources.ToList();
        }

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

        public void ReleaseAll(bool resumePooling = true)
        {
            // 如果应用正在退出，跳过释放逻辑以避免在关闭时执行
            if (!Application.isPlaying)
            {
                mLoadingCount = 0;
                mListener_ForLoadAllOK = null;
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
            mListener_ForLoadAllOK = null;
            LoaderResKeys.Clear();
            LoaderKeyToRes.Clear();
            LoaderResSources.Clear();
            LoaderResRefCounts.Clear();

            if (!resumePooling)
            {
                return;
            }
        }

        public void ReleaseAsset(object key, bool unloadWhenZero = false)
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

        public void ReleaseAssetBundle(object key, bool unloadWhenZero = false)
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

        private void ReleaseEntry(ESResSource res, bool unloadWhenZero)
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

        private void RegisterLocalRes(ESResSource res, object key, ESResSourceLoadType loadType, bool skipGlobalRetain)
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

        private static void RetainGlobalHandle(object key, ESResSourceLoadType loadType)
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
                default:
                    break;
            }
        }

        private int ReleaseReference(ESResSource res, object key, ESResSourceLoadType loadType, bool unloadWhenZero)
        {
            if (res == null || key == null)
            {
                return 0;
            }

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
            
            if(!ESSystem.IsQuitting) ESResMaster.Instance.ReleaseResHandle(key, loadType, unloadWhenZero);
            return LoaderResRefCounts.TryGetValue(res, out var remain) ? remain : 0;
        }

        private void RemoveResFromLoader(ESResSource res, object key)
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
    }
}
