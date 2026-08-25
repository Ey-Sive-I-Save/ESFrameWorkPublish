# 游戏 UI 知识的规范化适配层

`KnowledgeId`: `es.project.game-ui-normalized-adapter-layer.v1`  
`Authority`: `ES UI canonical contracts + external workflow calibration`  
`RouteKeys`: `ui-automation`, `normalized-adapter`, `canonical-owner`, `knowledge-deduplication`, `schema-adapter`, `ai-error-prevention`  
`HashSchema`: `v2`  
`ContentHash`: `7edcbf56b12d59a22661b65591f25eabdc0ff1935d067506a1a4547bccba26bc`  
`SourceSetHash`: `7edcbf56b12d59a22661b65591f25eabdc0ff1935d067506a1a4547bccba26bc`  
`EntryBodyHash`: `ab474ae491368334d52a4ba43da569af7878d6027d28ef677cd61cead4582bbc`  
`EvidenceLevel`: `S0`  
`StaleWhen`: 任一 canonical owner、ScreenSpec/AssetManifest/LayoutPlan/BehaviorSpec/Materializer 合同、外部来源快照或 SourceRef 哈希变化。  
`RuntimeEvidence`: `runtime-not-run`

## Scope and canonical ownership

本条目是外部 UI 资料、Unity 官方资料和 ES UI 合同之间的**去重适配层**。它只定义路由、归一化、
优先级和停止规则，不复制各 owner 的实现细节。一个事实只能有一个 canonical owner：

| 输入/问题 | ES canonical owner | 适配层输出 |
|---|---|---|
| 参考图、区域、观察、来源和哈希 | `es.project.game-ui-reference-design-evidence.v1` | `designEvidence` 引用，不把推断写成事实 |
| ScreenSpec、组件语义和稳定节点 ID | `es.project.ui-automation-authoring.v1` | `ScreenSpec v3` 候选 |
| 图标、字体、边框、稀有度、fallback 和许可 | `es.project.game-ui-asset-manifest.v1` | `AssetManifest` 引用 |
| Canvas、锚点、区域、响应式约束和冲突 | `es.unity.ui-canvas-layout.v1` | `LayoutPlan` 引用 |
| 状态、焦点、输入意图和切换 | `es.project.game-ui-behavior-focus-navigation.v1` | `BehaviorSpec`/`Fixture` 引用 |
| Prefab/Fixture Scene 物化 | `es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1` | 受治理的物化请求 |
| 结构快照、GPU 截图和人工视觉基线 | `es.editor.project-screen-spec-materializer.visual-evidence.v1` | 分层证据，不改变静态状态 |
| 开源方案的 IR、flow、source map、known-loss | `es.project.game-ui-open-source-automation-patterns.v1` | 外部校准，不成为 ES 事实 |

适配层只保留稳定 ID、输入哈希、版本、映射、冲突、缺失和证据边界。`topic`、自然语言别名、
第三方字段名和组件实现不得成为第二份权威事实。

## Normalization rules

1. 先按 `routeKeys` 和任务目标选择 owner，再读取该 owner；同义词只做路由别名，不复制条目。
2. 外部字段先进入 adapter mapping，再投影到 ES schema；未知字段必须为 `known-loss`、`approx` 或
   `Blocked`，不能静默删除。
3. 优先级固定为：当前 ES 源码/合同 > 项目锁定的 Unity 版本资料 > 固定 commit 的外部方案 > 模型推断。
4. 两个 owner 对同一字段给出不同定义时，不合并成折中值；保留来源、报告冲突，并停止正式物化。
5. 适配输出必须带 `sourceId/sourceHash`、`schemaVersion`、`canonicalOwner`、`mappingStatus` 和
   `evidenceBoundary`。输入或 owner 哈希变化时，所有下游映射和证据标记 `stale`。
6. prototype flow、按钮文案、截图外观只能产生 intent/状态候选；库存、经济、导航、网络和持久化
   必须通过明确的 ES Bridge，不由适配层生成。
7. 静态索引、Prefab 文件、validator 或跨后端截图只能证明对应层；Unity import、输入、GPU、PlayMode
   和发布证据必须分别取得。

## Normalized vocabulary

外部方案常用词只作为输入别名，不能直接生成同名 ES 类型：

| 外部别名 | 归一化对象 | 关键保留字段 |
|---|---|---|
| `frame` / `artboard` / `screen` | `ScreenSpec` | screenId、schemaVersion、sourceHash |
| `layer` / `node` / `element` | ScreenSpec node | stableNodeId、parentId、semanticRole |
| `component` / `instance` / `symbol` | Component Registry reference | componentId、capability、fallback |
| `image` / `icon` / `export` | `AssetManifest` item | assetId、contentHash、provenance、license |
| `constraint` / `auto-layout` / `responsive` | `LayoutPlan` rule | canvas/profile、anchor/pivot、axisOwner、safeArea |
| `prototype` / `flow` / `transition` | `BehaviorSpec` candidate | intent、state、focus、bridgeOwner |
| `prefab` / `materialize` / `import` | Materializer request | taskContract、writeScope、readiness |
| `screenshot` / `diff` / `baseline` | Visual Evidence record | source/profile/state/capture identity |

无法落入表中或同时落入两个 owner 的词，必须进入 `unknown/conflict`，不能由模型临时发明新的
KnowledgeId、Registry 或字段归属。

## Core AI failure firewall

| ID | AI 易错行为 | 防错检查 | 正确动作 |
|---|---|---|---|
| `UI-ADAPTER-001` | 为同一事实创建多个 Markdown/Registry 权威 | 查 canonical owner 和重复 `knowledgeId`/字段 | 只保留 owner，适配层存引用和映射 |
| `UI-ADAPTER-002` | 把外部 README 或 Unity 官方通用说明直接当 ES 合同 | 检查 source snapshot、版本、哈希和 authority | 先归一化，标记 external calibration |
| `UI-ADAPTER-003` | 把截图/flow 推断为业务语义或逻辑 | 检查 BehaviorSpec、Bridge owner 和 Fixture 边界 | 输出 intent/状态候选，缺 Bridge 则 `Blocked` |
| `UI-ADAPTER-004` | 未支持字段被静默丢失仍声称高保真 | 要求每字段 `render/approx/known-loss/structural` | 显式降级、告警或停止 |
| `UI-ADAPTER-005` | 静态生成成功被当成 Runtime/视觉通过 | 检查 evidence boundary 和各层回执 | 保持 `runtime-not-run`，等待 Unity/GPU/输入证据 |

## Adapter output contract

最小适配结果必须能够回放和审查：

```yaml
adapterVersion: ui-normalized-adapter.v1
sourceInputs:
  - sourceId: design-or-repository-id
    sourceHash: sha256
canonicalMappings:
  - externalField: external-name
    esOwner: screen-spec|asset-manifest|layout-plan|behavior-spec|materializer|evidence
    esPath: stable/path
    mappingStatus: render|approx|known-loss|structural|blocked
conflicts: []
evidenceBoundary: static-routing-only
```

该结构是候选交接，不授予写入 Prefab/Scene 或业务系统的权限；正式物化仍由 TaskContract、Materializer
和独立证据门决定。

## Verified facts, assumptions and non-claims

### Verified facts

- 当前 ES 已有 ScreenSpec、AssetManifest、BehaviorSpec、Fixture、Materializer 和视觉证据 owner。
- 开源 UI 方案提供 IR/packet、source map、flow、known-loss、conformance 等可复用模式；其仓库快照
  和哈希由 `game-ui-open-source-automation-patterns` 负责。
- Unity 官方资料是版本相关的技术参考，不能替代项目合同或运行时证据。

### Assumptions

- 当前目标以 Unity 2022.3 + UGUI 的高保真 Prefab/场景内 UI 为主。
- 适配层先服务 UI 自动化知识路由，真实业务 Bridge 后续独立接入。

### Non-claims

- 不声明外部方案或官方文档已在 ES 中完整实现。
- 不声明适配层能从单图自动恢复隐藏业务、所有响应式变体或 Unity Runtime 行为。
- 不声明任何静态验证、Prefab、Fixture、GPU、PlayMode、Profiler、Player、IL2CPP 或发布已通过。

## RequiredReads

- `Documentation/AIKnowledge/entries/game-ui-normalized-adapter-layer.md`
- `Documentation/AIKnowledge/entries/game-ui-open-source-automation-patterns.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `Documentation/AIKnowledge/entries/game-ui-reference-design-evidence.md`
- `Documentation/AIKnowledge/entries/game-ui-asset-manifest.md`
- `Documentation/AIKnowledge/entries/game-ui-behavior-focus-navigation.md`
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md`

## SourceRefs

- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`6785f682878cfaba2fb0f525e947eadace8cd8f31e5ba3cc0df62d3a4da5098d`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)
- `Documentation/AIKnowledge/UI/unity-ui-ai-failure-prevention.md` (`1f48ec5d7dc61214d6b1dd35bd90d0e656db0f543eb4e04d63d32d67e683ce81`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)

## Evidence boundary

本条目只提供静态知识归一化和路由防错；未执行第三方安装、Unity Editor、Prefab/Scene 导入、PlayMode、
GPU capture、输入交互、Profiler、Player、IL2CPP 或发布验收，统一为 `runtime-not-run`。
