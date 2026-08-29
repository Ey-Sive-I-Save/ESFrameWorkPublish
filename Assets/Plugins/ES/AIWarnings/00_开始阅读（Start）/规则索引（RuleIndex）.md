# AIWarnings 规则索引
Status: current
StableId: es.aiwarnings.start.rule-index.v1
Authority: ESFramework AIWarnings / routing index
RouteKeys: aiwarnings, rule-index, p0, architecture, runtime, editor, validation, handover, skills
Applicability: AIWarnings 任务命中、P0/专项规则选择和最小上下文读取
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-rule-index.md`
StaleWhen: AIWarnings 文件、RouteCatalog、P0/专项目录或 Skill 路由变化
Knowledge: `es.aiwarning.rule-index.v1`

## 加载规则
1. 所有文本/源码修改先读 UTF-8 P0；再按任务命中行读取 P0 与领域专项原文。
2. 多领域任务分批读取；Handover 只在决策背景/失败复盘/交接需要时读取，Archive 只在提案/废止评审时读取。
3. 预算只是建议，不能跳过命中的 P0、CurrentStatus 或专项原文；摘要、Catalog 和搜索片段不能替代权威原文。

## 主要路由分组
- GameCore/Identity/内容注册：GameCore、ConfigKey、RuntimeData、稳定 Key、事务边界、Info/Group/Pack。
- 运行时资源/性能/战斗：ResourcePlan、Scope、Manifest、Provider、Pool、Item、Shot、Projectile、Tag、Buff、State/IK、Input、Audio、VFX、Profile。
- 编辑器/工具/资产：EditorLifecycle、菜单/窗口、SimpleTools、SO Table、Shader/Material、Workbench、AssetPackage、Graph V2、序列化与第三方包。
- 验证/交付/协作：实际可玩闭环、AI 交付声明、Runtime/Profiler/Player/IL2CPP/发布、AICommand/Agent Skill、模块成熟度、测试场景、Codex 会话。

## 当前路由硬边界
- 普通路由均为 `current`；`reserved` 只预留未来读取边界，不表示模块、API、AICommand、注册或授权存在。
- `AIWarningsRouteCatalog.json` 是机器投影；人工仍从本索引和命中的 Markdown 原文进入。无匹配 AICommand 时标记 `NoMatchingCommand`，不得套用无关合同。
- RuleIndex 是路由入口，不是全目录阅读清单；禁止因“熟悉项目”递归加载全部 AIWarnings。

## 不可省略的特殊入口
- 修改 EditorWindow/工具/UI：EditorLifecycle、编辑器扩展 AI 常识和对应菜单/工具专项。
- 修改资源加载/发布：RuntimeAssets 与 ValidationRelease；修改 Shot/热路径：RuntimePerformance 与 Pool。
- 修改 Agent Skill/AICommand/交接/Codex：AgentSkills 边界、AI 交付声明、session/交接 P0 和对应 Skill。
- 任何完成声明、可用性或验证评分：AI 交付声明 P0，并区分源码、静态、Unity、Runtime 和 Release 证据。

## Skill 快速路由
GameCore→`es-gamecore-integration`；资源→`es-resource-pipeline`；Tag→`es-tag-config`；Entity→`es-entity-authoring`；Input→`es-input-action`；Command→`es-command-authoring`；Editor→`es-editor-tooling`；Unity/发布→`es-release-acceptance`；模块审计→`es-module-lifecycle`；Codex 会话→`es-codex-session-bootstrap`。

完整逐项任务映射、必读路径和预留路由矩阵见 Knowledge；任何实现仍必须回读命中的 P0、专项原文与当前源码。
