using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /// <summary>
    /// ESResTable
    /// 
    /// 资源句柄索引表：
    /// - 通过 object 索引缓存 ESResSource，避免到处用字符串查找；
    /// - AssetsSources：按资源索引管理具体资源；
    /// - ABSources：按 AB 索引管理 AssetBundle 级别的资源；
    /// 
    /// 自身不负责加载 / 卸载，只作为“运行时快速索引层”。
    /// </summary>
    public class ESResTable 
    {
        private readonly Dictionary<object, ESResSourceBase> _assetSources = new Dictionary<object, ESResSourceBase>();
        private readonly Dictionary<object, ESResSourceBase> _abSources = new Dictionary<object, ESResSourceBase>();
        private readonly Dictionary<object, int> _assetRefCounts = new Dictionary<object, int>();
        private readonly Dictionary<object, int> _abRefCounts = new Dictionary<object, int>();

        private readonly object _assetLock = new object();
        private readonly object _abLock = new object();

        public int AssetCount
        {
            get
            {
                lock (_assetLock)
                {
                    return _assetSources.Count;
                }
            }
        }

        public int ABCount
        {
            get
            {
                lock (_abLock)
                {
                    return _abSources.Count;
                }
            }
        }

        public ESResSourceBase GetAssetResByKey(object key)
        {
            if (key == null)
            {
                return null;
            }

            lock (_assetLock)
            {
                return TryResolveEntry(_assetSources, key);
            }
        }

        public ESResSourceBase GetABResByKey(object key)
        {
            if (key == null)
            {
                return null;
            }

            lock (_abLock)
            {
                return TryResolveEntry(_abSources, key);
            }
        }

        public bool TryRegisterAssetRes(object key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                return false;
            }

            lock (_assetLock)
            {
                return TryRegisterEntry(_assetSources, _assetRefCounts, key, res);
            }
        }

        public bool TryRegisterABRes(object key, ESResSourceBase res)
        {
            if (key == null || res == null)
            {
                return false;
            }

            lock (_abLock)
            {
                return TryRegisterEntry(_abSources, _abRefCounts, key, res);
            }
        }

        public int AcquireAssetRes(object key)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_assetLock)
            {
                return InternalAcquire(_assetSources, _assetRefCounts, key);
            }
        }

        public int AcquireABRes(object key)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_abLock)
            {
                return InternalAcquire(_abSources, _abRefCounts, key);
            }
        }

        public int ReleaseAssetRes(object key, bool unloadWhenZero)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_assetLock)
            {
                return InternalRelease(_assetSources, _assetRefCounts, key, unloadWhenZero);
            }
        }

        public int ReleaseABRes(object key, bool unloadWhenZero)
        {
            if (key == null)
            {
                return 0;
            }

            lock (_abLock)
            {
                return InternalRelease(_abSources, _abRefCounts, key, unloadWhenZero);
            }
        }

        public bool RemoveAssetRes(object key, bool releaseResource = false)
        {
            if (key == null)
            {
                return false;
            }

            lock (_assetLock)
            {
                _assetRefCounts.Remove(key);
                return TryRemoveEntry(_assetSources, _assetRefCounts, key, releaseResource);
            }
        }

        public bool RemoveABRes(object key, bool releaseResource = false)
        {
            if (key == null)
            {
                return false;
            }

            lock (_abLock)
            {
                _abRefCounts.Remove(key);
                return TryRemoveEntry(_abSources, _abRefCounts, key, releaseResource);
            }
        }

        public void ClearAll(bool releaseResources = false)
        {
            lock (_assetLock)
            {
                InternalClear(_assetSources, _assetRefCounts, releaseResources);
            }

            lock (_abLock)
            {
                InternalClear(_abSources, _abRefCounts, releaseResources);
            }
        }

        public List<KeyValuePair<object, ESResSourceBase>> SnapshotAssetEntries()
        {
            lock (_assetLock)
            {
                return new List<KeyValuePair<object, ESResSourceBase>>(_assetSources);
            }
        }

        public List<KeyValuePair<object, ESResSourceBase>> SnapshotABEntries()
        {
            lock (_abLock)
            {
                return new List<KeyValuePair<object, ESResSourceBase>>(_abSources);
            }
        }

        private static ESResSourceBase TryResolveEntry(Dictionary<object, ESResSourceBase> map, object key)
        {
            if (!map.TryGetValue(key, out var res) || res == null)
            {
                return null;
            }

            if (res.IsRecycled)
            {
                map.Remove(key);
                return null;
            }

            return res;
        }

        private static bool TryRegisterEntry(Dictionary<object, ESResSourceBase> map, Dictionary<object, int> refCounts, object key, ESResSourceBase res)
        {
            if (map.TryGetValue(key, out var existing))
            {
                if (existing == null || existing.IsRecycled)
                {
                    map[key] = res;
                    refCounts[key] = 0;
                    res?.ResetReferenceCounter();
                    return true;
                }

                if (!ReferenceEquals(existing, res))
                {
                    Debug.LogWarning($"重复注册资源键: {key}");
                }

                return false;
            }

            map.Add(key, res);
            refCounts[key] = 0;
            res?.ResetReferenceCounter();
            return true;
        }

        private static int InternalAcquire(Dictionary<object, ESResSourceBase> map, Dictionary<object, int> refCounts, object key)
        {
            if (!map.TryGetValue(key, out var res) || res == null)
            {
                return 0;
            }

            refCounts.TryGetValue(key, out var count);
            count = Mathf.Max(0, count) + 1;
            refCounts[key] = count;
            res.RetainReference();
            return count;
        }

        private static int InternalRelease(Dictionary<object, ESResSourceBase> map, Dictionary<object, int> refCounts, object key, bool unloadWhenZero)
        {
            if (!refCounts.TryGetValue(key, out var count))
            {
                return 0;
            }

            count = Mathf.Max(0, count - 1);

            ESResSourceBase res = null;
            map.TryGetValue(key, out res);
            res?.ReleaseReference();

            if (count == 0)
            {
                refCounts.Remove(key);
                if (unloadWhenZero)
                {
                    TryRemoveEntry(map, refCounts, key, true);
                }
            }
            else
            {
                refCounts[key] = count;
            }

            return count;
        }

        private static bool TryRemoveEntry(Dictionary<object, ESResSourceBase> map, Dictionary<object, int> refCounts, object key, bool releaseResource)
        {
            if (!map.TryGetValue(key, out var res))
            {
                return false;
            }

            map.Remove(key);
            refCounts.Remove(key);

            if (res != null)
            {
                if (releaseResource)
                {
                    if (res.ReleaseTheResSource())
                    {
                        res.TryAutoPushedToPool();
                    }
                    else
                    {
                        res.ResetReferenceCounter();
                    }
                }
                else
                {
                    res.ResetReferenceCounter();
                }
            }

            return true;
        }

        private static void InternalClear(Dictionary<object, ESResSourceBase> map, Dictionary<object, int> refCounts, bool releaseResource)
        {
            if (!releaseResource)
            {
                map.Clear();
                refCounts.Clear();
                return;
            }

            foreach (var pair in map)
            {
                var res = pair.Value;
                if (res == null)
                {
                    continue;
                }

                if (res.ReleaseTheResSource())
                {
                    res.TryAutoPushedToPool();
                }
            }

            map.Clear();
            refCounts.Clear();
        }
    }
}
