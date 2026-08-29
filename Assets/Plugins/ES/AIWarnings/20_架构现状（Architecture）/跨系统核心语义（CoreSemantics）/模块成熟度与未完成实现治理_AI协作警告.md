# 模块成熟度与未完成实现治理 AI 协作警告

Status: current
StableId: es.aiwarning.arch.module-maturity-incomplete-governance
Authority: AIWarnings；模块唯一状态入口与当前证据为事实权威。
RouteKeys: aiwarnings, architecture, module, maturity, evidence, blocked, lifecycle
Applicability: 新模块、重构、实验、集成、验证、稳定、废止与归档状态。
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-architecture-module-maturity-incomplete-governance.md#evidence`
Owner: ES module lifecycle/governance owners。
StaleWhen: 模块状态入口、证据等级、成熟度合同或续接协议变化。

## 长期约束

- 目录、类型、接口、菜单或源码存在不等于模块完成；状态必须由当前实现范围和证据决定，禁止用 TODO 数量、百分比、旧文档或 AI 报告升级结论。
- `ModuleMaturity`（Proposed→Archived）、`EvidenceLevel`（S0-S6）与 `DeliveryVerdict`（Designed/Implemented-Unverified/Accepted/Released）必须分开；`Blocked`/`Failed` 只是附加结论。
- 半成品必须可识别、可隔离、可失败、可回退，不得默认注册、默认启用、渗透稳定模块或进入正式发布；未实现路径必须明确失败/不支持。
- 状态唯一入口应记录承诺范围、权威入口、默认激活、依赖/消费者、已有/缺失证据、退出条件和失效条件；禁止在多个 README、Warning 或注释维护冲突状态。
- 审计续接只允许写固定 `ES/Documentation/Status/MODULE_AUDIT_STATE.md` 的对象块；检查点不授予后续源码、Git、Unity 或发布权限，事实漂移后必须 stale。
- 静态源码/`.csproj`/一次演示不能证明 Unity、PlayMode、Profiler、Player、IL2CPP 或发布；稳定状态只能覆盖已取得证据的范围。
- 详细状态跃迁、审计矩阵、续接格式、失败模式和验收要求由专用 Knowledge 承接；Knowledge 不替代用户授权。
