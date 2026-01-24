using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 场景管理数据 - 存储自定义场景集合和资产快捷方式
    /// </summary>
    [Serializable]
    public class SceneQuickAccessData
    {
        [LabelText("场景名称")]
        public string DisplayName;

        [LabelText("场景路径"), ReadOnly]
        public string ScenePath;

#if UNITY_EDITOR
        [LabelText("场景资产"), AssetsOnly]
        public SceneAsset SceneAsset;
#endif

        [LabelText("颜色标记")]
        public Color Color = Color.white;

        [LabelText("描述")]
        [TextArea(1, 3)]
        public string Description;

        [LabelText("分组")]
        public string Group = "默认";

        [LabelText("是否启用")]
        public bool Enabled = true;

        public SceneQuickAccessData() { }

#if UNITY_EDITOR
        public SceneQuickAccessData(string scenePath)
        {
            ScenePath = scenePath;
            SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            DisplayName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            Color = Color.white;
            Group = "默认";
            Enabled = true;
        }
#endif
    }

    /// <summary>
    /// 资产快捷访问数据
    /// </summary>
    [Serializable]
    public class AssetQuickAccessData
    {
        [LabelText("显示名称")]
        public string DisplayName;

        [LabelText("资产对象"), AssetsOnly]
        public UnityEngine.Object Asset;

        [LabelText("分组")]
        public string Group = "默认";

        [LabelText("是否启用")]
        public bool Enabled = true;

        public AssetQuickAccessData() { }

        public AssetQuickAccessData(string name, UnityEngine.Object asset, string group = "默认")
        {
            DisplayName = name;
            Asset = asset;
            Group = group;
            Enabled = true;
        }
    }

    /// <summary>
    /// ES场景管理器全局数据
    /// </summary>
    [CreateAssetMenu(fileName = "ESSceneGlobalData", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "场景管理器数据")]
    public class ESSceneGlobalData : ESEditorGlobalSo<ESSceneGlobalData>
    {
        [TabGroup("场景管理")]
        [Title("自定义场景快捷方式", "管理常用场景的快速访问", bold: true)]
        [InfoBox("在此添加常用场景，可通过工具栏快速打开")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "DisplayName")]
        public List<SceneQuickAccessData> CustomScenes = new List<SceneQuickAccessData>();

        [TabGroup("场景管理")]
        [Title("场景分组列表")]
        [InfoBox("场景的自定义分组")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<string> SceneGroups = new List<string> { "默认", "核心场景", "测试场景", "UI场景" };

        [TabGroup("资产快捷方式")]
        [Title("自定义资产快捷方式", "管理常用资产的快速访问", bold: true)]
        [InfoBox("在此添加常用资产，可通过工具栏快速定位")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "DisplayName")]
        public List<AssetQuickAccessData> CustomAssets = new List<AssetQuickAccessData>();

        [TabGroup("资产快捷方式")]
        [Title("资产分组列表")]
        [InfoBox("资产的自定义分组")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<string> AssetGroups = new List<string> { "默认", "脚本", "预制体", "配置", "材质" };

        [TabGroup("设置")]
        [Title("全局设置")]
        [LabelText("自动保存当前场景")]
        [InfoBox("跳转场景前自动保存当前场景")]
        public bool AutoSaveBeforeSwitch = true;

        [TabGroup("设置")]
        [LabelText("使用叠加模式")]
        [InfoBox("使用叠加模式打开场景，而不是替换当前场景")]
        public bool UseAdditiveMode = false;

        [TabGroup("设置")]
        [LabelText("显示场景路径")]
        [InfoBox("在菜单中显示完整的场景路径")]
        public bool ShowScenePath = false;

        [TabGroup("设置")]
        [LabelText("包含Build Settings场景")]
        [InfoBox("在快捷菜单中包含Build Settings中的场景")]
        public bool IncludeBuildScenes = true;

        [TabGroup("设置")]
        [LabelText("包含所有项目场景")]
        [InfoBox("在快捷菜单中包含项目中的所有场景")]
        public bool IncludeAllScenes = false;

        /// <summary>
        /// 添加场景到自定义列表
        /// </summary>
#if UNITY_EDITOR
        public void AddScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogWarning("场景路径为空！");
                return;
            }

            // 检查是否已存在
            if (CustomScenes.Exists(s => s.ScenePath == scenePath))
            {
                Debug.LogWarning($"场景已存在: {scenePath}");
                return;
            }

            // 记录撤销操作
            Undo.RegisterCompleteObjectUndo(this, "添加场景");
            Undo.SetCurrentGroupName("场景管理操作");

            var newScene = new SceneQuickAccessData(scenePath);
            CustomScenes.Add(newScene);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"已添加场景: {newScene.DisplayName}");
        }
#endif

        /// <summary>
        /// 移除场景
        /// </summary>
#if UNITY_EDITOR
        public void RemoveScene(string scenePath)
        {
            int index = CustomScenes.FindIndex(s => s.ScenePath == scenePath);
            if (index >= 0)
            {
                // 记录撤销操作
                Undo.RegisterCompleteObjectUndo(this, "移除场景");
                Undo.SetCurrentGroupName("场景管理操作");

                string name = CustomScenes[index].DisplayName;
                CustomScenes.RemoveAt(index);
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log($"已移除场景: {name}");
            }
        }
#endif

        /// <summary>
        /// 添加资产到快捷列表
        /// </summary>
#if UNITY_EDITOR
        public void AddAsset(string displayName, UnityEngine.Object asset, string group = "默认")
        {
            if (asset == null)
            {
                Debug.LogWarning("资产对象为空！");
                return;
            }

            // 检查是否已存在
            if (CustomAssets.Exists(a => a.Asset == asset))
            {
                Debug.LogWarning($"资产已存在: {displayName}");
                return;
            }

            // 记录撤销操作
            Undo.RegisterCompleteObjectUndo(this, "添加资产");
            Undo.SetCurrentGroupName("资产管理操作");

            var newAsset = new AssetQuickAccessData(displayName, asset, group);
            CustomAssets.Add(newAsset);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"已添加资产: {displayName}");
        }
#endif

        /// <summary>
        /// 移除资产
        /// </summary>
#if UNITY_EDITOR
        public void RemoveAsset(UnityEngine.Object asset)
        {
            int index = CustomAssets.FindIndex(a => a.Asset == asset);
            if (index >= 0)
            {
                // 记录撤销操作
                Undo.RegisterCompleteObjectUndo(this, "移除资产");
                Undo.SetCurrentGroupName("资产管理操作");

                string name = CustomAssets[index].DisplayName;
                CustomAssets.RemoveAt(index);
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log($"已移除资产: {name}");
            }
        }
#endif

        /// <summary>
        /// 获取所有启用的场景
        /// </summary>
#if UNITY_EDITOR
        public List<SceneQuickAccessData> GetEnabledScenes()
        {
            return CustomScenes.FindAll(s => s.Enabled && s.SceneAsset != null);
        }
#endif

        /// <summary>
        /// 获取所有启用的资产
        /// </summary>
        public List<AssetQuickAccessData> GetEnabledAssets()
        {
            return CustomAssets.FindAll(a => a.Enabled && a.Asset != null);
        }

        /// <summary>
        /// 按分组获取场景
        /// </summary>
#if UNITY_EDITOR
        public List<SceneQuickAccessData> GetScenesByGroup(string group)
        {
            return CustomScenes.FindAll(s => s.Enabled && s.Group == group && s.SceneAsset != null);
        }
#endif

        /// <summary>
        /// 按分组获取资产
        /// </summary>
        public List<AssetQuickAccessData> GetAssetsByGroup(string group)
        {
            return CustomAssets.FindAll(a => a.Enabled && a.Group == group && a.Asset != null);
        }
    }
}
