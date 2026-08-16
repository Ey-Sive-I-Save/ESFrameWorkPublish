using UnityEditor;

namespace ES.EditorInternal
{
    /// <summary>
    /// 第三方工具仍由 ES 的受管发布流程调用，不在 Unity 顶栏提供旁路入口。
    /// 第三方包菜单特性由本地 PackageCache 补丁直接注释；此类保留为空兼容入口。
    /// </summary>
    internal sealed class ESExternalPackageMenuSuppressor : ES.EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            // Third-party menu attributes are disabled in the package-cache sources.
        }
    }
}
