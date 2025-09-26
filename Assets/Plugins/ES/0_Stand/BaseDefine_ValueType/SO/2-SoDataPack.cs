
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
        [LabelText("缓存应用的 列表"), NonSerialized, OdinSerialize]
        public List<ISoDataGroup> applingGroups = new List<ISoDataGroup>();
        [LabelText("预览全部数据")]
        public Dictionary<string, Info> allInfos = new Dictionary<string, Info>();
        public IDictionary AllInfos => allInfos;
        

        public List<ISoDataGroup> CachingGroups => applingGroups;

        public bool EnableAutoRefresh => enableAutoRefresh;

        public IEnumerable<string> Keys => allInfos.Keys;

        public string FileName => throw new NotImplementedException();

        public Type GetSOInfoType()
        {
            return typeof(Info);
        }

        public void SetKey(string o)
        {
          
        }

        public void _AddInfoToDic(string k, ScriptableObject so)
        {
            if (allInfos.ContainsKey(k) && allInfos[k] != null)
            {
                Debug.LogWarning($"发现重复的键{k}，默认跳过处理");
            }
            else if (so is Info info)
            {
                allInfos[k]=info;
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
                if(so!=null)
                this._AddInfoToDic(use.GetKey(), so);
            }
        }

        public ISoDataInfo _GetInfoByKey(string key)
        {
            if(allInfos.TryGetValue(key,out var value))
            {
                return value;
            }
            return null;
        }

        public void Check()
        {
            var keys = allInfos.Keys.ToArray();
            for(int i = 0; i < keys.Length; i++)
            {
                var info = allInfos[keys[i]];
                if ((info as UnityEngine.Object) == null) { allInfos.Remove(keys[i]);continue; }
                if (info.GetKey() != keys[i])
                {
                    allInfos.Remove(keys[i]);
                    allInfos.Add(info.GetKey(), info);
                }
            }
            foreach(var (i,k) in allInfos)
            {
               
            }
        }
    }


}
