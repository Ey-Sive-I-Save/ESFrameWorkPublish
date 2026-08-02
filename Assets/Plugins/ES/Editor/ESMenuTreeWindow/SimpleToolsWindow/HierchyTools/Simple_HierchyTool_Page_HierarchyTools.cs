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
        [HideInInspector]
        public string readMe = "层级工具集包含以下功能：\n\n1. 批量重命名：批量修改选中GameObject的名称，支持前缀、后缀、替换和编号模式。\n\n2. 物理对齐：对齐多个GameObject的位置，支持各种对齐方式和间距设置。\n\n3. 批量静态设置：批量设置GameObject的静态标记，用于优化渲染和导航。";

        [OnInspectorGUI, PropertyOrder(100)]
        private void DrawHierarchyToolGuide()
        {
            SimpleToolsPanelUtility.DrawToolHeader(
                "层级工具集",
                "按当前任务选择重命名、对齐、静态设置和其它场景批处理工具。",
                SimpleToolsMaturity.Industrial,
                "具体工具可能修改场景对象；进入工具后先确认选区、规则和预览。");
            SimpleToolsPanelUtility.DrawSectionTitle("工具目录", "先判断要改什么，再进入对应工具；本页只负责理解入口，不会扫描或修改场景。");
            EditorGUILayout.LabelField("批量重命名", "修改选中 GameObject 名称，支持前缀、后缀、替换和编号。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("物理对齐", "对齐、分布和尺寸匹配多个 GameObject，执行前提供审计和预览。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("批量静态设置", "批量调整 GameObject 静态标记，执行前确认当前选区和变更范围。", EditorStyles.wordWrappedMiniLabel);
        }
    }
    #endregion

}
