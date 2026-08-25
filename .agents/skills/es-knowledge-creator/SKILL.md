---
name: es-knowledge-creator
description: Create, update, review, and route bounded ESFramework AIKnowledge outputs from current source, AIWarnings, AICommands, Skills, tests, evidence, and consent-gated external primary sources. Use when a task asks to create a knowledge entry, summarize project facts for another AI, mine AI failure modes, calibrate version-sensitive facts against official documentation, update KnowledgeIndex, repair stale knowledge, or limit AIKnowledge output size and authority.
---

## Verification boundary

When updating established ES knowledge routes, preserve the existing AIBrain entry and routing semantics; use the ES preservation refactor contract for any structural migration and record SourceRef/ContentHash changes explicitly.

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

## External authority and failure-surface weighting

For every `detailed-entry`, read [the research and failure-surface policy](references/research-and-failure-surface-policy.md) before drafting.

Static replay contract names: `external primary-source provenance` and `AI failure-surface weighting`.

- Project source and real evidence remain the fact authority. Quality weights allocate research and review attention; they never let an external page override current ES ownership, permission, routing, or runtime evidence.
- External primary-source calibration carries 25% of the quality rubric when the entry contains version-sensitive API, package, platform, language, protocol, or vendor behavior. The Creator must proactively propose a bounded official-source lookup, name domains/version/page budget, and pause until the user explicitly authorizes that network action.
- AI failure prevention carries 40% of the quality rubric. A detailed entry with no material failure modes is not ready; do not pad a quota with generic cautions.
- A web page is not a valid project `SourceRef`. Persist it only when the user authorizes a bounded project-local source snapshot recording URL, product/version, retrieval time, quoted contract, and content hash. Without that snapshot, use the page only for the current analysis and mark the long-lived claim `Deferred` or `external-source-not-bound`.
- After a candidate entry is complete, proactively offer `$es-knowledge-validator`'s consent-gated three-condition comparison. Creation and effectiveness validation remain separate responsibilities.

Hard failures override any weighted score: missing or drifting SourceRefs, fabricated provenance, route/index mismatch, permission expansion, unsupported Runtime claims, or an unhandled irreversible/identity-loss failure mode keep the output blocked.

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

- **Permission matrix**：Skill 自主运行默认只读路由和候选输出；当前用户明确要求的正式条目、索引、来源清单或路由权威修改可直接执行，`Test-ESUserDirectedLowRiskPolicy.ps1` 只验证声明范围闭合。AIBrain `planTask`、AICommand 和 TaskContract 仅在选用受管通道时作为协议输入；Git、Unity/Runtime 和发布动作仍须被用户单独点名。
- **Change budget**：每批声明目标功能区、允许写入的条目/索引文件、最大条目数、最大上下文预算、停止条件和回滚路径。
- **Risk register**：识别来源漂移、路由过宽、证据夸大、重复覆盖、权限扩展、输出爆量、外部资料版本错配、低权威网页替代一手来源，以及遗漏不可逆/身份/生命周期/部分成功/恢复失败；用验证器、失败面矩阵和拒绝扩权用例检测。
- **Acceptance replay**：至少执行正向、非法输入、拒绝扩权、重复/幂等和中断恢复用例；详细条目额外执行 SourceRef/ContentHash forward-test。

## Workflow

1. 先读取项目根 `AGENTS.md`、`Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`.agents/SKILL_RESOURCE_INDEX.yaml` 和本任务命中的 AIWarnings Start/RuleIndex。
2. 用任务对象、动作和风险匹配 `KnowledgeIndex.yaml` 的 `routeKeys`，先选择最小 `route-pack`；不要按目录递归收集素材。
3. 读取命中条目的 `requiredReads`、SourceRefs、当前源码、相关 AICommand/Skill 与已有测试；区分 verified facts、assumptions、non-claims。
4. 对 `detailed-entry` 先判定外部权威资料是否适用；适用时主动提出限定域名、版本、页面数和停止条件的查询，未获当次同意则不联网并记录证据缺口。
5. 在写正文前完成 failure-surface matrix：从异常/返回值、取消、部分成功、回滚、幂等、并发漂移、身份/Owner/生命周期、权限、证据夸大、负向测试及官方 Warning/Note/Known Issue 中提取可检查规则。
6. 选择输出模式：`index`、`route-pack`、`detailed-entry` 或用户明确授权的 `full-audit`；按质量权重检查来源校准、失败预防、项目事实和路由可执行性，硬门禁优先。
7. 写入/更新条目和 KnowledgeIndex 时保持原子一致；更新来源后重新计算哈希，旧 AIBrain 计划视为 stale。
8. 运行 `scripts/Test-ESKnowledgeEntry.ps1`、严格 UTF-8、KnowledgeIndex 路由/路径检查和相关 AIWarnings/Skill 合同校验。
9. 交付时只报告本批输出、来源、证据等级、未覆盖范围和残余风险，并主动询问是否执行三情况效果对比；不用“完整”“已验收”覆盖未验证事实。

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范：首次进度更新只声明本轮实际使用的 Skill 及直接关系；最终答复单列实际使用的 Skill 及其对结论、修改或验证的影响。披露不代表脚本已运行、权限已获得或 Runtime 已验收。

## Failure and recovery

- 缺少 SourceRef、路径越界、哈希漂移、重复 KnowledgeId、非法 EvidenceLevel 或 RequiredRead 缺失：拒绝输出为有效条目。
- 目标文件被其他未交付改动覆盖：停止，保留原文并报告重叠，不自行合并。
- 读取中断或来源发生变化：丢弃当前计划，重新读取并重新计算 ContentHash。
- 重复运行必须幂等；不删除冲突条目，不把旧摘要覆盖成新事实。

## Resources

- `references/output-policy.md`：输出模式、预算、权威和证据裁决。
- `references/knowledge-entry-contract.md`：条目字段和哈希合同。
- `references/research-and-failure-surface-policy.md`：外部权威资料查询、来源快照、质量权重、易错面矩阵和 Validator 交接合同。
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
