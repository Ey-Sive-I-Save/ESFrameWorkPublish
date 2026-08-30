# ES 团队协作区

团队是独立于个人的公共协作对象：

```text
ES/AISpace/Public/Teams/<team-id>/team.json
```

团队卡记录名称、使命、成员 `personId`、成员职责和团队分支/合并规则。个人私有资料不进入团队卡；成员必须先在 `Public/People` 注册。团队更新同样使用主体哈希、Mutex 和 `ExpectedRevision`。

统一入口：

```powershell
& '.\ES\Automation\UserSpace\Invoke-ESTeamSpace.ps1' -Action Initialize -TeamId '<id>' -DisplayName '<name>'
& '.\ES\Automation\UserSpace\Invoke-ESTeamSpace.ps1' -Action Discover
& '.\ES\Automation\UserSpace\Invoke-ESTeamSpace.ps1' -Action Validate
```
