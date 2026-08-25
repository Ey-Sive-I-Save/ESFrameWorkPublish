# ESFramework AI 协作基线

> 状态：现行治理导航（Foundation）  
> 版本：2026-08-21  
> 权威组合：项目 `AGENTS.md` + `Assets/Plugins/ES/AIWarnings` + `Assets/Plugins/ES/AICommands` + `.agents/skills`

## 目的与边界

本基线把 AI 协作拆成四种职责，避免规则、权限和工具相互冒充：

| 层 | 唯一职责 | 权威入口 |
|---|---|---|
| 长期规则 | 架构事实、P0 禁止事项、验收标准 | `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）` |
| 用户授权 | 当前用户明确目标、修改意图和单独点名的副作用动作 | 当前用户指令 |
| 任务合同 | 受管执行的输入、边界、必读路径和交付格式；不是二次用户审批 | `Assets/Plugins/ES/AICommands` |
| 可复用流程 | 触发条件、步骤、失败恢复和确定性脚本 | `.agents/skills/es-*/SKILL.md` |
| 编排控制面 | 按 routeKeys 发现知识、校验 Skill/Command、生成计划并签发一次性执行许可 | `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` |
| 证据与状态 | 当前导航、模块成熟度、证据等级、交付结论 | `AIWarnings/当前状态（CurrentStatus）.md`、`ES/Documentation/Status` |

`.agent` 不是项目入口；从项目根启动时使用 `.agents`。不得创建同职责的单数目录、第二份索引或第二份当前状态。

## 标准协作循环

1. 读取根 `AGENTS.md`，再读取 AIWarnings `README -> CurrentStatus -> RuleIndex -> 命中规则`。
2. 审计工作树、目标路径和现有变更；不把脏工作树当作本次任务产物。
3. 用户当前明确目标或动作直接授权其有界范围。只有选用受管执行时才选择唯一匹配的 AICommand；没有匹配项记录 `NoMatchingCommand`，它不缩小用户请求。
4. 若经 AIBrain 编排，先执行 `planTask`，再由 `runTask` 消费与 Invocation 绑定的计划；这是该通道的技术协议与证据，不是额外用户批准。直接项目修改不以该通道可用为前提。
5. 选择最小职责的 `es-*` Skill。Skill 不授予 AI 自行扩张，也不得限制当前用户已经授权的目标；候选/只读模式只约束 Skill 自主执行。
6. 先做最小实现，再按风险补静态检查、编译、Unity/Test Runner、运行时或发布证据。
7. 交付时分开报告：`ModuleMaturity`、`EvidenceLevel`、`DeliveryVerdict`；源码存在或静态通过不能代替运行/发布证据。

## 证据与声明门禁

- `S0`：仅阅读/推理；`S1`：源码或静态结构检查；`S2`：确定性脚本通过；`S3`：编译或测试；`S4`：Unity 编辑器/运行时闭环；`S5`：Profiler、Player/IL2CPP 或资源发布；`S6`：经责任人确认的交付/发布证据。
- 只能声明已收集到的最高证据级别；缺失 Unity、设备、Profiler、IL2CPP 或发布验证时，必须显式标为未验证。
- “完成”至少包含变更范围、验证命令、结果、未验证项和阻断原因；不得用历史交接或 AI 摘要替代当前源码与工作树。

## 维护与失效

- 规则、合同、Skill、工具和会话档案不得互相复制正文；引用实时权威路径。
- 路由目录是机器可读投影，人工入口仍是 `RuleIndex.md`。
- 当 HEAD、目标路径、接口或验证环境改变时，相关 `evidenceRef` 视为 stale，需重新验证。
- 修改 `.agents/skills` 后至少运行对应 `quick_validate.py`（如可用）、严格 UTF-8 检查和项目级 AIWarnings/AICommands 校验。
- 会话档案只在用户明确要求时写入；普通任务不自动记录历程。

## 当前风险与下一步

项目已有 AIBrain 第一阶段生产力面、完整规则族和大量领域 Skill；AIBrain 当前证据仍主要是源码与静态构建，不能宣称 Unity/Worker/运行闭环已验收。本轮只补治理基线，不宣称所有模块、Skill 或 AICommand 已达到 `Stable`/`Released`。下一次最小增量是把 `Test-ESAIWarnings.ps1` 扩展为同时检查 AIBrain Knowledge/SourceRef 哈希、Skill frontmatter、AICommand 路径引用和 UTF-8，再在干净工作树上复跑。

盘点与保留关系见 [`GOVERNANCE_RECONCILIATION_LEDGER.md`](GOVERNANCE_RECONCILIATION_LEDGER.md)。
