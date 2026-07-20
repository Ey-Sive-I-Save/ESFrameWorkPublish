namespace ES
{
    #region ESEditorDraw 设计约束
    /*
     * ESEditorDraw 是轻量 IMGUI 自绘协议，不是 SerializedProperty Drawer 框架。
     *
     * 适合：
     * - 编辑器工具窗口中的小块对象自绘。
     * - 调试面板、预览面板、分页列表。
     * - 非 Unity 序列化字段、临时运行时对象、工具数据的显示。
     *
     * 不适合：
     * - 替代 Unity / Odin 的正式 Inspector。
     * - 承担复杂 SO 配置器。
     * - 处理字段级 Drawer、Undo、Prefab Override 等完整序列化编辑语义。
     */
    #endregion

    public interface IDrawIMGUI
    {
        public void Editor_DrawIMGUI();
    }

    public class DrawIMGUISolver
    {
        public virtual void Draw()
        {
        }
    }
}
