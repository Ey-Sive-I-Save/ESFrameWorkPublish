# Agent Skills 与 AICommands 协作边界 AI 协作警告

> 状态：现行约束 + 已实现事实 + 后续能力展望。
> 最后核对：2026-08-16。
> 适用范围：`.agents/skills`、`Assets/Plugins/ES/AICommands`、`Assets/Plugins/ES/AIWarnings`、UnityMCP、确定性验证脚本。

## 当前结论

ESFramework 已采用项目级 Agent Skills。目录结构而非手写数量清单是发现权威：

```text
.agents/skills/
└── <skill-name>/
    ├── SKILL.md                 名称、触发边界与完整工作流权威
    └── agents/openai.yaml       可发现元数据
```

`.agents/skills` 是 Codex 支持的项目级 Skill 发现位置。它位于 Unity `Assets` 外，不由 AssetDatabase 导入，不生成 Unity `.meta`，也不得进入 AssetBundle、ResourcePlan 或 Player 发布内容。Codex 应从项目根启动；当注入清单遗漏一个明显匹配的项目 Skill 时，必须报告“清单注入缺口”并直接核对项目内对应 `SKILL.md`。新开窗口或重启只用于刷新发现状态，不能作为 Skill 不存在的证据。

四类协作入口职责不同：

| 入口 | 职责 | 能否授权修改 |
|---|---|---:|
| `AIWarnings` | 长期事实、P0 边界、禁止事项、证据标准 | 否；只定义约束 |
| `AICommands` | 受管通道的任务协议、输入范围、必读路径、风险与交付格式 | 否；当前用户指令授权动作，命令只约束选中的受管通道 |
| `.agents/skills` | 可复用执行工作流、工具调用方式、确定性脚本 | 否；Skill 自身不能扩大权限 |
| UnityMCP / PowerShell / 编译器 | 执行和采集证据 | 否；工具可调用不等于用户已授权 |

正确链路是：

```text
用户当前目标与动作授权
  -> 直接实施有界请求，或在需要受管通道时匹配一个 AICommand
  -> 已匹配时由 AICommand 约束该通道输入与回执；无匹配时记录 NoMatchingCommand，但不缩小用户请求
  -> Skill 提供可复用执行流程
  -> UnityMCP / 脚本 / 编译器执行
  -> 按证据等级交付
```

AIWarnings 在该链路中也必须按任务加载：先读取入口、当前状态与规则索引，再读取 AICommand 或任务命中的 P0 和专项原文。Skill 可以导航到相关规则，但不得递归加载整个 AIWarnings 目录，也不得用缓存摘要替代 P0、现行状态或当前专项。跨系统任务应分领域读取，并保留规则路径、状态、结论、禁止事项和证据入口。

## 当前 Skill 映射

| Skill | 当前能力 | 典型 AICommand 或任务 |
|---|---|---|
| `$es-codex-session-bootstrap` | 从固定项目根启动新 Codex、恢复或分叉已保存会话，并注入最小只读初始化提示 | 打开新 Codex、开启新对话、恢复对话、分叉会话、初始化 Codex、接手项目 |
| `$es-use-ai-command` | 校验 AICommand 的 UTF-8、元数据、目录和项目路径；选择并执行一个命令 | 用户发送 AICommand 路径、要求选择命令、进入项目任务 |
| `$es-unity-compile` | 区分 `.csproj`、Unity Console、Domain Reload、Test Runner、PlayMode、Profiler、IL2CPP 和发布证据 | `检查_编译错误定位`、`编译与ReloadDomain内存_检查`、程序集或 Unity 验收 |
| `$es-fix-compile-error` | 只定位、最小修复并验证一个明确编译错误 | `执行_修复单个编译错误_AI命令.md` |
| `$es-utf8-guard` | 严格 UTF-8、U+FFFD、疑似乱码与 scoped `git diff --check` | `检查_中文编码风险_AI命令.md`、所有文本修改 |
| `$es-worktree-audit` | 统计 staged、unstaged、untracked、deleted、renamed，并检查目标路径重叠 | `检查_脏工作树影响面_AI命令.md`、任何修改前后的工作区审计 |
| `$es-gamecore-integration` | 识别 GameCore 根 SO、RuntimeData、全局索引、静态模块与事务重注入边界 | GameCore 根接入、全局索引、RuntimeData 重注入、GameManager 模块 |
| `$es-resource-pipeline` | 贯通 AssetLibrary、Book、Catalog、ResourcePlan、Manifest、Provider、Scope 与发布资源链 | 资源治理、依赖分析、预览、导出和发布管线 |
| `$es-tag-config` | 维护 ESGameTag、ESTag 稳定引用、ConfigKey、Catalog、BakeTable 与 RuntimeKey | `新增GameTag_AI命令.md`、Tag 与配置稳定身份任务 |
| `$es-entity-authoring` | 按 Entity、角色 Prefab、DataInfo、部件、控制、运动和池化契约构建实体 | 玩家模板、角色层级、控制请求、Item/Shot/运动任务 |
| `$es-input-action` | 贯通 ActionId、元数据、绑定、Profile、RuntimeMode、输入服务与玩家消费链 | 新增输入动作、绑定缺失、RuntimeMode 和控制请求 |
| `$es-command-authoring` | 按 ESCommand 标准维护类型、分类、Context、Player、Runner 与生命周期 | ESCommand 上下文和新增运行时命令 |
| `$es-editor-tooling` | 开发 ReloadDomain 安全的窗口、Drawer、ESEditorSection、SO 表格和预览工具 | 编辑器窗口、序列化、预览、ReloadDomain 与 SimpleTools |
| `$es-release-acceptance` | 建立源码、Unity、测试、Profiler、Player、IL2CPP、Provider 和发布证据矩阵 | 发布、性能、资源生命周期与外部交付复核 |
| `$es-module-lifecycle` | 分类模块成熟度，审计默认激活和依赖渗透，并在用户确认后维护可失效的续接检查点 | `检查_模块成熟度与半成品影响_AI命令.md`、模块交付争议、重构前审计与跨窗口恢复 |
| `$es-first-principles-analysis` | 从事实、约束、权威和失败机制推导复杂方案 | 根因分析、复杂架构设计和设计到实现 |
| `$es-adversarial-review` | 对已完成方案、代码或交付执行只读反向验证 | 风险、漏洞、代码审查和商业可行性复核 |
| `$es-generate-agent-artifacts` | 从 Agent Authoring Graph 请求生成隔离的 AICommand/AISkill 候选包 | Graph 生成 AICommand 或项目 Agent Skill 候选 |
| `$es-start-estest` | 通过现有 Unity/Player/公开入口启动、监控或取消 ESTEST | 运行 ESAITest、ESTEST 或测试计划 |
| `$es-publish-aitest-prompt` | 向运行中的 ESAITest AI 投递一次性优先消息 | 通知测试 AI、补充当前测试要求 |

命令与 Skill 不是一对一关系。一个命令可以组合多个 Skill，例如修复 Unity 编译错误通常依次使用：

```text
$es-worktree-audit
  -> $es-use-ai-command
  -> $es-fix-compile-error
  -> $es-unity-compile
  -> $es-utf8-guard
```

这不代表多个 Skill 共同授予更大修改范围。权限来自当前用户明确指令；存在匹配合同时，唯一选中的 AICommand 只约束受管执行通道。不存在匹配合同时可报告 `NoMatchingCommand`，但不得套用无关合同，也不得据此缩小或延迟用户请求。

## 当前已实现事实

- 2026-08-16 的项目目录快照包含 20 个 Skill，均有 `SKILL.md` 与 `agents/openai.yaml`；该数字只用于本次对账，后续发现仍以实际目录为准。
- 当前能力覆盖会话交接、AICommand、编译与单错误修复、UTF-8、工作树、复杂分析、反向审查、Agent 候选生成、ESTEST 调度，以及 GameCore、资源、Tag/Config、Entity、输入、ESCommand、编辑器工具、模块治理和发布验收。
- `$es-unity-compile` 与 `$es-release-acceptance` 声明 `unityMCP` 依赖，服务地址为 `http://127.0.0.1:8080/mcp`。
- 已提供 AICommand 校验、显式 `.csproj` 构建、UTF-8 守卫、工作树审计、会话治理、候选生成和 ESTEST 调度等确定性 PowerShell 脚本；可用脚本以各 Skill 的实际 `scripts/` 目录为准，不维护固定总数。
- PowerShell 脚本不得依赖系统默认代码页。能使用 ASCII 时优先保持 ASCII；确需非 ASCII 文本时必须使用严格 UTF-8，并在声明支持的 Windows PowerShell / PowerShell 环境执行代表性验证。
- 旧批次的 `quick_validate.py` 结果不能自动覆盖新增或修改后的 Skill；每个变更仍须重新执行官方验证。当前机器可发现 `uv`，但“工具存在”不等于全部 Skill 已重新验证。
- 2026-08-16 的 AICommand 校验实跑结果为 54 个合同、2 个导航入口、目录 54 条、0 个无效项。

这些 Skill 提供的是正式可发现的项目导航、工作流、边界与交付协议，不等于其涉及的 Unity、Player、Profiler、IL2CPP 或发布流程已经自动化，也不等于所有 Skill 已完成全部真实场景前向测试。每次任务仍需读取当前 Skill、当前 AICommand、最新 AIWarnings 和源码。

## 绝对边界

- Skill 不得覆盖用户指令、P0 AIWarnings、当前源码或最新 Unity 证据。
- Skill 的存在不授予 AI 自行写文件、运行发布、上传远端、修改场景或维护协作历程；当前用户明确点名的动作仍是充分授权。
- 禁止把全部 AICommand 机械复制成同数量的 Skill；只有高频、稳定、可复用的执行能力才适合 Skill 化。
- 禁止把 Skill 放入 `Assets`，或让 Unity 运行时、资源管线、Player 依赖 `.agents/skills`。
- 禁止在 Skill 中携带隐蔽 DLL、预编译 Unity 业务程序集或自动修改项目的未审阅二进制。
- Skill 脚本必须说明输入、输出、写入范围和证据等级；默认优先只读。
- `.csproj` 构建脚本必须明确声明它不能替代 Unity Editor、Test Runner、PlayMode、Profiler、IL2CPP 或发布验收。
- 新增或修改 Skill 后必须运行严格 UTF-8 检查和官方 Skill 验证器；涉及脚本时必须实际执行代表性测试。

## Skills 能力展望

以下是适合继续 Skill 化的方向，不代表当前已经实现：

### 1. Unity 验收自动化

当前 `$es-release-acceptance` 已定义证据矩阵，后续可增加确定性脚本：自动读取 Unity 实例、等待编译、归档 Console、运行指定 EditMode/PlayMode 测试并生成机器可读结果。仍不得自动把测试结果升级为 Profiler、IL2CPP 或发布通过。

### 2. 更多领域专项 Skill

GameCore、资源、Tag/Config、Entity、输入、ESCommand、编辑器工具和发布验收已经具备第一版领域 Skill。后续只有在流程稳定且高频复用时，再考虑 Pool 生命周期、State/IK、Buff/ValueChange、存档或已完成源码验收的音频等专项 Skill；不得从提案文档直接生成“已实现”Skill。

### 3. 变更验收包

可把工作树影响、相关 AICommand、源码路径、Unity Console、测试结果、UTF-8 和本地台账证据组合成结构化验收包，供文档整合或外部审查使用。它不能自动推进 `ready-for-html` 或 `integrated`。

### 4. Unity 上下文采集

可通过 UnityMCP 获取选中对象、Console 错误、程序集、场景、Prefab、测试和运行状态，再把必要上下文交给 AICommand。默认只读；场景、资产和配置修改必须由当前用户明确点名。选择 AIBrain/Worker 受管通道时，再满足其 AICommand、TaskContract 与回执协议；这些协议不是第二次用户批准。

### 5. 团队分发

项目内稳定后，可将多个 Skill 与 MCP 依赖打包为 Plugin，提供统一安装与版本管理。Plugin 化属于分发方式升级，不改变 AIWarnings 与 AICommands 的权威边界。

## 验收标准

修改 Skills 或协作映射后至少确认：

```text
1. 从项目根能发现 .agents/skills 下的 Skill。
2. 每个 SKILL.md 的 name、description 和触发边界准确。
3. agents/openai.yaml 为合法 UTF-8/YAML，默认 Prompt 显式引用对应 $skill-name。
4. 官方 quick_validate.py 通过；Windows 执行时显式使用 PYTHONUTF8=1。
5. 脚本通过语法解析和代表性实跑。
6. AICommands 引用路径仍全部存在。
7. 没有把低层编译或脚本输出写成 Unity/Player/发布已通过。
```
