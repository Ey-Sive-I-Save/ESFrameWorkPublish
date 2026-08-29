# Knowledge Routing Quality Registration Candidate

状态：已应用的注册变更记录；当前路由以 `KnowledgeIndex.yaml` 和验证结果为准，本记录不是 Runtime 或发布证据。

## 目标

将 `Documentation/AIKnowledge/entries/knowledge-routing-quality.md` 注册为 Knowledge 质量、canonical 去重、路由探针、stale、bounded-output 和证据边界的唯一事实所有者，同时收窄会把 Knowledge 质量任务导向 Unity/Editor 领域条目的通用投影。

## 应用前置

- 用户授权仍覆盖本批 Knowledge 整改。
- AIBrain `planTask`、`knowledge.entry.update` 和当前 TaskContract 对 `KnowledgeIndex.yaml`/`AIBRAIN_ENTRY.md` 写入形成有效交集。
- 下列目标文件在应用前连续采样稳定；任何哈希变化都必须重新读取、重算并重放探针。
- 不修改 Assets、源码、Skill、AICommand、Git、审计、发布或外部状态。

## KnowledgeIndex 新绑定

在 `Documentation/AIKnowledge/KnowledgeIndex.yaml` 的 `entries` 中增加且只增加一次：

```yaml
  - knowledgeId: es.knowledge.routing-quality.v1
    file: entries/knowledge-routing-quality.md
    topic: AIKnowledge 最小路由、canonical 去重、新鲜度与错误预防
    routeKeys: [knowledge, knowledge-quality, knowledge-output, source-ref, content-hash, stale, canonical-entry, dedup, route-probe, misroute, bounded-output, evidence-boundary, permission-boundary]
    relatedSkills: [es-knowledge-creator, es-knowledge-validator, es-ai-knowledge-curation, es-aibrain-route-authoring, es-task-read-snapshot]
    requiredReads:
      - Documentation/AIKnowledge/entries/knowledge-routing-quality.md
      - Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
      - Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
      - Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
    authority: Derived from current project contracts and routing source
    evidenceLevel: S1
    contentHash: 2826a3e369a1936af0c84308ee957fba53abdb65dbc0eede09653f1d525efdf1
    staleWhen: 任一 SourceRef 哈希、Knowledge 条目合同、AIBrain 选择算法、用户定向低风险策略、AICommand 权限或验证器判定规则变化
```

应用前必须重新运行条目验证；若 ContentHash 变化，以新验证结果同时更新正文和绑定，禁止只改索引值。

## KnowledgeIndex 路由收窄

只修改索引投影，不删除领域条目正文或事实：

1. 从以下 11 个绑定的 `routeKeys` 删除通用 `knowledge`：
   - `es.unity.ui-canvas-layout.v1`
   - `es.project.editor-asset-authoring.v1`
   - `es.unity.editor-window-lifecycle-menu.v1`
   - `es.unity.editor-prefab-asset-transaction.v1`
   - `es.unity.editor-serialized-undo-dirty.v1`
   - `es.unity.editor.project-scene-builder-authority.v1`
   - `es.unity.compile-player-il2cpp-evidence.v1`
   - `es.unity.lifecycle-domain-reload.v1`
   - `es.unity.rendering-material-atlas.v1`
   - `es.unity.serialization-prefab-identity.v1`
   - `es.engineering.hot-path-container-performance-evidence.v1`
2. 从 `es.project.editor-asset-authoring.v1` 和 `es.unity.editor.project-scene-builder-authority.v1` 删除通用 `source-ref`。
3. 从 `es.skill.resource-index.v1` 删除 `knowledge-output` 和 `bounded-output`；Skill 资源条目继续通过 `skill/resource-index/catalog/...` 发现。

这些删除只消除跨域辅助键；每个条目仍保留多个本领域 routeKeys，因此不破坏其主要发现路径。应用后必须用 Knowledge Validator 确认正文与索引仍至少有一个 routeKey 交集。

## AIBRAIN_ENTRY 投影更新

注册时同时把 canonical 条目的状态行更新为“已注册的现行 Knowledge 质量治理条目”；不得保留“仅完成条目正文”的候选状态冒充当前事实。

将“Knowledge 输出、验证与条目治理”行的 RouteKeys 扩展为：

```text
knowledge, knowledge-quality, knowledge-output, validation, source-ref, content-hash, hash, routing, route-probe, misroute, canonical-entry, dedup, evidence, evidence-boundary, permission-boundary, bounded-output, stale
```

首选 Skills 保持：

```text
es-knowledge-creator, es-knowledge-validator, es-ai-knowledge-curation, es-aibrain-route-authoring
```

AIBRAIN_ENTRY 只保留发现投影；详细质量、去重和恢复规则继续由 canonical 条目拥有，不复制正文。

## 静态探针结果

按当前 `ESAIBrainCoordinator` 的“命中数降序、命中比例降序、KnowledgeId 升序、最多 3 条”算法进行内存回放，以上候选差异得到：

| 探针 | 预期首选 | 候选实际结果 | 判定 |
|---|---|---|---|
| 更新 AIKnowledge 并校验 SourceRef | routing-quality | routing-quality | pass |
| SourceRef 漂移后旧计划继续 | routing-quality | routing-quality, function-governance | pass |
| canonical 去重与误命中审计 | routing-quality | routing-quality | pass |
| 限制 Knowledge 输出并验证证据边界 | routing-quality | routing-quality | pass |
| Unity PlayMode 与发布证据 | scene-release-evidence | scene-release-evidence, function-release, fixture-visual-qa | pass |
| 飞书虚拟团队派发 | feishu-task-lifecycle | feishu-task-lifecycle | pass |
| Entity 输入与 Command 生命周期 | function-area-entity | function-area-entity, entity-runtime, function-lifecycle | pass |
| Skill Catalog 与证据合同 | skill-resource-index | skill-resource-index | pass |
| 文件快照哈希漂移 | task-read-snapshot | task-read-snapshot | pass |
| Prefab SerializedObject Undo/Dirty/Save | editor-asset-authoring | editor-asset-authoring | pass |

这是当前索引的静态内存回放结果；它证明路由选择结果，不证明 Unity Runtime、外部服务或发布行为。

## 验证与恢复

应用后必须按顺序执行：

1. `Test-ESKnowledgeEntry.ps1` 验证 canonical 条目 SourceRefs/ContentHash。
2. `Invoke-ESKnowledgeValidation.ps1 -Mode Entry` 验证唯一索引绑定。
3. `Invoke-ESKnowledgeValidation.ps1 -Mode All` 验证全量静态闭包。
4. 重放上述 10 个探针，并增加零命中、非法路径、重复绑定和 SourceRef 漂移用例。
5. 严格 UTF-8 与 `git diff --check`。
6. 单模型多视角对抗审查，单独报告 `runtime-not-run`。

任一步失败：保留 canonical 条目候选，不删除旧条目；撤销本批索引/AIBRAIN 投影差异或按当前文件重新规划，禁止修改源码事实来让 Knowledge 校验变绿。
