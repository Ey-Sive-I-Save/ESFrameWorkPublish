---
name: es-agent-mechanism-replication
description: "将公开 Agent 机制（SWE-agent ACI、Reflexion、Tree of Thoughts、Petri、EnvTrustBench、AuditBench）映射为 ESFramework 的受证据门控 RoutePlan、TaskContext、Knowledge 和闭环验收合同。当用户要求机制复刻、研究转合同、ES 适配、失败面审查或可发现路由时使用；只做静态设计/验证，不自行启动 Unity、Runtime、网络或未授权写入。"
---

# ES Agent Mechanism Replication

## Overview

本 Skill 把六类公开 Agent 机制统一投影到 ES 的 `GoalRevision -> RoutePlan -> RouteStage -> EvidenceSet -> Receipt -> completionDecision -> deliveryAcceptance` 链路。它负责可追溯的机制设计、路由/知识绑定、静态回放和失败闭环；不把“有设计”或“有 finding”当成 Runtime/发布证明。

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的“Skill 使用披露”规范。实际使用本 Skill 时，首次用户可见的进度更新必须说明该 Skill 与任务的关系；最终答复必须列出本轮实际影响工作的 Skill 与作用。不要列出仅可用、未使用的 Skill，也不得把披露视为授权、执行或验收证据。

## 权威读取与发现

按最小读取集合执行，路径均相对项目根：

1. `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`，确认唯一机制路由。
2. `Documentation/AIKnowledge/KnowledgeIndex.yaml`，读取机制条目的 `requiredReads`、正文和 `SourceRefs`。
3. 依 `requiredReads` 读取 `ES/Automation/Contracts/es-route-stage.registry.json`、TaskContext/Evidence/Receipt 合同及命中的 `Assets/Plugins/ES/AIWarnings/` 规则。
4. 只消费快照或当前权威文件声明的路径；禁止用 `sourceAbsolutePath` 替换私有 `absolutePath`，禁止递归加载无关 Knowledge。

若中文别名解析到多个 Skill，先消歧；无唯一命中报告 `NoSkillRoute`。别名、RouteKey、Catalog 和 Knowledge 只提供发现/导航，不授予写入、网络或 Runtime 权限。

## 六类机制到 ES 的合同映射

Static assertions: six mechanism identities; GoalRevision and RoutePlan; four RouteStages with requires/produces/failures; EvidenceSet and immutable Receipt; completionDecision versus deliveryAcceptance; SourceRef/hash invalidation; Chinese discovery alias; ABCD divergence-audit-iteration orchestration; ABCD bounded dynamic controller; ABCD immutable snapshot and receipt verification; ABCD bounded self-iteration and learning emission; evidence-gated learning review; independent certification assessment gate.

工业级扩展断言：adaptive learning partition isolation and no-regression；external source lock and network hash verification；cross-process TaskContext CAS and atomic event publication。上述断言只支持静态合同与本地子进程证据，不宣称 Unity/Worker/宿主 Runtime 或外部权威认证。

| 机制 | ES 投影 | 必须闭合的风险 |
|---|---|---|
| SWE-agent ACI | 受限工具/命令投影到 `RouteStage`，每次动作有输入、权限和 Receipt | 工具越界、未授权副作用 |
| Reflexion | 失败回顾写入候选 Evidence，经过 Verifier 后才可进入下一轮 | 未验证反思污染记忆 |
| Tree of Thoughts | 每个分支使用不可变快照和独立 `TaskContext`，由 OutcomeEvaluator 选择 | 分支共享可变状态、选择不可重放 |
| Petri | Place/Transition 映射 TaskContext 状态迁移、CAS 与恢复 | 非法迁移、重复消费、丢失恢复 |
| EnvTrustBench | 环境/工具信任作为 EvidenceSet 字段和门禁，不是模型自报 | 环境证据缺失仍继续执行 |
| AuditBench | 审计问题、规则、Finding、Receipt 和 completionDecision 可重放 | 审计结论无来源或被压平 |

详细字段和案例见 `references/mechanism-contract.md`；开源机制到 ABCD 发散/审计/迭代的映射见 `references/open-source-mechanism-mapping.md`。

## 专属超级语义

本 Skill 的高层触发器唯一声明在 `references/super-semantics.json`，不是散落在提示词或菜单文本中的隐式规则。The super-semantics trigger registry is the only authoritative trigger list for this Skill。面向用户优先使用短句；技术别名只用于兼容和精确路由。可发现的专属语义包括：

项目级命中由 `.agents/scripts/Resolve-ESSuperSemantics.ps1` 解析。唯一命中时必须在回复第一条可见文本输出 `✨✅【已触发超级语义“<稳定 label>”】`；这是一条独立语义回执，不是常规路由推荐，也不表示动作已经执行。未命中不显示，多重命中必须先消歧。

最短入口是 `帮我做机制复刻`。它只打开下面六个选项，不要求用户记忆技术词，也不会直接执行任何阶段。

- `把这个机制研究接到ES`（兼容：`机制研究转ES合同`、`六类机制ES设计`）：进入分析与合同设计。
- `看看机制流程怎么走`（兼容：`机制路由探针同步`、`机制RouteStage链审查`）：审查 Knowledge、RouteProbe 和 RouteStage 绑定。
- `检查机制证据够不够`（兼容：`机制证据回执复核`、`机制EvidenceSet验收`）：复核 EvidenceSet、Verifier、Receipt 和交付决策边界。
- `源文件变了重查机制`（兼容：`机制源漂移重放`、`机制SourceRef漂移复核`）：处理 SourceRef、EntryBodyHash 和 SourceSetHash 漂移。
- `这套机制会不会越权`（兼容：`机制权限扩展审查`、`机制副作用边界复核`）：审查写入、网络、Runtime、宿主进程和 handoff 边界。
- `验收这套机制闭环`（兼容：`六类机制闭环静态验收`、`机制研究闭环验收`）：运行专用 StaticDeepReplay 与三类证据回执检查。

这些超级语义只产生 `routeKey + operation` 的发现输入，命中后仍需用户选择、GoalRevision 和 RoutePlan；它们不授予权限、不执行阶段，也不替代 Runtime 验收。若目标同时命中通用 `super-semantics` 菜单与本 Skill 语义，先保留菜单作为只读入口，再按对象/动作/风险消歧。

## 工作流与闭环判定

### A. 设计（默认只读）

- 冻结 `GoalRevision`、对象范围、证据等级和非目标声明。
- 选择机制 Knowledge 与最小 `requiredReads`，记录源文件哈希；发现漂移即停止旧计划并回读权威来源。
- 生成四段 RouteStage：`analysis -> review -> knowledge-candidate -> knowledge-validation`；每段必须有 `requires`、`produces` 和稳定失败码。
- ABCD 外循环使用 `Divergence -> Independent Audit -> Selection -> CorrectionCycle -> Verification Receipt -> next round/stop`；不得把 CorrectionCycle 变成第二套 Task 状态机。
- `ESABCDDynamicController` 提供 ABCD bounded dynamic controller；候选、审计、轮次、尝试和验证预算均为有限值，任一缺口 fail-closed。
- `ESABCDLearningReview` 提供 evidence-gated learning review；学习候选必须先经验证且来源稳定，始终保持 `promotionAllowed=false`，等待明确的 Knowledge 审查/Apply。
- `ESABCDCertification` 提供 independent certification assessment gate；DesignReview、RuntimeAcceptance、ReleaseAcceptance 分层，独立 verifier 和外部签名缺失时只能是 `conditional`。
- 设计 `EvidenceSet`、不可变 Receipt、`completionDecision` 与 `deliveryAcceptance` 的分离。

### B. 写入候选（需当前用户明确授权）

只在用户明确要求创建/更新 Skill、Knowledge、路由或合同后写入项目内目标文件。写前保留工作树现状；不写会话历史、审计状态、Git、发布或删除。SourceRef 漂移时先确认内容有效性，再决定是否重算 `ContentHash`/`SourceSetHash`，绝不盲抄当前值。

### C. 静态验收

运行本 Skill 的 `scripts/Test-es-agent-mechanism-replication-StaticReplay.ps1`，并按 `static-replay.manifest.json` 覆盖七个通用案例和专用闭环案例。静态通过只证明合同、结构、哈希/路由声明和确定性回放；`runtime-not-run` 保留为未运行状态。

### D. 闭环谓词

只有同时满足下列谓词，才可报告“静态合同闭环”：

1. 目标、范围和 `GoalRevision` 已冻结且可重读。
2. Knowledge、RouteProbe、RouteStage、requiredReads 和源快照身份一致；零 finding 只表示该探针未发现问题，不是全局通过。
3. 每个关键转移有 verifier 产生的 EvidenceSet 和不可变 Receipt。
4. 未授权写入、网络、宿主进程和 Runtime 启动均被拒绝或明确升级。
5. `completionDecision` 与 `deliveryAcceptance` 独立，且失败时 fail-closed 到对象/字段/Profile 范围。
6. SourceRef/内容变化会使旧计划、缓存或 Receipt 失效并要求重放。

缺任一谓词只能报告 `review`、`stale`、`runtime-not-run` 或对象级 `blocked`，不得宣称项目整体闭环。

## 固定失败处理

- `SOURCE_HASH_DRIFT`：回读声明来源并审查语义；来源有效且确已变更时才重算哈希。
- `ENTRY_BODY_HASH_MISMATCH`：先确认正文是否被篡改/截断，再重算声明哈希；SourceRefs 未变时不顺手改 `SourceSetHash`。
- `ROUTE_REQUIRED_READS_MISMATCH`：比较探针私有快照与当前索引，确认真实依赖后再更新；不机械添加读取项。
- `ROUTE_TOP3_MISMATCH`：先判断 canonical 路由归属和竞争变化，再更新期望 Top3。
- 新探针零 finding：记为该探针静态观察结果，不把它汇总成失败或成功证据。
- `external-source-not-bound`：外部论文/仓库只作待绑定来源；未绑定不得升级为事实或完成证明。

## 运行边界、恢复与交付

本 Skill 不调用 Unity、Player、Profiler、网络、外部仓库或任意宿主命令；Runtime 只能在用户单独明确授权后由对应 Runtime Skill 执行。中断后从已接受的不可变 Transcript/Context 和快照恢复，禁止换用另一 handoff 源；重复运行必须保持稳定排序、相同输入相同输出并复用有效哈希缓存。

交付报告必须列出：实际读取与哈希、对象级 finding、静态/Runtime 分轴状态、已验证证据、未证实项和下一步。默认交付等级为 `Implemented-Unverified`/`S2`，除非有新鲜的专用验收和 Runtime/Release 证据支持更高状态。写入范围和停止条件必须在报告中可重读。

## Engineering controls

- 身份：每次输出绑定 `GoalRevision`、RoutePlan、源快照和 Skill 版本。
- 权限：`allowDirectExecution=false`；写入、网络、宿主进程和 Runtime 均须当前用户单独授权。
- Change boundary: write scope、change budget 和 stop condition 必须显式声明；超出范围立即停止。
- 风险：SourceRef、哈希、路由竞争和分支隔离按对象/字段 fail-closed，不把计数汇总成全局阻断；生命周期、reload 和 unbind 变化触发重放。
- 证据：只接受可重读来源、Verifier 结果和不可变 Receipt；静态证据不投影为 Runtime 事实。
- 恢复/性能：使用有界读取、稳定排序和缓存失效；中断从已接受快照恢复，不替换 handoff。
- 兼容/供应链：外部仓库和论文保持待绑定状态；外部数据保持 untrusted，不自动联网或执行第三方代码。
- 操作白名单：仅允许声明的 operation allowlist 和 fixed route，其他操作拒绝并记录 finding。

## 参考与脚本

- `references/mechanism-contract.md`：六类机制的字段、状态和闭环检查表。
- `references/static-specialized-acceptance.md`：专用静态验收案例与非声明边界。
- `references/static-replay-adapter.md`：七个通用回放案例与治理检查映射。
- `references/open-source-mechanism-mapping.md`：六类公开机制到 ABCD 发散/审计/迭代闭环的映射与场景覆盖。
- `scripts/Test-es-agent-mechanism-replication-StaticReplay.ps1`：只读调用共享 StaticDeepReplay 引擎。
