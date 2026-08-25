# ES 商业级静态组合一致性合同

## 目的

`Test-ESCommercialCoherence.ps1` 是只读控制面审计器，不替代任何领域验证器，也不启动 Unity、外部进程、网络或 Runtime。
它只回答一个有限问题：多个静态治理面是否在同一份源快照上得出可追溯结论。

## 必须组合的门禁

- Skill Architecture：Skill、治理元数据、Registry Manifest、命令绑定闭环；
- AICommand：命令正文、Catalog、风险和写入模式一致；
- ES Automation Compatibility：保留 ES Center、Facade、AIBrain、Bridge 和 Contract 入口；
- AIKnowledge：SourceRef、ContentHash、KnowledgeIndex、路由和证据边界。

## 快照规则

审计开始和结束分别计算治理面快照哈希，至少包括：

- `.agents/SKILL_DISCOVERY_POLICY.json`；
- `.agents/SKILL_CATALOG.yaml`；
- `.agents/SKILL_REGISTRY.manifest.json`；
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json`；
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`；
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`。

`snapshotStable=false` 时，所有子结果只能用于诊断，整体不得声明静态一致。

## 结果语义

- `static-coherent`：组合静态门禁通过，不能解释为 Runtime 或 Release 已通过；
- `static-review-required`：至少一个子门禁阻断、验证器异常或快照不稳定；
- `review` 子门禁：源码快照稳定但 SourceRef/ContentHash 尚未刷新，或源码在审计窗口内变化；它要求证据刷新，不等价于代码合同失败。
- `validator-error`：验证器没有产生可解析收据，必须修复验证器/环境，不得改写业务证据；
- `runtime-not-run`：明确记录 Runtime 尚未授权或尚未执行；
- `claimsNotProven`：列出 Unity、进程、网络、Profiler、Player、IL2CPP 等未证明范围。
- `claimsProven`：列出本次组合静态门禁实际证明的结构、合同、证据身份和交付跟踪范围；不得把它扩写为 Runtime 或 Release 结论。

## 证据合同

聚合报告必须保留每个子门禁的 `status`、`reportPath` 和适用的 `findingCount`。聚合器不能把子门禁的 `Passed` 改写成 `Accepted`，也不能吞掉底层 Finding、SourceRef 漂移或收据缺失。`blockedCheckCount` 与 `reviewCheckCount` 必须分开统计。

每个收据绑定还必须包含 `reportExists` 和当前 `reportHash`；缺失收据不得被当作通过，哈希只证明本次聚合读取到的工件身份，不替代工件内容验证。

AIKnowledge 底层收据仍保留 `rawStatus=blocked`；当且仅当 Finding 代码全部属于 `SOURCE_HASH_DRIFT` 或 `CONTENT_HASH_MISMATCH` 时，组合层可映射为 `status=review`，并必须保留 `freshnessOnly=true` 与原始 Finding。任何路径、格式、索引、重复 ID 或权限错误都不得降级为 `review`。

## ES 兼容原则

该合同只增加审计、快照和证据边界，不替换 ES 原有类型、菜单、Facade、AIBrain、Worker 或任务入口。任何破坏兼容的修改必须另有迁移、回滚和批准证据。

组合审计还必须运行 `Test-ESStaticAcceptanceCoverage.ps1`，确认每个已注册 Skill 都有职责 Profile、专属静态案例、可发现的指导文档和存在的证据工件。该覆盖检查只证明静态验收基础设施完整，不证明各 Skill 的 Runtime 行为。

发布前还要检查治理脚本与合同是否已被版本库跟踪；未跟踪工件属于 `review`，不是代码阻断，但在发布前必须纳入版本控制，否则换机器或换分支后治理能力可能丢失。
