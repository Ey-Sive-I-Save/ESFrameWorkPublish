# Commercial-grade controls

商业级 Skill 不是“功能更多”，而是每个可产生影响的面都有可审计的控制。以下控制与 Skill 等级、AICommand 授权和 AIBrain 计划共同生效。

## 1. Identity and version

- `skillName`、目录名和触发描述必须唯一且稳定。
- 破坏输入/输出、权限或证据语义时必须升级 schema 或版本，不得静默改变旧行为。
- `SKILL.md`、`openai.yaml` 和 `governance.json` 的哈希必须进入验收证据；AIBrain 计划绑定这些哈希。

## 2. Authority and permission

权威顺序固定为：当前源码/真实证据 > AIWarnings P0 > AICommand > AIBrain 路由 > Skill > AIKnowledge 摘要。Skill 只能提供工作流；AICommand 提供单次授权；AIBrain 只做定向计划、门禁和一次性授权。

- 不得把 `governance.json`、Knowledge、聊天确认或按钮可见当作写权限。
- `authorityClass` 只表示 Skill 在路由和门禁链中的优先级；不得用 Engineering 或 project-gate 冒充修改、发布、删除或网络权限。
- 读、计划、写、发布、删除、外部网络和 AI 调用分别声明，不用一个 `allow` 布尔值包办。
- 缺少明确授权、命令、TaskContract、路径或目标身份时必须 fail closed。

## 3. Risk and change budget

使用最高风险单值加独立确认策略：

```text
ReadOnly < StateChanging < AssetWriting < Destructive
None | Confirm | PreviewThenConfirm | ExplicitPhrase
```

风险不能被 Skill 等级覆盖。每次 Workflow/Engineering 运行都要给出变更预算：目标路径、最多对象数、最大重试、超时、并发度和停止条件。预算不足时停止，不自动放宽。

## 4. Data and supply chain

- 明确允许读取的项目根和禁止读取的凭据、用户目录、外部缓存。
- 外部内容必须记录来源、拉取时间、哈希和是否可写回。
- 不执行 Skill 内未声明的二进制、脚本、网络上传或子进程。
- AIKnowledge 是索引和摘要，不是事实源；SourceRefs 或哈希失效时标记 stale。

## 5. Observability and auditability

每次流程应能回答：谁发起、何时发起、使用哪个 Skill/Command/PlanHash、读写了什么、结果是什么、失败在哪里、如何恢复。日志或 RunRecord 必须是可重读证据，不把瞬时 Console 文本伪装成长期规则。

## 6. Idempotency and concurrency

- 重跑要么产生同一结果，要么明确拒绝并说明冲突。
- 同一稳定身份只能有一个活动写事务；并发请求必须通过 Lease、InvocationId 或 TaskContract 所有权收口。
- 取消、超时、Domain Reload、进程退出和部分失败都要有清理/恢复动作。
- 不把 UI 重绘次数、线程调度或网络重试次数当作业务进度。

## 7. Performance and capacity

Workflow/Engineering 必须记录规模假设、首次与稳态成本、内存峰值、分配热点、批处理策略、并发限制和超时。执行时还必须遵守 [`performance-controls.md`](performance-controls.md) 的快速路径/深度路径边界。没有 Profiler/目标平台证据，不得声称低 GC、0 GC 或商业级性能。

## 8. Compatibility and migration

声明输入格式、输出格式、Skill/Command 版本和向后兼容策略。迁移必须支持 dry-run、差异报告、分批提交、断点恢复和回滚；不能用显示名称、菜单路径或可变文件位置替代稳定身份。

## 9. Incident and recovery

对每个高风险步骤定义预防、检测、隔离、恢复四项动作。失败报告必须包含影响范围和未完成责任；禁止吞异常后报告成功。达到 `Blocked` 或 `Failed` 时，不得自动降级到另一个未授权入口。

## 10. Ownership and retirement

Engineering Skill 必须有维护责任、验收责任、支持范围和废弃条件。废弃时提供迁移目标和兼容窗口；不能仅删除目录后让 AIBrain 继续引用旧 Knowledge 或旧 relatedSkills。
