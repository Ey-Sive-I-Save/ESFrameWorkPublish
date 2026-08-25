# ESFramework 项目级 Agent 内核提案

> **Status: proposal-only**  
> 本文件是 `AGENTS.md` 的对比稿，不是当前项目指令，不得被自动加载为权威入口。只有经过人工审查、迁移、验证并明确替换后，才可以进入项目根 `AGENTS.md`。

## 1. 目标与边界

本提案只解决项目级 Agent 治理入口的四件事：

1. 让新 Agent 能稳定发现项目事实、模块和可用能力。
2. 让知识条目可更新、可验证、可标记过期，而不是复制一份会漂移的摘要。
3. 让权限、证据和失败状态在入口处收口，不因 Skill、索引或缓存而扩大。
4. 让根文件保持短小，把领域知识和会话细节放回各自权威位置。

本提案不定义 Unity 运行时行为、不替代 AIWarnings P0、不授予 Git/Unity/发布/删除权限，也不规定某个模块的实现方案。

## 2. 稳定内核：根 `AGENTS.md` 应只保留这些规则

### 2.1 启动与发现

- 读取项目事实前，先读取 `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`。
- 再按任务的 **对象 + 动作 + 风险 + 版本** 匹配 `Documentation/AIKnowledge/KnowledgeIndex.yaml`。
- 默认只读取 1～3 个命中条目、其 `requiredReads` 和条目正文；不得递归加载全部 `entries/`。
- 没有命中路由时，回到 AIWarnings Start、CurrentStatus、RuleIndex 和当前权威来源，并报告 Knowledge 覆盖缺口。
- 任何项目事实包括源码、配置、资产、Prefab、Scene、Package、测试、AIWarnings、AICommands、Skills、运行时回执和发布证据。

### 2.2 权威与证据

事实裁决顺序固定为：

```text
当前源码/配置/测试/真实回执
  > 当前版本 Unity 官方文档、UnityCsReference、已安装包源码
  > AIWarnings P0 与领域规则
  > AICommand、TaskContract、Skill 合同
  > AIBrain 路由记录
  > AIKnowledge 条目与索引
  > 缓存、搜索摘要、模型记忆
```

- AIKnowledge 只负责导航和可追溯摘要，不是最终事实源。
- 静态证据不得宣称 Unity、PlayMode、Profiler、Player、IL2CPP、视觉或发布已通过。
- 每个正式条目必须有 `KnowledgeId`、`Authority`、`RouteKeys`、`RequiredReads`、`SourceRefs`、`ContentHash`、`EvidenceLevel`、`StaleWhen`；有验证事实时增加 `EvidenceRefs`。
- SourceRef 缺失、哈希漂移、索引绑定不一致、`StaleWhen` 命中或验证器 `blocked` 时，条目及依赖它的旧计划标记为 `stale`，先回读权威来源再继续。

### 2.3 权限与失败

- Skill 只提供工作流，不扩大源码、资产、Git、Unity、历史、审计、发布或删除权限。
- AICommand、TaskContract 和用户当前授权共同决定可执行范围；目录、索引和缓存均不授予权限。
- 只读检查可以直接执行；项目内明确限定的文档写入可在 `NoMatchingCommand` 下执行，但必须记录范围和验证结果。
- Unity、外部进程、发布、删除、历史/审计状态和跨模块写入必须使用匹配的 AICommand/计划合同；找不到合同则停止并报告缺口。
- 首次上下文接受的非零结果是 HardFailure；后续已接受上下文的缓存/信封丢失不回溯否定当前对话，也不得替换为另一份交接来源。

### 2.4 安全编辑

- PowerShell 读取中文或编码未知文件必须显式 `UTF8`；文本修改优先使用 `apply_patch`。
- 修改前做只读工作树审计；不覆盖、回滚或清理其他 Agent 的改动。
- 修改后至少执行 UTF-8 检查、`git diff --check` 和针对本次结构的静态验证。
- 所有输出都必须包含适用的 `non-claims`，明确哪些行为没有被验证。

## 3. 统一路由输入

每个任务在进入 AIBrain 前规范化为以下对象；缺失字段由 Agent 标记为未知，不得猜测：

```yaml
intent:
  object: "目标对象或模块"
  action: "inspect | design | change | validate | publish | handoff"
  risk: "read-only | documentation | scoped-write | unity | external | release | destructive"
  version: "Unity/包/协议版本；未知则 unknown"
  routeKeys: []
  requestedEvidence: []
```

`routeKeys` 是索引选择提示，不是 Skill 名称，也不是权限声明。路由器必须验证至少一个语义重叠，并把未闭合、重复或过时路由报告为发现失败。

## 4. KnowledgeIndex 模块注册表

`KnowledgeIndex.yaml` 继续作为机器可读注册表。每个模块或功能区使用唯一注册项，至少包含：

```yaml
moduleId: es.<area>.<name>.v<major>
scope: "源码/资产/编辑器/运行时/工程治理"
routeKeys: []
knowledgeIds: []
aiwarnings: []
relatedSkills: []
aiCommands: []
evidenceBoundary:
  static: []
  runtimeRequiredFor: []
status: active | provisional | stale | retired
owner: "维护责任"
sourcePolicy: "canonical source locations"
staleWhen: "可观察的失效条件"
```

约束：

- 一个事实只能有一个 canonical KnowledgeId；其他模块通过引用或投影使用，不复制正文。
- `AIBRAIN_ENTRY.md` 的人类可读功能区表与索引注册表必须由验证器做 route 闭合检查；任一路由只存在一侧都应失败。
- 新模块先注册 `provisional`，完成 SourceRef、ContentHash、最小 requiredReads 和验证后才能变为 `active`。
- 删除或退休条目保留重定向和历史原因，不复用旧 `KnowledgeId`。

## 5. 条目正文的 AI 可执行格式

每个条目只回答 Agent 在任务中需要作出的决定，推荐固定顺序：

1. **Use when**：触发条件与不适用条件。
2. **Read first**：最小前置读取及顺序。
3. **Canonical facts**：带 SourceRef 的当前事实。
4. **Decision checks**：可逐项执行的检查、分支和阻断条件。
5. **Allowed actions**：允许的只读/文档/受管写入范围。
6. **Failure and recovery**：stale、blocked、冲突或回滚处理。
7. **Evidence required**：静态、编辑器、运行时、发布证据的明确边界。
8. **Non-claims**：本条目不能证明的内容。

背景、历史和例子只能服务于上述决策，不得重复其他 canonical 条目的规则。

## 6. 更新协议

### 新增

先做 route 探针和重复事实检查，再创建 `provisional` 注册项、正文和 SourceRefs；完成 UTF-8、Schema、route 闭合和内容哈希验证后提交审查。

### 修改

先读取旧条目和全部 SourceRefs，确认当前工作树无冲突；只改动声明范围，更新 `ContentHash`、`staleWhen` 和验证回执。若事实归属改变，建立新 canonical 条目并保留旧条目重定向。

### 事实漂移

哈希漂移不允许静默刷新。先把条目和依赖计划标为 `stale`，回读当前权威来源，重新生成摘要和证据边界，再恢复 `active`。

### 退休

只能在存在替代 `KnowledgeId`、迁移映射和停止条件时退休；禁止物理删除以掩盖历史或验证失败。

## 7. 根文件迁移顺序

1. 将本提案与现行 `AGENTS.md` 做逐条差异审查，确认没有丢失硬门禁。
2. 先补齐索引注册表与 AIBRAIN 路由闭合验证器，再压缩根文件。
3. 将会话 New/Resume/Fork/Close 细节留在 `es-codex-session-bootstrap`，根文件仅保留条件性指针。
4. 将领域规则迁移到 Knowledge/AIWarnings/Skill 合同，并以 SourceRef 互链。
5. 在至少一次新会话、一次 stale、一次无匹配路由和一次权限阻断场景中做静态回放。
6. 只有所有验证通过并经人工确认，才考虑把提案内容拆回正式 `AGENTS.md`；本文件本身不触发替换。

## 8. 验收停止条件

以下任一项失败，停止迁移并保留提案状态：

- 根入口、AIBRAIN_ENTRY、KnowledgeIndex 的首读链不可解析。
- route 存在单侧、重复 canonical 事实或不可解释的覆盖缺口。
- 条目缺少 SourceRef/ContentHash/StaleWhen 或哈希与来源不一致。
- 任一静态证据被写成运行时、发布或性能结论。
- 权限边界无法区分只读、文档写入、Unity/外部进程和破坏性操作。
- UTF-8、差异完整性或工作树保护检查失败。

## 9. 当前对比时应特别关注

- 现行根文件中的 AIKnowledge 规则是否重复，以及是否把领域细节误放在根入口。
- `AIBRAIN_ENTRY.md` 与 `KnowledgeIndex.yaml` 是否对所有 routeKey 完全闭合。
- 当前索引中已知的未闭合路由、缺失 UI 路由和 ContentHash 不匹配，不应在重构中被静默掩盖；应作为独立 stale/coverage 缺口处理。
- 任何“已权威”“已验证”“可发布”表述，都必须能追溯到当前源码或真实回执。
