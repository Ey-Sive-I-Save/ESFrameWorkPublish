# ES 个人协作区

每位用户或 Agent 可以在这里拥有一个独立公开目录：

```text
ES/AISpace/Public/People/<person-id>/registration.json
```

`registration.json` 只公开稳定 ID、显示名、职责、偏好、常用分支策略、可发现入口和私有资料的 opaque locator；不得放入凭据、原始 Feishu 身份、私人知识正文或试验产物。

空间身份由 `spaceId`、`ownerId`、`visibility`、`membershipPolicy`、`storageLocator` 和 `revision` 共同描述；`storageLocator` 只是可迁移的当前位置，不是空间身份。接管记录遵循 `es-ownership-transfer-v1.schema.json` 的状态机并携带不可变 Receipt 字段。

私人工作区默认位于 `ES/AISpace/Local/<person-id>/`（已加入 Git 忽略）或项目外部目录。公共注册卡可以引用私有目录地址，但 AI 必须在获得当前用户授权后才能读取该地址指向的内容。

初始化、更新、发现和验证统一使用：

```powershell
& '.\ES\Automation\UserSpace\Invoke-ESUserSpace.ps1' -Action Initialize -PersonId '<id>' -DisplayName '<name>'
& '.\ES\Automation\UserSpace\Invoke-ESUserSpace.ps1' -Action Discover
& '.\ES\Automation\UserSpace\Invoke-ESUserSpace.ps1' -Action Validate
# Updates use compare-and-swap; read the current revision before writing.
& '.\ES\Automation\UserSpace\Invoke-ESUserSpace.ps1' -Action Update -PersonId '<id>' -ExpectedRevision 1 -DisplayName '<new-name>'
```

公共注册卡是导航，不是权限授予；真正的写入、外部调用、Unity/Runtime 和 Git 操作仍受现有 AICommand、TaskContract、AIBrain 和用户指令约束。

实现约束：注册卡绑定当前 Windows 主体的 `ownerSubjectHash`（只保存 SHA-256，不保存主体明文）；更新同时受 `ExpectedRevision` 和按 `PersonId` 的互斥锁保护；受管调用会在 `ES/Automation/Runs/UserSpace/<runId>/run-record.json` 留下状态证据。Unity/AIBrain Runtime 未产生回执前，任何 AI 都不得把静态注册解释为运行时成功。
