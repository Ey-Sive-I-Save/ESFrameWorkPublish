# ES AI Commands

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

常规 AI 不需要把 53 条目录和所有 Markdown 一次读入上下文。优先从项目根执行：

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
