using UnityEngine;
using UnityEditor;

namespace ES
{
    /// <summary>
    /// ESMenuWindow OnPageDisable功能测试和调试工具
    /// </summary>
    public static class ESMenuWindowDebugTest
    {
        /// <summary>
        /// 测试延迟保存功能 - 在Console中查看Debug日志
        /// </summary>
        [MenuItem("ES/Debug/测试窗口关闭保存机制", priority = 9999)]
        public static void TestOnPageDisableFeature()
        {
            Debug.Log("=================================================");
            Debug.Log("[测试] 开始测试ESMenuWindow OnPageDisable功能");
            Debug.Log("=================================================");
            Debug.Log("[测试] 测试步骤:");
            Debug.Log("  1. 打开任意包含Library的ESMenuWindow窗口");
            Debug.Log("  2. 修改Library的名称或描述（触发延迟保存）");
            Debug.Log("  3. 直接关闭窗口（不要手动保存）");
            Debug.Log("  4. 在Console中查看以下关键日志：");
            Debug.Log("     - [ESMenuTreeWindow] 注册页面: Page_Index_Library - 库名");
            Debug.Log("     - [ESMenuTreeWindow] 窗口销毁，开始调用 N 个页面的OnPageDisable");
            Debug.Log("     - [Page_Index_Library] OnPageDisable调用 - Library: XXX, pendingSave: True");
            Debug.Log("     - [Page_Index_Library] 检测到未保存的修改，执行立即保存");
            Debug.Log("     - [Page_Index_Library] 保存完成");
            Debug.Log("     - [ESMenuTreeWindow] OnPageDisable调用完成");
            Debug.Log("  5. 重新打开窗口，验证修改是否已保存");
            Debug.Log("=================================================");
            Debug.Log("[重要] 修复了动态创建页面未注册的问题：");
            Debug.Log("  • 使用 RegisterAndAddPage() 替代 tree.Add()");
            Debug.Log("  • 所有动态创建的Library/Consumer页面现在都会被注册");
            Debug.Log("  • OnPageDisable 会对所有页面生效");
            Debug.Log("=================================================");
            Debug.Log("[测试] 准备就绪，请按上述步骤进行测试");
            Debug.Log("=================================================");
        }
        
        /// <summary>
        /// 清理Console日志
        /// </summary>
        [MenuItem("ES/Debug/清理Console日志", priority = 10000)]
        public static void ClearConsole()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
            Debug.Log("[调试] Console已清理，准备查看新日志");
        }
        
        /// <summary>
        /// 显示当前已注册的页面数量（需要修改ESMenuTreeWindowAB使registeredPages可访问）
        /// </summary>
        [MenuItem("ES/Debug/显示调试信息", priority = 10001)]
        public static void ShowDebugInfo()
        {
            Debug.Log("=================================================");
            Debug.Log("[调试信息]");
            Debug.Log($"  Unity版本: {Application.unityVersion}");
            Debug.Log($"  编辑器运行中: {Application.isEditor}");
            Debug.Log($"  当前时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Debug.Log("=================================================");
            Debug.Log("[提示] 延迟保存机制已启用");
            Debug.Log("  • 修改操作会标记为pendingSave=true");
            Debug.Log("  • 窗口关闭时自动调用SaveAssetsImmediate()");
            Debug.Log("  • 关键操作（删除/拖拽）仍立即保存");
            Debug.Log("=================================================");
        }
    }
}
