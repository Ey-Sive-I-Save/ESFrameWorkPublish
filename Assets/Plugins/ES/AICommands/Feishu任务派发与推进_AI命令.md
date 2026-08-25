# Feishu 任务派发与推进 AI 命令

## 直接生效协议

当用户明确要求创建、分配或推进飞书任务时，AI 必须：

```text
1. 先读取 AIWarnings 启动链、Automation Worker 治理、es-feishu-cli 和本文件。
2. 选择且只选择一个 TaskContract：es.feishu.task.dispatch@1 或 es.feishu.task.transition@1。
3. 第一次调用固定 dryRun=true，输出远端变更计划；不得在 DryRun 访问网络或修改远端。
4. 实网写入必须重新 planTask，使用新的 InvocationId，并通过 `dryRunEvidenceRunId` 绑定 30 分钟内、同输入和同 Hash 的已接受 DryRun；同时绑定用户当前批准、Actor、租户、应用身份 Hash、目标清单/任务、AICommand/TaskContract/Worker/Schema Hash、60 秒超时和停止条件。
5. 任务创建使用官方 client_token；清单创建使用确定性后缀与精确名称恢复。无幂等令牌的写操作不自动重试，响应丢失必须返回 `UNCERTAIN_REMOTE_RESULT` 并停止；更新必须携带 fresh expectedUpdatedAt。
6. 逐项报告成功、冲突、失败和遗留对象；部分成功不得写成整批成功。
7. 角色化分配优先使用 `claimedRoles`；C# 只能从同 AppId 分区、已明确 `allowTaskAssignment=true` 的本地角色解析成员。禁止同时提交 `members` 与 `claimedRoles`。
```

命令类型：安全执行：外部受控写入。
默认改文件：否；只允许写飞书测试清单/任务和受管 RunRecord 临时目录。
风险等级：L3。

## TaskContract

派发：

```text
commandId: feishu.task.mutate
taskId: es.feishu.task.dispatch
taskVersion: 1
operations: tasklist-create / task-create / virtual-team-fixture-create
batchLimit: 20
```

推进：

```text
commandId: feishu.task.mutate
taskId: es.feishu.task.transition
taskVersion: 1
operations: task-update / task-complete / task-reopen / members-add / members-remove / reminder-add / reminder-remove
```

## 虚拟团队测试边界

- “虚拟团队”默认是一份名称含 `[ES-TEST:<InvocationHash>]` 的隔离任务清单及 Product Owner、Technical Lead、Developer、QA、Release Owner 五个角色任务。
- 不创建真实或虚假租户用户，不建立部门，不发群消息；成员只有在用户提供并批准受管 OpenId 清单时才可绑定。
- 每个人可通过独立 `es.feishu.identity.claim@1` 合同认领自己的本地角色并选择是否允许任务分配；角色认领不授权本合同写入。
- 测试不自动删除。交付远端清单/任务 ID，供用户检查后人工归档；删除能力不在本合同内。

## 绝对禁止

```text
- 禁止直启 Node、npm、npx、CLI 或 ProcessRunner。
- 禁止删除任务/清单、发送消息、上传、发布、修改权限或创建租户用户。
- 禁止在没有 expectedUpdatedAt 时覆盖既有任务。
- 禁止把源码提交或 AI 判断直接等同于 Completed；完成必须满足绑定验收证据。
- 禁止凭据、Authorization、Cookie 或完整成员身份进入普通日志和 AIKnowledge。
```

## 验收与停止条件

- 默认预算：一次清单、最多 5 个测试任务、最多 20 次 API 请求、总超时 60 秒、无自动删除。
- 首次远端写失败、权限不足、版本冲突、Hash 漂移、取消不确定、任一敏感信息泄漏或部分成功无法确定时立即停止。
- `virtual-team-fixture-create` 中途失败必须返回已创建清单、任务和失败序号；不得自动删除或继续补写。
- 必须返回 PlanHash、RunId、DryRun、远端对象 ID、输入/输出 Hash、退出码、终态、部分成功明细和未验证项。
- 静态构建、DryRun 或客户端已打开不代表真实飞书写入成功。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
.agents/skills/es-feishu-cli/SKILL.md
Assets/Plugins/ES/AICommands/Feishu任务派发与推进_AI命令.md
```

## 交付格式

```text
1. 已读规则、PlanHash、TaskContract/Worker/Schema Hash。
2. DryRun RunId 和远端对象计划。
3. 实际 RunId、对象 ID、输入/输出 Hash、部分成功明细。
4. 停止原因、未验证项和剩余人工归档动作。
```
