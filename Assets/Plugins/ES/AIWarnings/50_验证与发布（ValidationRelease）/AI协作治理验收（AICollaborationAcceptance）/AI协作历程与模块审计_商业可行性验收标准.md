# AI 协作历程与模块审计：商业可行性验收标准

> Status：current；StableId：`es.aiwarnings.validation.ai-collaboration-commercial-acceptance`
> Authority：`AIWarnings`；RouteKeys：`aiwarnings`、`validation`、`collaboration`、`audit`、`commercial`
> Applicability：历程、模块审计、跨窗口交接、长周期多 AI 协作验收。
> EvidenceRef：`Documentation/AIKnowledge/entries/aiwarning-validation-ai-collaboration-commercial-acceptance.md`；对应验收脚本与回执。
> Owner：ES AI governance；StaleWhen：历程/审计合同、恢复格式、权限边界或验收用例变化。
> Knowledge：`Documentation/AIKnowledge/entries/aiwarning-validation-ai-collaboration-commercial-acceptance.md`

## 不可下放的长期边界

- C0 仅证明规则/工具存在；C1 需单窗口闭环；C2 需长窗口恢复、失败注入和跨窗口接手；C3 还需多人/多 AI、权限、性能、回归和长期运行证据。未获证据只能称“已实现，待商业验收”。
- 历程保留完整过程、失败、纠正、交付和未完成项；固定状态只保存恢复导航。权限、Git、Runtime、网络、发布和交接彼此独立。
- 必须覆盖长消息、多条补充、失败/撤回、JSONL 恢复、错误 session、HEAD/工作树漂移、并行审计、覆盖失败和交接询问等负向场景；静态通过不得升级为商业可行。
- 只有用户明确要求时才生成新 AI 交接文案；不得自动写入审计历史或用摘要冒充完整恢复。

详细 C0-C3 定义、W1-W10 用例、商业维度、签收条件和原文事实见 Knowledge；当前未声明 Unity/Runtime/发布通过。
