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
        void _RemoveInfoFromDic(string s);
    }
    public abstract class SoDataGroup<SoType> : ESSO, ISoDataGroup where SoType : ScriptableObject, ISoDataInfo
    {
        [LabelText("数据组字典")]
        [HideLabel]
        public Dictionary<string, SoType> Infos = new Dictionary<string, SoType>();
        public string FileName => name;
        public List<string> AllKeys => Infos.Keys.ToList();
        public void _TryAddInfoToDic(string s, ScriptableObject o)
        {
            if (Infos.ContainsKey(s))
            {
                //键重复
                Debug.LogWarning("重复的键"+s+" 无法放入 "+name);
            }
            else if (o is SoType typeMatch)
            {
                Infos.Add(s, typeMatch);
            }
        }
        public bool NotContainsInfoKey(string s)
        {
            if (Infos.ContainsKey(s)) return false;
            else return true;
        }
        public ISoDataInfo GetInfoByKey(string k)
        {
            if (Infos.ContainsKey(k))
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
        public void _RemoveInfoFromDic(string k)
        {
            if (Infos.TryGetValue(k, out var info))
            {
                Infos.Remove(k);

            }
        }
    }
}
