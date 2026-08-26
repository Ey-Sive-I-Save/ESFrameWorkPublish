using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace ES
{
    //  [CreateAssetMenu(fileName = "SoDataInfoGroup",menuName = "EvData/SoGroup")]
    public interface ISoDataGroup
    {
        string FileName { get; }
        Type GetSOInfoType();
        bool NotContainsInfoKey(string s);
        ISoDataInfo GetInfoByKey(string k);
        void _TryAddInfoToDic(string key, ScriptableObject o);
        List<string> AllKeys { get; }
        IEnumerable<ISoDataInfo> AllInfos { get; }
        void _RemoveInfoFromDic(string s); 
    }
    public abstract class SoDataGroup<SoType> : ESSO, ISoDataGroup, IGameCoreSO where SoType : ScriptableObject, ISoDataInfo
    {
        [LabelText("数据组字典")]
        [HideLabel]
        public Dictionary<string, SoType> Infos = new Dictionary<string, SoType>();
        public string FileName => name;
        public List<string> AllKeys => Infos == null ? new List<string>() : Infos.Keys.ToList();
        public IEnumerable<ISoDataInfo> AllInfos => Infos == null
            ? Enumerable.Empty<ISoDataInfo>()
            : Infos.Values;//.Cast<ISoDataInfo>().ToList();
        public void _TryAddInfoToDic(string s, ScriptableObject o)
        {
            if (Infos == null)
                Infos = new Dictionary<string, SoType>();
            if (string.IsNullOrWhiteSpace(s))
            {
                Debug.LogWarning("拒绝写入空 Info Key：" + name);
                return;
            }
            if (o == null)
            {
                Debug.LogWarning("拒绝写入空 Info 对象：" + s + " -> " + name);
                return;
            }
            if (Infos.ContainsKey(s))
            {
                //键重复
                Debug.LogWarning("重复的键"+s+" 无法放入 "+name);
            }
            else if (o is SoType typeMatch)
            {
                Infos.Add(s, typeMatch);
            }
            else
            {
                Debug.LogWarning("Info 类型不匹配" + o.GetType().FullName + " 无法放入 " + name);
            }
        }
        public bool NotContainsInfoKey(string s)
        {
            if (Infos == null) return true;
            if (Infos.ContainsKey(s)) return false;
            else return true;
        }
        public ISoDataInfo GetInfoByKey(string k)
        {
            if (Infos != null && Infos.ContainsKey(k))
            {
                return Infos[k];
            }
            else
            {
                return default;
            }
        }
        public Type GetSOInfoType()
        {
            return typeof(SoType);
        }

        /// <summary>聚合启动入口：只转发已声明为 GameCore 的 Info，不反射、不猜测类型。</summary>
        public void InjectGameCoreTables()
        {
            if (Infos == null) return;
            foreach (SoType info in Infos.Values)
                if (info is IGameCoreSO gameCore)
                    gameCore.InjectGameCoreTables();
        }
        public void _RemoveInfoFromDic(string k)
        {
            if (Infos != null && Infos.TryGetValue(k, out var info))
            {
                Infos.Remove(k);

            }
        }
    
        
        
        public virtual IEnumerable<SoType> Query(Func<SoType, bool> predicate)
        {
            if (Infos == null || predicate == null)
                return Enumerable.Empty<SoType>();
            return Infos.Values.Where(predicate);
        }
        // 批量获取
        public virtual List<SoType> GetInfosByKeys(IEnumerable<string> keys)
        {
            var result = new List<SoType>();
            if (Infos == null || keys == null)
                return result;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (Infos.TryGetValue(key, out var info))
                {
                    result.Add(info);
                }
            }
            return result;
        }
        public virtual List<SoType> GetInfosByKeys(params string[] keys)
        {
            var result = new List<SoType>();
            if (Infos == null || keys == null)
                return result;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (Infos.TryGetValue(key, out var info))
                {
                    result.Add(info);
                }
            }
            return result;
        }
        /// <summary>
        /// 从当前数据组的字典中查找指定 Info 对象对应的 Key
        /// </summary>
        /// <param name="infos">要查找的 Info 对象列表</param>
        /// <returns>在字典中找到的对应 Key 列表</returns>
        public virtual List<string> GetKeysByInfos(IEnumerable<SoType> infos)
        {
            if (infos == null)
                return new List<string>();

            var result = new List<string>();

            // 遍历字典，查找匹配的 Info
            foreach (var info in infos)
            {
                if (info == null) continue;

                if (Infos == null) continue;
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
        public virtual List<string> GetKeysByInfos(params SoType[] infos)
        {
            if (infos == null)
                return new List<string>();

            var result = new List<string>();

            // 遍历字典，查找匹配的 Info
            foreach (var info in infos)
            {
                if (info == null) continue;

                if (Infos == null) continue;
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
