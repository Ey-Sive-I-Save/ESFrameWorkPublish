using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 资产收集优先级枚举
    /// </summary>
    public enum ESAssetCollectionPriority
    {
        [InspectorName("禁用")] Disabled = 0,
        [InspectorName("最低")] Lowest = 1,
        [InspectorName("低")] Low = 2,
        [InspectorName("中")] Medium = 3,
        [InspectorName("高")] High = 4,
        [InspectorName("最高")] Highest = 5
    }

    /// <summary>
    /// 资产类型分类
    /// </summary>
    public enum ESAssetCategory
    {
        [InspectorName("全部资产")] All,
        [InspectorName("预制体")] Prefab,
        [InspectorName("场景")] Scene,
        [InspectorName("精灵")] Sprite,
        [InspectorName("图集")] SpriteAtlas,
        [InspectorName("材质")] Material,
        [InspectorName("贴图")] Texture,
        [InspectorName("模型")] Model,
        [InspectorName("网格")] Mesh,
        [InspectorName("音频")] Audio,
        [InspectorName("动画")] Animation,
        [InspectorName("动画控制器")] AnimatorController,
        [InspectorName("角色骨架Avatar")] Avatar,
        [InspectorName("Timeline")] Timeline,
        [InspectorName("Playable")] Playable,
        [InspectorName("地形数据")] TerrainData,
        [InspectorName("脚本化对象(SO)")] Script,
        [InspectorName("着色器")] Shader,
        [InspectorName("字体")] Font,
        [InspectorName("视频")] Video,
        [InspectorName("文本")] TextAsset,
        [InspectorName("其他")] Other,
        [InspectorName("Raw 二进制")] Raw
    }

    /// <summary>
    /// ESAssetRefer 资产引用类型。
    /// 用于 ESAssetPage / ConfigKey / 构建索引之间保持同一套类型语义。
    /// </summary>
    public enum ESAssetReferKind
    {
        [InspectorName("未指定")] None = 0,
        [InspectorName("预制体")] Prefab,
        [InspectorName("场景")] Scene,
        [InspectorName("精灵")] Sprite,
        [InspectorName("图集")] SpriteAtlas,
        [InspectorName("贴图2D")] Texture2D,
        [InspectorName("通用贴图")] Texture,
        [InspectorName("材质")] Material,
        [InspectorName("网格")] Mesh,
        [InspectorName("动画片段")] AnimationClip,
        [InspectorName("动画控制器")] AnimatorController,
        [InspectorName("角色骨架Avatar")] Avatar,
        [InspectorName("音频")] AudioClip,
        [InspectorName("视频")] VideoClip,
        [InspectorName("Timeline")] TimelineAsset,
        [InspectorName("Playable")] PlayableAsset,
        [InspectorName("ScriptableObject")] ScriptableObject,
        [InspectorName("地形数据")] TerrainData,
        [InspectorName("其他")] Other,
        [InspectorName("Raw 二进制")] Raw
    }

#if UNITY_EDITOR
    /// <summary>
    /// Library 作者态分类工具。它只负责把资产映射到默认 Book 类别，不负责选择 Library 或写入内容。
    /// </summary>
    public static class ESAssetCategoryUtility
    {
        public static ESAssetCategory Determine(UnityEngine.Object asset)
        {
            if (asset is Shader)
                return ESAssetCategory.Shader;
            if (asset is Font)
                return ESAssetCategory.Font;

            switch (ESAssetPage.DetermineKind(asset))
            {
                case ESAssetReferKind.Prefab: return ESAssetCategory.Prefab;
                case ESAssetReferKind.Scene: return ESAssetCategory.Scene;
                case ESAssetReferKind.Sprite: return ESAssetCategory.Sprite;
                case ESAssetReferKind.SpriteAtlas: return ESAssetCategory.SpriteAtlas;
                case ESAssetReferKind.Texture2D:
                case ESAssetReferKind.Texture: return ESAssetCategory.Texture;
                case ESAssetReferKind.Material: return ESAssetCategory.Material;
                case ESAssetReferKind.Mesh: return ESAssetCategory.Mesh;
                case ESAssetReferKind.AnimationClip: return ESAssetCategory.Animation;
                case ESAssetReferKind.AnimatorController: return ESAssetCategory.AnimatorController;
                case ESAssetReferKind.Avatar: return ESAssetCategory.Avatar;
                case ESAssetReferKind.AudioClip: return ESAssetCategory.Audio;
                case ESAssetReferKind.VideoClip: return ESAssetCategory.Video;
                case ESAssetReferKind.TimelineAsset: return ESAssetCategory.Timeline;
                case ESAssetReferKind.PlayableAsset: return ESAssetCategory.Playable;
                case ESAssetReferKind.ScriptableObject: return ESAssetCategory.Script;
                case ESAssetReferKind.TerrainData: return ESAssetCategory.TerrainData;
                case ESAssetReferKind.Raw: return ESAssetCategory.Raw;
                default: return ESAssetCategory.Other;
            }
        }
    }
#endif

    /// <summary>
    /// 旧自动收集优先级的序列化兼容数据。当前内容注册不读取该配置。
    /// </summary>
    [Serializable]
    public class LibraryCollectionConfig
    {
        [LabelText("总体优先级")]
        [EnumToggleButtons]
        public ESAssetCollectionPriority overallPriority = ESAssetCollectionPriority.Lowest;
        
        [LabelText("分类优先级")]
        [DictionaryDrawerSettings(KeyLabel = "资产类型", ValueLabel = "优先级")]
        public Dictionary<ESAssetCategory, ESAssetCollectionPriority> categoryPriorities = new Dictionary<ESAssetCategory, ESAssetCollectionPriority>();

        public LibraryCollectionConfig()
        {
            // 初始化所有分类为最低优先级
            foreach (ESAssetCategory category in Enum.GetValues(typeof(ESAssetCategory)))
            {
                if (category != ESAssetCategory.All)
                {
                    categoryPriorities[category] = ESAssetCollectionPriority.Lowest;
                }
            }
        }

        /// <summary>
        /// 获取指定类型的优先级
        /// </summary>
        public ESAssetCollectionPriority GetPriority(ESAssetCategory category)
        {
            if (category == ESAssetCategory.All)
            {
                return overallPriority;
            }
            
            if (categoryPriorities.TryGetValue(category, out var priority))
            {
                return priority;
            }
            
            return ESAssetCollectionPriority.Lowest;
        }

        /// <summary>
        /// 设置指定类型的优先级
        /// </summary>
        public void SetPriority(ESAssetCategory category, ESAssetCollectionPriority priority)
        {
            if (category == ESAssetCategory.All)
            {
                overallPriority = priority;
            }
            else
            {
                categoryPriorities[category] = priority;
            }
        }
    }

    [HideMonoScript]
    [CreateAssetMenu(fileName = "资产工具支持配置", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "资产工具支持配置")]
    [ESOnlyEditorSO("资产工具支持配置只服务编辑器资产收集辅助，不应进入运行时构建或AB资源包。")]
    public class ESGlobalResToolsSupportConfig : ESEditorGlobalSo<ESGlobalResToolsSupportConfig>
    {
        [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string createText = "--资产工具支持配置--";

        [HideInInspector]
        [Obsolete("旧自动收集开关仅保留序列化兼容；内容注册必须走 ESContentRegistrationAuthoring.Execute。")]
        public bool enableAutoCollection = true;

        [HideInInspector]
        [Obsolete("旧自动去重配置仅保留序列化兼容；统一内容注册事务负责冲突预检。")]
        public bool autoDeduplication = true;

        [HideInInspector]
        [Obsolete("旧确认框配置仅保留序列化兼容；统一内容注册使用预检和显式提交。")]
        public bool showConfirmDialog = true;

        [HideInInspector]
        [Obsolete("旧自动选择策略仅保留序列化兼容；activeCollectLibrary 是当前唯一默认目标。")]
        public bool preferActiveLibrary = true;

        [Title("统一内容注册")]
        [InfoBox("这里只指定统一内容注册窗口的默认目标 Library。注册仍必须先预检，再通过 ESContentRegistrationAuthoring.Execute 显式提交。", InfoMessageType.Info)]
        [LabelText("默认目标 Library")]
        [AssetsOnly]
        public ESAssetLibrary activeCollectLibrary;

#if UNITY_EDITOR
        [Button("使用当前选中的 Library", ButtonSizes.Medium)]
        private void UseSelectionAsActiveLibrary()
        {
            if (Selection.activeObject is ESAssetLibrary library)
            {
                SetActiveCollectLibrary(library);
                Debug.Log($"[内容注册] 默认目标 Library 已设置为: {library.Name}", library);
            }
            else
            {
                Debug.LogWarning("[内容注册] 当前选中的对象不是 ESAssetLibrary，无法设置为默认目标。");
            }
        }
#endif

        public static ESAssetLibrary ActiveCollectLibrary => Instance != null ? Instance.activeCollectLibrary : null;

        public static void SetActiveCollectLibrary(ESAssetLibrary library)
        {
#if UNITY_EDITOR
            var config = Instance;
            Undo.RecordObject(config, "Set Active Collect Library");
            config.activeCollectLibrary = library;
            EditorUtility.SetDirty(config);
#endif
        }

        /// <summary>
        /// 已禁用的旧资产收集签名。统一内容注册事务是唯一写入入口。
        /// </summary>
        /// <param name="asset">要收集的资产</param>
        /// <param name="showConfirmDialog">是否显示确认对话框（null则使用全局配置）</param>
        /// <param name="silent">静默模式，不输出日志</param>
        /// <returns>不会返回；调用始终抛出 <see cref="NotSupportedException"/>。</returns>
        [Obsolete("旧收集 API 已禁用。人工请打开统一内容注册窗口；代码与 MCP 请调用 ESContentRegistrationAuthoring.Execute。", true)]
        public static ESAssetLibrary CollectAssetToRecommendedLibrary(UnityEngine.Object asset, bool? showConfirmDialog = null, bool silent = false)
        {
            throw new NotSupportedException("旧收集 API 已禁用，请使用统一内容注册事务。");
        }

        /// <summary>
        /// 已禁用的旧批量资产收集签名。统一内容注册事务是唯一写入入口。
        /// </summary>
        /// <param name="assets">要收集的资产数组</param>
        /// <param name="showConfirmDialog">是否显示确认对话框（null则使用全局配置）</param>
        /// <returns>不会返回；调用始终抛出 <see cref="NotSupportedException"/>。</returns>
        [Obsolete("旧批量收集 API 已禁用。每个资产必须通过统一内容注册事务获得独立预检、revision 与 requestId。", true)]
        public static int CollectAssetsToRecommendedLibraries(UnityEngine.Object[] assets, bool? showConfirmDialog = null)
        {
            throw new NotSupportedException("旧批量收集 API 已禁用，请逐项使用统一内容注册事务。");
        }

        public override void OnEditorInitialized()
        {
#if UNITY_EDITOR
            base.OnEditorInitialized();
            this.SHOW_Global = () => { return Selection.activeObject == this; };
#endif
        }


    }
}
