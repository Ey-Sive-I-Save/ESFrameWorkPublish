# 项目最高警告 P0：AI 交付声明与责任契约

`Status`: `current`
`StableId`: `es.aiwarning.p0.ai-delivery-contract.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `delivery-evidence`, `acceptance`, `disclosure`
`Applicability`: 所有 AI 设计、修改、生成、验证或交付的 ESFramework 源码、资产、工具、运行链路和技术结论。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-ai-delivery-contract.md`；当前验证脚本与可重读回执。
`StaleWhen`: 本 Warning、交付状态枚举、证据等级、验证合同或其 Knowledge SourceRefs 变化。

## 长期 P0 约束

- 完成声明必须绑定实际证据等级（S0–S6），主动列出未验证项、阻断、影响和下一步；不得把源码存在、静态编译、按钮可见或临时预览冒充运行时/发布完成。
- 证据不足必须降级结论；不得用相邻证据替换缺失证据。报告至少包含目标、实际修改、当前等级、已验证、未验证、阻断原因、影响范围和下一步。
- 性能或 0 GC 声明必须说明结果身份、修改语义、所有权、分配阶段、并发边界和验证证据；仅编译或未发现 `new` 不足以证明 0 GC。
- `Designed`、`Implemented-Unverified`、`Blocked`、`Failed`、`Accepted`、`Released` 不能互相压平；模块成熟度与 S0–S6、交付状态并列，不得把未验证状态称为完成。
- 不得把临时对象、缓存、截图、日志、模拟数据或单元测试当作正式资产、Unity 交互、PlayMode、Player、Profiler、IL2CPP 或发布证据。
- AIWarnings 只保留长期证据/权限/披露边界，不写入瞬时编译日志、Console 明细或 Warning 数量；失败机制仅保留可复用的最小规则。
- 地图、场景、资源等声明必须区分作者态、数据源、正式 Unity 资产、Scene/Prefab 与运行时/发布产物，并实际写入、重读目标对象后才可声明保存。
- AI 交付责任链为：识别真实目标 → 修改正确权威对象 → 执行匹配等级验证 → 披露证据缺口 → 给出可恢复下一步。禁止模糊完成措辞和静默替换验证来源。

详细证据等级、报告字段、性能附加合同、禁止等价关系、状态定义、地图门禁和原文快照见 Knowledge：`es.aiwarning.p0.ai-delivery-contract.v1`。
