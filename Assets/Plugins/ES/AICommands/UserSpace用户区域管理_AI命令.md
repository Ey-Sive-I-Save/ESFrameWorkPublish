# UserSpace 用户区域管理 AI 命令

## Skill 受管入口（文档仅作指针）

本文件不定义独立执行流程。外部注册必须先路由到
`.agents/skills/es-ai-space-organization/SKILL.md`，再由该 Skill 读取本合同并调用 Worker；
以下命令形式仅用于显示确定的 Worker 入口，不得绕过 Skill、合同或权限门禁。

用户明确要求初始化、更新、发现或验证个人协作区域时，AI 使用：

```text
powershell -NoProfile -File ES/Automation/UserSpace/Invoke-ESUserSpace.ps1 -Action <Initialize|Update|Discover|Validate>
```

`Initialize` 与 `Update` 必须由当前用户明确授权，并且 Update 必须携带 `ExpectedRevision`；发现和验证只读。公共注册卡可记录职责、语言、工作时间、分支/合并习惯和可发现路由；个人私有试验、习惯细节和凭据不得写入 Public 注册卡。

## 接管确认表

用户用自然语言表达“接管这个区域”时，AI 必须先连续询问并记录四项答案：

1. 你确认自己属于该团队或拥有该区域的接管资格吗？
2. 你确认该区域的 Public 内容会继续对团队可见、Local 内容不会因此公开吗？
3. 你确认接管后原负责人将不能继续直接更新该区域吗？
4. 接管原因和责任范围是什么？

四项均明确确认后，受管调用才可携带 `TransferOwnership=true` 及对应确认字段；缺一项即拒绝。用户不需要记忆命令参数。

命令类型：安全执行：项目内用户区域注册与校验。
默认改文件：是（仅 Initialize/Update 写入 ES/AISpace/Public/People/<person-id>/registration.json；私有目录由 `.gitignore` 忽略）。
风险等级：L2。

## 必须先读

- `ES/AISpace/README.md` 及 `ES/AISpace/MUSTREAD_PROJECT_INSTRUCTIONS.md`。
- `ES/Automation/Contracts/es-userspace-profile-v1.json`。
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、当前状态和规则索引。
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json`（机器发现权威）。

## 合同绑定

```text
commandId: userspace.profile.manage
taskId: es.userspace.profile
taskVersion: 1
operations: initialize / update / discover / validate
```

当前可执行入口是确定性的 PowerShell Worker，且已有 Unity AutomationFacade 源码注册适配；仍需在当前 Unity Editor 中完成运行时验收，不得把静态注册当作运行时成功。

机器可读合同：`ES/Automation/Contracts/es-userspace-profile-v1.json`。

## 边界、恢复与验收

- PersonId 只允许小写字母、数字和连字符；私有 ProjectIgnored locator 只能是 `ES/AISpace/Local/<person-id>`。
- 注册卡含 revision 与 SHA-256 contentHash；旧 ExpectedRevision 必须拒绝，避免并发覆盖。
- Discover/Validate 遇到重复 ID、路径穿越、Hash 漂移或 malformed JSON 立即失败。
- `Test-ESUserSpace.ps1` 覆盖初始化、验证、CAS 更新和旧版本拒绝；结果 `runtime-not-run` 不证明 Unity/AIBrain 运行时可用。
- 取消或进程中断后可安全重跑；不执行 Git、发布、删除或外部网络动作。

## ContractCompleteness

```text
commandId: userspace.profile.manage
cancellation: before Initialize/Update registry commit only; after commit returns RecoveryRequired and preserves prior revision.
recovery: reread registration and contentHash, require ExpectedRevision/CAS and a new idempotencyKey; ownership conflicts return NeedsReissue.
validation: PersonId/path grammar, ownership confirmation fields, schema, revision/CAS, duplicate ID, malformed JSON and contentHash checks.
evidenceRef: commandId, commandBodyHash, planHash, invocationId, personId, revision/contentHash, Test-ESUserSpace receipt and source SHA-256.
allowRoots: ES/AISpace/Public/People/<person-id>/registration.json for authorized Initialize/Update only; Discover/Validate remain read-only.
denyPaths: ES/AISpace/Local contents, Assets/ES/AISpace/Local, Assets/ES/Space/Local (legacy), other people/teams, source, Git, release, Runtime, credentials and external network; deny-overrides.
```

## 交付格式

返回 `action`、personId、目标区域、revision/contentHash、只读或写入状态及 `Test-ESUserSpace.ps1` 收据；CAS 冲突、路径越界或 malformed 输入必须逐项报告，不得宣称 Unity/Runtime 已验收。
