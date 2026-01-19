using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;


namespace ES
{

    #region 层级工具集介绍
    [Serializable]
    public class Page_HierarchyTools : ESWindowPageBase
    {
        [Title("层级工具集介绍", "快速入门指南", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "层级工具集包含以下功能：\n\n1. 批量重命名：批量修改选中GameObject的名称，支持前缀、后缀、替换和编号模式。\n\n2. 物理对齐：对齐多个GameObject的位置，支持各种对齐方式和间距设置。\n\n3. 批量静态设置：批量设置GameObject的静态标记，用于优化渲染和导航。";
    }
    #endregion

}