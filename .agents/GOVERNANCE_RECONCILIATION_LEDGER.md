# AI 协作治理盘点与保留台账

> 目标：为本次“强化 AI 协作能力”建立 target-owned 盘点。项目未提供外部 source governance root，因此这是建立/协调，不是跨项目复制。
> 盘点日期：2026-08-21

## 基线判断

| 项目 | 结论 | 依据 |
|---|---|---|
| Target | 当前项目根 | 用户指定项目；运行时从当前工作目录或 `ProjectRoot` 解析，不固化本机盘符 |
| Stack | Unity/C#，含 Editor、Runtime、Test Runner、资源与发布链 | `.sln/.csproj`、`Assets`、`Packages`、`ES/Tools` |
| Existing state | `partial + mature-but-needs-reconciliation` | AIWarnings/AICommands/.agents 均存在；治理入口此前缺少一份跨层基线 |
| Source | `absent` | 未提供外部源项目或源治理根 |
| Contract | 多个目标域合同；未为本轮创建新合同 | `Assets/Plugins/ES/AICommands/README.md`、规则索引的 `NoMatchingCommand` |

## 现有主动规则族处置

| 规则族 | 处置 | 目标责任与证据 |
|---|---|---|
| Start / CurrentStatus / RuleIndex / RouteCatalog | `keep + normalize-navigation` | AIWarnings Start 目录；由 `Test-ESAIWarnings.ps1` 检查路径、JSON、短状态和路由 |
| P0 Guardrails | `keep` | 目标 Unity 架构、编码、生命周期、性能、资源和交付边界；按命中路由读取原文 |
| Architecture / RuntimeOperations | `keep` | 目标模块事实与运行时风险；以源码、测试、Profiler 和运行闭环为证据 |
| EditorTooling / ValidationRelease | `keep + adapt-evidence` | 目标 Editor、Test Runner、Player/IL2CPP、资源发布证据；不把静态检查冒充实机验收 |
| Handover / Archive | `keep-separate` | 历史与提案不覆盖当前事实；保留原路径和时间范围 |
| AICommands | `keep + NoMatchingCommand fallback` | 合同定义权限；没有匹配合同不创建伪合同、不借用无关合同 |
| `.agents/skills/es-*` | `keep + route-by-smallest-scope` | Skill 只描述流程，不授予权限；按治理 Skill 规则验证 frontmatter、脚本和恢复路径 |
| AIBrain / ESAutomation | `keep + evidence-downgrade` | `ESAIBrainCoordinator` 已实现计划、路由和一次性授权；当前知识条目标注源码/静态证据，Unity、Worker、运行闭环仍需单独验收 |
| Session history | `keep-protected` | 仅用户明确授权时写入；不在本轮改写既有档案 |

## 本轮变更与保留映射

| 原有材料 | 保留位置 | 本轮处理 |
|---|---|---|
| `.agents/README.md` | 原路径不变 | 增加基线链接，不改既有目录规则 |
| AIWarnings Start 入口与索引 | 原路径不变 | 不复制、不重写 |
| AICommands 与 Skill 全文 | 原路径不变 | 不复制、不改写 |
| 既有交接/复盘/状态文件 | 原路径不变 | 不删除、不截断、不迁移 |

## 2026-08-24 用户直接授权模型协调

| 治理面 | 处置 | 机械证据 |
|---|---|---|
| 当前用户明确请求 | `normalize-in-place`：作为项目动作授权来源；只拒绝 AI 自主扩张、项目越界、实质歧义和未点名专项动作 | `user-directed-action-authority.md`、策略 JSON、策略验证器与 `.agents/tests/Test-ESUserDirectedActionAuthority.ps1` |
| AIBrain / AICommand / TaskContract | `keep + lane-separation`：保留 `ManagedAIBrain` 技术协议，不再作为 `CurrentUserDirect` 的二次批准 | Architecture 与组合报告中的 `authorizationLane` / `laneCoverage` |
| 商业组合门禁 | `normalize-in-place`：快照绑定入口、授权策略、验证器、回归和商业验证器自身；新增用户直接授权子检查 | `Test-ESCommercialCoherence.ps1` 与 `commercial-coherence-contract.md` |
| 版本化交付 | `keep + expand-tracking`：授权权威文件全部进入 delivery tracking；未跟踪状态保持 `review`，不伪装成代码阻断 | `commercial-coherence.json.checks.deliveryTracking` |

## 未导入与延期

- 未导入任何外部项目 P0、构建日志、发布声明、历史状态或源码路径：没有 source root，且这些内容不是可移植事实。
- 未创建新的 AICommand：本轮没有一个需要新增授权合同的重复工作流，现有 `NoMatchingCommand` 机制足够。
- 未创建单数 `.agent` 兼容目录：会造成并行发现入口，与项目 `AGENTS.md` 的 `.agents` 规范冲突。
- 延期：在干净工作树和 Unity 环境可用时，扩展并运行统一结构校验；当前只做文档层 Foundation，不宣称 S3+。
