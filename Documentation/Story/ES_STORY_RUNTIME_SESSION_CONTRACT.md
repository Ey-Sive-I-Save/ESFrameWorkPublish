# ES Story Runtime and Foreground Session Contract

状态：切片 A 冻结契约。

## 1. 三层身份

```text
DefinitionId                 内容
StoryInstanceId + Revision   一次运行
SessionId + Generation       一次前台对话会话
```

任何 UI 提交必须同时包含：`StoryInstanceId`、`ExpectedInstanceRevision`、`SessionId`、`SessionGeneration`、`ViewRevision`、`OptionId`。

## 2. 实例状态

切片 A 使用：`Created`、`Running`、`WaitingForForeground`、`WaitingForUI`、`Completed`、`Failed`、`Aborted`。

- 所有推进只能进入 StoryModule 的统一入口。
- 每次接受推进结果后增加 Instance Revision。
- 同一推进轮次最多执行固定数量同步节点，防止无进展循环。
- UI、Interactable 和回调不得直接修改 CurrentNode、变量或 QuestRecord。

## 3. 前台会话

- 只有前台 Winner 可以申请 Dialogue RuntimeMode Lease、显示 UI 和接受选择。
- 前台申请保存的是逻辑意图，不保存 Lease、Token、Owner、Generation 或仲裁序号。
- 会话结束、失败、取消、目标丢失或模块销毁时，统一按 UI → RuntimeMode Lease → Interaction Binding 的顺序收口。
- 暂停抢占后允许恢复同一实例，但恢复必须建立新的 SessionGeneration 和 ViewRevision；旧 UI 按钮永久失效。
- 存档 Load 不是暂停抢占。Load 必须销毁全部旧 StoryInstance，并使旧 StoryInstanceId、SessionId、SessionGeneration、ViewRevision、Lease 与 Binding 永久失效。
- Quest 在 Load 后保持零活动实例；下一次合法交互从 QuestRecord.CurrentNodeId 延迟水合，并创建全新的 StoryInstanceId、SessionId 与 SessionGeneration。

## 4. Interaction

- InteractionBinding 至少包含 Token、Generation、Owner 与目标身份。
- Story 结束交互时必须提交本次 Binding；旧 Binding 不得结束后来建立的新交互。
- 必须覆盖 ActorLeftRange、TargetLost、ModuleDisabled、UserCancelled、StoryFailed 与 Completed。
- `OnInteractEnded` 的业务异常不得阻断 IK、MatchTarget、State、SupportFlag 和占用清理。

## 5. RuntimeMode

- Story 只能申请现有 `ESRuntimeModeService` 的 generation-safe Dialogue Lease。
- Lease 拥有 Host、Generation、Owner 和共享释放状态。
- Active Set 条目必须声明 `LeaseOwned`、`LegacyUnowned` 或 `SystemOwned`。Story Lease 只能生成 `LeaseOwned` 条目。
- 重复 Dispose 幂等；旧代 Lease 或错误 Owner 不得移除当前请求。
- Pop、按枚举删除和旧 RuntimeMode Command 不得删除 LeaseOwned；Owner 批量清理只能处理明确允许的 SystemOwned 条目。
- Tag Handle 必须携带并校验 Host、Generation、Owner、Handle 与 OwnershipKind。无 Owner、错误 Host 或旧 Generation 的 Handle 删除必须拒绝。
- `Clear()` 只用于 Service/场景安全点，并推进 Generation，使全部旧 Lease 失效。
- Story 不直接写输入开关；输入消费者继续读取已提交 RuntimeMode Policy。

## 6. UI

- UI 只显示 `ESDialogueViewData` 并提交选择，不保存运行权威。
- StoryModule 拒绝旧 InstanceRevision、旧 SessionGeneration、旧 ViewRevision、重复 OptionId、非当前节点选项和非前台实例。
- 拒绝的迟到输入只能写入有界诊断，不得推进或重复执行 Action。
- StoryModule 不绘制 UI；具体 UI 通过 presenter/bridge 接入。

## 7. 最小执行票据

```text
ExecutionId
StoryInstanceId
ExpectedInstanceRevision
NodeId
NodeVisitSequence
ActionId
ExecutionState
ActionResult
```

同步结果返回后重新校验实例存在、Revision、CurrentNodeId、NodeVisitSequence 与当前 ExecutionId。任一不一致即丢弃。

## 8. 禁止事项

- 不建立第二套 RuntimeMode、Interaction 或 UI 权威。
- 不由 StoryModule Tick ESCommand 或通用 Operation。
- 不将 SessionId 当作存档身份。
- 不保存或恢复 StoryInstanceId、SessionId、SessionGeneration、ViewRevision、RuntimeMode Lease 或 InteractionBinding。
- 不让 DefinitionId 代替 StoryInstanceId 区分并发实例。
