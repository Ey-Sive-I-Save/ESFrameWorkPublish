# UI AIKnowledge 快速补强登记候选

状态：`CandidateOnly / PlanTaskUnavailable`。本文件不是正式索引、AIBrain 计划、Runtime 回执或发布证据。

## 本批目标

只补两个高收益 canonical owner：

1. 商业游戏 UI 屏幕族到当前十个通用 ScreenSpec 模板的受限映射。
2. UI 视觉 Token、层级、密度、状态与素材视觉合同。

候选正文位于 `candidate/Documentation/AIKnowledge/entries/`。审查期间正式 `KnowledgeIndex.yaml` 被并发修改：观测值从 67 条 / `9952bb91c57e5457a8b432815b606092cdad073a3731ad906951787c2f3bdc52` 变为 68 条 / `0b1191faf9c8873e56e713c84f8c5fd540f005252a875619d01e9a48d1e7b75b`、`ca0555eade8efe7961795d232016bff3ffe2118a1a66df7a768b97a0ac6aa9cc`、`bd549af4584f1e2f8ddf2b73731962b2d70da93faf3542d319f800227413cf27`，并在 `2026-08-23T16:20:03Z` 采样为 71 条 / `92f543739d1d7bf13e1d3608e7f2fc07cecf3ff60f209aab9bb11713aa834b28`。这些都只是历史观测，不是可复用的提升基线；本批没有修改正式条目、索引、AIBRAIN_ENTRY、`.agents`、`Assets` 或 Unity 源码。

## 权限阻断

- 唯一匹配命令：`knowledge.entry.update`，`L2 / controlled-execution / scoped-write`。
- 命令正文 SHA-256：`bb3e567392463be03caa4408ead2251287e8af560967e766a21fd0afc23bc486`。
- `KnowledgeIndex.yaml` 属于控制面，必须取得 AIBrain `planTask`、匹配 AICommand 和当前 TaskContract。
- 本机当前没有 Unity 进程消费 AI Inbox；只读 `listCapabilities` 请求 `e0c5b8d2f1a34c6f9e7d0b2a4c6e8f10` 仍待处理。
- 静态注册表未发现 `knowledge.entry.update` 对应的可调用 TaskContract；不得借用 `es.scene.scan` 或其他无关任务。

## 建议索引绑定

```yaml
  - knowledgeId: es.project.game-ui-screen-family-decisions.v1
    file: entries/game-ui-screen-family-decisions.md
    topic: 商业游戏 UI 屏幕族、信息架构与当前 ScreenSpec 模板映射
    routeKeys: [ui-automation, game-ui-screen-family, commercial-ui, hud-ui, inventory-ui, shop-ui, dialogue-ui, map-ui, progression-ui, result-ui, settings-ui, ui-information-architecture]
    relatedSkills: [es-ui-prefab-authoring, es-first-principles-analysis, es-adversarial-review, es-knowledge-creator]
    requiredReads:
      - Documentation/AIKnowledge/entries/game-ui-screen-family-decisions.md
      - Documentation/AIKnowledge/entries/ui-automation-authoring.md
      - .agents/skills/es-ui-prefab-authoring/SKILL.md
      - .agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json
      - .agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md
      - .agents/skills/es-ui-prefab-authoring/references/high-fidelity-ui-recipes.md
      - .agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py
      - .agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py
      - Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs
      - Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md
      - Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md
    authority: Derived from current UI authoring contracts and advisory references
    evidenceLevel: S0
    contentHash: f5b4b056c385d5412283bdc72febd1813158ac4f7f8b2c3867f4d3a14745fa58
    staleWhen: 组件注册表、ScreenSpec v3、Materializer 合同、商业 UI 参考、现有 UI canonical 条目或任一 SourceRef 哈希变化
  - knowledgeId: es.project.game-ui-visual-design-system.v1
    file: entries/game-ui-visual-design-system.md
    topic: 游戏 UI 视觉角色、Token、层级、密度与素材视觉合同
    routeKeys: [ui-automation, visual-qa, ui-visual-design, visual-design, design-token, color-role, typography-role, spacing-token, visual-hierarchy, information-density, rarity-visual, ui-material]
    relatedSkills: [es-ui-prefab-authoring, es-editor-tooling, es-knowledge-creator]
    requiredReads:
      - Documentation/AIKnowledge/entries/game-ui-visual-design-system.md
      - .agents/skills/es-ui-prefab-authoring/SKILL.md
      - .agents/skills/es-ui-prefab-authoring/references/commercial-ui-patterns.md
      - .agents/skills/es-ui-prefab-authoring/references/high-fidelity-ui-recipes.md
      - .agents/skills/es-ui-prefab-authoring/references/ai-visual-brief.md
      - .agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json
      - .agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py
      - .agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py
      - Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs
      - Documentation/ES_UI_AUTHORING_WORKFLOW.md
      - Documentation/AIKnowledge/entries/ui-automation-authoring.md
      - Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md
      - Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/unity-rendering-material-atlas.md
    authority: Derived from current UI authoring contracts and advisory references
    evidenceLevel: S0
    contentHash: bd830fc2270037fdd67c65d9e11cb83af48b1b1f0a2aba75c7f3dae068561807
    staleWhen: 视觉参考、ScreenSpec v3 token schema、字体/素材治理、项目渲染约束或任一 SourceRef 哈希变化
```

## 静态路由回放

按当前 `ESAIBrainCoordinator` 的“命中数降序、命中比例降序、KnowledgeId 升序、最多三条”规则，把两个候选绑定仅加入内存后得到：

| 探针 routeKeys | 首选 | 候选是否进入前三 | Top 3 |
|---|---|---|---|
| `ui-automation` | ScreenSpec Components | 是，屏幕族第 3 | ScreenSpec Components、UI Automation、Screen Family |
| `ui-automation, visual-qa` | UI Automation | 是，视觉设计第 2 | UI Automation、Visual Design |
| `inventory-ui, game-ui-screen-family` | Screen Family | 是，第 1 | Screen Family |
| `ui-visual-design, design-token` | Visual Design | 是，第 1 | Visual Design |

这只证明显式 routeKeys 和当前已有桥接键的静态排序，不证明自然语言推导已经完整，也不证明 AIBrain 已加载、登记或授权这些候选。`TryReadKnowledge` 会先按每个 route 的最大命中数过滤，因此 Top 3 不保证填满三个结果。

## 自然语言负向探针

| 目标文本 | 当前推导结果 | 候选结果 | 结论 |
|---|---|---|---|
| `背包界面` | 无 UI routeKey | 无法发现 Screen Family | 产品名词不能替代显式 `inventory-ui` / `game-ui-screen-family` |
| `UI 字体` | 仅 `ui-automation` | Visual Design 不进 Top 3 | 需要显式 `ui-visual-design` / `design-token` 或后续扩展自然语言投影 |
| `UI 颜色` | 仅 `ui-automation` | Visual Design 不进 Top 3 | 当前候选没有修复自然语言路由层 |

这些负向探针把本批能力限定为“显式 routeKeys 与现有组合桥接可用”。若目标是仅凭产品自然语言稳定发现两条知识，需要另行授权修改 `InferObjectiveRouteKeys`、对应测试与路由探针注册表。

## 登记前必须复核

1. 取得新的 PlanHash，并确认计划精确覆盖两个 `entries/*.md` 与 `KnowledgeIndex.yaml`。
2. 再次采样所有 SourceRef 和 `KnowledgeIndex.yaml`；任一变化即重算 ContentHash 并重新规划。
3. 将候选正文提升到正式路径后，原子添加两条索引绑定，不覆盖当前 staged/unstaged 变更。
4. 运行单条与全量 Knowledge Validator、严格 UTF-8、重复 KnowledgeId、requiredReads/relatedSkills 闭包和 `git diff --check`。
5. 重放屏幕族与视觉设计探针。`HUD/UI` 可经 `ui-automation` 桥接，含“视觉/visual”的 UI 请求可经 `visual-qa` 桥接；当前 `InferObjectiveRouteKeys` 仍不会从“背包/商店/技能树/设置/Token/字体”等词单独推导专用键，后续若要完整自然语言命中，需要独立授权修改 AIBrain 推导投影。
6. 保持 `runtime-not-run`：本批不证明 Unity、视觉、Profiler、Player、IL2CPP 或发布。
