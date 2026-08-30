---
name: es-ai-space-organization
description: >-
  Govern the placement of AI-generated project content in ES/AISpace Local or
  Public and Unity-facing Assets/ES/AISpace/Public. Use when an AI creates,
  discovers, or organizes generated files and folders, especially when paths
  are duplicated or unclear. This Skill guides classification; it does not
  authorize deletion, renaming, Git, Unity, network, or release actions.
---

# ES AI Space 目录归属

## 目标

让每个 AI 协作索引和迁移标记只有一个明确归属，避免在 `ES/AISpace`、`Assets`、
`Documentation`、AITalk 和 Automation 之间复制或随意新建同义目录。

## 唯一入口

先访问：

```text
ES/AISpace/README.md
```

它是 AI 生成内容的放置导航；长期规则、AITalk、Automation、Skill 和
Unity 业务资产仍以各自既有权威入口为准。

机器可读身份固定为 `ES/AISpace/AISPACE_AUTHORITY.json`。`README.md` 是唯一 AISpace
正文；MUSTREAD 标记、Public 索引和 `Assets/ES/AISpace/Public` 只能指向或投影该正文，
不得形成第二个根。AISpace 不使用运行时租约或 last-write-wins；身份/正文冲突时先重读
当前入口并拒绝陈旧覆盖。

静态验收断言标识：`local-public-routing`、`local-temp-routing`、`unity-boundary`、
`authority-preservation`、`no-delete-by-default`、`skill-aispace-binding`、`stale-marking`、
`single-canonical-authority`、`non-redundant-body`、`no-competing-root`、
`no-runtime-competition`、`discovery-closure`。

统一工具链入口：`scripts/Invoke-ESAISpaceToolchain.ps1`。它按
`references/toolchain-contract.md` 固定顺序串联权威、临时策略、Skill 双向绑定、关系投影
和绑定 Skill 入口检查；任一失败即非零退出。默认只读，`-ReportPath` 仅允许项目相对回执路径。

## 外部注册的唯一工作流权威

当任务涉及 UserSpace/People 的外部注册、更新、发现或验证时，本 Skill 是工作流权威，
不是 `README.md` 或 AICommand 文档。AI 必须依次：

1. 读取本 Skill 与 `ES/AISpace/AISPACE_AUTHORITY.json` 的 `workflowAuthority`。
2. 读取并校验 `ES/Automation/Contracts/es-userspace-profile-v1.json` 与匹配 AICommand。
3. 先执行 `Discover`/`Validate`；只有当前用户明确要求时才执行 `Initialize`/`Update`。
4. `Update` 必须携带当前 `ExpectedRevision`，并按 Worker 返回的 revision/contentHash 复核。
5. 输出受管回执；不得把文档中的示例命令当作绕过 Skill 的执行授权。

AICommand 文档仅是合同指针和用户可读说明；若文档与本 Skill、合同或机器身份冲突，
以 Skill → 合同 → 机器权威顺序为准并停止执行。

## 分类规则

```text
仅当前 AI/当前机器的内容       -> ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>/
需要其他 AI 审阅的协作内容     -> ES/AISpace/Public/<category>/<YYYYMMDD>/<topic-or-task>/
必须被 Unity 导入或引用        -> Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/<domain>/
AITalk 会话/消息/共识         -> Assets/Plugins/ES/AITalk/
Codex 历史与交接               -> ES/AI协作历程（Codex）/
Automation 合同/回执/候选      -> ES/Automation/
长期 P0、架构事实、验收规则    -> Assets/Plugins/ES/AIWarnings/
可复用工作流                  -> .agents/skills/es-*/
```

不要创建 `Assets/ES/AISpace/Local`。Unity 会导入该目录，它不是私密隔离。
旧路径 `Assets/ES/Space` 已完成迁移，不得作为新内容落点。
不要把 AITalk、历史、Automation 或 AIWarnings 复制到 AISpace。

大量临时文件（包括截图、录屏、缓存和失败重试产物）统一放入
`ES/AISpace/Local/Screenshots/<YYYYMMDD>/<agent-or-task>/`，其他内容先分类再进入
`ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>/`，默认截图目录为
`Temp/Screenshots`。Local 根目录不得散落临时文件；需要协作审阅时，正文进入现有
`ES/UIEvidence` 或所属测试证据目录，Public 只保存索引。只有 Unity 必须导入或引用的
正式参考图、测试夹具截图才保留在 Assets 的既有目录；`Assets/Screenshots` 不是 AI 临时
截图默认位置。机器可读约束见 `ES/AISpace/Public/LOCAL_TEMP_POLICY.json`；静态合同检查使用
`scripts/Test-ESLocalTempPolicy.ps1`。

## 操作流程

1. 先读取 `ES/AISpace/README.md` 与 `ES/AISpace/Public/LOCAL_TEMP_POLICY.json`，再确认目标文件是否已有权威位置。
2. 新内容先判断 Local/Public；真实工作产物不进入 AISpace，再判断是否真的需要进入 `Assets/ES/AISpace/Public`。
3. 不确定时放入对应 `ES/AISpace/*/Quarantine/<task>/`，只记录来源和待确认原因。
4. 迁移前保留相对路径、引用关系、`.meta` 配对和 UTF-8；禁止批量删除或静默覆盖。
5. 迁移后检查是否产生重复副本，并在需要时更新唯一索引，而不是复制内容。

## Skill 生成/缓存注册

当某个 Skill 会生成候选、截图、缓存、重放中间产物或协作索引时，注册阶段必须在
`.agents/SKILL_AISPACE_BINDINGS.json` 写入稳定 `bindingId` 和项目相对 `pathTemplate`。
绑定记录中的 `skillContractRef` 必须指向该 Skill 的 `governance.json`；随后由
`Build-ESSkillRelationRegistry.py` 把绑定投影到 `ES/AISpace/Public/Skills/registry.json`。
该投影同时包含 `registryPath` 和 `skillContractPath`，用于双向发现，但不授予写入权限。

默认模板只有三类：私有临时内容使用
`ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>/`，协作索引或稳定内容使用
`ES/AISpace/Public/<category>/<YYYYMMDD>/<topic-or-task>/`，必须导入 Unity 的正式资产使用
`Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/<domain>/`。Skill 本体不得复制或移动到 AISpace。

## 权限与证据

- 本 Skill 不扩大当前用户授权；删除、重命名、Git、Unity、网络和发布仍需单独指令。
- “已归类”只表示静态路径判断，不表示 Unity 导入、运行时读取或发布成功。
- 发现源文件、索引或规则漂移时标记 `stale`，回读当前权威来源。

## Workflow controls

- 默认只读分类；只有当前用户明确要求时才执行项目内移动或重命名。
- 任何迁移必须先列出目标、保留 `.meta`/引用关系，并提供可复核的差异。
- 不删除未知内容；不跨项目根；不把 Skill、AIWarnings、AITalk 或 Automation 复制成第二份。

## Skill 使用披露

使用本 Skill 时，按项目根 `AGENTS.md` 与 `.agents/README.md` 的 Skill 使用披露规范，
在首次进度更新和最终答复中说明本 Skill 及其职责。披露不等于授权或验收证据。
