namespace ES
{
    /// <summary>
    /// 受控的灯光目标适配器；由场景/URP 宿主注入，不由 ES 创建 MonoBehaviour。
    /// 实现必须只操作其声明拥有的灯光资源，并能从真实后端重新捕获状态。
    /// </summary>
    public interface IESRenderLightingTarget
    {
        bool TryApply(ESRenderLightingRecipe target, out string reason);
        bool TryCapture(out ESRenderLightingRecipe current, out string reason);
    }
}
