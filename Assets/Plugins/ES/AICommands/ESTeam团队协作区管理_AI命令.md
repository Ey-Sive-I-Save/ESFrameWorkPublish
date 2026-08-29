# ES Team 团队协作区管理 AI 命令

支持团队公共区域的初始化、更新、发现和验证。成员必须先存在于 `ES/AISpace/Public/People`；个人私有资料不会自动进入团队区。更新必须携带 `ExpectedRevision`，并校验团队所有者主体哈希。

命令类型：安全执行：团队公共协作区注册与校验。
默认改文件：是（仅写入 `ES/AISpace/Public/Teams/<team-id>/team.json`）。
风险等级：L2。

```text
commandId: teamspace.profile.manage
taskId: es.teamspace.profile
taskVersion: 1
operations: initialize / update / discover / validate
```

入口：`ES/Automation/UserSpace/Invoke-ESTeamSpace.ps1`。团队卡只记录公共使命、成员 personId、成员职责和分支/合并规则；Runtime 状态仍须单独验收。

## 必须先读

- `ES/AISpace/README.md` 及 `ES/AISpace/MUSTREAD_PROJECT_INSTRUCTIONS.md`。
- `ES/Automation/Contracts/es-teamspace-profile-v1.json`。
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、当前状态和规则索引。
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json`（机器发现权威）。

## ContractCompleteness

```text
cancellation: before commit only; conflict or cancellation preserves prior team.json.
recovery: ExpectedRevision/CAS and contentHash; conflict returns NeedsReissue, no blind replay.
validation: team owner hash, member personId, path placement, schema and revision checks.
evidenceRef: commandId, commandBodyHash, planHash, invocationId, revision/contentHash, receipt and source SHA-256.
allowRoots: ES/AISpace/Public/Teams/<team-id>/team.json only.
denyPaths: ES/AISpace/Local, Assets/ES/Space/Local, source, Git, release, Runtime and unrelated teams; deny-overrides.
```

## 交付格式

返回 `action`、teamId、目标区域、revision/contentHash、成员校验结果和验证收据；ExpectedRevision/所有者哈希冲突必须报告为失败，不得宣称 Unity/Runtime 已验收。
