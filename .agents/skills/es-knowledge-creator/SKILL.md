---
name: es-knowledge-creator
description: Create, update, review, and route bounded ESFramework AIKnowledge outputs from current source, AIWarnings, AICommands, Skills, tests, and evidence. Use when a task asks to create a knowledge entry, summarize project facts for another AI, update KnowledgeIndex, repair stale knowledge, or limit AIKnowledge output size and authority.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Knowledge Creator

把 Knowledge 当成可追溯的路由产品，而不是把项目压成一篇大摘要。这个 Skill 负责控制输出范围、事实来源、证据等级、哈希新鲜度和 AIBrain 可发现性。

## Hard output policy

- 默认输出模式是 `route-pack`：只返回最相关的 1～3 个 Knowledge 条目、对应 `routeKeys`、`requiredReads`、`relatedSkills` 和证据等级。
- 只有用户明确要求“建立/更新详细知识条目”时，才使用 `detailed-entry`；一次默认只处理一个功能域。
- `index` 模式只输出 KnowledgeId、Topic、RouteKeys、Authority、EvidenceLevel、StaleWhen，不展开正文或源码片段。
- `full-audit` 必须由用户明确指定，并分批执行；禁止把全部 AIWarnings、全部源码或全部 `entries/` 无条件塞进上下文。
- 输出超出模式预算时，停止扩写，返回已覆盖范围、未覆盖范围和下一批路由建议。

## Authority and evidence gates

固定权威顺序：当前源码/真实验证证据 > AIWarnings P0 > AICommand > AIBrain 路由 > Skill > AIKnowledge 摘要。

每个详细条目必须具备：`KnowledgeId`、`Authority`、`RouteKeys`、`ContentHash`、`SourceRefs`、`EvidenceLevel` 和 `StaleWhen`；若有测试/发布事实，还要写 `EvidenceRefs`。`SourceRefs` 必须是真实文件和 SHA-256，ContentHash 按排序后的 SourceRef 哈希集合计算。

禁止把源码存在、目录存在、测试文件存在或静态阅读结果写成 Unity/PlayMode/Profiler/Player/IL2CPP/发布已通过。证据不足时使用 `Deferred` 或 `Blocked`，并写出缺失证据。

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `knowledge-output-governance`
- Required cases: `source-ref-hash, content-hash-recompute, bounded-output, stale-entry-detection, unsupported-claim-rejection`
- Static assertions: SourceRef; ContentHash; bounded output; stale; unsupported runtime claims
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `knowledge`
- Custom checks: `knowledge-boundary, bounded-output, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- **Permission matrix**：默认只读路由和候选输出；写入正式条目、索引或来源清单必须有明确用户授权；Skill、AICommand、源码、Git、Unity、发布权限彼此独立。
- **Change budget**：每批声明目标功能区、允许写入的条目/索引文件、最大条目数、最大上下文预算、停止条件和回滚路径。
- **Risk register**：识别来源漂移、路由过宽、证据夸大、重复覆盖、权限扩展和输出爆量；用验证器和拒绝扩权用例检测。
- **Acceptance replay**：至少执行正向、非法输入、拒绝扩权、重复/幂等和中断恢复用例；详细条目额外执行 SourceRef/ContentHash forward-test。

## Workflow

1. 先读取项目根 `AGENTS.md`、`Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`.agents/SKILL_RESOURCE_INDEX.yaml` 和本任务命中的 AIWarnings Start/RuleIndex。
2. 用任务对象、动作和风险匹配 `KnowledgeIndex.yaml` 的 `routeKeys`，先选择最小 `route-pack`；不要按目录递归收集素材。
3. 读取命中条目的 `requiredReads`、SourceRefs、当前源码、相关 AICommand/Skill 与已有测试；区分 verified facts、assumptions、non-claims。
4. 选择输出模式：`index`、`route-pack`、`detailed-entry` 或用户明确授权的 `full-audit`。
5. 写入/更新条目和 KnowledgeIndex 时保持原子一致；更新来源后重新计算哈希，旧 AIBrain 计划视为 stale。
6. 运行 `scripts/Test-ESKnowledgeEntry.ps1`、严格 UTF-8、KnowledgeIndex 路由/路径检查和相关 AIWarnings/Skill 合同校验。
7. 交付时只报告本批输出、来源、证据等级、未覆盖范围和残余风险，不用“完整”“已验收”覆盖未验证事实。

## Failure and recovery

- 缺少 SourceRef、路径越界、哈希漂移、重复 KnowledgeId、非法 EvidenceLevel 或 RequiredRead 缺失：拒绝输出为有效条目。
- 目标文件被其他未交付改动覆盖：停止，保留原文并报告重叠，不自行合并。
- 读取中断或来源发生变化：丢弃当前计划，重新读取并重新计算 ContentHash。
- 重复运行必须幂等；不删除冲突条目，不把旧摘要覆盖成新事实。

## Resources

- `references/output-policy.md`：输出模式、预算、权威和证据裁决。
- `references/knowledge-entry-contract.md`：条目字段和哈希合同。
- `scripts/Test-ESKnowledgeEntry.ps1`：只读条目验证器。
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`：AIBrain 发现与路由入口。
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`：机器可读知识索引。


## Specialized static acceptance

Acceptance ID: `knowledge-output-governance`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- SourceRef
- ContentHash
- bounded output
- stale
- unsupported runtime claims

Required specialized cases: `source-ref-hash, content-hash-recompute, bounded-output, stale-entry-detection, unsupported-claim-rejection`
Guidance: `references/static-specialized-acceptance.md`
