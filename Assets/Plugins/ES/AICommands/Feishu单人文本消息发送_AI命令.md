# Feishu 单人文本消息发送 AI 命令

## 直接生效协议

当用户明确要求通过飞书机器人发送消息时，AI 必须：

```text
1. 只选择 commandId=feishu.message.send 与 TaskContract es.feishu.message.send@1。
2. 输入只允许 send-text、一个已认领 roleId 和 1～1000 字符纯文本；收件人 ID 必须由 C# 本地角色库解析。
3. 第一次固定 dryRun=true；实网发送需要新计划、新 InvocationId 和 30 分钟内同输入、同角色解析结果、同 Hash 的 DryRun 回执。
4. Worker 只调用 im.v1.message.create，msg_type 固定 text，并用当前 RunId 派生的服务端 uuid 做幂等。
5. 发送与任务创建是两个独立 Run；任一失败都必须显式报告 partial，不得伪装事务成功。
```

命令类型：安全执行：外部受控写入。
默认改文件：否；只允许发送一条飞书纯文本并写受管临时 RunRecord。
风险等级：L3。

## TaskContract

```text
commandId: feishu.message.send
taskId: es.feishu.message.send
taskVersion: 1
operation: send-text
recipientLimit: 1
textLimit: 1000
```

## 绝对禁止

```text
- 禁止任意收件人 ID、批量广播、Webhook、群创建、富文本、卡片、附件、@ 注入、编辑、撤回或删除。
- 禁止在 roleId 未认领、AppId Hash 不匹配或 allowDirectMessage=false 时发送。
- 禁止直启 Node、npm、npx、CLI、ProcessRunner 或绕过 AutomationFacade。
- 禁止输出凭据、原始收件人 ID、消息正文或含秘密的远端错误材料。
```

## 失败与停止条件

- 权限不足、机器人能力未启用、收件人不可达、限流、网络中断、超时、取消、Hash 漂移或响应不确定时停止。
- 带相同 uuid 的有界瞬态重试耗尽后返回 `UNCERTAIN_REMOTE_RESULT`；不得改用新 InvocationId 猜测重发。
- 必须返回消息 ID、收件人引用 Hash、幂等 UUID Hash、尝试账本、RunRecord 和终态；消息正文不进入回执。
- Static、DryRun、SDK 类型存在或已打开飞书客户端都不证明消息已送达。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
.agents/skills/es-feishu-cli/SKILL.md
Assets/Plugins/ES/AICommands/Feishu单人文本消息发送_AI命令.md
```

## 交付格式

```text
1. PlanHash、TaskContract/Worker/Schema/角色绑定 Hash。
2. DryRun RunId、内容 Hash 和单收件人计划。
3. Live RunId、消息 ID、尝试账本与终态。
4. 未验证的送达/已读状态和停止原因。
```
