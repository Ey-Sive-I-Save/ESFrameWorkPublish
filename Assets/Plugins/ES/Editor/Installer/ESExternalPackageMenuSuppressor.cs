using UnityEditor;

namespace ES.EditorInternal
{
    /// <summary>
    /// 第三方工具仍由 ES 的受管发布流程调用，不在 Unity 顶栏提供旁路入口。
    /// 每次 AssemblyStream 后延迟再执行一次，覆盖 Unity 重建菜单的时序。
    /// </summary>
    internal sealed class ESExternalPackageMenuSuppressor : ES.EditorInvoker_Level2
    {
        private static readonly string[] HiddenMenuPaths =
        {
            "HybridCLR/About",
            "HybridCLR/Installer...",
            "HybridCLR/Settings...",
            "HybridCLR/Documents/Quick Start",
            "HybridCLR/Documents/Performance",
            "HybridCLR/Documents/FAQ",
            "HybridCLR/Documents/Common Errors",
            "HybridCLR/Documents/Bug Report",
            "HybridCLR/Generate/AOTDlls",
            "HybridCLR/Generate/All",
            "HybridCLR/Generate/MethodBridgeAndReversePInvokeWrapper",
            "HybridCLR/Generate/LinkXml",
            "HybridCLR/Generate/Il2CppDef",
            "HybridCLR/Generate/AOTGenericReference",
            "HybridCLR/CompileDll/ActiveBuildTarget",
            "HybridCLR/CompileDll/ActiveBuildTarget_Release",
            "HybridCLR/CompileDll/ActiveBuildTarget_Development",
            "HybridCLR/CompileDll/Win32",
            "HybridCLR/CompileDll/Win64",
            "HybridCLR/CompileDll/MacOS",
            "HybridCLR/CompileDll/Linux",
            "HybridCLR/CompileDll/Android",
            "HybridCLR/CompileDll/IOS",
            "HybridCLR/CompileDll/WebGL",
            "Luban/About",
            "Luban/Quick Start"
        };

        public override void InitInvoke()
        {
            RemoveKnownExternalMenus();
            EditorApplication.delayCall -= RemoveKnownExternalMenus;
            EditorApplication.delayCall += RemoveKnownExternalMenus;
        }

        internal static void RemoveKnownExternalMenus()
        {
            for (int i = 0; i < HiddenMenuPaths.Length; i++)
                Menu.RemoveMenuItem(HiddenMenuPaths[i]);
        }
    }
}
