namespace ES
{
    /// <summary>
    /// 核心预览接口：Domain / Module 实现此接口后即可在 CorePreviewEditor 中自动绘制。
    /// </summary>
    public interface ICorePreviewProvider
    {
        /// <summary>是否允许在预览中显示</summary>
        bool EditorPreviewCanPreview { get; }

        /// <summary>是否独占整个预览区域</summary>
        bool EditorPreviewIsSingleArea { get; }

        /// <summary>播放模式下的 GUI 绘制</summary>
        void EditorPreviewDrawPreviewGUI();

        /// <summary>非播放模式下的 GUI 绘制（可为空实现）</summary>
        void EditorPreviewDrawPreviewGUINonPlay();
    }
}