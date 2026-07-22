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
        [InspectorName("其他")] Other
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
        [InspectorName("地形数据")] TerrainData,
        [InspectorName("其他")] Other
    }

    /// <summary>
    /// Library的资产收集配置
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

        [Title("资产收集配置")]
        [InfoBox("配置各Library对不同资产类型的收集优先级", InfoMessageType.Info)]
        [LabelText("启用自动资产收集")]
        public bool enableAutoCollection = true;

        [ShowIf("enableAutoCollection")]
        [LabelText("收集时自动去重")]
        public bool autoDeduplication = true;

        [ShowIf("enableAutoCollection")]
        [LabelText("收集时显示确认对话框")]
        public bool showConfirmDialog = true;

        [ShowIf("enableAutoCollection")]
        [LabelText("优先使用当前Library")]
        [InfoBox("开启后，ESAssetRefer 等自动收集入口会优先收集到当前Library；当前Library为空时才按优先级规则自动选择。", InfoMessageType.Info)]
        public bool preferActiveLibrary = true;

        [ShowIf("@enableAutoCollection && preferActiveLibrary")]
        [LabelText("当前收集Library")]
        [AssetsOnly]
        public ESAssetLibrary activeCollectLibrary;

#if UNITY_EDITOR
        [ShowIf("@enableAutoCollection && preferActiveLibrary")]
        [Button("使用当前选中的Library", ButtonSizes.Medium)]
        private void UseSelectionAsActiveLibrary()
        {
            if (Selection.activeObject is ESAssetLibrary library)
            {
                SetActiveCollectLibrary(library);
                Debug.Log($"[资产收集] 当前收集Library已设置为: {library.Name}", library);
            }
            else
            {
                Debug.LogWarning("[资产收集] 当前选中的对象不是 ESAssetLibrary，无法设置为当前收集Library。");
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
        /// 判断资产类型
        /// </summary>
#if UNITY_EDITOR
        public static ESAssetCategory DetermineAssetCategory(UnityEngine.Object asset)
        {
            if (asset is GameObject)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                if (path.EndsWith(".prefab"))
                    return ESAssetCategory.Prefab;
                return ESAssetCategory.Other;
            }
            else if (asset is SceneAsset)
                return ESAssetCategory.Scene;
            else if (asset is Sprite)
                return ESAssetCategory.Sprite;
            else if (asset is UnityEngine.U2D.SpriteAtlas)
                return ESAssetCategory.SpriteAtlas;
            else if (asset is Material)
                return ESAssetCategory.Material;
            else if (asset is Texture || asset is Texture2D || asset is RenderTexture)
                return ESAssetCategory.Texture;
            else if (asset is Mesh)
                return ESAssetCategory.Mesh;
            else if (asset is AudioClip)
                return ESAssetCategory.Audio;
            else if (asset is AnimationClip)
                return ESAssetCategory.Animation;
            else if (asset is RuntimeAnimatorController)
                return ESAssetCategory.AnimatorController;
            else if (asset is Avatar)
                return ESAssetCategory.Avatar;
            else if (asset is UnityEngine.Timeline.TimelineAsset)
                return ESAssetCategory.Timeline;
            else if (asset is UnityEngine.Playables.PlayableAsset)
                return ESAssetCategory.Playable;
            else if (asset is TerrainData)
                return ESAssetCategory.TerrainData;
            else if (asset is MonoScript)
                return ESAssetCategory.Other; // .cs脚本文件不支持收集预编译掉
            else if (asset is ScriptableObject)
                return ESAssetCategory.Script; // ScriptableObject归类为SO
            else if (asset is Shader)
                return ESAssetCategory.Shader;
            else if (asset is Font)
                return ESAssetCategory.Font;
            else if (asset is UnityEngine.Video.VideoClip)
                return ESAssetCategory.Video;
            else if (asset is TextAsset)
                return ESAssetCategory.TextAsset;

            return ESAssetCategory.Other;
        }
#endif


#if UNITY_EDITOR
        /// <summary>
        /// 检测是否为子资产
        /// </summary>
        /// <param name="asset">要检测的资产</param>
        /// <param name="mainAsset">输出主资产（如果是子资产）</param>
        /// <returns>true=子资产, false=主资产</returns>
        public static bool IsSubAsset(UnityEngine.Object asset, out UnityEngine.Object mainAsset)
        {
            mainAsset = null;
            
            if (asset == null)
                return false;
            
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                return false;
            
            // 获取该路径下的主资产
            mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            
            // 如果主资产与asset不同，则asset是子资产
            return mainAsset != null && mainAsset != asset;
        }
#endif

        /// <summary>
        /// 高性能查找资产在所有Library中的位置
        /// </summary>
        /// <returns>包含Library和Book的元组，如果未找到则两者都为null</returns>
        private static (ESAssetLibrary library, ESAssetBook book) FindAssetInLibraries(UnityEngine.Object asset, List<ESAssetLibrary> libraries)
        {
#if UNITY_EDITOR
            if (asset == null || libraries == null) 
                return (null, null);

            // 使用缓存加速查找：O(1)而非O(n)
            foreach (var library in libraries)
            {
                if (library != null && library.ContainsAsset(asset))
                {
                    // 找到了，但需要定位具体的Book（用于日志输出）
                    var book = FindBookContainingAsset(library, asset);
                    return (library, book);
                }
            }

            return (null, null);
#else
            return (null, null);
#endif
        }

        /// <summary>
        /// 在指定Library中查找包含资产的Book
        /// </summary>
        private static ESAssetBook FindBookContainingAsset(ESAssetLibrary library, UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            if (library == null || asset == null)
                return null;

            // 使用 GetAllUseableBooks() 统一遍历（高性能，自动过滤空 Book）
            var useableBooks = library.GetAllUseableBooks();
            if (useableBooks != null)
            {
                foreach (var book in useableBooks)
                {
                    if (book != null && ContainsAsset(book, asset))
                    {
                        return book;
                    }
                }
            }

            return null;
#else
            return null;
#endif
        }

        /// <summary>
        /// 高性能检查Book是否包含指定资产
        /// </summary>
        private static bool ContainsAsset(ESAssetBook book, UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            if (book?.pages == null || asset == null)
                return false;

            // 直接遍历pages，使用强类型避免反射
            foreach (var page in book.pages)
            {
                if (page is ESAssetPage resPage && resPage.OB == asset)
                {
                    return true;
                }
            }

            return false;
#else
            return false;
#endif
        }

        /// <summary>
        /// 收集资产到推荐的Library
        /// </summary>
        /// <param name="asset">要收集的资产</param>
        /// <param name="showConfirmDialog">是否显示确认对话框（null则使用全局配置）</param>
        /// <param name="silent">静默模式，不输出日志</param>
        /// <returns>成功收集的Library，失败返回null</returns>
        public static ESAssetLibrary CollectAssetToRecommendedLibrary(UnityEngine.Object asset, bool? showConfirmDialog = null, bool silent = false)
        {
#if UNITY_EDITOR
            if (asset == null)
            {
                if (!silent) Debug.LogError("[资产收集] 资产为null");
                return null;
            }

            // 检测是否为子资产
            if (IsSubAsset(asset, out var mainAsset))
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                
                // 弹窗告知用户
                EditorUtility.DisplayDialog(
                    "⚠️ 禁止收集子资产",
                    $"资产 '{asset.name}' 是子资产，不能直接使用 ESAssetRefer！\n\n"
                    + $"主资产: {mainAsset?.name}\n"
                    + $"路径: {assetPath}\n\n"
                    + "请使用以下方式：\n"
                    + "1. 引用主资产，运行时动态获取子资产\n"
                    + "2. 将子资产导出为独立文件\n\n"
                    + "后续将提供专门工具处理子资产场景。",
                    "确定"
                );
                
                if (!silent)
                {
                    Debug.LogWarning(
                        $"[资产收集] 拒绝收集子资产: {asset.name}\n"
                        + $"主资产: {mainAsset?.name}\n"
                        + $"路径: {assetPath}",
                        asset
                    );
                }
                
                return null;
            }

            var config = Instance;
            
            // 检查是否启用自动收集
            if (!config.enableAutoCollection)
            {
                if (!silent) Debug.LogWarning("[资产收集] 自动资产收集功能已禁用");
                return null;
            }

            // 1. 判断资产类型
            var category = DetermineAssetCategory(asset);
            if (!silent) Debug.Log($"[资产收集] 资产 [{asset.name}] 类型判断为: {category}");

            // 2. 查找所有 ESAssetLibrary
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>();
            if (libraries == null || libraries.Count == 0)
            {
                if (!silent) Debug.LogWarning("[资产收集] 未找到任何 ESAssetLibrary");
                return null;
            }

            // 2.5 高性能去重检查：先检查资产是否已在任何Library中
            if (config.autoDeduplication)
            {
                var existingLocation = FindAssetInLibraries(asset, libraries);
                if (existingLocation.library != null)
                {
                    if (!silent) 
                    {
                        string bookName = existingLocation.book != null ? existingLocation.book.Name : "未知Book";
                        Debug.LogWarning($"[资产收集] 资产 [{asset.name}] 已存在于 Library [{existingLocation.library.Name}] 的 Book [{bookName}] 中，跳过收集");
                    }
                    return existingLocation.library;
                }
            }

            // 3. 查找目标 Library：当前Library优先，否则按优先级规则选择
            var resolveResult = ResolveTargetLibrary(category, libraries, config, silent);
            ESAssetLibrary bestLibrary = resolveResult.library;
            ESAssetCollectionPriority bestPriority = resolveResult.categoryPriority;
            ESAssetCollectionPriority bestOverallPriority = resolveResult.overallPriority;
            bool useActiveLibrary = resolveResult.useActiveLibrary;

            if (bestLibrary == null)
            {
                if (!silent) Debug.LogWarning($"[资产收集] 未找到合适的 Library 收集资产 [{asset.name}]（所有Library都被禁用）");
                return null;
            }

            if (!silent)
            {
                string source = useActiveLibrary ? "当前Library" : "优先级推荐";
                Debug.Log($"[资产收集] 目标 Library: {bestLibrary.Name} ({source}, 类型优先级: {bestPriority}, 总体优先级: {bestOverallPriority})");
            }

            // 4. 查找对应的 DefaultBook
            var targetBook = bestLibrary.GetDefaultBookByCategory(category);
            
            // 如果没有匹配的 DefaultBook，使用 Other 类型的 DefaultBook
            if (targetBook == null)
            {
                if (!silent) Debug.Log($"[资产收集] 未找到类型 [{category}] 的 DefaultBook，尝试使用 Other 类型");
                targetBook = bestLibrary.GetDefaultBookByCategory(ESAssetCategory.Other);
            }

            if (targetBook == null)
            {
                if (!silent) Debug.LogError($"[资产收集] Library [{bestLibrary.Name}] 没有可用的 DefaultBook");
                return null;
            }

            if (!silent) Debug.Log($"[资产收集] 目标 Book: {targetBook.Name}");

            // 5. 显示确认对话框
            bool shouldShowDialog = showConfirmDialog ?? config.showConfirmDialog;
            if (shouldShowDialog)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "确认收集资产",
                    $"将资产 [{asset.name}] 收集到:\n\nLibrary: {bestLibrary.Name}\nBook: {targetBook.Name}\n\n是否继续？",
                    "确认", "取消");
                
                if (!confirmed)
                {
                    if (!silent) Debug.Log("[资产收集] 用户取消操作");
                    return null;
                }
            }

            // 6. 添加资产到 Book（会自动去重）
            Undo.RecordObject(bestLibrary, $"Collect Asset: {asset.name}");
            targetBook.EditorOnly_DragAtArea(new[] { asset });
            EditorUtility.SetDirty(bestLibrary);
            AssetDatabase.SaveAssets();

            if (!silent) Debug.Log($"[资产收集] 成功收集资产 [{asset.name}] 到 Library [{bestLibrary.Name}] 的 Book [{targetBook.Name}]");
            return bestLibrary;
#else
            return null;
#endif
        }

        /// <summary>
        /// 批量收集资产到推荐的Library
        /// </summary>
        /// <param name="assets">要收集的资产数组</param>
        /// <param name="showConfirmDialog">是否显示确认对话框（null则使用全局配置）</param>
        /// <returns>成功收集的数量</returns>
        public static int CollectAssetsToRecommendedLibraries(UnityEngine.Object[] assets, bool? showConfirmDialog = null)
        {
#if UNITY_EDITOR
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[资产收集] 资产数组为空");
                return 0;
            }

            var config = Instance;
            if (!config.enableAutoCollection)
            {
                Debug.LogWarning("[资产收集] 自动资产收集功能已禁用");
                return 0;
            }

            // 获取所有Library
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>();
            if (libraries == null || libraries.Count == 0)
            {
                Debug.LogWarning("[资产收集] 未找到任何 ESAssetLibrary");
                return 0;
            }

            // 批量模式：使用实时检测，无需缓存
            // 每次检测都是实时遍历，确保数据准确性
            
            // 批量收集时显示进度条
            int successCount = 0;
            int failedCount = 0;
            int skippedCount = 0;
            
            try
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    var asset = assets[i];
                    if (asset == null) continue;

                    // 显示进度
                    EditorUtility.DisplayProgressBar(
                        "批量收集资产",
                        $"正在处理: {asset.name} ({i + 1}/{assets.Length})",
                        (float)i / assets.Length);

                    // 批量模式下，只在第一个资产显示确认对话框
                    bool shouldConfirm = (showConfirmDialog ?? config.showConfirmDialog) && (i == 0);
                    
                    // 使用内部方法
                    var result = CollectAssetToRecommendedLibraryInternal(asset, libraries, shouldConfirm, silent: true, rebuildCache: false);
                    if (result.success)
                    {
                        successCount++;
                        Debug.Log($"[批量收集 {i + 1}/{assets.Length}] 成功: {asset.name} → {result.library.Name}");
                    }
                    else if (result.skipped)
                    {
                        skippedCount++;
                        Debug.Log($"[批量收集 {i + 1}/{assets.Length}] 跳过（已存在）: {asset.name}");
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning($"[批量收集 {i + 1}/{assets.Length}] 失败: {asset.name}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[批量收集完成] 成功: {successCount}, 跳过: {skippedCount}, 失败: {failedCount}, 总计: {assets.Length}");
            
            // 显示结果对话框
            EditorUtility.DisplayDialog(
                "批量收集完成",
                $"成功收集: {successCount} 个资产\n已存在跳过: {skippedCount} 个资产\n失败: {failedCount} 个资产\n总计: {assets.Length} 个资产",
                "确定");

            return successCount;
#else
            return 0;
#endif
        }

        /// <summary>
        /// 内部收集方法，支持控制是否重建缓存
        /// </summary>
        private static (bool success, bool skipped, ESAssetLibrary library) CollectAssetToRecommendedLibraryInternal(
            UnityEngine.Object asset, 
            List<ESAssetLibrary> libraries, 
            bool? showConfirmDialog, 
            bool silent, 
            bool rebuildCache)
        {
#if UNITY_EDITOR
            if (asset == null)
            {
                if (!silent) Debug.LogError("[资产收集] 资产为null");
                return (false, false, null);
            }

            var config = Instance;

            // 1. 判断资产类型
            var category = DetermineAssetCategory(asset);
            if (!silent) Debug.Log($"[资产收集] 资产 [{asset.name}] 类型判断为: {category}");

            // 2. 去重检查（使用已有缓存，不重建）
            if (config.autoDeduplication)
            {
                var existingLocation = FindAssetInLibrariesWithoutRebuild(asset, libraries);
                if (existingLocation.library != null)
                {
                    if (!silent)
                    {
                        string bookName = existingLocation.book != null ? existingLocation.book.Name : "未知Book";
                        Debug.LogWarning($"[资产收集] 资产 [{asset.name}] 已存在于 Library [{existingLocation.library.Name}] 的 Book [{bookName}] 中，跳过收集");
                    }
                    return (false, true, existingLocation.library);
                }
            }

            // 3. 查找目标Library：当前Library优先，否则按优先级规则选择
            var resolveResult = ResolveTargetLibrary(category, libraries, config, silent);
            ESAssetLibrary bestLibrary = resolveResult.library;

            if (bestLibrary == null)
            {
                if (!silent) Debug.LogWarning($"[资产收集] 未找到合适的 Library 收集资产 [{asset.name}]（所有Library都被禁用）");
                return (false, false, null);
            }

            // 4. 查找DefaultBook
            var targetBook = bestLibrary.GetDefaultBookByCategory(category);
            if (targetBook == null)
            {
                targetBook = bestLibrary.GetDefaultBookByCategory(ESAssetCategory.Other);
            }

            if (targetBook == null)
            {
                if (!silent) Debug.LogError($"[资产收集] Library [{bestLibrary.Name}] 没有可用的 DefaultBook");
                return (false, false, null);
            }

            // 5. 确认对话框
            bool shouldShowDialog = showConfirmDialog ?? config.showConfirmDialog;
            if (shouldShowDialog)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "确认收集资产",
                    $"将资产 [{asset.name}] 收集到:\n\nLibrary: {bestLibrary.Name}\nBook: {targetBook.Name}\n\n是否继续？",
                    "确认", "取消");
                
                if (!confirmed)
                {
                    if (!silent) Debug.Log("[资产收集] 用户取消操作");
                    return (false, false, null);
                }
            }

            // 6. 添加资产
            Undo.RecordObject(bestLibrary, $"Collect Asset: {asset.name}");
            targetBook.EditorOnly_DragAtArea(new[] { asset });
            EditorUtility.SetDirty(bestLibrary);
            AssetDatabase.SaveAssets();

            if (!silent) Debug.Log($"[资产收集] 成功收集资产 [{asset.name}] 到 Library [{bestLibrary.Name}] 的 Book [{targetBook.Name}]");
            return (true, false, bestLibrary);
#else
            return (false, false, null);
#endif
        }

        private static (ESAssetLibrary library, ESAssetCollectionPriority categoryPriority, ESAssetCollectionPriority overallPriority, bool useActiveLibrary)
            ResolveTargetLibrary(ESAssetCategory category, List<ESAssetLibrary> libraries, ESGlobalResToolsSupportConfig config, bool silent)
        {
#if UNITY_EDITOR
            if (config != null && config.enableAutoCollection && config.preferActiveLibrary && config.activeCollectLibrary != null)
            {
                var active = config.activeCollectLibrary;
                if (libraries != null && libraries.Contains(active))
                {
                    return (active, active.collectionConfig.GetPriority(category), active.collectionConfig.overallPriority, true);
                }

                if (!silent)
                {
                    Debug.LogWarning($"[资产收集] 当前收集Library [{active.Name}] 不在已扫描的 ESAssetLibrary 列表中，将退回优先级选择。", active);
                }
            }

            ESAssetLibrary bestLibrary = null;
            ESAssetCollectionPriority bestPriority = ESAssetCollectionPriority.Disabled;
            ESAssetCollectionPriority bestOverallPriority = ESAssetCollectionPriority.Disabled;

            foreach (var lib in libraries)
            {
                if (lib == null)
                {
                    continue;
                }

                var priority = lib.collectionConfig.GetPriority(category);
                var overallPriority = lib.collectionConfig.overallPriority;

                if (priority == ESAssetCollectionPriority.Disabled && overallPriority == ESAssetCollectionPriority.Disabled)
                {
                    continue;
                }

                if (bestLibrary == null || priority > bestPriority || (priority == bestPriority && overallPriority > bestOverallPriority))
                {
                    bestLibrary = lib;
                    bestPriority = priority;
                    bestOverallPriority = overallPriority;
                }
            }

            return (bestLibrary, bestPriority, bestOverallPriority, false);
#else
            return (null, ESAssetCollectionPriority.Disabled, ESAssetCollectionPriority.Disabled, false);
#endif
        }

        /// <summary>
        /// 实时查找资产（用于批量操作，无缓存依赖）
        /// </summary>
        private static (ESAssetLibrary library, ESAssetBook book) FindAssetInLibrariesWithoutRebuild(UnityEngine.Object asset, List<ESAssetLibrary> libraries)
        {
#if UNITY_EDITOR
            if (asset == null || libraries == null) 
                return (null, null);

            // 实时遍历所有Library，不依赖缓存
            foreach (var library in libraries)
            {
                if (library != null && library.ContainsAsset(asset))
                {
                    var book = FindBookContainingAsset(library, asset);
                    return (library, book);
                }
            }

            return (null, null);
#else
            return (null, null);
#endif
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
