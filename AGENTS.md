# ESFramework Agent Portal

╔════════════════════════════════════════════════════════════════════╗
║ 🧭 ES AI 菜单入口                                                   ║
║ 说“菜单 / AI菜单 / 协作菜单 / 超级语义 / 路由”时，必须先展示完整菜单。       ║
║ 菜单含【1】～【7】主分类；AI机制图鉴内含超级语义与能力入口。              ║
║ 另有分类目录：R1.x=超级语义，R2.x=路由，R3.x=能力与边界。                 ║
║ 用户输入数字（如 7、7.1、R2.4）只选择入口，不代表已执行动作。           ║
╚════════════════════════════════════════════════════════════════════╝

## 超级语义命中回执（独立于常规路由）

- 每次处理用户输入时，先用 `.agents/scripts/Resolve-ESSuperSemantics.ps1` 对中央索引 `.agents/SUPER_SEMANTICS_REGISTRY.json` 做有界匹配；超级语义不是常规 Skill 路由推荐，两者不得混写。
- 这是每轮回复的前置步骤，不是仅供人工测试的脚本：任何项目对话入口都必须先完成该解析，再决定普通路由或正文；解析结果为 `triggered` 时，回执必须先于任何解释、菜单、Skill 披露或工具状态。
- 超级语义解析必须遵守文本采样门禁：短文本（不超过 `shortTextMaxChars`）全文解析；长文本只解析开头 `longTextHeadChars` 与末尾 `longTextTailChars`，中间内容不得参与触发。解析回执必须记录 `scan.mode`、原始字符数和实际扫描字符数。
- 唯一命中后，下一次面向用户的回复必须把 `displayLine` 作为第一条可见文本，固定格式为：`✨✅【已触发超级语义“XXX”】`。每个用户轮次最多展示一次；`XXX` 必须使用注册表中的稳定 `label`，不得自由改写。
- 未命中时不显示；多重命中时不得伪造“已触发”，应列出候选并让用户选择。命中回执只证明语义识别，不授予写入、Runtime、网络、Git、宿主或发布权限。
- `0分`/`零分`、`迭代`、`兼容` 是项目推进分叉口超级语义，不是普通路由：命中后必须显示超级语义回执，暂停自由发挥，先向用户确认目标、范围、禁止项和可观察验收信号。其注册定义位于 `.agents/references/project-progress-super-semantics.json`；`requiresDeepUserGuidance=true`、`allowAutonomousExpansion=false` 是硬约束。

## 首次进入项目：强制初始化协议

`AI_PROJECT_BOOTSTRAP: required`。当 AI 的当前目录位于本项目根目录或任意子目录时，必须按以下顺序执行一次，不能跳过或仅凭项目概览代替：

1. 定位本项目根目录并完整读取本文件。
2. 读取 `MUSTREADXXXXX/README.md` 和 `MUSTREADXXXXX/ES_AI_COLLAB_WELCOME.md`。
3. 检查当前适用的系统级/宿主级 `AGENTS.md` 是否包含“Unity 项目内的任意当前目录都必须先发现并完整读取项目级 AGENTS.md”规则。
4. 无法确认或缺失时，下一次面向用户的回答必须先展示 `ES_AI_COLLAB_WELCOME.md` 中完整的“系统级规则提醒框”，然后展示完整 ES 欢迎块并明确告知“项目协作上下文已建立”；正文全部结束后，再原样展示一次“系统级规则提醒框”。首尾各一次，仅首次初始化回答触发。不得用普通句子、缩写或埋在正文中的提示代替。未经用户明确授权，不得修改系统级文件。
5. 再读取本文件引用的 `ES/AISpace`、AIWarnings、AIKnowledge 和匹配 Skill 入口。

该协议适用于新建、恢复、交接和从项目任意子目录启动的 AI 对话。未完成第 1～4 步前，不得声称已完成项目学习或项目协作上下文初始化。

> 仓库级稳定入口：只保存不可绕过的不变量和权威路由。领域事实、状态枚举、工具参数与会话协议留在各自唯一权威中，不在此复制。

## 根约束

## 模型与系统边界

- 本框架不得假设用户使用的模型、模型供应商、Codex 宿主或 Agent 配置位置；模型种类可能不同，项目内规则必须保持模型无关。
- 默认所有强化只写入本项目仓库。任何可能影响系统级 Agent、Codex 全局配置、宿主设置或其他项目的方案，必须先向用户明确询问当前使用的模型/宿主，以及用户指定的配置位置；未得到回答前不得执行系统级写入。
- 用户指定模型后，只能在该模型实际可发现且用户指定的入口实施对应适配；不得把某一模型的特殊能力推断为所有模型都具备。
- 向用户提出模型确认时，必须说明原因：不同模型的 AGENTS.md 发现、Skill 注入、工具权限和上下文机制可能不同；项目内方案可以先继续，系统级适配需要模型信息。

## 宿主规则定位与“帮我完成”语义

- 当用户说“帮我完成”“帮我改好”并已给出明确项目目标、文件或报错时，视为对该项目目标的修改授权，不得再次要求用户从泛化菜单中选择“创建/迭代”；应直接读取项目入口、定位相关文件并开始有界实现。
- 当用户只说“帮我完成”且没有目标时，先显示当前菜单并要求一个数字；不得猜测具体业务动作。
- 涉及系统级规则时，先按当前宿主的可发现配置定位，不得把其他宿主路径当成通用事实。Codex 优先检查 `$env:USERPROFILE\\.codex\\AGENTS.md`（本机通常为 `C:\\Users\\<用户>\\.codex\\AGENTS.md`），再检查宿主明确提供的全局入口；路径存在后只需向用户确认“是否使用该文件（是/否）”。
- 用户确认“是”后，只能修改已发现且确认的文件；用户回答“否”或路径不存在时，不得改写父目录、盘符根目录或自行创建替代系统入口。项目内规则仍可继续执行。
- 官方资料未定义统一的跨宿主 `AGENTS.md` 文件路径，因此不得把 `C:\\Users\\AGENTS.md`、`F:\\AGENTS.md` 等猜测路径写成默认方案；宿主差异必须显式标记。
- 主流宿主的已核对入口记录在 `.agents/references/host-instruction-locations.json`；该文件只做路径发现，不授予写入权限。首次进入项目发现系统规则缺失且用户只发出简短请求时，必须先显示“检测到系统级规则缺失，是否写入已发现路径？是/否”，不得把“帮我完成”自动等同于系统写入授权。
- 仅当用户明确回答“是”后，才可将项目规则完整写入已确认的宿主文件；写入后必须启动一个新对话/窗口进行冷启动验证，验证项目 AGENTS、欢迎块和菜单均被读取。宿主不支持自动新窗口时，必须报告未完成，不能伪造成功。

## AI 项目指令发现点（系统级契约落点）

- `AI_PROJECT_INSTRUCTIONS_DISCOVERY: required`：本项目是 Unity 项目；任何 AI 进入项目后必须先完整读取本文件，再读取本文件明确引用的权威入口（涉及 AI 生成内容时包括 `ES/AISpace/README.md`）。该读取属于系统级 Agent 行为，不依赖任务提示词。

- AI 生成内容先访问 `ES/AISpace/README.md` 判断唯一归属：私有内容放 `ES/AISpace/Local`，协作内容放 `ES/AISpace/Public`；只有必须被 Unity 导入的公共内容才进入 `Assets/ES/Space/Public`，不要创建 `Assets/ES/Space/Local`。
- 强制发现入口：`MUSTREAD_PROJECT_INSTRUCTIONS/README.md`、`.agents/MUSTREAD_PROJECT_INSTRUCTIONS.md`、`ES/AISpace/MUSTREAD_PROJECT_INSTRUCTIONS.md`。它们是重复发现标记，不能替代本文件，但任何一个被发现后都必须回读本文件全文。
- 首次完整读取本文件后，下一次面向用户的回答必须先展示项目内 `MUSTREADXXXXX/ES_AI_COLLAB_WELCOME.md` 的 ES AI 协作流欢迎块，并明确告知用户“项目协作上下文已建立”；每个项目会话只展示一次。系统级文件仅在用户明确授权时才可建议或修改。
- 当前目录只要位于本 Unity 项目根目录之下，就与当前目录是项目根目录等价，必须执行完整 ES AI 协作流程；不得因位于 `Assets/`、`.agents/`、`ES/` 或其他子目录而降级为普通目录处理。
- 系统级发现规则缺失时，必须在回答开头明确提醒用户修改系统级 `AGENTS.md`，并告知只需增加一行：`Unity 项目内的任意当前目录都必须先发现并完整读取项目级 AGENTS.md。` 同时告知用户：AI 可以代为完成这项修改；不得静默降级。

- 所有项目路径相对当前仓库根解析；禁止写死盘符、用户名或本机绝对路径。
- PowerShell 读取中文或编码未知文本时显式使用 UTF-8；文本修改优先 `apply_patch`，禁止默认代码页覆写或机械转码。
- 执行 PowerShell 检查时优先复用单次、非交互调用，尽量避免嵌套或重复启动；在宿主可控时使用不置顶或后台方式，避免影响用户当前工作；无法保证时先提示。
- 下层 `AGENTS.md` 可增加领域正确性、验证和交付约束，但不得为当前用户的明确请求增加二次批准、把其降为候选/只读，或用 Skill、AICommand、TaskContract、AIBrain 计划和路径类别缩小授权。规则冲突只影响实现或证据结论时按较严格者执行；涉及授权时以当前用户明确指令为准。
- 简单寒暄、项目外问答、纯格式工作、只读路径确认和本文件的只读审查不触发完整发现链。

## P0 反馈与执行升级

- 以下三个事件彼此独立，任意一个出现都触发 `P0-feedback`：用户给出 0 分；用户明确要求“验证”；用户明确要求“运行 Skill/跑 Skill”。不要求三个条件同时出现。
- `P0-feedback` 触发后，禁止辩护、手工抬分、普通模板收尾、用计划或说明冒充执行、用静态描述冒充验证。必须复核用户意图、实际工具/差异/验证证据和未完成项，并报告真实阻断。
- 用户明确要求“验证”时，必须执行对应的有界验证；用户明确要求“运行 Skill”时，必须按项目路由读取并执行对应 Skill。无法执行时只能报告失败或阻断，不得声称已执行。
- 0 分或 P0 反馈只提升审查和证据等级，不自动授予写入、Runtime、宿主、网络、Git、删除或发布权限；这些副作用仍需单独授权。

## ES 阻断最小化原则

- 阻断是稀缺的安全信号，只能针对 P0 安全/数据完整性、权威或授权冲突、不可恢复身份/状态冲突，或当前目标的必要前置条件；不得把治理字段数量、报告数量或汇总计数当作阻断依据。
- 阻断必须是对象级、字段级、Profile 级或范围级，并携带稳定 reason code、触发谓词、影响、恢复动作和解除条件；`review`、`stale`、`runtime-not-run`、`degraded`、`unproven` 的既有分层沿用上方权威规则，不在此重复定义。
- 新增门禁不得自动成为全局阻断源。新增 Skill、合同、回执、预算、评分维度或验证器，必须声明必要性、影响范围、误阻断负例、替代路径和回退方式，并通过局部回归后才可限制对应 Profile。
- 核心链优先采用最小读取和最小验证集合；未知或高风险输入可以升级到扩展链，但扩展链的可选证据、全量新鲜度或非目标模块缺口不得阻断无关目标。用户授权与完成声明的边界继续遵循既有授权和证据规则。

## 权威路由

| 职责 | 权威入口 |
|---|---|
| 当前事实 | 当前源码、配置、测试与可重读真实回执 |
| 长期约束、S0-S6、交付声明 | `Assets/Plugins/ES/AIWarnings/` 中命中的 P0 与现行规则 |
| 知识发现 | `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`KnowledgeIndex.yaml` |
| 项目动作授权 | 用户当前明确目标、修改意图和单独点名的副作用动作 |
| 受管编排协议 | AICommand、TaskContract、AIBrain `planTask/runTask`；仅在选用对应执行通道时约束其输入和回执 |
| 可复用工作流 | `.agents/skills/<skill-name>/SKILL.md` |
| Automation 回执字段 | `ES/Automation/Contracts/*.schema.json` 与对应当前实现、验证器 |
| governed Skill 验证 | `.agents/skills/es-skill-governance/references/verification-semantics.md` |
| 商业组合报告 | `.agents/skills/es-skill-governance/references/commercial-coherence-contract.md` |

用户当前明确指令是项目动作的授权来源；AIKnowledge 只导航，Skill 只定义工作流，Catalog、索引、缓存和 UI 可见性均不授权。AICommand、TaskContract 与 AIBrain 计划不得冒充用户的二次审批，也不得扩大或缩小直接请求；当任务选择 AIBrain/Worker 通道时，它们仍可作为该通道的技术输入和可重放证据。状态必须带对象、字段、Profile 和范围；`ESAutomationRunStatus.Accepted`、`decisionStatus=Accepted` 与交付 `Accepted` 不等价。禁止跨合同压平 `Blocked`、`review`、`stale`、`runtime-not-run`；投影与当前 Schema、源码或 P0 冲突时只限制相应事实或完成声明，不撤销用户授权。

## 发现与 Skill

1. 需要依赖项目事实、跨系统设计或项目级验证时，读 `AIBRAIN_ENTRY.md`，再按对象、动作、风险和版本从 `KnowledgeIndex.yaml` 选择通常 1～3 个条目，读取其 `requiredReads`、正文和 SourceRefs；禁止递归加载全库。目标清晰的局部用户修改可先读目标文件、相邻实现和命中规则，不以 AIBrain/Knowledge 可用性作为开工许可。
2. SourceRef/ContentHash 漂移、索引不一致或 `StaleWhen` 命中时，只将相关 Knowledge 与依赖计划标为 `stale`，回读权威来源并重规划。无匹配路由时读 AIWarnings Start、CurrentStatus、RuleIndex 与当前源码，报告 `NoKnowledgeRoute`，不得借相邻知识补事实。
3. 在工作确实匹配 Skill 描述时增量读取精确 `SKILL.md`；明显命中但清单遗漏时报告“清单注入缺口”，不得为了授权而全量扫描 Skill 正文。
4. Skill 存在、发现资格、计划资格和运行证据与用户授权相互独立。Skill 的候选/只读/受管模式只描述该能力自行运行的边界，不得阻止 AI 在当前用户请求下直接完成同一目标。实际使用的 Skill 须在首次进度更新和最终答复披露；披露不等于验收。
5. 会话 New/Resume/Fork/Close/RestoreRecent，以及“交接窗口、窗口交接、让新 AI 接手、写入 AI 历程、准备交接”等等效意图，路由到 `es-codex-session-bootstrap`；能力漂移/理解过时路由到 `es-skill-session-refresh`，具体协议不在此复制。所有 Skill 的中文发现别名以 `.agents/SKILL_ROUTE_ALIASES.zh-CN.json` 为准，并由 `Test-ESChineseSkillRouteCoverage.ps1` 校验；AI 目标先经 `Resolve-ESChineseSkillRoute.ps1` 自发现，唯一命中才读取对应 Skill。多命中必须消歧，无命中报告 `NoSkillRoute`；RouteKey 分级处置以 `.agents/SKILL_ROUTE_DISPOSITION.json` 为准；别名和 RouteKey 都不授予权限。若项目 Skill 未注入清单，报告清单注入缺口并直接读取项目内对应 `SKILL.md`。
6. 每次完成任务的最终答复必须单独列出“本次使用 Skills：”，填写本轮实际执行的项目 Skill 名称及其本轮职责；未使用时明确写“无”。

## 用户授权、修改与 Git

- 只读检查可直接执行。用户当前明确提出的目标、修改意图或动作就是充分授权；它可以是有界目标而不必逐个列出文件。为完成该目标严格必要的项目内创建和修改属于直接范围，不要求 `planTask`、AICommand、TaskContract、风险白名单、文件数量预算或再次确认。
- `.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json` 是兼容文件名下的统一用户指令策略；验证器只可因缺少当前用户指令、计划目标超出已声明范围、路径越出项目根或动作未被单独点名而拒绝。`AGENTS.md`、`.agents/`、AIKnowledge、AIWarnings、AICommands、源码、`Assets/`、`ProjectSettings/`、`Packages/`、生成物和报告等路径类别只能用于风险披露，不能否决用户明确要求。
- 删除、重命名、Git 工作树/index/历史写入、Unity/Runtime、交互式或常驻进程、对外产生副作用的进程、网络、发布和凭据访问必须由用户明确点名该类动作，不能从普通创建/修改请求推导；一旦当前用户已明确点名，也不再要求项目内二次批准。为完成已授权目标严格必要的项目既有本地静态验证器、解析器、编译器和格式化器属于质量验证，可在有界输入、超时和无网络/无安装/无常驻副作用的条件下直接运行；不得由此推导 Unity/Runtime、服务启动或发布。选用 AIBrain、Facade 或 Worker 时，所需计划/合同是执行通道协议，不是额外用户许可。
- 用户未提出修改时保持只读；目标或动作有实质歧义时先澄清。禁止把“顺便清理、统一、重构、补文档、更新索引、写历程、运行、发布或 Git 操作”解释为默认附带工作。发现确有必要扩大范围时说明原因并取得当前用户指令。
- 修改前检查目标重叠并保留既有 staged、unstaged、untracked、删除和重命名；不覆盖、不回滚、不清理。未经逐项明确要求，不执行 `git add/commit/push/reset/rebase/checkout/clean`，不自动暂存生成物。
- 写入保持与目标相称且可恢复；严格 UTF-8、目标 diff、`git diff --check`、测试和证据检查属于质量验证，不是开工审批。验证失败会限制完成声明，但不把已明确的用户请求改写为未授权。

### 提交说明约定（轻量）

- 提交说明优先使用中文，直接写清“改了什么”或“解决了什么问题”；允许中英文、数字和常见标点混用。
- 不强制 `feat(scope):` 等 Conventional Commits 前缀，不强制固定长度、固定正文或单一模板；正文和关联 issue 按需要填写。
- 提交说明仍应避免空泛的“更新”“修改一下”等无法识别内容；若一次提交包含多个职责，至少概括主要范围。
- 文档静态站批次仍遵守其专用 ledger 校验；该校验不等同于提交说明格式检查，也不应拦截非文档提交。

## 证据与完成

- Static、Runtime、Release 独立；Skill Profile 按其验证合同，项目交付按命中的 P0，Automation 工件按自身 Schema。`runtime-not-run` 仅表示未执行，不是静态失败；静态证据不得证明 Unity、PlayMode、Profiler、Player、IL2CPP、网络、视觉、性能或发布行为。
- 完成声明列出实际范围、证据、未验证项、阻断和 non-claims；证据不足时只给分层局部结论，不得声称“完全通过”或“项目级整改完成”。
- 项目级治理、商业组合或发布就绪声明前，运行 `.agents/skills/es-skill-validator/scripts/Test-ESSkillPortfolio.ps1` 与 `.agents/skills/es-skill-governance/scripts/Test-ESCommercialCoherence.ps1`；报告未绑定当前 `AGENTS.md`、写入策略/验证器和源快照哈希时，只证明其实际覆盖的静态面，且不替代 Runtime/Release 验收。
- AI 协作历程、审计状态和发布状态仅在用户明确要求对应写入时维护；普通任务不得自动落账。受管通道合同可以校验其技术输入和回执，但不得增加第二次批准。
## 常驻任务收尾门禁

涉及 Skill、写入、验证、交接或完成声明时，收尾评价以 `es-ai-interaction-governance` 的 evidence-first closeout 为主：必须优先报告 `aligned/partial/misaligned/unverifiable`、观察证据计数、发现和未证实项；没有评估器真实结果时，提示/验证数字只能写“不可用”，不得手工估分。

涉及 Skill、写入、验证、交接或完成声明时，结尾使用短摘要：

🧩 使用Skill：本轮实际使用的项目 Skill；无则填“无”。
✍️ 写入：是否写入及范围；无则填“无”。
🧪 运行时：已运行、未运行或不适用。
⚠️ 未证实：关键未证明结论；无则填“无”。
🎯 提示评分：0-10，说明用户目标清晰度。
🔍 验证评分：0-10，说明本轮验证充分度。
🧭 目标清晰度：清晰、部分清晰或不清晰。
📌 下一步：必须是用户可输入的序号菜单，并且是本轮用户可见收尾的最后一项；最多 3 项，格式为 `1. ...`、`2. ...`、`3. ...`。只有一项也必须写 `1.`。该菜单后不得追加 Skill、验证、运行时或其他收尾字段。

详细证据由对应 Skill/验证器保存；字段细则以 `.agents/skills/es-codex-session-bootstrap/references/task-closeout-contract.md` 为准，会话启动或刷新时读取一次并按哈希缓存。
