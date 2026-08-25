# Feishu 本地角色与机器人认领 AI 命令

## 直接生效协议

当用户要求配置飞书、创建个人角色、认领本地机器人或查看团队角色时，AI 必须：

```text
1. 只选择 commandId=feishu.identity.manage 与 TaskContract es.feishu.identity.claim@1。
2. setup-status/list-claims 只返回布尔诊断、哈希和脱敏角色摘要，不读取或输出凭据值。
3. claim-role/release-role 第一次固定 dryRun=true；本地写入需要新计划、新 InvocationId 和 30 分钟内同输入 DryRun 回执。
4. 角色所有权绑定当前 Windows 本地主体 Hash、AIBrain Actor Hash 和当前 ES_FEISHU_APP_ID Hash。
5. 原始身份绑定只长期保存在 Git 忽略的 ES/Automation/Runs/FeishuIdentity；解析给受管 Worker 的一次性投影只可进入同样 Git 忽略的 ES/Automation/Runs/FeishuTasks 请求封套，普通结果仅返回绑定 Hash。
6. 角色许可分别声明 allowTaskAssignment 和 allowDirectMessage；默认 false，不从角色名称推断同意。
```

命令类型：安全执行：机器本地受控状态写入。
默认改文件：是；仅允许写入 `ES/Automation/Runs/FeishuIdentity` 的私有注册表与 RunRecord。
风险等级：L2。

## TaskContract

```text
commandId: feishu.identity.manage
taskId: es.feishu.identity.claim
taskVersion: 1
operations: setup-status / list-claims / claim-role / release-role
```

## 权限与身份边界

- 本地角色不会创建飞书用户、部门、群聊、机器人或租户权限。
- `claimBotOwnership` 只认领当前 AppId Hash 对应的本地治理所有权；它不证明机器人能力已启用或远端权限已批准。
- 只有原所有者可更新/释放角色；同 AppId 分区内其他 Actor 只能在角色显式许可范围内用于任务分配或单人消息。
- Windows 共享账号无法提供人员级强身份隔离，必须由部署方使用独立 OS 账号或后续企业身份代理。

## 绝对禁止

```text
- 禁止把 App Secret、Token、Cookie 或 Authorization 写入角色输入、注册表、RunRecord、报告或聊天。
- 禁止把本地认领解释为飞书认证、租户授权、任务写权或消息发送授权。
- 禁止接受脚本路径、Node 路径、Webhook、命令行或任意本地存储路径。
- 禁止把原始成员/收件人 ID 投影到 Git、AIKnowledge、AIWarnings、普通报告或 AI 可见摘要。
```

## 验收与停止条件

- 缺 AppId、角色冲突、机器人别名冲突、Actor/本地主体不匹配、DryRun 过期、Hash 漂移或状态目录异常时立即停止。
- 必须覆盖正向、无 AppId、跨所有者覆盖、跨 AppId 复用、重复 InvocationId、并发写和敏感信息泄漏检查。
- Static 源码与 DryRun 不证明真实飞书认证或机器人可用。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
.agents/skills/es-feishu-cli/SKILL.md
Assets/Plugins/ES/AICommands/Feishu本地角色与机器人认领_AI命令.md
```

## 交付格式

```text
1. PlanHash、TaskContract/Schema/Worker Manifest Hash。
2. setup-status 的非秘密诊断和 nextActions。
3. DryRun/Live RunId、roleId、AppId Hash、许可与绑定 Hash。
4. Runtime 未验证项和仍需人工完成的飞书开放平台步骤。
```
