
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public interface ISoDataInfo
    {
#if UNITY_EDITOR
        void DestroyDirecly();
#endif
        void SetKey(string key);
        string GetKey();
    }
    public abstract class SoDataInfo : ESSO, ISoDataInfo
    {
        [ReadOnly,LabelText("键名称")]
        public string KeyName;
        public void SetKey(string key)
        {
            KeyName = key;
        }

        public string GetKey()
        {
            return KeyName;
        }

#if UNITY_EDITOR

        [ContextMenu("删除自己")]
        public void DestroyDirecly()
        {
            Undo.DestroyObjectImmediate(this);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }
#endif
    }
}