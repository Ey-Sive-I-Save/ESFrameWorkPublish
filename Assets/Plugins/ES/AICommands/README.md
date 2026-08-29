# ES AI Commands

## 一句话定位

AICommand 是 ESFramework 的“单任务受管动作合同层”：把一次明确动作绑定到稳定 ID、输入/输出、风险、写入范围、取消/恢复和证据回执；它不是万能工具、权限授予器，也不替代 Skill、Knowledge、AIBrain、TaskContract、SubAgent 或 AITalk。

合同的 `role`、`riskLevel`、`writeMode` 与业务优先级 P0/P1/P2/P3 独立：前者描述合同角色、风险和执行方式，后者描述业务优先级。

## ABCD/ABCC 职责投影

Catalog 的 `responsibilityBoundary` 与 `abcBindingProjection` 只声明职责和能力映射，不授予额外权限：AIBrain/ABCD 负责意图与计划，AICommand 负责 B 侧能力适配，Automation 负责受管执行与 RunRecord，ABCC 负责能力、状态和证据门控。缺能力统一 `blocked`，语义不匹配为 `replan`，证据不足只能 `claim-cap`；AICommand 不得自行创建权威计划、越过声明范围执行或把静态证据提升为 Runtime 验收。

合同完整性验证默认是 `report-only`，用于生成迁移画像；迁移批次完成后可调用 `Test-ESAICommands.ps1 -StrictCompleteness`。Strict 模式才会阻断多值风险、缺少取消/恢复/验证/证据标记的执行合同，以及缺少对称 allow/deny 边界的 `scoped-write` 合同。该开关不授予写入或 Runtime 权限。

> AIBrain 统一入口：`Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` 的 `listCapabilities -> planTask -> runTask`。功能区与 Skill 路由见 `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`。

## 在 Unity 中直接使用

```text
1. 打开【ES】/自动化与开发/Agent 与协作/打开 Agent 控制台。
2. 在输入区点击“选择 AICommand”，按标题、用途、关键词、风险和写入模式筛选。
3. 选择后，工作台只附加合同 ID、项目路径、短摘要和 SHA-256；不会把完整正文重复塞进消息。
4. 填写“本次需求”，按需拖入 Unity 资产、文件或截图，再点击“发送”。
5. 发送前工作台会重新读取目录和所选 Markdown：目录或正文 Hash 漂移、路径越界、编码错误都会阻止发送并要求重新选择。
```

`AICommandCatalog.json` 是唯一机器可读发现目录；`README.md` 与 `命令合集索引_AI命令.md` 只是导航文档，不属于可选择的任务合同。Markdown 正文是受管通道的执行合同，不是用户授权来源；目录和合同都不能扩大或缩小用户当前明确请求。

## 低上下文检索

常规 AI 不需要把 Catalog 当前全部命令和所有 Markdown 一次读入上下文。命令数量以 `AICommandCatalog.json` 的 `commands` 数组为准，随目录更新；优先从项目根执行：

```powershell
& .agents/skills/es-use-ai-command/scripts/Find-ESAICommands.ps1 `
  -ProjectRoot (Get-Location).Path -Query "资源 发布" -Json
```

该查询器只加载 `AICommandCatalog.json`，并校验目录条目的路径边界与重解析点；它不读取任何合同正文，硬性最多返回 6 条短候选。选定一条后，才读取那一份 Markdown 全文并重新计算 SHA-256。目录、候选摘要和 Skill 均不能代替用户指令；选用受管通道时也不能代替命令正文。

已知合同路径时可用精确校验，不必让 AI 读取完整目录：

```powershell
& .agents/skills/es-use-ai-command/scripts/Find-ESAICommands.ps1 `
  -ProjectRoot (Get-Location).Path `
  -CommandPath "Assets/Plugins/ES/AICommands/执行_修复单个编译错误_AI命令.md" -Json
```

`Test-ESAICommands.ps1` 是命令库维护与 CI 的全量门禁，会读取全部合同正文，验证正文权限语义、目录字段、UTF-8 与引用一致性；普通任务选择不运行它。因此“低上下文”只描述 AI 的发现输入，不表示整条维护/CI 链路只有一次 18KB 磁盘读取。

命令文件必须声明 `命令类型`、`默认改文件`、`风险等级`，其中列出的项目内路径必须真实存在。Unity 面板只负责发现、校验、组合和发送任务，不会绕过命令自身的权限边界。

## 统一合同标准（es.aicommand.single-task-contract.v1）

AICommand 的最小合同字段固定为：

```text
commandId              稳定命令 ID
命令类型               information / review / controlled-execution / candidate-generation / handover
默认改文件             默认写入、外部执行或“否”
风险等级               L1 / L2 / L3
输入 schema             必填字段、类型、范围和缺省值
输出 schema             ResultEnvelope 的状态、结果与错误
必读路径               规则、源码、配置和证据入口
执行边界               允许对象、路径、进程和禁止扩展
dry-run                预览行为与不会产生的副作用
确认                   需要确认的条件；不替代用户授权
取消                   取消时点、信号和停止等待
恢复/回滚              可重试、幂等、恢复点或回滚策略
验证命令               静态/Runtime 验证及其证据边界
evidenceRef            Receipt、ResultEnvelope 和来源哈希
```

正文是受管通道合同，Catalog 是唯一机器发现权威。每次受管执行还必须绑定当前用户授权、PlanHash、TaskContract、命令正文 SHA-256、唯一 invocation/idempotency key、写入范围和取消/恢复策略；任何绑定哈希漂移都必须重新规划。

## ES 受管流

```text
用户自然语言目标
  → AIBrain 推导对象 / 动作 / 风险与 PlanHash
  → AICommandCatalog 查询（关键词仅发现）
  → 唯一命令：读取正文并进入 planTask
  → 多命令：review / 消歧，不静默择一
  → 无命令：NoMatchingCommand
  → runTask：用户授权 + TaskContract + hash + idempotencyKey
  → Worker 执行与 CAS / Lease / 幂等
  → ResultEnvelope + Evidence Receipt
  → 静态、Runtime、Release 分层收尾
```

P0/P1/P2/P3 只表示导航优先级，不表示权限等级。真正的执行约束由 `role + riskLevel + writeMode + 用户授权 + PlanHash + TaskContract` 共同决定；Skill 只提供稳定工作流，AITalk 只做对话/批次编排，SubAgent 的每个有副作用子任务仍必须绑定一个 AICommand。

## 路由与拒绝语义

| 输入情况 | 固定结果 | 是否执行 |
|---|---|---|
| 唯一命令且输入完整 | 进入 `planTask`，生成 PlanHash | 通过合同后才可执行 |
| 多条候选 | `review` / 请求消歧 | 否 |
| 无候选 | `NoMatchingCommand` | 否 |
| 缺少必填输入或路径越界 | `InvalidInput` / `WriteScopeDenied` | 否 |
| 命令、Catalog、Skill 或计划 hash 漂移 | `StalePlan` | 否，重新规划 |
| 重复 invocation | 返回同一幂等结果或 `DuplicateInvocation` | 不重复产生副作用 |
| 中断 | `Cancelled` / `RecoveryRequired` | 按恢复合同继续 |

## 合同模板

新增或升级命令时，按以下顺序填写正文；领域规则只引用 AIWarnings/Knowledge，不在模板复制权威内容：

```markdown
# <标题>

命令 ID：`<stable.command-id>`
命令类型：<类型>
默认改文件：<否 / 允许写入范围 / 外部入口>
风险等级：L1

## 输入与输出 schema
## 必读路径
## 执行边界
## dry-run
## 确认
## 取消
## 恢复/回滚
## 验证命令
## evidenceRef
## 交付格式
```

模板不是权限来源；命令仍须注册 Catalog、通过正文/目录 hash 校验，并接受当前用户授权和对应 TaskContract。

## 分阶段迭代路线

1. **目录与导航收口**：Catalog 唯一发现、README 动态数量、索引覆盖检查保持 75/75。
2. **合同模板统一**：新命令必须使用上方字段；旧命令按变更触达逐步补齐，不进行无必要的大规模重写。
3. **自然语言发现强化**：AIBrain → Catalog → 唯一/歧义/无匹配三态，不允许按关键词静默换命令。
4. **受管执行强化**：授权、PlanHash、TaskContract、正文 hash、幂等键、写范围、取消和恢复全部进入 ResultEnvelope。
5. **AITalk/SubAgent 协作**：AITalk 负责批次编排，SubAgent 负责子任务，AICommand 保留每个动作的边界与证据。

## 最小静态验收矩阵

必须覆盖：正向唯一命令、歧义、多命令消歧、无匹配、缺输入、越权写入、旧 hash、重复 invocation、中断恢复，以及 command-id closure、TaskContract binding、write-scope denial、risk-level consistency、command-hash stale。静态验证不能推出 Unity、PlayMode、性能或发布结论；未运行 Runtime 时统一标记 `runtime-not-run`。

## 与 Agent Skills 协同

项目级 Skills 位于：

```text
.agents/skills/
```

项目内 AI 文件归属、Skill 目录规范和完整简介见 `.agents/README.md`。

用户当前明确指令决定“这次任务授权做什么”，AICommand 约束所选受管通道的输入、范围和回执，Skill 决定“这类任务怎样稳定执行”。二者都不能让 AI 自行扩大范围，也不能把用户请求降为候选或只读。

当前可用映射：

| 任务 | 推荐 Skill |
|---|---|
| 选择、校验并执行一个 AICommand | `$es-use-ai-command` |
| Unity 编译、Console、ReloadDomain 与证据分层 | `$es-unity-compile` |
| 修复一个明确编译错误 | `$es-fix-compile-error` |
| 中文文本、UTF-8、乱码和补丁检查 | `$es-utf8-guard` |
| 修改前后检查脏工作树和路径重叠 | `$es-worktree-audit` |
| 打开新 Codex、恢复/分叉会话或初始化项目接手上下文 | `$es-codex-session-bootstrap` |
| 从 Agent Authoring Graph 生成 AICommand/Agent Skill 候选 | `$es-generate-agent-artifacts` |
| 直接启动、监控或安全中断 ESAITest/ESTEST | `$es-start-estest` |
| “你快告诉测试AI……”或向运行中的测试 AI 快速发送一次性提示 | `$es-publish-aitest-prompt` |
| GameCore 根 SO、RuntimeData、全局索引或模块接入 | `$es-gamecore-integration` |
| 资源库、计划、Manifest、Provider、Scope 或发布资源链路 | `$es-resource-pipeline` |
| ESGameTag、ESTag、ConfigKey、Catalog 与稳定身份 | `$es-tag-config` |
| Entity、角色 Prefab、DataInfo、部件、运动与池化 | `$es-entity-authoring` |
| 输入动作、绑定、Profile、RuntimeMode 与玩家控制 | `$es-input-action` |
| ESCommand、分类、Context、Player 与 Runner | `$es-command-authoring` |
| EditorWindow、Drawer、ESEditorSection、SO 表格和 ReloadDomain | `$es-editor-tooling` |
| 编译到 IL2CPP、资源 Provider 与真实发布的证据矩阵 | `$es-release-acceptance` |
| “审计”“审计并记录”“继续审计”，以及模块成熟度、半成品影响与固定续接检查点 | `$es-module-lifecycle` |

从项目根启动 Codex 后，可以显式输入 `$skill-name`；也可以让 Codex 根据任务自动匹配。当前窗口未显示新 Skill 时，应新开项目窗口或重启，不要把未热加载误判为文件不存在。

模块范围明确时，用户只说“审计”即可启动只读成熟度审计；“审计并记录”会更新 `ES/Documentation/Status/MODULE_AUDIT_STATE.md` 的对应模块块；“继续审计”从该固定入口恢复并重新核对事实。

## 遗留清理判定

维护命令库时，先以 `AICommandCatalog.json` 的 `commands[].path` 与目录中的合同文件做双向集合比较；路径必须按完整行读取，以保留文件名中的空格。只有同时满足以下条件才允许删除：合同明确标记为过时、未被 Catalog/人工索引/源码引用，并且删除可通过版本控制或隔离备份恢复。任一条件无法由当前文件和可重读搜索证据证明时，结论固定为 `NoSafeDeletionCandidate`，保留对象并报告缺口。

合同正文若声明 `命令 ID` 或 `commandId`，其值必须与 Catalog 对应条目的 `id` 精确一致；发现不一致时先修复身份映射，再进行任何执行或清理判断。

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测任务。
2. 按用户当前指令决定是否改代码；只有选用本命令的受管通道时，才用“命令类型”和“默认改文件”约束该通道。
3. 先读取本文列出的必须规则文件；若文件不存在，要明确说明。
4. 执行前先确认当前工作树和相关入口文件，避免误改其他 AI 或用户的改动。
5. 只做用户请求内的事情；如果所选命令覆盖不足，可更换受管命令或直接在同一用户授权范围内实现，不得要求二次批准。
6. 结束时必须给出：已读规则、执行内容、改动文件、验证结果、剩余风险。
```

命令类型：信息补全。
默认改文件：否，除非用户要求调整 AICommands 规范。
风险等级：L1。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/通用架构理解_跨系统纠偏_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/GameCoreGlobalData与AICommands_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md
```

## 执行要求

```text
说明 AICommands 的定位、命令类型、风险等级、使用方式，以及当前可复用 Skill；不得把 Skill 能力写成额外授权。
```

## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：用短句说明做了什么或发现什么。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：无需编译
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```
