using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Callbacks;

namespace ES
{
    #region Prefab信息类
    /// <summary>
    /// Prefab实例信息数据结构
    /// </summary>
    [Serializable]
    public class PrefabInstanceInfo
    {
        [ReadOnly, LabelText("实例对象")]
        public GameObject instance;

        [ReadOnly, LabelText("资产路径")]
        public string prefabPath;

        [ReadOnly, LabelText("已修改")]
        public bool hasModifications;

        [ReadOnly, LabelText("资产丢失")]
        public bool isMissing;

        [ReadOnly, LabelText("变体类型")]
        public bool isVariant;

        [Button("🎯 定位实例", ButtonSizes.Small), HorizontalGroup("Actions")]
        [Tooltip("在Hierarchy中选中并高亮显示此Prefab实例")]
        public void SelectInstance()
        {
            if (instance != null)
            {
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
            }
        }

        [Button("📁 定位资产", ButtonSizes.Small), HorizontalGroup("Actions")]
        [Tooltip("在Project窗口中定位并高亮显示对应的Prefab资产文件")]
        public void PingAsset()
        {
            if (!string.IsNullOrEmpty(prefabPath) && !isMissing)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }
        }
    }
    #endregion

    #region Prefab管理工具
    /// <summary>
    /// 商业级Prefab实例批量管理工具
    /// 提供全面的Prefab实例管理、检测、优化功能
    /// 支持批量应用、还原、断开、替换等操作
    /// 所有危险操作均带有确认对话框和Undo支持
    /// </summary>
    [Serializable]
    public class Page_PrefabManagement : ESWindowPageBase
    {
        #region UI配置
        [Title("Prefab实例管理工具", "商业级Prefab实例批量管理解决方案", bold: true, titleAlignment: TitleAlignments.Centered)]

        [TabGroup("📖 使用说明", "功能概览")]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.8f, 0.9f, 1f)]
        public string featureOverview =
            "🔧 批量应用/还原Prefab实例修改到原始资产\n" +
            "🔗 断开Prefab实例连接或替换为其他Prefab\n" +
            "🔍 检测丢失/修改的Prefab实例\n" +
            "🎯 查找和选择场景中的相同类型Prefab实例\n" +
            "🏷️ Prefab变体检测和管理";

        [TabGroup("📖 使用说明", "操作流程")]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.9f, 0.9f, 0.8f)]
        public string operationFlow =
            "1️⃣ 在Hierarchy中选择目标对象\n" +
            "2️⃣ 点击'分析选中对象'查看详情\n" +
            "3️⃣ 根据需要执行批量操作\n" +
            "4️⃣ 所有危险操作支持Undo撤销";

        [TabGroup("📖 使用说明", "使用提示")]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.9f, 0.8f, 0.9f)]
        public string usageTips =
            "💡 勾选'包含子对象'可处理嵌套Prefab\n" +
            "💡 操作前建议先分析以了解影响范围\n" +
            "💡 批量操作会显示操作对象数量\n" +
            "💡 所有危险操作都有确认对话框\n" +
            "💡 支持Ctrl+Z撤销大部分操作";
        #endregion

        #region 配置参数
        [BoxGroup("⚙️ 基础设置", showLabel: false)]
        [LabelText("包含子对象")]
        [Tooltip("启用后，分析和操作将包含选中对象的所有子级对象，包括嵌套的Prefab实例")]
        [InfoBox("勾选后将处理选中对象的所有子级Prefab实例", InfoMessageType.Info)]
        public bool includeChildren = true;

        [BoxGroup("⚙️ 基础设置", showLabel: false)]
        [LabelText("替换目标Prefab"), AssetsOnly]
        [Tooltip("选择用于'替换为目标Prefab实例'操作的Prefab资产。替换时会保留原对象的Transform信息")]
        [InfoBox("设置用于'替换为目标Prefab实例'操作的Prefab资产", InfoMessageType.Info)]
        public GameObject targetPrefab;
        #endregion

        #region 统计信息
        [BoxGroup("📊 当前选择统计", showLabel: false)]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.7f, 1f, 0.7f)]
        [Tooltip("显示当前选中对象的分析结果，包括Prefab实例数量、修改状态等统计信息")]
        public string currentStats = "📌 请先在Hierarchy中选择对象，然后点击'分析选中对象'...";

        [BoxGroup("📊 当前选择统计", showLabel: false)]
        [ListDrawerSettings(IsReadOnly = true, DraggableItems = false, HideAddButton = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
        [LabelText("Prefab实例列表")]
        [Tooltip("列出所有检测到的Prefab实例，包含详细信息和快速操作按钮")]
        public List<PrefabInstanceInfo> detectedPrefabs = new List<PrefabInstanceInfo>();
        #endregion

        #region 分析功能
        /// <summary>
        /// 刷新并分析当前选择的Prefab实例，生成详细统计信息
        /// </summary>
        [Button("🔍 分析选中对象", ButtonHeight = 40), GUIColor(0.6f, 0.8f, 1f)]
        [Tooltip("分析当前选中的对象，统计Prefab实例数量、修改状态、变体类型等详细信息。结果会显示在下方统计面板中。")]
        public void AnalyzeSelection()
        {
            detectedPrefabs.Clear();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                currentStats = "❌ 未选择任何对象，请在Hierarchy中选择GameObject";
                return;
            }

            var allObjects = new List<GameObject>(selectedObjects);

            // 如果包含子对象，则递归添加所有子级
            if (includeChildren)
            {
                foreach (var obj in selectedObjects)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>(true)
                        .Select(t => t.gameObject));
                }
            }

            int totalCount = allObjects.Count;
            int prefabCount = 0;
            int modifiedCount = 0;
            int missingCount = 0;
            int variantCount = 0;

            // 遍历所有对象进行分析
            foreach (var obj in allObjects.Distinct())
            {
                if (PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    prefabCount++;
                    var info = new PrefabInstanceInfo
                    {
                        instance = obj,
                        hasModifications = PrefabUtility.HasPrefabInstanceAnyOverrides(obj, false)
                    };

                    // 获取Prefab资产路径和类型
                    var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(obj);
                    if (prefabAsset != null)
                    {
                        info.prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
                        info.isVariant = PrefabUtility.IsPartOfVariantPrefab(prefabAsset);
                        if (info.isVariant) variantCount++;
                    }
                    else
                    {
                        info.isMissing = true;
                        info.prefabPath = "⚠️ Prefab资产丢失";
                        missingCount++;
                    }

                    if (info.hasModifications) modifiedCount++;

                    detectedPrefabs.Add(info);
                }
            }

            // 更新统计信息显示
            currentStats = $"📊 分析结果:\n" +
                $"━━━━━━━━━━━━━━━━━━━━\n" +
                $"总对象数: {totalCount}\n" +
                $"Prefab实例: {prefabCount}\n" +
                $"已修改实例: {modifiedCount}\n" +
                $"变体实例: {variantCount}\n" +
                $"丢失引用: {missingCount}\n" +
                $"━━━━━━━━━━━━━━━━━━━━";

            Debug.Log($"[Prefab管理] 分析完成 - Prefab实例: {prefabCount}, 已修改: {modifiedCount}, 丢失: {missingCount}, 变体: {variantCount}");
        }
        #endregion

        #region 分析和检测功能

        /// <summary>
        /// 在当前场景中查找所有丢失Prefab引用的对象
        /// </summary>
        [FoldoutGroup("🔍 分析和检测")]
        [Button("🔍 检测丢失的Prefab", ButtonHeight = 35), GUIColor(1f, 0.7f, 0.5f)]
        [Tooltip("扫描整个场景，查找所有Prefab引用丢失的对象。找到的对象会被自动选中，方便批量处理。")]
        public void FindMissingPrefabs()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var missingList = new List<GameObject>();

            // 遍历场景中的所有对象
            foreach (var root in rootObjects)
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    {
                        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                        if (prefabAsset == null)
                        {
                            missingList.Add(t.gameObject);
                        }
                    }
                }
            }

            if (missingList.Count > 0)
            {
                Selection.objects = missingList.ToArray();
                EditorUtility.DisplayDialog("检测完成",
                    $"⚠️ 发现 {missingList.Count} 个丢失Prefab引用的对象！\n\n已自动选中这些对象，请检查并修复。\n建议删除或重新连接这些对象。",
                    "确定");
                Debug.LogWarning($"[Prefab管理] 发现 {missingList.Count} 个丢失Prefab引用的对象");
            }
            else
            {
                EditorUtility.DisplayDialog("检测完成", "✅ 场景中没有丢失的Prefab引用！\n\n所有Prefab实例都正常连接。", "确定");
            }
        }

        /// <summary>
        /// 选择场景中所有与当前选中对象相同类型的Prefab实例
        /// </summary>
        [FoldoutGroup("🔍 分析和检测")]
        [Button("🎯 选择相同Prefab", ButtonHeight = 35), GUIColor(0.8f, 0.9f, 0.7f)]
        [Tooltip("选择场景中所有与当前选中Prefab实例相同类型的对象。适用于批量修改相同类型的Prefab。")]
        public void SelectSamePrefabs()
        {
            var selected = Selection.activeGameObject;
            if (selected == null || !PrefabUtility.IsPartOfPrefabInstance(selected))
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择一个Prefab实例！", "确定");
                return;
            }

            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selected);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog("错误", "❌ 无法获取Prefab资产！\n该对象的Prefab引用可能已丢失。", "确定");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var sameTypeList = new List<GameObject>();

            // 遍历场景查找相同类型的Prefab实例
            foreach (var root in rootObjects)
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    {
                        var asset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                        if (asset == prefabAsset)
                        {
                            sameTypeList.Add(t.gameObject);
                        }
                    }
                }
            }

            if (sameTypeList.Count > 0)
            {
                Selection.objects = sameTypeList.ToArray();
                EditorUtility.DisplayDialog("选择完成",
                    $"✅ 已选中 {sameTypeList.Count} 个相同的Prefab实例！\n\nPrefab路径: {AssetDatabase.GetAssetPath(prefabAsset)}",
                    "确定");
                Debug.Log($"[Prefab管理] 选择了 {sameTypeList.Count} 个相同的Prefab实例: {prefabAsset.name}");
            }
        }
        #endregion

        #region 基础Prefab操作
        /// <summary>
        /// 批量应用所有选中Prefab实例的更改到资产文件
        /// </summary>
        [FoldoutGroup("⚡ 基础操作")]
        [Button("✅ 应用所有Prefab实例修改", ButtonHeight = 45), GUIColor(0.5f, 1f, 0.5f)]
        [Tooltip("将选中Prefab实例的所有修改应用到原始Prefab资产文件。这会影响项目中所有使用该Prefab的地方。")]
        public void ApplyAllPrefabs()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择GameObject！", "确定");
                return;
            }

            // 统计需要应用的Prefab实例数量
            int prefabCount = selectedObjects.Count(obj => PrefabUtility.IsPartOfPrefabInstance(obj));
            if (prefabCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "ℹ️ 选中的对象中没有Prefab实例！", "确定");
                return;
            }

            // 确认操作
            if (!EditorUtility.DisplayDialog("确认应用Prefab实例修改",
                $"⚠️ 确定要应用 {prefabCount} 个Prefab实例的所有更改吗？\n\n" +
                $"此操作将:\n" +
                $"• 覆盖Prefab资产文件\n" +
                $"• 影响所有引用该Prefab的场景\n" +
                $"• 支持Ctrl+Z撤销\n\n" +
                $"建议操作前备份重要资产！",
                "应用", "取消"))
            {
                return;
            }

            // 执行应用操作
            int appliedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    try
                    {
                        PrefabUtility.ApplyPrefabInstance(obj, InteractionMode.UserAction);
                        appliedCount++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Prefab管理] 应用失败: {obj.name} - {e.Message}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("操作完成", $"✅ 成功应用 {appliedCount} / {prefabCount} 个Prefab实例的更改！", "确定");
            Debug.Log($"[Prefab管理] 应用完成 - 成功: {appliedCount} / 总数: {prefabCount}");
        }

        /// <summary>
        /// 批量还原所有选中Prefab实例的更改，恢复到资产原始状态
        /// </summary>
        [FoldoutGroup("⚡ 基础操作")]
        [Button("↩️ 还原所有Prefab实例修改", ButtonHeight = 45), GUIColor(1f, 0.8f, 0.5f)]
        [Tooltip("将选中Prefab实例的所有修改还原到原始Prefab资产的状态。所有未应用的更改将会丢失。")]
        public void RevertAllPrefabs()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择GameObject！", "确定");
                return;
            }

            // 统计需要还原的Prefab实例数量
            int prefabCount = selectedObjects.Count(obj => PrefabUtility.IsPartOfPrefabInstance(obj));
            if (prefabCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "ℹ️ 选中的对象中没有Prefab实例！", "确定");
                return;
            }

            // 确认操作
            if (!EditorUtility.DisplayDialog("确认还原Prefab实例修改",
                $"⚠️ 确定要还原 {prefabCount} 个Prefab实例的所有更改吗？\n\n" +
                $"此操作将:\n" +
                $"• 丢失所有未应用的修改\n" +
                $"• 恢复到Prefab资产原始状态\n" +
                $"• 支持Ctrl+Z撤销\n\n" +
                $"请确认是否继续！",
                "还原", "取消"))
            {
                return;
            }

            // 执行还原操作
            int revertedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    try
                    {
                        PrefabUtility.RevertPrefabInstance(obj, InteractionMode.UserAction);
                        revertedCount++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Prefab管理] 还原失败: {obj.name} - {e.Message}");
                    }
                }
            }

            EditorUtility.DisplayDialog("操作完成", $"✅ 成功还原 {revertedCount} / {prefabCount} 个Prefab实例的更改！", "确定");
            Debug.Log($"[Prefab管理] 还原完成 - 成功: {revertedCount} / 总数: {prefabCount}");
        }

        /// <summary>
        /// 批量断开Prefab连接，将Prefab实例转换为普通GameObject
        /// </summary>
        [FoldoutGroup("⚡ 基础操作")]
        [Button("🔗 断开Prefab实例连接", ButtonHeight = 45), GUIColor(1f, 0.6f, 0.6f)]
        [Tooltip("将选中Prefab实例转换为普通GameObject，断开与Prefab资产的连接。转换后无法再接收Prefab更新。")]
        public void UnpackPrefabs()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择GameObject！", "确定");
                return;
            }

            // 统计需要断开的Prefab实例数量
            int prefabCount = selectedObjects.Count(obj => PrefabUtility.IsPartOfPrefabInstance(obj));
            if (prefabCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "ℹ️ 选中的对象中没有Prefab实例！", "确定");
                return;
            }

            // 确认操作
            if (!EditorUtility.DisplayDialog("确认断开Prefab实例连接",
                $"⚠️ 确定要断开 {prefabCount} 个Prefab实例的连接吗？\n\n" +
                $"此操作将:\n" +
                $"• 对象转换为普通GameObject\n" +
                $"• 失去与Prefab资产的关联\n" +
                $"• 无法接收Prefab资产更新\n" +
                $"• 支持Ctrl+Z撤销\n\n" +
                $"请谨慎操作！",
                "断开", "取消"))
            {
                return;
            }

            // 执行断开操作
            int unpackedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    try
                    {
                        PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                        unpackedCount++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Prefab管理] 断开失败: {obj.name} - {e.Message}");
                    }
                }
            }

            EditorUtility.DisplayDialog("操作完成", $"✅ 成功断开 {unpackedCount} / {prefabCount} 个Prefab连接！", "确定");
            Debug.Log($"[Prefab管理] 断开完成 - 成功: {unpackedCount} / 总数: {prefabCount}");
        }

        /// <summary>
        /// 批量替换选中对象为指定的目标Prefab
        /// </summary>
        [FoldoutGroup("⚡ 基础操作")]
        [Button("🔄 替换为目标Prefab实例", ButtonHeight = 45), GUIColor(0.6f, 0.8f, 1f)]
        [Tooltip("将选中对象替换为指定的目标Prefab。会保留原对象的Transform信息、名称和层级关系。")]
        public void ReplacePrefabs()
        {
            if (targetPrefab == null)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先设置目标Prefab！\n\n在'基础设置'中选择要替换的目标Prefab资产。", "确定");
                return;
            }

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择要替换的GameObject！", "确定");
                return;
            }

            // 确认操作
            if (!EditorUtility.DisplayDialog("确认替换为目标Prefab实例",
                $"⚠️ 确定要将 {selectedObjects.Length} 个对象替换为目标Prefab吗？\n\n" +
                $"目标Prefab: {targetPrefab.name}\n" +
                $"路径: {AssetDatabase.GetAssetPath(targetPrefab)}\n\n" +
                $"此操作将:\n" +
                $"• 销毁原对象并创建新Prefab实例\n" +
                $"• 保留Transform信息(位置/旋转/缩放)\n" +
                $"• 保留对象名称和父级关系\n" +
                $"• 支持Ctrl+Z撤销",
                "替换", "取消"))
            {
                return;
            }

            // 执行替换操作
            int replacedCount = 0;
            foreach (var obj in selectedObjects)
            {
                try
                {
                    var parent = obj.transform.parent;
                    var position = obj.transform.position;
                    var rotation = obj.transform.rotation;
                    var scale = obj.transform.localScale;
                    var name = obj.name;
                    var siblingIndex = obj.transform.GetSiblingIndex();

                    // 实例化新Prefab
                    var newObj = PrefabUtility.InstantiatePrefab(targetPrefab) as GameObject;
                    newObj.transform.SetParent(parent);
                    newObj.transform.position = position;
                    newObj.transform.rotation = rotation;
                    newObj.transform.localScale = scale;
                    newObj.name = name;
                    newObj.transform.SetSiblingIndex(siblingIndex);

                    // 注册Undo并销毁原对象
                    Undo.RegisterCreatedObjectUndo(newObj, "Replace Prefab");
                    Undo.DestroyObjectImmediate(obj);
                    replacedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Prefab管理] 替换失败: {obj.name} - {e.Message}");
                }
            }

            EditorUtility.DisplayDialog("操作完成",
                $"✅ 成功替换 {replacedCount} / {selectedObjects.Length} 个对象为目标Prefab！\n\n新Prefab: {targetPrefab.name}",
                "确定");
            Debug.Log($"[Prefab管理] 替换完成 - 成功: {replacedCount} / 总数: {selectedObjects.Length}");
        }
        #endregion
      }
    #endregion
}

