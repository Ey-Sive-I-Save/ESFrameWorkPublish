# ES AIWarnings 协作入口
Status: current
StableId: es.aiwarnings.start.readme.v1
Authority: ESFramework AIWarnings / startup route
RouteKeys: aiwarnings, start, current-status, rule-index, p0, knowledge, skills, evidence
Applicability: 所有需要读取 AIWarnings、AIKnowledge 或项目规则的 ESFramework 任务
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-start-readme.md`
StaleWhen: Start 链、AIBRAIN_ENTRY、RuleIndex 或 AIWarnings 路由协议变化
Knowledge: `es.aiwarning.start.readme.v1`

AIBrain 统一知识入口：`Documentation/AIKnowledge/AIBRAIN_ENTRY.md`；需要编排时遵循其受管路由，本入口仍是 P0 与规则人工权威。

## 不可绕过的最小读取链
1. 本入口 → `当前状态（CurrentStatus）.md`（短活跃索引）→ `规则索引（RuleIndex）.md`。
2. 读取命中的 P0 原文，再读取当前任务对应的架构/运行时/编辑器/验证专项原文。
3. 按需读取直接关联的 Handover 或 Archive；最后回读当前源码、工作树，并按风险验证。
4. 命中项目 Skill 时读取 `.agents/skills/<skill>/SKILL.md`；Skill 提供工作流，不扩大授权。

## 上下文与路由边界
- 禁止把“开始任务”解释为递归读取全部 Warning；按 RuleIndex、明确 AICommand 或任务命中关系分批读取，摘要不能替代原文。
- `CurrentStatus` 只保存活跃模块、路由状态和证据入口；详细日志放在真实回执或按需历史中。Handover/Archive 默认不全量加载。
- `AIWarningsRouteCatalog.json` 是机器路由投影；Markdown 索引和当前源码才是事实权威。`reserved` 路由不代表模块、API 或授权存在。

## 目录分层
- `10_P0最高约束`：最高优先级长期约束；`20_架构现状`：职责边界；`30_运行时专项`、`40_编辑器与工具`：按任务读取；`50_验证与发布`：验收必读。
- `80_交接与复盘` 仅作历史参考；`90_提案与废止` 不是现行事实。

## 当前长期边界
- 文本严格 UTF-8；RuntimeKey 不持久化；Tag 使用 ESTagCollection 的 Host/Lease 所有权；资源寻址以 Manifest/Table/Bundle Index 为准。
- 资源生命周期区分 Resident、Owner Scope、ResourcePlan、Temporary 引用与独立 Lease；普通任务只释放自己的 Lease。
- GameManager Module 使用 `TryGetModule<T>()`；GameCore 不反向引用 Prefab/GameObject/Scene；编辑器初始化优先 AssemblyStream，禁止域重载全盘扫描。
- 热路径须先做 Prepare/静态门禁和分配清单；无 Profiler 不得宣称 0 GC 或商业级签收。测试场景导视复用 ESSceneValidationGuide，不污染正式 Prefab。
- AIWarnings 保存长期约束/事实/验收规则；AICommands 是受管执行协议；Skills 是工作流；AI 协作历程只有用户明确要求时才维护。

维护本入口时必须保留状态、StableId、Authority、RouteKeys 和 EvidenceRef；冲突以 P0、当前源码和最新验收证据为准。完整启动导航和长期约束映射见 Knowledge。
