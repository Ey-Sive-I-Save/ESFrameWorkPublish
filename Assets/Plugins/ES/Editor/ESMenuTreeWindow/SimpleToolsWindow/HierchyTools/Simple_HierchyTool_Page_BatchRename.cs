using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;


namespace ES
{

    #region 批量重命名工具
    [Serializable]
    public class Page_BatchRename : ESWindowPageBase
    {
        [Title("批量重命名工具", "批量重命名选中的GameObject", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择层级中的GameObject，\n设置重命名规则，\n点击重命名按钮批量修改";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary => $"当前选择: {(Selection.gameObjects != null ? Selection.gameObjects.Length : 0)} 个对象 | 模式: {renameMode}";

        [ShowInInspector, ReadOnly, LabelText("重命名预览（前10项）"), ListDrawerSettings(DraggableItems = false)]
        [PropertyTooltip("显示选中对象的示例预览：原名 -> 新名，最多显示前10项。")]
        public List<string> renamePreview
        {
            get
            {
                var result = new List<string>();
                var selected = Selection.gameObjects ?? new GameObject[0];
                int limit = Math.Min(selected.Length, 10);
                for (int i = 0; i < limit; i++)
                {
                    var obj = selected[i];
                    string newName = obj.name;
                    switch (renameMode)
                    {
                        case RenameMode.Prefix:
                            newName = prefixText + obj.name;
                            break;
                        case RenameMode.Suffix:
                            newName = obj.name + suffixText;
                            break;
                        case RenameMode.Replace:
                            if (!string.IsNullOrEmpty(findText))
                            {
                                if (replaceCaseSensitive)
                                    newName = obj.name.Replace(findText, replaceText ?? string.Empty);
                                else
                                    newName = System.Text.RegularExpressions.Regex.Replace(obj.name, System.Text.RegularExpressions.Regex.Escape(findText), replaceText ?? string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            }
                            break;
                        case RenameMode.Number:
                            newName = baseName + numberSeparator + (startNumber + i).ToString($"D{numberDigits}");
                            break;
                    }
                    result.Add($"{obj.name} -> {newName}");
                }
                if (selected.Length > 10) result.Add($"... 共 {selected.Length} 个对象，显示前 {limit} 个示例");
                if (result.Count == 0) result.Add("未选择对象");
                return result;
            }
        }

        public enum RenameMode
        {
            [LabelText("前缀模式")]
            Prefix,
            [LabelText("后缀模式")]
            Suffix,
            [LabelText("替换模式")]
            Replace,
            [LabelText("编号模式")]
            Number
        }

        [InfoBox("@RenameModeInfo", InfoMessageType.Info)]
        [LabelText("重命名模式"), Space(5)]
        public RenameMode renameMode = RenameMode.Prefix;

        [ShowInInspector, HideLabel]
        private string RenameModeInfo
        {
            get
            {
                switch (renameMode)
                {
                    case RenameMode.Prefix:
                        return "前缀模式：在现有名称前添加指定前缀。例如，将 'Cube' 变为 'New_Cube'。";
                    case RenameMode.Suffix:
                        return "后缀模式：在现有名称后添加指定后缀。例如，将 'Cube' 变为 'Cube_Copy'。";
                    case RenameMode.Replace:
                        return "替换模式：将名称中匹配的文本替换为新的文本。例如，将 'OldCube' 中的 'Old' 替换为 'New'。";
                    case RenameMode.Number:
                        return "编号模式：使用基础名称并附加按序号格式化的编号。例如，'Object_001'、'Object_002'（可配置起始编号和位数）。";
                    default:
                        return string.Empty;
                }
            }
        }

        [LabelText("前缀文本"), ShowIf("renameMode", RenameMode.Prefix), Space(5)]
        public string prefixText = "New_";

        [LabelText("后缀文本"), ShowIf("renameMode", RenameMode.Suffix), Space(5)]
        public string suffixText = "_Copy";

        [LabelText("查找文本"), ShowIf("renameMode", RenameMode.Replace), Space(5)]
        public string findText = "Old";

        [LabelText("替换文本"), ShowIf("renameMode", RenameMode.Replace), Space(5)]
        public string replaceText = "New";

        [LabelText("基础名称"), ShowIf("renameMode", RenameMode.Number), Space(5)]
        public string baseName = "Object";

        [LabelText("起始编号"), ShowIf("renameMode", RenameMode.Number), Space(5)]
        public int startNumber = 1;

        [ShowIf("renameMode", RenameMode.Number)]
        [InfoBox("指定序号的位数，左侧补零。例如：3 -> 001。", InfoMessageType.Info)]
        [LabelText("编号位数"), Range(1, 5), Space(5)]
        public int numberDigits = 3;

        [ShowIf("renameMode", RenameMode.Replace), LabelText("区分大小写"), Space(5)]
        public bool replaceCaseSensitive = true;

        [ShowIf("renameMode", RenameMode.Number), LabelText("编号分隔符"), Space(5)]
        public string numberSeparator = "_";

        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawResultSummary("最近重命名结果", lastResultSummary, lastResultDetail);
        }

        [Button("批量重命名", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void BatchRename()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择要重命名的GameObject！", "确定");
                return;
            }
            // 输入校验
            if (renameMode == RenameMode.Replace && string.IsNullOrEmpty(findText))
            {
                EditorUtility.DisplayDialog("错误", "替换模式下请输入要查找的文本。", "确定");
                return;
            }
            string safeReplaceText = replaceText ?? string.Empty;
            string safePrefixText = prefixText ?? string.Empty;
            string safeSuffixText = suffixText ?? string.Empty;
            string safeBaseName = string.IsNullOrEmpty(baseName) ? "Object" : baseName;
            string safeSeparator = numberSeparator ?? string.Empty;

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Batch Rename");
            int changedCount = 0;
            string preview = string.Empty;

            try
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var obj in selectedObjects)
                {
                    var parent = obj.transform.parent;
                    if (parent != null)
                    {
                        for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
                        {
                            var sibling = parent.GetChild(childIndex).gameObject;
                            if (!selectedObjects.Contains(sibling))
                                usedNames.Add(parent.GetInstanceID() + "|" + sibling.name);
                        }
                    }
                    else if (obj.scene.IsValid())
                    {
                        foreach (var root in obj.scene.GetRootGameObjects())
                        {
                            if (!selectedObjects.Contains(root))
                                usedNames.Add("root:" + obj.scene.handle + "|" + root.name);
                        }
                    }
                }
                var newNames = new string[selectedObjects.Length];

                // 预计算新名称并检测冲突
                for (int i = 0; i < selectedObjects.Length; i++)
                {
                    var obj = selectedObjects[i];
                    string newName = obj.name;
                    switch (renameMode)
                    {
                        case RenameMode.Prefix:
                            newName = safePrefixText + obj.name;
                            break;
                        case RenameMode.Suffix:
                            newName = obj.name + safeSuffixText;
                            break;

                        case RenameMode.Replace:
                            if (replaceCaseSensitive)
                                newName = obj.name.Replace(findText, safeReplaceText);
                            else
                                newName = System.Text.RegularExpressions.Regex.Replace(obj.name, System.Text.RegularExpressions.Regex.Escape(findText), safeReplaceText, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            break;
                        case RenameMode.Number:
                            newName = safeBaseName + safeSeparator + (startNumber + i).ToString($"D{numberDigits}");
                            break;
                    }
                    newNames[i] = newName;
                    string parentKey = obj.transform.parent != null ? obj.transform.parent.GetInstanceID() + "|" : "root:" + obj.scene.handle + "|";
                    if (usedNames.Contains(parentKey + newName))
                    {
                        // 冲突：追加索引以保证唯一
                        int suffix = 1;
                        var candidate = newName + "(" + suffix + ")";
                        while (usedNames.Contains(parentKey + candidate))
                        {
                            suffix++;
                            candidate = newName + "(" + suffix + ")";
                        }
                        newName = candidate;
                        newNames[i] = newName;
                    }
                    usedNames.Add(parentKey + newName);
                }

                var previewLines = new List<string>();
                for (int i = 0; i < selectedObjects.Length; i++)
                {
                    if (selectedObjects[i].name == newNames[i])
                        continue;

                    changedCount++;
                    previewLines.Add($"{selectedObjects[i].name} -> {newNames[i]}");
                }

                if (changedCount == 0)
                {
                    EditorUtility.DisplayDialog("没有需要修改的对象", "按当前规则计算后，所有对象名称都不会变化。", "知道了");
                    return;
                }

                preview = SimpleToolsSafetyUtility.JoinPreview(previewLines, 12);
                if (!EditorUtility.DisplayDialog("确认批量重命名",
                    $"将重命名 {changedCount} / {selectedObjects.Length} 个对象。\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                    "开始重命名", "取消"))
                    return;

                EditorUtility.DisplayProgressBar("批量重命名", "准备重命名...", 0f);

                // 应用修改
                bool changedAny = false;
                for (int i = 0; i < selectedObjects.Length; i++)
                {
                    var obj = selectedObjects[i];
                    var newName = newNames[i];

                    // 跳过无变化的项
                    if (obj.name == newName) continue;

                    Undo.RecordObject(obj, "Rename Object");
                    obj.name = newName;
                    EditorUtility.SetDirty(obj);
                    changedAny = true;

                    if (i % 10 == 0)
                        EditorUtility.DisplayProgressBar("批量重命名", $"正在重命名: {i + 1}/{selectedObjects.Length}", (float)i / selectedObjects.Length);
                }

                if (changedAny)
                    MarkScenesDirty(selectedObjects);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(group);
            }

            EditorUtility.DisplayDialog("成功", "批量重命名已完成。", "确定");
            lastResultSummary = $"重命名完成: 修改 {changedCount} / {selectedObjects.Length} 个对象 | 模式: {renameMode}";
            lastResultDetail = preview;
        }

        private void MarkScenesDirty(IEnumerable<GameObject> targets)
        {
            if (targets == null)
                return;

            foreach (var scene in targets
                .Where(obj => obj != null && obj.scene.IsValid())
                .Select(obj => obj.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }


    #endregion

}
