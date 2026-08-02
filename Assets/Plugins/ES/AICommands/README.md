# ES AI Commands

## 在 Unity 中直接使用

```text
1. 打开【ES】/开发与维护/Cmd Agent。
2. 在“AICommand / 项目记忆”区域点击“执行预设指令”。
3. 选择一个命令，填写“本次需求”，按需拖入 Unity 资产、文件或截图。
4. 确认命令类型、默认改文件和风险等级，再点击“发送指令”。
5. 首次使用或修改命令库后，点击“验证指令库”；无效命令会标记为“无效”并禁止发送。
```

命令文件必须声明 `命令类型`、`默认改文件`、`风险等级`，其中列出的项目内路径必须真实存在。Unity 面板只负责发现、校验、组合和发送任务，不会绕过命令自身的权限边界。

## 与 Agent Skills 协同

项目级 Skills 位于：

```text
.agents/skills/
```

项目内 AI 文件归属、Skill 目录规范和完整简介见 `.agents/README.md`。

AICommand 决定“这次任务允许做什么”，Skill 决定“这类任务怎样稳定执行”。Skill 不会因为能调用脚本或 UnityMCP 就自动获得写权限。

当前可用映射：

| 任务 | 推荐 Skill |
|---|---|
| 选择、校验并执行一个 AICommand | `$es-use-ai-command` |
| Unity 编译、Console、ReloadDomain 与证据分层 | `$es-unity-compile` |
| 修复一个明确编译错误 | `$es-fix-compile-error` |
| 中文文本、UTF-8、乱码和补丁检查 | `$es-utf8-guard` |
| 修改前后检查脏工作树和路径重叠 | `$es-worktree-audit` |
| GameCore 根 SO、RuntimeData、全局索引或模块接入 | `$es-gamecore-integration` |
| 资源库、计划、Manifest、Provider、Scope 或发布资源链路 | `$es-resource-pipeline` |
| ESGameTag、ESTag、ConfigKey、Catalog 与稳定身份 | `$es-tag-config` |
| Entity、角色 Prefab、DataInfo、部件、运动与池化 | `$es-entity-authoring` |
| 输入动作、绑定、Profile、RuntimeMode 与玩家控制 | `$es-input-action` |
| ESCommand、分类、Context、Player 与 Runner | `$es-command-authoring` |
| EditorWindow、Drawer、ESEditorSection、SO 表格和 ReloadDomain | `$es-editor-tooling` |
| 编译到 IL2CPP、资源 Provider 与真实发布的证据矩阵 | `$es-release-acceptance` |
| 未开始、开发中、待验收、稳定或废弃模块的成熟度与半成品影响 | `$es-module-lifecycle` |

从项目根启动 Codex 后，可以显式输入 `$skill-name`；也可以让 Codex 根据任务自动匹配。当前窗口未显示新 Skill 时，应新开项目窗口或重启，不要把未热加载误判为文件不存在。

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测任务。
2. 按“命令类型”和“默认改文件”决定是否允许改代码。
3. 先读取本文列出的必须规则文件；若文件不存在，要明确说明。
4. 执行前先确认当前工作树和相关入口文件，避免误改其他 AI 或用户的改动。
5. 只做本文允许的事情；如果用户需求超出本文范围，先说明需要换用哪个命令。
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
