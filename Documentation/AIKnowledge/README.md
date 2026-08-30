# ESFramework AIKnowledge

> 统一发现入口：`AIBRAIN_ENTRY.md`。所有 AI 组件先通过 AIBrain 生产力面发现，再按 `routeKeys` 定向规划。
>
> 状态：第一阶段基础库，面向 AIBrain 的定向检索与证据导航。
>
> 本目录不是 AIWarnings、源码、AICommand、Skill 或 Unity 验收日志的替代品。每条知识必须保留权威来源、内容哈希、证据等级和失效条件。

## 目标

AIKnowledge 解决“针对一个任务挑选最小相关知识集合”的问题，而不是把整个项目压缩成一份总结。

三态落地可用性评分规范见：[`tools/ESKnowledgeEffectivenessRubric.md`](tools/ESKnowledgeEffectivenessRubric.md)。
本轮初步三态观察见：[`reports/knowledge-routing-quality-three-state-preliminary.md`](reports/knowledge-routing-quality-three-state-preliminary.md)；该报告不是正式验收证据。

WebPageStudio 知识覆盖与缺口入口见：[`WebKnowledgeCoverageMatrix.yaml`](WebKnowledgeCoverageMatrix.yaml)。该矩阵只做静态路由、证据层和缺口导航，不把 runtime-not-run 或外部资料 Deferred 升级为通过。
覆盖矩阵静态审计：`Documentation/AIKnowledge/tools/Test-ESWebKnowledgeCoverage.ps1 -ProjectRoot . -Json`。
外部官方资料校准请求包：[`WebKnowledgeExternalSourcePlan.yaml`](WebKnowledgeExternalSourcePlan.yaml)；官方静态快照已建立，后续仍需版本/许可证复核与运行时验证。
计划边界审计：`Documentation/AIKnowledge/tools/Test-ESWebKnowledgeExternalSourcePlan.ps1 -ProjectRoot . -Json`。
Web 统一静态门禁：`Documentation/AIKnowledge/tools/Test-ESWebKnowledgeStaticGate.ps1 -ProjectRoot . -Json`。

## 使用理念：以落地决策为中心

Knowledge 的价值不是替 AI 读完全部源码，也不是让 AI 背诵摘要；它应当用最小的、可追溯的路由集合，
把 AI 引向足以做出当前决策的源码、Schema、测试和风险边界。使用 Knowledge 后允许减少源码读取深度，
但必须显式报告尚未覆盖的调用方、生命周期、并发、迁移、回滚或运行行为。

- 评价优先看 `DecisionUtility`：能否选对动作、阻止危险动作、在失败或漂移时停下并恢复。
- 同时记录 `EvidenceCoverage` 和 `ReadCost`：少读源码是 Knowledge 的效率收益，不自动等于证据错误；
  覆盖不足要作为 `CoverageGap` 报告。
- Knowledge 是导航和边界层，不替代当前源码、AIWarnings P0、Schema、测试或真实回执；摘要不能冒充
  项目权威，`runtime-not-run` 不能冒充运行通过。
- Knowledge 新增的内容只有在改变实际决策、减少误操作或补足版本边界时才算增益；引用数量、哈希数量、
  文本长度和通用网络背景本身不加分。
- 三态比较应区分“项目源码基线”“项目源码 + Knowledge”“再加外部权威资料”，并把 Knowledge 带来的
  路由、治理、停止条件和读取成本变化单列，不能把更浅的源码覆盖误判为事实冲突。

典型查询：

- 做 Weapon/Shot 热路径：只返回 ProjectileWeapon P0、热路径容器 P0、ItemShotPhysics、Pool 和对应验证 Skill。
- 做 Editor 工具：只返回 EditorLifecycle、Editor Common Sense、目标窗口专项和 `es-editor-tooling`。
- 做 Feishu 同步：只返回 AutomationCenter、凭据边界、外部适配器合同和 `es-feishu-cli`。

## 权威层级

```text
源码与真实验证证据
  > AIWarnings P0
  > 当前用户明确动作授权
  > AICommand / TaskContract 受管通道协议
  > AIBrain 编排记录
  > AIKnowledge 摘要与索引
  > zread / Feishu 缓存
```

AIKnowledge 条目只能引用权威内容，不能把摘要伪装成权威内容。

## 条目最小合同

每个条目应包含：

- `KnowledgeId`：稳定 ID，不使用显示名作为身份。
- `Topic`、`Summary`：面向任务的短事实，不写泛化口号。
- `Authority`：`Source`、`AIWarnings`、`AICommand`、`Evidence` 或 `Derived`。
- `RouteKeys`：任务路由键，用于 AIBrain 精确筛选。
- `RequiredReads`、`RelatedSkills`：触发后应读取的规则与 Skill。
- 被治理的正式 Skill 可提供 `governance.json`，声明等级、成熟度、交付结论、证据等级、风险类别和 AIBrain 计划要求；它是可校验元数据，不是权限令牌。
- `SourceRefs`、`EvidenceRefs`、`ContentHash`：可回读、可校验、可判 stale。第一阶段的 `ContentHash` 是按稳定排序的 SourceRef SHA-256 集合再次计算出的 SHA-256，不对条目正文做自引用哈希。
- `StaleWhen`：源码、规则、HEAD、证据或外部版本变化时的失效条件。

## 生命周期

```text
采集来源 -> 规范化条目 -> 哈希与路由校验 -> AIBrain 检索 -> 任务执行 -> 证据回写 -> 标记 stale/复核
```

第一阶段只建立文档和索引，不把 AIKnowledge 接入 Unity Runtime，也不自动改写 AIWarnings。

AIWarnings 规模化采集使用 `AIWarningsDomainInventory.yaml`（稳定、可审阅的目录统计）和 `AIWarningsGeneratedInventory.json`（脚本按当前文件哈希生成的详细清单）。生成脚本为 `.agents/skills/es-ai-knowledge-curation/scripts/Build-ESAIWarningsInventory.ps1`；它只读取 AIWarnings，写入目标文件时使用 UTF-8 无 BOM，并以目录/文件哈希漂移作为 stale 信号。

## 日粒度新鲜度筛查

AIWarnings 与 AIKnowledge 的受管文件由 `AIKnowledgeFreshness.json` 做集中式快照，记录内容哈希变化观察日（`lastModifiedDate`，精确到日），不伪造语义审查时间。默认规则是 `ageDays > 7` 标记 `stale`；当前文件哈希与快照不一致标记 `drift`，必须先刷新并重新核对来源。`generatedAtUtc` 只表示生成时间，不参与新鲜度判断。

```powershell
.agents/skills/es-ai-knowledge-curation/scripts/Update-ESAIKnowledgeFreshness.ps1 -ProjectRoot . -AsOfDate 2026-08-27
.agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIKnowledgeFreshness.ps1 -ProjectRoot . -AsOfDate 2026-08-27
```

具体字段、范围、初始化估计和非声明见 `AIKnowledgeFreshnessContract.md`。筛查结果只决定优先回读范围，不替代 `StaleWhen`、SourceRef/ContentHash、AIWarnings P0 或 Runtime/Release 验收。

## 详细源码知识包

AIWarnings 只负责给出风险、禁止事项和路由提示。下列条目从提示回到当前源码、测试定义和配置实现，记录真实数据流、所有权、生命周期、失败模式、验证入口与尚未取得的证据：

- `gamecore-identity-registration.md`
- `resource-pipeline-runtime.md`
- `entity-input-command-runtime.md`
- `automation-aibrain-graph.md`
- `editor-workbench-authoring.md`
- `shot-performance-evidence.md`
- `state-buff-tag-value-arbitration.md`
- `pool-operation-skill-lifecycle.md`
- `audio-vfx-runtime.md`
- `runtime-ui-window-current-state.md`
- `shader-atlas-rendering.md`
- `context-interaction-runtime.md`
- `story-world-runtime-authoring.md`
- `scene-release-evidence.md`
- `game-manager-save-transaction.md`
- `vehicle-mount-motion.md`

这些条目以源码事实为主体，AIWarnings 为解释和门禁来源。源码存在只证明实现表面存在；没有对应 Unity/Test/Profiler/Player 回执时，条目必须保留 S1 和明确 non-claim。

## 目录

```text
Documentation/AIKnowledge/
├── README.md
├── KnowledgeIndex.yaml
├── AIWarningsDomainInventory.yaml
├── AIWarningsGeneratedInventory.json
├── AIKnowledgeFreshness.json
├── AIKnowledgeFreshnessContract.md
├── WebKnowledgeCoverageMatrix.yaml
└── entries/
    ├── aibrain-orchestration.md
    ├── authority-and-startup.md
    ├── skill-selection-and-quality-loop.md
    └── feishu-adapter-boundary.md
```

`.zread/wiki/` 如果后续生成，只作为代码导航缓存，不属于本目录的权威条目。
