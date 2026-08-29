# SO 表格工具 AI 协作说明

Status: current
StableId: es.aiwarning.so-table-tool-boundary.v1
Authority: AIWarnings（SO 表格工具长期约束）；详细安全与表格式见 Knowledge
RouteKeys: aiwarnings, editor, so-table, import, export, plan, batch, serialization, utf8
Applicability: ESSoTableDataRule 导入/导出、批处理、映射、Group/Info、Editor UI 与缓存
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-so-table-tool-boundary.md
StaleWhen: ESSoTableDataRule、映射/批处理协议、Group/Info 同步、编辑器命名空间或 SourceRef 哈希变化。

## 长期约束

- 修改表格工具必须按相干步骤编译；计划生成只读，不写 SO、表或 SaveAssets。导入/导出、删除、清空、覆盖、重建必须经过断言、风险提示和相应确认。
- CSV/XLSX 写入使用临时替换与 `_backups`；删除后清理执行缓存。字段、列、行键和目标 Group/Info 优先使用当前映射候选选择，不以手输字符串作为主流程；错误必须包含结构化上下文。
- `soFieldPath` 是稳定字段身份；子表默认稀疏并继承 owner；Group/Info 键为空或多候选时不得猜测。Super Batch 未达到商业完成，不得宣称完成。
- 公共/序列化/反射/UXML/菜单类型不得盲目迁移命名空间；中文文本必须保持严格 UTF-8。静态编译或搜索不能冒充 Unity/运行时验收。

## Knowledge 导航

详细命名空间政策、表格式、断言、子行、映射、Group/Info、缓存和性能规则见 `es.aiwarning.so-table-tool-boundary.v1`。本 Warning 不授权批量重构、源码、Git、运行时或发布操作。
