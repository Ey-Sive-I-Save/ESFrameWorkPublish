using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// 编辑器预览元素接口。
    /// 目标对象（如 <see cref="BasePreviewEditor{T}"/> 的泛型 T）内部的模块、子对象或领域数据，
    /// 可统一实现此接口，以便在自定义的 Inspector 预览面板中自动集成并渲染内容。
    /// </summary>
    /// <remarks>
    /// 该接口设计上与业务逻辑解耦，支持标注“独占预览区域”的元素，并区分运行态与非运行态的 UI 绘制。
    /// </remarks>
    public interface IPreviewElement
    {
        /// <summary>
        /// 获取一个值，指示当前元素是否允许在预览面板中显示。
        /// </summary>
        /// <remarks>
        /// 接口实现者可在此处控制预览的开关，例如当元素未初始化或无数据时，返回 <see langword="false"/> 以隐藏自身。
        /// </remarks>
        bool CanPreview { get; }

        /// <summary>
        /// 获取一个值，指示当前元素是否需要独占整个预览面板区域进行渲染。
        /// </summary>
        /// <remarks>
        /// 返回 <see langword="true" /> 时，预览面板会将其放入“独占区块”中，并在视觉上与其他常规元素隔离开来。
        /// 通常适用于需要绘制图表、大段文本或复杂交互界面的元素。
        /// </remarks>
        bool IsSingleArea { get; }

        /// <summary>
        /// 在 Unity 编辑器处于“播放模式 (Play Mode)”时，绘制当前元素的预览 GUI。
        /// </summary>
        /// <remarks>
        /// 用于显示实时数据、游戏运行状态或运行时调试信息的界面。
        /// 此方法将在编辑器每帧刷新时被调用，请避免在其中执行过重的运算或资源加载。
        /// </remarks>
        void DrawPreviewGUIPlaying();

        /// <summary>
        /// 在 Unity 编辑器处于“非播放模式 (Edit Mode)”时，绘制当前元素的预览 GUI。
        /// </summary>
        /// <remarks>
        /// 用于在编辑状态下展示配置信息、静态预览、或结构总览。
        /// 未实现运行时逻辑的元素，或仅需静态展示的元素，可在此处进行自定义绘制。
        /// 若无实际绘制内容，此方法可留空实现。
        /// </remarks>
        void EditorPreviewDrawPreviewGUINonPlay();
    }

    /// <summary>
    /// 编辑器预览元素收集器。
    /// 用于 Core 这类自身不直接绘制、但持有多个可预览对象的聚合类型。
    /// </summary>
    public interface IPreviewCollector
    {
        void CollectPreviewElements(
            List<IPreviewElement> normalProviders,
            List<IPreviewElement> singleProviders);
    }
}
