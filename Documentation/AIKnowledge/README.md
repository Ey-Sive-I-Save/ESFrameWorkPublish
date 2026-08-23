# ESFramework AIKnowledge

> 统一发现入口：`AIBRAIN_ENTRY.md`。所有 AI 组件先通过 AIBrain 生产力面发现，再按 `routeKeys` 定向规划。
>
> 状态：第一阶段基础库，面向 AIBrain 的定向检索与证据导航。
>
> 本目录不是 AIWarnings、源码、AICommand、Skill 或 Unity 验收日志的替代品。每条知识必须保留权威来源、内容哈希、证据等级和失效条件。

## 目标

AIKnowledge 解决“针对一个任务挑选最小相关知识集合”的问题，而不是把整个项目压缩成一份总结。

典型查询：

- 做 Weapon/Shot 热路径：只返回 ProjectileWeapon P0、热路径容器 P0、ItemShotPhysics、Pool 和对应验证 Skill。
- 做 Editor 工具：只返回 EditorLifecycle、Editor Common Sense、目标窗口专项和 `es-editor-tooling`。
- 做 Feishu 同步：只返回 AutomationCenter、凭据边界、外部适配器合同和 `es-feishu-cli`。

## 权威层级

```text
源码与真实验证证据
  > AIWarnings P0
  > AICommand 权限合同
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
└── entries/
    ├── aibrain-orchestration.md
    ├── authority-and-startup.md
    ├── skill-selection-and-quality-loop.md
    └── feishu-adapter-boundary.md
```

`.zread/wiki/` 如果后续生成，只作为代码导航缓存，不属于本目录的权威条目。
