# ESFramework 项目级 Agent 能力目录

> 协作基线：本目录的职责、证据等级、权限边界和维护入口见
> [`AI_COLLABORATION_BASELINE.md`](AI_COLLABORATION_BASELINE.md)。
> 该文件是 `.agents` 内的治理导航，不替代 `Assets/Plugins/ES/AIWarnings`、`AICommands` 或源码验证。

所有项目 AI 组件寻找 AIBrain 的统一入口：`Documentation/AIKnowledge/AIBRAIN_ENTRY.md`。
AISpace 放置与唯一权威入口：`ES/AISpace/README.md`；机器可读身份：
`ES/AISpace/AISPACE_AUTHORITY.json`。本目录只提供发现指针，不复制 AISpace 正文。

验证结论必须分层显示：`static-blocked` 才表示源码/边界需要修改；`runtime-blocked` 或 `runtime-not-run` 表示外部证据阶段尚未授权、未运行或不可用，不得让 AI 重写已经静态通过的代码。

本目录是 ESFramework 的项目级 Agent 执行能力入口。它只保存可被 Codex 发现和复用的 Skills，以及本目录的组织规范；不保存 Unity 运行时代码、AIWarnings 正文、AICommands 副本、会话历程或文档构建产物。

## 项目内完整归属

```text
ESFrameWorkPublish/
├── .agents/
│   ├── README.md                         # 本入口：Skill 分类与放置规范
│   └── skills/                           # Codex 项目级 Skill 固定发现路径
├── Assets/Plugins/ES/
│   ├── AIWarnings/                       # 长期事实、P0 边界和验收规则
│   └── AICommands/                       # 受管任务协议与执行模板
├── Documentation/                       # 稳定的开发者、架构和使用文档
└── ES/
    ├── AI协作历程（Codex）/               # 用户明确授权后维护的逐轮会话档案
    ├── Config/                           # Luban、SoTable 等项目级 ES 配置
    ├── Documentation/Status/             # 模块审计续接状态唯一固定入口
    ├── Documentation/StaticSite/         # HTML 文档、同步规则和本地更新台账
    ├── ResourcePipeline/                 # ES 资源管线外部产物
    ├── Tools/ 与 Tests/                  # 项目级工具、样例与夹具
    └── Output/ 与 Releases/              # 可交付输出和发布包
```

这些目录共同属于同一个项目，但承担不同权威职责。禁止为了“看起来集中”而复制或搬迁固定入口。

## 为什么不能全部搬进一个文件夹

| 固定位置 | 必须保留的原因 |
|---|---|
| `.agents/skills` | Codex 项目级 Skill 发现路径；从项目根启动时加载 |
| `Assets/Plugins/ES/AIWarnings` | Unity 资产体系和 ES 协作规则的现行权威入口 |
| `Assets/Plugins/ES/AICommands` | ESCmdAgent 在 Unity 内发现、校验和发送命令的来源 |
| `ES/AI协作历程（Codex）` | 一窗口一文件的会话档案边界，与执行能力隔离 |
| `ES/Documentation/StaticSite` | 文档哈希、台账和 HTML 延迟整合门禁 |
| `ES/Documentation/Status/MODULE_AUDIT_STATE.md` | 模块审计跨窗口续接的唯一导航状态文件 |

统一管理采用“一个索引、多个唯一权威位置”，不采用“复制多份再人工同步”。

## Skills 目录规则

所有 Skill 必须是 `.agents/skills` 的直接子目录：

```text
.agents/skills/<skill-name>/
├── SKILL.md                 # 必需：触发条件、工作流、边界和交付
├── agents/
│   └── openai.yaml          # 推荐：界面名称、简介、默认 Prompt、工具依赖
├── references/              # 可选：项目路径、领域规则和详细导航
├── scripts/                 # 可选：需要确定性执行的脚本
└── assets/                  # 可选：会被输出复用的模板或素材
```

约束：

1. Skill 文件夹名只使用小写字母、数字和连字符，并统一使用 `es-` 前缀。
2. 不在 `skills` 下增加 `base/`、`domain/` 等分类嵌套；分类、路由、状态和哈希由 `SKILL_RESOURCE_INDEX.yaml`、`SKILL_CATALOG.yaml` 与 `.agents/SKILL_REGISTRY.manifest.json` 表达；发现资格由 `.agents/SKILL_DISCOVERY_POLICY.json` 统一裁决。所有直接 Skill 的中文自然语言别名由 `.agents/SKILL_ROUTE_ALIASES.zh-CN.json` 注册，并用 `skills/es-skill-governance/scripts/Test-ESChineseSkillRouteCoverage.ps1` 校验；别名只负责发现，不授予权限。
3. 每个 Skill 只保留一个明确职责，不把多个领域揉成万能 Skill。
4. `SKILL.md` 保留核心步骤；详细项目路径进入一层 `references`，避免深层引用链。
5. 重复且需要可靠执行的流程才进入 `scripts`；脚本必须说明写入范围和证据等级。
6. Skill 内不创建 `README.md`、安装说明、更新日志或阶段总结；项目级说明统一放在本文件。
7. 不复制 AIWarnings 或 AICommands 正文。Skill 只引用实时权威文件。
8. 不携带隐蔽 DLL、Unity 业务程序集、会话日志、临时输出或发布产物。

## Skill 使用披露规范

当本轮实际使用项目或系统 Skill 时，AI 必须在首次用户可见的进度更新中声明所用 Skill 及其任务关联，并在最终答复中以“本轮使用的 Skill”列出实际影响工作的 Skill 与作用。不得把可用但未使用的 Skill 充入清单。

这项披露只说明工作流来源；它不替代用户当前指令或验证证据。AICommand、TaskContract 与 AIBrain 计划仅在选用相应受管通道时提供编排和回执，不是用户授权的前置条件。未使用任何 Skill 的纯文本答复不需要增加该段。

## 用户明确指令与项目修改

项目采用“当前用户指令直接授权、AI 推断扩张默认拒绝”的单一模型。用户已经明确目标、修改意图或动作时，实现该目标严格必要的项目内创建和修改可直接进行；源码、`Assets/`、`.agents/`、AIKnowledge、AIWarnings、AICommands、设置、生成物和报告不再按路径类型要求二次批准。AICommand、TaskContract、AIBrain 计划和 Skill 状态可服务受管编排与证据，但不能否决、缩小或延迟该请求。

`references/user-directed-low-risk-policy.json` 与 `Test-ESUserDirectedLowRiskPolicy.ps1` 保留旧名用于兼容，实际只检查声明的用户范围、计划目标、项目根包含关系和动作类型。文件数/字节阈值及路径类别只产生复核信号，不是授权黑名单。详细语义见 `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`。

用户未提出修改时保持只读。删除、重命名、Git 写、Unity/Runtime、交互式或常驻进程、对外产生副作用的进程、网络、发布和凭据访问必须被用户单独点名，不能由普通文件修改推导；一旦点名，不再要求项目内部二次批准。为完成已授权目标严格必要的项目既有本地静态验证器、解析器、编译器和格式化器属于常规质量验证，可在有界、超时、无网络、无安装和无常驻副作用的条件下直接运行。UTF-8、工作树保护、Diff 和证据门禁继续约束实现质量与完成声明。

## 全局 Static / Runtime 语义

所有 Skill 的验证必须分成两个独立轴：`Static` 只证明源码、配置、合同、哈希和确定性脚本可以证明的事实；`Runtime` 只证明 Unity、进程、显示器、时序、布局引擎或其他外部环境中的实际行为。`runtime-not-run` 表示证据尚未运行，不表示静态设计失败。`StaticReview` 可在 Runtime 未运行时完成；`RuntimeAcceptance` 和 `ReleaseAcceptance` 才要求对应的运行证据。完整规则见 `.agents/skills/es-skill-governance/references/verification-semantics.md`。

## 当前 Skill 简介

### 基础协作层

| Skill | 简介 |
|---|---|
| `$es-use-ai-command` | 在用户选择受管执行时发现、校验并使用一个 AICommand，作为编排与回执合同。 |
| `$es-skill-creator` | 以官方 Skill Creator 为底座创建、升级、验证和 forward-test 项目级 Skill；默认输出到 `.agents/skills/es-*`。 |
| `$es-unity-compile` | 区分 `.csproj`、Unity Console、ReloadDomain、Test Runner、PlayMode、Profiler、IL2CPP 与发布证据。 |
| `$es-fix-compile-error` | 定位、最小修复并验证一个明确的 C# 或 Unity 编译错误。 |
| `$es-utf8-guard` | 检查严格 UTF-8、U+FFFD、疑似乱码和补丁完整性。 |
| `$es-worktree-audit` | 审计 staged、unstaged、untracked、删除、重命名和目标路径重叠。 |
| `$es-codex-session-bootstrap` | 从项目根启动、恢复或分叉 Codex 会话，并加载最小权威初始化上下文。 |
| `$es-generate-agent-artifacts` | 按 Agent Authoring Graph 请求生成隔离的 AICommand 与 Agent Skill 候选包。 |
| `$es-ai-collaboration-menu` | 用户提出“菜单”时显示可发现、可选择、不会自动执行的 ES 制作/迭代/治理/验收协作菜单。 |
| `$es-start-estest` | 通过 Unity 菜单、Player 参数或公开 API 直接启动、监控和安全中断 ESTEST。 |
| `$es-publish-aitest-prompt` | 响应“你快告诉测试AI……”等自然语言，把一次性 P0–P4 提示快速投递到运行中的 ESTEST。 |

### ES 领域层

| Skill | 简介 |
|---|---|
| `$es-gamecore-integration` | 处理 GameCore 根 SO、RuntimeData、全局索引、静态模块与事务重注入。 |
| `$es-resource-pipeline` | 贯通 AssetLibrary、Book、Catalog、ResourcePlan、Manifest、Provider、Scope 与发布资源链。 |
| `$es-tag-config` | 维护 ESGameTag、ESTag、ConfigKey、Catalog、BakeTable 和稳定运行时身份。 |
| `$es-entity-authoring` | 按 Entity、角色 Prefab、DataInfo、部件、控制、运动与池化契约构建实体。 |
| `$es-input-action` | 处理 ActionId、绑定、Profile、RuntimeMode、输入服务和玩家控制消费链。 |
| `$es-command-authoring` | 按 ESCommand 标准维护命令类型、分类、Context、Player、Runner 和生命周期。 |
| `$es-editor-tooling` | 开发 ReloadDomain 安全的窗口、Drawer、ESEditorSection、SO 表格和预览工具。 |
| `$es-ui-prefab-authoring` | 制作高保真 Unity UI Prefab、场景内 UI、状态夹具和截图视觉验收；不负责运行时 UI 系统。 |
| `$es-release-acceptance` | 组织 Unity 编译、测试、Profiler、Player、IL2CPP、Provider 与发布证据矩阵。 |

### 跨系统治理层

| Skill | 简介 |
|---|---|
| `$es-module-lifecycle` | 响应“审计”“审计并记录”“继续审计”，分类模块成熟度并管理固定续接检查点。 |
| `$es-skill-governance` | 治理 Skill 的等级、权限、证据、规模与执行成本；处理慢启动、重复扫描/Hash、缓存及 Fast/Deep Path 边界。 |
| `$es-feishu-cli` | 规划并验收经 AIBrain 与精确 TaskContract 受管执行的飞书知识读取、任务监控、派发、虚拟团队夹具和进度推进；外部写必须由当前用户明确点名，受管执行仍先 DryRun 并把该指令绑定到单次动作，Skill 不授予直启 CLI 或实网权限。 |
| `$es-ai-knowledge-curation` | 从源码、AIWarnings、AICommands、Skills、测试和证据维护可追溯 Knowledge 条目与索引。 |
| `$es-knowledge-creator` | 限制 AIKnowledge 输出范围，按 route-pack/detailed-entry/full-audit 分级生成并校验来源、哈希和证据。 |
| `$es-knowledge-validator` | 独立、只读验证 Knowledge 条目与索引的来源哈希、身份、路由、RequiredRead 和相关 Skill 闭包。 |
| `$es-skill-validator` | 只读验证 Skill 的结构、治理、Catalog 哈希、安全信号与验收证据；不授予执行权限。 |
| Skill Portfolio Gate | 交付/发布前对全部直接 Skill 执行组合门禁；任一 Skill 失败或安全阻断都会阻止组合通过。 |

### 商业级协作闭环入口

当任务涉及“创建/重构/验收 Skill、AICommand、AIBrain、ES Automation 或 Knowledge”时，按以下顺序执行：

```text
1. 读取 Documentation/AIKnowledge/AIBRAIN_ENTRY.md
2. 读取对应 Skill 的 static-replay.manifest.json 与 static-specialized-acceptance.md
3. 运行 Test-ESStaticAcceptanceCoverage.ps1，确认职责静态验收基础完整
4. 运行 Test-ESCommercialCoherence.ps1，检查 Skill、AICommand、ES Automation、Knowledge 和交付跟踪
5. SourceRef 变化时先运行 Export-ESKnowledgeRefreshPlan.ps1
6. 仅对稳定来源显式运行 Invoke-ESKnowledgeStableRefresh.ps1 -Apply
7. Runtime 另行取得授权，不把 runtime-not-run 改写为失败或通过
```

商业组合报告位于 `ES/Output/Governance/commercial-coherence.json`。其中 `blocked` 表示合同或结构失败，`review` 表示证据新鲜度或版本控制待处理；两者不可混淆。

## 新文件放置决策

```text
是可复用 Agent 工作流？
  -> 新建或更新 .agents/skills/es-*/

是长期架构事实、P0 禁止事项或验收规则？
  -> Assets/Plugins/ES/AIWarnings/

是受管通道的一次任务输入、必读路径和交付模板？
  -> Assets/Plugins/ES/AICommands/

是稳定开发者文档或架构契约？
  -> Documentation/

是逐轮 AI 会话档案？
  -> 仅在用户明确授权后写入 ES/AI协作历程（Codex）/

是 HTML、文档哈希、同步门禁或本地整合台账？
  -> ES/Documentation/StaticSite/

是临时日志、缓存、构建中间物或试验输出？
  -> 不进入上述权威目录；使用既有 Temp、Logs、Library 或明确的输出目录，并遵守 Git 忽略规则。
```

## 整理与扩展流程

1. 先判断文件职责和唯一权威位置，再创建文件。
2. 搜索是否已有同职责 Skill、AIWarning、AICommand 或文档，优先扩展而不是复制。
3. 新增 Skill 时使用官方初始化器，保持直接子目录和标准内部结构；完成后必须依次更新
   `SKILL_CATALOG.yaml`、`.agents/SKILL_REGISTRY.manifest.json` 与
   `ES/AISpace/Public/Skills/registry.json`，再运行对应的只读关系验证。若 Skill 会生成或
   缓存内容，先在 `.agents/SKILL_AISPACE_BINDINGS.json` 注册稳定绑定，并运行
   `Test-ESSkillAISpaceBindings.py` 验证 Skill 合同与 AISpace 反向投影一致。
   新 Skill 的 `SKILL.md` 必须包含并遵循“Skill 使用披露”段，初始化器会生成该段；不得在定稿时删除。
4. 更新 AIWarnings/AICommands 的映射，但不复制 Skill 全文。
5. 运行官方 Skill 验证器、严格 UTF-8、项目路径检查和 AICommands 全库校验。
6. 在本地更新台账登记完成项；批次未到 `ready-for-html` 时不得提前修改 HTML。

## 当前边界

- 项目级 Skill 的存在性和数量以 `.agents/skills/*/SKILL.md` 的实际目录为准；分类与生命周期以 `SKILL_CATALOG.yaml` 为可校验导航快照，禁止声明固定总数。
- Skill 的验证状态以各自的官方验证记录为准。`$es-module-lifecycle` 已补充“审计”“审计并记录”“继续审计”、固定状态入口与一层续接状态契约，仍需按当前环境能力补跑官方 `quick_validate.py`。
- `$es-publish-aitest-prompt` 的确定性投递脚本已完成 PowerShell 语法、原子 JSON 和严格 UTF-8 代表性实跑；其他领域 Skill 目前以工作流和真实项目路径导航为主。
- 新 Skill 通常需要从项目根重启或新开 Codex 窗口后才会进入技能选择器。
- Skill 存在不代表 Unity、PlayMode、Profiler、IL2CPP 或真实发布已经通过。
- Skill 的 `discoveryState`、`planEligibility` 和 `runtimeEligibility` 必须分开理解；`candidate` 或 `operational-candidate` 不是 Accepted。
- `.agents/skills/es-skill-governance/scripts/Test-ESSkillArchitecture.ps1` 是组织架构门禁；它发现生命周期错配、通用路由污染和注册清单缺失时，Portfolio 不得报告完整通过。
