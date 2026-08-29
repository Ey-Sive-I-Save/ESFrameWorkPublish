# ES 编辑器绘制与序列化套件：PropertyTree、多目标与迁移边界
Status: current
StableId: es.aiwarning.editor.propertytree-multitarget-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, editor, propertytree, serialize-reference, multitarget, undo
Applicability: ESEditorSection、ESPolymorphicReferenceDrawer、ESTypeCatalog、ESFieldRow 与 Odin PropertyTree/Processor
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-editor-propertytree-multitarget-boundary.md
StaleWhen: PropertyTree、Drawer、类型目录、序列化迁移或 SourceRef 变化。

- Unity 序列化字段和 `SerializeReference` 是唯一真源；窗口/PropertyTree/SessionState 只是投影，不得跨窗口复用实例或缓存反写资产。
- 多态选择必须来自合法 `ESTypeCatalog`；缺失类型显式保留原始信息，多目标 mixed 仅在用户明确选择后统一覆盖。
- 所有写入必须走 Undo、ApplyModifiedProperties、Dirty 和可恢复迁移路径；重选、销毁、域重载时释放并从当前资产重建。
- 禁止 Drawer 偷渡业务验证或 OnGUI 静默迁移；未完成 Unity Test Runner 回归时，不得宣称序列化迁移已验收。Knowledge：`es.aiwarning.editor.propertytree-multitarget-boundary.v1`。
