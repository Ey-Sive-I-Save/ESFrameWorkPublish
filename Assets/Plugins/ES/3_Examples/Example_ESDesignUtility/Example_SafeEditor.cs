using UnityEngine;
using ES;

namespace ES.Examples
{
    /// <summary>
    /// SafeEditor API 演示 - 编辑器功能封装工具
    /// 提供在运行时也可安全调用的编辑器功能
    /// 注意：大部分功能仅在Unity Editor下有效，运行时会安全返回默认值
    /// </summary>
    public class Example_SafeEditor : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== SafeEditor API 演示 ===");
            Debug.Log("注意：大部分功能仅在Editor模式下有效");

            // ========== 获取特殊数据 ==========
            Debug.Log("--- 获取特殊数据 ---");

            // 1. 获取所有标签
            string[] tags = ESDesignUtility.SafeEditor.GetAllTags();
            Debug.Log($"系统标签数: {tags.Length}");
            if (tags.Length > 0)
            {
                Debug.Log($"第一个标签: {tags[0]}");
            }

            // 2. 获取所有层级
            var layers = ESDesignUtility.SafeEditor.GetAllLayers();
            Debug.Log($"系统层级数: {layers.Count}");
            foreach (var layer in layers)
            {
                Debug.Log($"  Layer {layer.Key}: {layer.Value}");
            }

            // 3. 添加标签（仅Editor模式）
            ESDesignUtility.SafeEditor.AddTag("CustomTag");
            Debug.Log("尝试添加自定义标签（仅Editor有效）");

            // ========== 对话框封装 ==========
            Debug.Log("--- 对话框封装 ---");

            // 4. 显示对话框（仅Editor模式）
            bool dialogResult = ESDesignUtility.SafeEditor.Wrap_DisplayDialog(
                title: "提示",
                message: "这是一个测试对话框",
                ok: "确定",
                cancel: "取消"
            );
            Debug.Log($"对话框结果: {dialogResult}（运行时始终返回true）");

            // ========== 文件夹选择 ==========
            Debug.Log("--- 文件夹选择 ---");

            // 5. 打开文件夹选择器（仅Editor模式）
            string selectedPath = ESDesignUtility.SafeEditor.Wrap_OpenSelectorFolderPanel(
                targetPath: "Assets",
                title: "选择文件夹"
            );
            Debug.Log($"选择的路径: {selectedPath}");

            // 6. 验证文件夹是否有效（仅Editor模式）
            bool isValid = ESDesignUtility.SafeEditor.Wrap_IsValidFolder("Assets", IfPlayerRuntime: false);
            Debug.Log($"'Assets'文件夹有效: {isValid}");

            // ========== SetDirty 操作 ==========
            Debug.Log("--- SetDirty 操作 ---");

            // 7. 标记对象为脏（仅Editor模式）
            ESDesignUtility.SafeEditor.Wrap_SetDirty(this.gameObject, saveAndRefresh: false);
            Debug.Log("GameObject已标记为dirty（仅Editor有效）");

            // 8. 商业级语义版本（显式控制SaveAssets/Refresh）
            ESDesignUtility.SafeEditor.Wrap_SetDirty(
                which: this.gameObject,
                saveAssets: false,
                refresh: false
            );
            Debug.Log("使用商业级SetDirty（仅Editor有效）");

            // ========== 资产查询示例 ==========
            Debug.Log("--- 资产查询示例 ---");

            // 注意：以下功能主要用于Editor脚本，运行时会返回空

#if UNITY_EDITOR
            // 9. 查找所有ScriptableObject资产
            // var allSOs = ESDesignUtility.SafeEditor.FindAllSOAssets<ScriptableObject>();
            // Debug.Log($"找到 {allSOs.Count} 个SO资产");

            // 10. 查找资产路径
            // string assetPath = ESDesignUtility.SafeEditor.GetAssetPath(someObject);
            // Debug.Log($"资产路径: {assetPath}");
#endif

            // ========== 快捷功能示例 ==========
            Debug.Log("--- 快捷功能示例 ---");

            // 11. 打开文件夹（仅Editor模式）
            string assetsPath = Application.dataPath;
            ESDesignUtility.SafeEditor.Quick_OpenInSystemFolder(assetsPath);
            Debug.Log($"尝试打开系统文件夹: {assetsPath}（仅Editor有效）");

            // 12. 创建文件夹（仅Editor模式）
            string newFolderPath = "Assets/TestFolder";
            ESDesignUtility.SafeEditor.Quick_CreateFolderByFullPath(newFolderPath, refresh: false);
            Debug.Log($"尝试创建文件夹: {newFolderPath}（仅Editor有效）");

            // ========== 实用提示 ==========
            Debug.Log("\n=== 使用提示 ===");
            Debug.Log("• SafeEditor的主要优势是可以在任何地方调用，不需要#if UNITY_EDITOR包裹");
            Debug.Log("• 运行时调用会安全返回，不会报错");
            Debug.Log("• 大部分功能主要用于Editor工具脚本");
            Debug.Log("• 资产创建、查找等功能仅在Editor模式下有效");
        }
    }
}
