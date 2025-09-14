using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    /// <summary>
    /// ##这个特性专用于收集通过显示名-来获得RunTime类型
    /// 1.用于中文化＋自动化 管理 SoDataInfo
    /// ESEditorRunTimeMaster 读取和存储 关于它的信息
    /// </summary>
    public class ESDisplayToType : Attribute
    {
        public string SelectGroup = "收集到";
        public string DisplayKeyName = "显示名与键";
        public ESDisplayToType(string selectGroup,string displayName)
        {
            SelectGroup = selectGroup;
            DisplayKeyName = displayName;
        }
    }
}

