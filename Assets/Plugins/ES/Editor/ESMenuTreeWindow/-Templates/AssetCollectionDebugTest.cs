using UnityEngine;
using UnityEditor;
using ES;

namespace ES.Editor
{
    /// <summary>
    /// Library 作者态与统一内容注册入口测试工具。
    /// </summary>
    public static class AssetCollectionDebugTest
    {
        [MenuItem(MenuItemPathDefine.DEBUG_PATH + "内容注册/测试 Book 去重功能", false, 9110)]
        public static void TestBookDuplication()
        {
            Debug.Log("===== Book去重功能测试 =====");
            Debug.Log("请手动测试：");
            Debug.Log("1. 打开任意 ESAssetLibrary 编辑窗口");
            Debug.Log("2. 拖拽一个资产到某个 Book 中");
            Debug.Log("3. 再次拖拽同一个资产");
            Debug.Log("4. 查看 Console 是否输出警告：'资源 [xxx] 已存在于Book [xxx] 中，跳过添加'");
            Debug.Log("===== 测试说明完成 =====");
        }
        
        [MenuItem(MenuItemPathDefine.DEBUG_PATH + "内容注册/测试默认 Book 类别匹配", false, 9120)]
        public static void TestDefaultBookCategoryMatching()
        {
            Debug.Log("===== DefaultBook类别匹配测试 =====");
            
            // 查找所有 ESAssetLibrary 资产
            var libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>();
            if (libraries == null || libraries.Count == 0)
            {
                Debug.LogWarning("未找到任何 ESAssetLibrary 资产");
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
        
        [MenuItem("Assets/【ES】/资源与发布/注册到 Library", true)]
        public static bool ValidateCollectAsset()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }
        
        [MenuItem("Assets/【ES】/资源与发布/注册到 Library")]
        public static void CollectSelectedAsset()
        {
            var assets = Selection.objects;
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("未选中任何资产");
                return;
            }

            ESResourceCollectionWorkflowWindow.OpenForAssetRegistration(
                assets[0],
                ESGlobalResToolsSupportConfig.ActiveCollectLibrary);
            if (assets.Length > 1)
                Debug.LogWarning("[ESRes][Register] 统一事务当前逐项提交，已打开第一个资产；其余资产请逐项预检和提交。");
        }
    }
}
