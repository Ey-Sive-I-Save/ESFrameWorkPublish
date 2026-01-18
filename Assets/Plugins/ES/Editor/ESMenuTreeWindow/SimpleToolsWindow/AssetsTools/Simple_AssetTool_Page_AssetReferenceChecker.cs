using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace ES
{
    #region 资源引用检查工具
    [Serializable]
    public class Page_AssetReferenceChecker : ESWindowPageBase
    {
        [Title("资源引用检查工具", "检查资源的引用关系", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择要检查的资源，\n点击查找按钮检测引用，\n查看引用列表";

        [LabelText("检查文件夹"), FolderPath, Space(5)]
        public string checkFolder = "Assets";

        [ShowInInspector, ReadOnly, LabelText("未使用的资源"), ListDrawerSettings(DraggableItems = false)]
        public List<string> unusedAssets = new List<string>();

        [Button("查找未使用的资源", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void FindUnusedAssets()
        {
            if (!AssetDatabase.IsValidFolder(checkFolder))
            {
                EditorUtility.DisplayDialog("错误", "请选择有效的文件夹！", "确定");
                return;
            }

            unusedAssets.Clear();
            var allAssets = AssetDatabase.FindAssets("", new[] { checkFolder });

            EditorUtility.DisplayProgressBar("检查资源引用", "正在分析...", 0f);

            for (int i = 0; i < allAssets.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(allAssets[i]);

                // 跳过文件夹和脚本
                if (AssetDatabase.IsValidFolder(assetPath) || assetPath.EndsWith(".cs"))
                    continue;

                EditorUtility.DisplayProgressBar("检查资源引用", $"检查: {Path.GetFileName(assetPath)}", (float)i / allAssets.Length);

                // 查找依赖
                var dependencies = AssetDatabase.GetDependencies(assetPath, false);
                bool isReferenced = false;

                // 简化检查：查看是否在场景中被引用
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (obj != null)
                {
                    // 这是一个简化的检查，实际项目中可能需要更复杂的逻辑
                    var referencingAssets = AssetDatabase.FindAssets("t:Scene");
                    foreach (var sceneGuid in referencingAssets)
                    {
                        var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                        var sceneDeps = AssetDatabase.GetDependencies(scenePath, true);
                        if (System.Array.IndexOf(sceneDeps, assetPath) >= 0)
                        {
                            isReferenced = true;
                            break;
                        }
                    }
                }

                if (!isReferenced)
                {
                    unusedAssets.Add(assetPath);
                }
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("完成", $"找到 {unusedAssets.Count} 个可能未使用的资源！", "确定");
        }

        [Button("选中未使用的资源", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void SelectUnusedAssets()
        {
            var objects = unusedAssets.Select(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)).ToArray();
            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
        }

        [ShowInInspector, ReadOnly, LabelText("选中资源的引用"), ListDrawerSettings(DraggableItems = false)]
        public List<string> selectedAssetReferences = new List<string>();

        [Button("查找选中资源的引用", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
        public void FindReferencesToSelected()
        {
            selectedAssetReferences.Clear();

            var selectedAsset = Selection.activeObject;
            if (selectedAsset == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择一个资源！", "确定");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selectedAsset);
            var allAssets = AssetDatabase.GetAllAssetPaths();

            EditorUtility.DisplayProgressBar("查找引用", "正在分析...", 0f);

            for (int i = 0; i < allAssets.Length; i++)
            {
                EditorUtility.DisplayProgressBar("查找引用", $"检查: {Path.GetFileName(allAssets[i])}", (float)i / allAssets.Length);

                var dependencies = AssetDatabase.GetDependencies(allAssets[i], false);
                if (System.Array.IndexOf(dependencies, assetPath) >= 0)
                {
                    selectedAssetReferences.Add(allAssets[i]);
                }
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("完成", $"找到 {selectedAssetReferences.Count} 个引用！", "确定");
        }
    }
    #endregion
}