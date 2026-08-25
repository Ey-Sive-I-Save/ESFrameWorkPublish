# ES 商业级静态组合一致性合同

## 目的

`Test-ESCommercialCoherence.ps1` 是对项目源资产只读的控制面审计器，不替代任何领域验证器；它不启动 Unity/Runtime，也不访问网络。
它会调用本地验证脚本（其中用户直接授权检查使用独立 PowerShell runspace），执行只读的 Git object 查询；审计器及子验证器会在 `ES/Output/` 下写入或刷新静态报告，并可能在系统临时目录创建后清理隔离测试夹具。
它只回答一个有限问题：多个静态治理面是否在同一份源快照上得出可追溯结论。

## 必须组合的门禁

- Skill Architecture：以 `AuthorizationLane=ManagedAIBrain` 显式执行，验证 Skill、治理元数据、Registry Manifest、命令绑定闭环；
- User-Directed Action Authority：以 `CurrentUserDirect` 语义执行 `.agents/tests/Test-ESUserDirectedActionAuthority.ps1`，验证用户明确请求可直接授权普通文件、控制面和 AIKnowledge 修改，同时拒绝缺少用户指令、AI 自主扩张、项目越界和未点名专项动作；create/modify 标签与目标存在性不一致时只产生非阻断复核；
- AICommand：命令正文、Catalog、风险和写入模式一致；
- ES Automation Compatibility：保留 ES Center、Facade、AIBrain、Bridge 和 Contract 入口；
- AIKnowledge：SourceRef、ContentHash、KnowledgeIndex、路由和证据边界。

## 快照规则

审计开始和结束分别计算治理面快照哈希，至少包括：

- `AGENTS.md`；
- `.agents/SKILL_DISCOVERY_POLICY.json`；
- `.agents/SKILL_CATALOG.yaml`；
- `.agents/SKILL_REGISTRY.manifest.json`；
- `.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json`；
- `.agents/skills/es-skill-governance/scripts/Test-ESUserDirectedLowRiskPolicy.ps1`；
- `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`；
- `.agents/tests/Test-ESUserDirectedActionAuthority.ps1`；
- `.agents/skills/es-skill-governance/scripts/Test-ESCommercialCoherence.ps1`；
- `.agents/skills/es-skill-governance/scripts/Test-ESStaticAcceptanceCoverage.ps1`；
- `.agents/skills/es-skill-governance/scripts/Test-ESRuntimeAuthorizationContract.ps1`；
- `.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1`；
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1`；
- `.agents/skills/es-skill-governance/references/commercial-coherence-contract.md`；
- `.agents/tests/Test-ESCommercialDeliveryTracking.ps1`；
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json`；
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`；
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`。

报告必须保留 `snapshotFilesBefore` 与 `snapshotFilesAfter` 的逐文件 SHA-256 映射，使聚合哈希可审计并在不稳定时定位变化。`snapshotStable=false` 时，所有子结果只能用于诊断，整体不得声明静态一致。

## 授权 Lane

聚合报告必须写出顶层 `authorizationLane=ManagedAIBrain` 和 `laneCoverage`。商业组合是项目级受管验收，因此 Architecture 的命令绑定、计划和合同检查必须保持严格；AICommand 与 ES Automation Compatibility 作为受管协议结构检查也必须在报告中标为 `ManagedAIBrain`。这不意味着普通用户请求需要进入受管通道。

Architecture 回执必须回显 `authorizationLane=ManagedAIBrain`。参数未识别、回执缺少 lane、回显不一致或仅存在旧报告时，组合层必须以 `authorization-lane-mismatch` 阻断，不能复用旧的通过状态。

`userDirectedActionAuthority` 子检查固定声明 `authorizationLane=CurrentUserDirect`。其 `static-passed` 仅证明策略验证器在声明输入下满足范围闭合与专项动作边界；脚本无法认证聊天宿主是否诚实传入了当前用户指令，因此该限制必须保留在 `claimsNotProven`。

## 结果语义

- `static-coherent`：组合静态门禁通过，不能解释为 Runtime 或 Release 已通过；
- `static-review-required`：至少一个子门禁阻断、验证器异常或快照不稳定；
- `review` 子门禁：源码快照稳定但 SourceRef/ContentHash 尚未刷新，或源码在审计窗口内变化；它要求证据刷新，不等价于代码合同失败。
- `validator-error`：验证器没有产生可解析收据，必须修复验证器/环境，不得改写业务证据；
- `runtime-not-run`：明确记录 Runtime 尚未授权或尚未执行；
- `claimsNotProven`：列出 Unity、进程、网络、Profiler、Player、IL2CPP 等未证明范围。
- `claimsProven`：列出本次组合静态门禁实际证明的结构、合同、证据身份和交付跟踪范围；不得把它扩写为 Runtime 或 Release 结论。

## 证据合同

当前聚合报告使用 `schemaVersion=2`，必须包含授权 lane、逐文件快照映射和用户直接授权回归证据。

聚合报告必须保留每个子门禁的 `status`、`reportPath` 和适用的 `findingCount`。聚合器不能把子门禁的 `Passed` 改写成 `Accepted`，也不能吞掉底层 Finding、SourceRef 漂移或收据缺失。`blockedCheckCount` 与 `reviewCheckCount` 必须分开统计。

每个收据绑定还必须包含 `reportExists` 和当前 `reportHash`；缺失收据不得被当作通过，哈希只证明本次聚合读取到的工件身份，不替代工件内容验证。

用户直接授权子检查必须保留回归脚本的 `rawStatus`、`caseCount`、`failedCount`，并为 `AGENTS.md`、策略 JSON、策略验证器、授权合同和回归脚本记录存在性与当前 SHA-256。任一必需文件缺失、回归失败或输出不可解析时，该子检查为 `blocked`；不得降级为 `review`。

只有 `userDirectedActionAuthority.status=passed` 时，聚合报告才可把用户直接授权范围闭合写入 `claimsProven`；失败时不得保留该声明。

AIKnowledge 底层收据仍保留 `rawStatus=blocked`；当且仅当 Finding 代码全部属于 `SOURCE_HASH_DRIFT` 或 `CONTENT_HASH_MISMATCH` 时，组合层可映射为 `status=review`，并必须保留 `freshnessOnly=true` 与原始 Finding。任何路径、格式、索引、重复 ID 或权限错误都不得降级为 `review`。

## ES 兼容原则

该合同只增加审计、快照和证据边界，不替换 ES 原有类型、菜单、Facade、AIBrain、Worker 或任务入口。任何破坏兼容的修改必须另有迁移、回滚和批准证据。

组合审计还必须运行 `Test-ESStaticAcceptanceCoverage.ps1`，确认每个已注册 Skill 都有职责 Profile、专属静态案例、可发现的指导文档和存在的证据工件。该覆盖检查只证明静态验收基础设施完整，不证明各 Skill 的 Runtime 行为。

发布前还要检查治理脚本、合同和定向回归是否由当前本地 `HEAD` 提交承载；至少包含 `AGENTS.md`、用户指令策略 JSON、策略验证器、用户直接授权合同、授权回归脚本、商业组合验证器、本合同及商业交付跟踪回归。`deliveryTracking.artifactVersionStates` 必须为每个工件记录项目相对路径、worktree/index/HEAD Git object ID、查询退出码、匹配关系和以下互斥 `versionState`：

- `untracked`：工作树存在，但 index 没有该路径；
- `index-only-staged-new`：工作树与 index 一致，但当前 `HEAD` 尚无该路径；
- `worktree-differs-from-index`：当前工作树内容与 index 不同，包括暂存后又修改；
- `index-differs-from-head`：工作树与 index 一致，但 index 内容尚未进入当前 `HEAD`；
- `committed-clean`：工作树、index 与当前 `HEAD` 三者为同一 Git object；
- `worktree-missing`、`invalid-path`、`git-error`：缺失或无法可靠查询，必须 fail closed 为复核。

只有所有必需工件都是 `committed-clean` 时，`deliveryTracking.status` 才能为 `passed`。路径仅被 `git ls-files` 识别、仅完成 `git add`、处于 `A / M / AM / MM`，或当前工作树字节尚未被当前提交承载时都保持 `review`，不是代码阻断。该检查只证明本地 `HEAD` 身份，不证明该提交已 push、已进入共享分支或可在另一台机器取得；远端发布与跨机器可用性必须保留在 `claimsNotProven`。
