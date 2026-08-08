# Agent Authoring GraphView 任务交接包

## 交接身份与范围

- 项目根目录：`F:\aaProject\ESFrameWorkPublish`
- 窗口档案 ID：`ES-CODEX-20260804-222731`
- 对应生命历程：`ES/AI协作历程（Codex）/2026-08-04_222731_审计GraphV2_CommandSkill接入_记录生命周期与进度追踪.md`
- 该生命历程当前已记录至：`T034`
- 固定模块状态：`ES/Documentation/Status/MODULE_AUDIT_STATE.md`

用户的最终目标是一个可用、中文友好、操作流畅的 Agent Authoring GraphView：用户通过图编排 Goal、Reference、Constraint、OutputArtifact、Validation；系统将图烘焙为 AI 能理解的强类型需求，发送给现有 Codex 窗口；AI 只在候选目录生成 AICommand 与 Agent Skill；通过 UTF-8、结构、路径校验、Diff/Review 和人工批准后，才导入正式目录。

这不是 Command/Skill 行为运行图。不得实现第二套 Graph Runtime、Command Runner、Skill Runner、通用异步 Operation、Story Runtime 或 BehaviorTree Runtime。

## 已实现的源码事实

### 作者资产与图规则

- Graph Core：`Assets/Plugins/ES/1_Design/Graph/`
- GraphView V2：`Assets/Plugins/ES/Editor/ESGraphViewV2/`
- Agent Authoring DomainId：`es.agent-authoring`。
- Agent 资产菜单：`Assets/Create/【ES】/图与流程/Agent Authoring/...`。
- Agent Graph 默认保存：`Assets/ESNormalAssets/Data/AgentAuthoring/Graphs`；通用稳定图默认保存：`Assets/ESNormalAssets/Data/Graphs`。
- 完整需求思路图预设：10 个节点、16 条关系，含 Goal、2 个 Reference、Required/Forbidden/Permission/Quality、AICommand Output、Agent Skill Output、Validation。
- 语义端口固定为 `Context → Requirement → Artifact`，并由端口类型、Transition、Topology、Schema 锁定四层阻止乱连。
- 可使用 Inspector 的“修复领域端口规则”迁移旧 Agent 图，同时保留 NodeId、PortId、EdgeId。

### AI 生成与批准链路

```text
Graph
  → ESAgentArtifactGenerationSpec（含完整 relations）
  → generation-request.json + generation-prompt.md（严格 UTF-8）
  → ESCmdAgentWindow.OpenAndSendPrompt(...)
  → candidate/ 下的 AICommand / Agent Skill + candidate-manifest.json
  → UTF-8 / 路径 / AICommand / Skill 结构验证
  → Diff Review
  → 人工批准
  → 正式目录导入与 approval-report.md
```

- 入口和候选工作流：`ESAgentArtifactGenerationWorkflow.cs`。
- Graph Payload、领域 Validator、Baker、Asset Catalog、强类型 DTO：`ESAgentAuthoringGraphIntegration.cs`。
- Profile 和固定端口：`ESGraphAuthoringProfiles.cs`。
- Inspector 发送入口：`ESStableGraphInspector.cs`。
- Codex 发信稳定入口：`Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentWindow.cs` 的 `OpenAndSendPrompt`。
- Prompt 包含 Goal、上下文、Reference、四类 Constraint、Output 的输入/步骤/验收、Validation、relations 和 Mermaid 思路图；它明确说明关系不是运行时执行图。
- 中文标题、描述、规则、路径和验收文本必须原样保留，允许中文文件和目录名，禁止 U+FFFD。

## 用户体验与页面规范的当前实现

- 深色画布、节点类型主题色、端口语义颜色、Badge、圆角节点和较宽节点尺寸。
- Toolbar 包含 Agent 预设、自动布局、显式校验等操作。
- 自动布局按 DAG 深度分层；存在循环时拒绝布局并提示先修复。
- Reference、AICommand、Agent Skill 的资产发现按类型缓存；仅首次使用或点击刷新时扫描，禁止 Domain Reload/InitializeOnLoad 全盘扫描。
- Payload 编辑不逐字符重建整张图或全图校验，避免大图卡顿；用户可主动点击校验。
- Candidate Review 窗口含刷新候选、打开目录、差异预览与“人工批准并导入”；静态验证失败时不可批准。

## 已有验证证据

- Design、Editor、Tests 的隔离静态编译均通过：`0 warning / 0 error`。
- 中文 Payload → JSON → Graph → Baked Spec → Prompt 往返测试已加入。
- UTF-8 Guard 对核心 Graph/Workflow/Test/记录文件通过。
- 临时编译工程已删除。

下列证据仍缺失，不能宣称完成：Unity Editor 域重载、真实 GraphView 拖线和 Undo/Redo、EditMode Test Runner、中文输入法与字体、中文路径下真实候选导入、真实 Codex 生成、Candidate/Diff/Approve 端到端、Profiler、Player、IL2CPP。

## 接手后必须先读

1. `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
2. `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
3. `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
4. `.agents/skills/es-module-lifecycle/SKILL.md`
5. `.agents/skills/es-editor-tooling/SKILL.md`
6. `.agents/skills/es-utf8-guard/SKILL.md`
7. `.agents/skills/es-unity-compile/SKILL.md`
8. `Assets/Plugins/ES/AICommands/生成_AgentArtifact候选_AI命令.md`
9. `.agents/skills/es-generate-agent-artifacts/SKILL.md`
10. `.agents/skills/es-generate-agent-artifacts/references/generation-contract.md`
11. 当前窗口生命历程和 `ES/Documentation/Status/MODULE_AUDIT_STATE.md`

## 下一位 AI 的最小执行顺序

1. 不修改任何源码，先在 Unity 进行 Domain Reload，并检查 Console 中新产生的错误或警告。
2. 从 `Assets/Create/【ES】/图与流程/Agent Authoring/完整需求思路图` 创建资产，确认默认目录为 `Assets/ESNormalAssets/Data/AgentAuthoring/Graphs`。
3. 逐项确认操作手感：节点创建、搜索、框选、移动、缩放、MiniMap、拖线拒绝提示、删除、复制、Undo/Redo、自动布局、Inspector 字段、中文输入和资产刷新。
4. 用完整预设执行“立即校验”和“发送到 Codex”；检查生成请求、中文 Prompt、Mermaid 和 Candidate 目录。
5. 让 Codex 仅在 `candidate/` 生成一个 AICommand 与一个 Agent Skill；验证 `candidate-manifest.json`、UTF-8、结构与 Diff。
6. 人工检查 Diff 后再点击批准，确认正式落点符合 Graph Output，且 approval-report.md 存在。若任何验证失败，不允许导入。
7. 运行 EditMode Test Runner 中的 `ESAgentAuthoringGraphTests` 和 `ESGraphAssetTests`；必要时对本次修改重跑隔离静态编译与 UTF-8 Guard。
8. 仅在上述结果真实记录后，才更新模块状态；不要把静态编译当成 Unity 交互或端到端验收。

## 工作树与安全边界

- 工作树极脏，含资源系统、AIWarnings、Automation、Package、文档等其他任务的已修改和未跟踪文件。
- 当前 Agent Authoring 文件大部分亦处于未跟踪状态；它们属于本功能范围，应保留。
- 禁止 `git reset --hard`、`git checkout --`、`git clean` 或批量删除；不得修改生成的 `.csproj`。
- Command Runtime 的唯一驱动仍为现有 `MODULE_ESCommandModule` / `ESCommandPlayerRunner`；本图不得调用 `TickAll()` 或注入 Player。
- Skill 仍以 `ESSkillConfigKey` / GameCore Skill Table 为权威；不得持久化 RuntimeKey。

## 模块状态

| 模块 | 状态 | 接手判断 |
|---|---|---|
| `es-graph-authoring-bake` | `Implementing` | 源码和隔离编译存在，Unity 实机验收缺失 |
| `es-command-skills-graph-integration` | `Implementing` | 实际职责为 Agent Artifact Generation，端到端生成/批准验收缺失 |
| `es-command-runtime` | `Integrating` | 本轮未改动，保持唯一 Runner 边界 |
| `es-skill-definition-runtime` | `Integrating` | 本轮未改动；生成 Agent Skill 不等于改造游戏 Skill Runtime |

## 可直接发给下一位 AI 的任务说明

你接手 ESFramework 的 Agent Authoring GraphView。先完成 Unity Editor 验收，不要立即扩展 Runtime。目标是验证并完善“Graph 编排需求 → AI 可读 GenerationSpec/Prompt → Codex 仅生成候选 AICommand 与 Agent Skill → Validator → Diff/Review → 人工批准 → 正式导入”的闭环。优先处理操作手感、页面规范、中文输入/路径、错误提示与大图性能；保留现有 Command/Skill Runtime 权威，不新增 Runner 或 Graph Runtime。严格依据本交接包、当前生命历程、MODULE_AUDIT_STATE 与项目 AIWarnings 行动；先报告 Unity 实测事实，再决定最小修复。
