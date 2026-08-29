---
name: es-ai-space-organization
description: >-
  Govern the placement of AI-generated project content in ES/AISpace Local or
  Public and Unity-facing Assets/ES/Space/Public. Use when an AI creates,
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

## 分类规则

```text
仅当前 AI/当前机器的协作索引  -> ES/AISpace/Local/<agent-or-task>/
需要其他 AI 审阅的协作索引    -> ES/AISpace/Public/<topic-or-task>/
必须被 Unity 导入或引用       -> Assets/ES/Space/Public/<domain>/
AITalk 会话/消息/共识         -> Assets/Plugins/ES/AITalk/
Codex 历史与交接               -> ES/AI协作历程（Codex）/
Automation 合同/回执/候选      -> ES/Automation/
长期 P0、架构事实、验收规则    -> Assets/Plugins/ES/AIWarnings/
可复用工作流                  -> .agents/skills/es-*/
```

不要创建 `Assets/ES/Space/Local`。Unity 会导入该目录，它不是私密隔离。
不要把 AITalk、历史、Automation 或 AIWarnings 复制到 AISpace。

## 操作流程

1. 先读取 `ES/AISpace/README.md`，再确认目标文件是否已有权威位置。
2. 新内容先判断 Local/Public；真实工作产物不进入 AISpace，再判断是否真的需要进入 `Assets/ES/Space/Public`。
3. 不确定时放入对应 `ES/AISpace/*/Quarantine/<task>/`，只记录来源和待确认原因。
4. 迁移前保留相对路径、引用关系、`.meta` 配对和 UTF-8；禁止批量删除或静默覆盖。
5. 迁移后检查是否产生重复副本，并在需要时更新唯一索引，而不是复制内容。

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
