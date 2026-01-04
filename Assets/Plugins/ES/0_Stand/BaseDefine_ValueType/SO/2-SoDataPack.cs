
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;

namespace ES
{
    public interface ISoDataPack
    {
        string FileName { get; }
        Type GetSOInfoType();
        void _AddInfosFromGroup(ISoDataGroup group);
        void _AddInfoToDic(string s, ScriptableObject so);
        ISoDataInfo _GetInfoByKey(string key);
        IDictionary AllInfos { get; }
        IEnumerable<string> Keys { get; }
        List<ISoDataGroup> CachingGroups { get; }
        bool EnableAutoRefresh { get; }
        void Check();
    }
    public abstract class SoDataPack<Info> : ESSO, ISoDataPack where Info : ScriptableObject, ISoDataInfo
    {

        [LabelText("启用自动更新")] public bool enableAutoRefresh = true;
        [LabelText("缓存应用的 列表"), NonSerialized, OdinSerialize,ListDrawerSettings(ShowFoldout =false)]
        public List<ISoDataGroup> applingGroups = new List<ISoDataGroup>();
        [LabelText("预览全部数据")]
        public Dictionary<string, Info> Infos = new Dictionary<string, Info>();
        public IDictionary AllInfos => Infos;


        public List<ISoDataGroup> CachingGroups => applingGroups;

        public bool EnableAutoRefresh => enableAutoRefresh;

        public IEnumerable<string> Keys => Infos.Keys;

        public string FileName => name;

        public Type GetSOInfoType()
        {
            return typeof(Info);
        }

        public void _AddInfoToDic(string k, ScriptableObject so)
        {
            if (Infos.ContainsKey(k) && Infos[k] != null)
            {
                Debug.LogWarning($"发现重复的键{k}，默认跳过处理");
            }
            else if (so is Info info)
            {
                Infos[k] = info;
            }
            else
            {
                Debug.LogWarning($"发现无效或者已经销毁的内容，键{k}，值{so}");
            }
        }

        public void _AddInfosFromGroup(ISoDataGroup group)
        {
            if (group.GetSOInfoType() != typeof(Info))
            {
                Debug.LogError("试图加入不合法数据组:" + group.FileName);
                return;
            }
            var keys = group.AllKeys;
            //加入已经缓存
            if (applingGroups.Contains(group))
            {

            }
            else
            {
                applingGroups.Add(group);
            }
            foreach (var k in keys)
            {
                ISoDataInfo use = group.GetInfoByKey(k);
                var so = use as SerializedScriptableObject;
                if (so != null)
                    this._AddInfoToDic(use.GetKey(), so);
            }
        }

        public ISoDataInfo _GetInfoByKey(string key)
        {
            if (Infos.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        public void Check()
        {
            var keys = Infos.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                var info = Infos[keys[i]];
                if ((info as UnityEngine.Object) == null) { Infos.Remove(keys[i]); continue; }
                if (info.GetKey() != keys[i])
                {
                    Infos.Remove(keys[i]);
                    Infos.Add(info.GetKey(), info);
                }
            }
            foreach (var (i, k) in Infos)
            {

            }
        }



        public virtual IEnumerable<Info> Query(Func<Info, bool> predicate)
        {
            return Infos.Values.Where(predicate);
        }
        // 批量获取
        public virtual List<Info> GetInfosByKeys(IEnumerable<string> keys)
        {
            var result = new List<Info>();
            foreach (var key in keys)
            {
                if (Infos.TryGetValue(key, out var info))
                {
                    result.Add(info);
                }
            }
            return result;
        }
        public virtual List<Info> GetInfosByKeys(params string[] keys)
        {
            var result = new List<Info>();
            foreach (var key in keys)
            {
                if (Infos.TryGetValue(key, out var info))
                {
                    result.Add(info);
                }
            }
            return result;
        }
        /// <summary>
        /// 从当前数据包的字典中查找指定 Info 对象对应的 Key
        /// </summary>
        /// <param name="infos">要查找的 Info 对象列表</param>
        /// <returns>在字典中找到的对应 Key 列表</returns>
        public virtual List<string> GetKeysByInfos(IEnumerable<Info> infos)
        {
            if (infos == null)
                return new List<string>();

            var result = new List<string>();

            // 遍历字典，查找匹配的 Info
            foreach (var info in infos)
            {
                if (info == null) continue;

                foreach (var kvp in Infos)
                {
                    if (ReferenceEquals(kvp.Value, info))
                    {
                        result.Add(kvp.Key);
                        break; // 找到后跳出内层循环
                    }
                }
            }

            return result;
        }
        public virtual List<string> GetKeysByInfos(params Info[] infos)
        {
            if (infos == null)
                return new List<string>();

            var result = new List<string>();

            // 遍历字典，查找匹配的 Info
            foreach (var info in infos)
            {
                if (info == null) continue;

                foreach (var kvp in Infos)
                {
                    if (ReferenceEquals(kvp.Value, info))
                    {
                        result.Add(kvp.Key);
                        break; // 找到后跳出内层循环
                    }
                }
            }

            return result;
        }

    
    }


}
