using UnityEngine;
using UnityEditor;
using ES;

namespace ES.Editor
{
    /// <summary>
    /// 资产收集功能测试工具
    /// </summary>
    public static class AssetCollectionDebugTest
    {
        [MenuItem("ES/Debug/测试资产收集配置")]
        public static void TestAssetCollectionConfig()
        {
            Debug.Log("===== 资产收集配置测试 =====");
            
            // 查找第一个ResLibrary进行测试
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            if (libraries == null || libraries.Count == 0)
            {
                Debug.LogWarning("未找到任何ResLibrary资产");
                return;
            }
            
            var testLibrary = libraries[0];
            
            if (testLibrary == null)
            {
                Debug.LogError("无法加载ResLibrary");
                return;
            }
            
            Debug.Log($"测试Library: {testLibrary.Name}");
            
            // 测试配置访问
            Debug.Log("\n--- 测试1: 访问配置 ---");
            var config = testLibrary.collectionConfig;
            Debug.Log($"总体优先级: {config.overallPriority}");
            
            // 测试设置优先级
            Debug.Log("\n--- 测试2: 设置优先级 ---");
            config.SetPriority(ESAssetCategory.Texture, ESAssetCollectionPriority.Highest);
            Debug.Log($"纹理优先级已设为: {config.GetPriority(ESAssetCategory.Texture)}");
            
            config.SetPriority(ESAssetCategory.Audio, ESAssetCollectionPriority.Medium);
            Debug.Log($"音频优先级已设为: {config.GetPriority(ESAssetCategory.Audio)}");
            
            // 测试资产类型判断
            Debug.Log("\n--- 测试3: 资产类型判断 ---");
            var testTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Editor Default Resources/Gaskellgames/FolderSystem/Editor/Icons/Folder_01.png");
            if (testTexture != null)
            {
                var category = ESGlobalResToolsSupportConfig.DetermineAssetCategory(testTexture);
                Debug.Log($"测试纹理判断结果: {category}");
            }
            
            EditorUtility.SetDirty(testLibrary);
            Debug.Log("\n===== 测试完成 =====");
        }
        
        
        [MenuItem("ES/Debug/测试Book去重功能")]
        public static void TestBookDuplication()
        {
            Debug.Log("===== Book去重功能测试 =====");
            Debug.Log("请手动测试：");
            Debug.Log("1. 打开任意 ResLibrary 编辑窗口");
            Debug.Log("2. 拖拽一个资产到某个 Book 中");
            Debug.Log("3. 再次拖拽同一个资产");
            Debug.Log("4. 查看 Console 是否输出警告：'资源 [xxx] 已存在于Book [xxx] 中，跳过添加'");
            Debug.Log("===== 测试说明完成 =====");
        }
        
        [MenuItem("ES/Debug/测试DefaultBook类别匹配")]
        public static void TestDefaultBookCategoryMatching()
        {
            Debug.Log("===== DefaultBook类别匹配测试 =====");
            
            // 查找所有 ResLibrary 资产
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            if (libraries == null || libraries.Count == 0)
            {
                Debug.LogWarning("未找到任何 ResLibrary 资产");
                return;
            }
            
            foreach (var library in libraries)
            {
                Debug.Log($"\n【Library: {library.Name}】");
                
                // 测试所有资产类别
                var categories = System.Enum.GetValues(typeof(ESAssetCategory));
                foreach (ESAssetCategory category in categories)
                {
                    if (category == ESAssetCategory.All)
                        continue;
                    
                    var book = library.GetDefaultBookByCategory(category);
                    if (book != null)
                    {
                        Debug.Log($"  {category} → {book.Name}");
                    }
                }
            }
            
            Debug.Log("\n===== 测试完成 =====");
        }
        
        [MenuItem("Assets/收集到推荐Library", true)]
        public static bool ValidateCollectAsset()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }
        
        [MenuItem("Assets/收集到推荐Library")]
        public static void CollectSelectedAsset()
        {
            var assets = Selection.objects;
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("未选中任何资产");
                return;
            }

            // 单个资产直接收集
            if (assets.Length == 1)
            {
                var library = ESGlobalResToolsSupportConfig.CollectAssetToRecommendedLibrary(assets[0]);
                if (library != null)
                {
                    EditorUtility.DisplayDialog("收集成功", 
                        $"已将资产 [{assets[0].name}] 收集到 Library [{library.Name}]", 
                        "确定");
                }
            }
            else
            {
                // 多个资产批量收集
                ESGlobalResToolsSupportConfig.CollectAssetsToRecommendedLibraries(assets);
            }
        }
    }
}
