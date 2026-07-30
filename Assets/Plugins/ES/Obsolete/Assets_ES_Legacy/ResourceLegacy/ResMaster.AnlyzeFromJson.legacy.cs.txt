
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES资源系统主控类 - JSON配置解析部分
    /// 
    /// 【注意】
    /// ESResKey已移动到独立文件：Assets/Plugins/ES/0_Stand/_Res/ResUse/ESResKey.cs
    /// 如需查看或修改ESResKey，请访问新的文件位置。
    /// </summary>
    public partial class ESResMaster
    {
        public static string[] ABNames;
    }
}
