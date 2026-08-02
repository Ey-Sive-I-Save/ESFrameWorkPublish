# ES Story Save and Recovery Contract

状态：切片 A 冻结契约；Receipt/Outbox 属于切片 B。

## 1. 权威

| StoryKind | 持久权威 | 活跃执行 |
| --- | --- | --- |
| Quest | QuestRecord | StoryInstance |
| 临时 Dialogue | 无持久记录；不写入切片 A 存档 | StoryInstance |
| 长期 Story | 专用进度记录；切片 A 不支持 | StoryInstance |

StorySnapshot 只是 DTO，不是运行期第二权威。

切片 A 尚无稳定 Actor/Target 身份，因此不保存或恢复临时 Dialogue；同一 Definition 可能对应多个 NPC 时不得按 DefinitionId 猜测认领。该能力等待 StableWorldIdentity 或显式稳定交互参与者身份。

## 2. Quest 保存字段

- QuestRecordSnapshot 保存 DefinitionId、固定 ContentVersion/Signature、CurrentNodeId、持久变量、分支结果、状态与 RecordRevision。
- QuestRecord 是 Quest 持久进度唯一权威。
- `ActiveInstanceSnapshot` 不属于切片 A；不得保存 StoryInstanceId、SessionId、SessionGeneration、ViewRevision、前台意图、Lease 或 Binding。
- StorySnapshot/SaveSection 只是序列化 DTO，不得被运行时长期持有或独立推进。

## 3. Checkpoint

```text
读取当前节点与 Revision
→ 条件判断
→ 准备最小执行票据
→ 执行同步幂等 SetTag
→ 重新校验票据
→ 更新 QuestRecord/实例节点
→ 增加 Revision
→ 标脏并生成一致的 Story 内存 Checkpoint
```

- StoryModule 不在每个节点后直接写磁盘。
- `ESGameSave.Set` 只写一个统一 `story.runtime` 分区 DTO。
- 真正磁盘 Save 仍由 ESGameSave 工作流负责。
- Load 使用通用事务协议：读取候选 Archive → ValidateCandidate → PrepareCandidate → 全部 Prepare 成功 → 按阶段 CommitCandidate。
- ValidateCandidate 只能读取候选 Archive、解析并缓存候选 DTO，不得读取已切换缓存，也不得清理或修改当前运行状态。
- PrepareCandidate 只能构造可提交状态和 Rollback 快照，不得修改 StoryRecord、UI、RuntimeMode、Interaction 或其他运行态。
- 任一 Commit 失败时，Save 必须按成功 Commit 的逆序调用对应 Rollback；Rollback 失败必须返回明确错误和诊断，禁止伪报 Load 成功。
- Story Rollback 必须恢复旧 QuestRecord、StoryInstance 集合、前台 UI、Session 身份、等价 RuntimeMode Lease 与仍有效的 InteractionBinding。旧 Interaction 只在全部 Commit 成功后的 Finalize 阶段结束。
- `story.runtime` 缺失表示合法空 Story 状态；提交时清理旧实例、UI、Lease、Binding 与 QuestRecord，并成功应用空状态。
- Schema 不兼容、JSON 无效、DefinitionId 重复或 DTO 非法必须使 Load 失败；验证失败时不得切槽或修改旧运行状态。
- SaveModule 保留最近一次成功验证并提交的当前候选。StoryModule 晚注册时必须重新验证并只重放其中的 QuestRecords；重放失败写入有界诊断。
- 晚注册重放不得创建 StoryInstance，也不得恢复旧 Session、Generation、ViewRevision、Lease、UI 或 Binding。

Load 后的 Quest 语义固定为：

```text
清理旧 StoryInstance / UI / Lease / Binding
→ 恢复 QuestRecord
→ 活动 StoryInstance 数量为 0
→ 下一次合法交互
→ 创建全新 StoryInstanceId + SessionId + SessionGeneration
→ 从 QuestRecord.CurrentNodeId 继续
```

## 4. 切片 A 崩溃语义

切片 A 只允许绝对、同步、可重复执行仍安全的 `Tags.SetTag(tag, active)`。

- Action 前崩溃：恢复后允许再次执行。
- SetTag 已执行但 Story Checkpoint 未落盘：恢复后再次执行相同绝对 SetTag，结果仍一致。
- Story 节点已更新但仅内存未落盘：恢复到旧 Checkpoint 后允许重复 SetTag。
- Checkpoint 已进入 Save 缓存但磁盘 Save 失败：内存 Revision 不回退；继续保持 Dirty 并允许后续重试磁盘 Save。

切片 A 禁止累加数值、发奖励、生成/销毁对象和任意非幂等 Operation。上述能力必须等待切片 B Receipt/Outbox。

## 5. 分区

切片 A 的 `story.runtime`：

```text
SnapshotSchemaVersion
QuestRecords
Metadata
```

不预建空 ResultLedger、Outbox 或 Receipt 字段。切片 B 通过 SnapshotSchemaVersion 迁移加入。

## 6. 诊断与恢复

- 旧 Instance、旧 UI Submission、旧 SessionGeneration 与旧 Binding 在 Load 后必须失效，不得覆盖或推进新 QuestRecord。
- Definition 版本或签名缺失时安全挂起/失败，不猜测节点。
- CompletedRecord 仅用于历史与显示，不得重新参与推进。
- 诊断数量和字符串长度必须有上限。

## 7. 验收

- Unity Test Runner：覆盖跨槽位空分区清理、Schema/DTO 验证失败不切槽、不部分应用、DTO 往返、延迟水合与迟到提交拒绝。
- PlayMode：覆盖 Load 后零活动实例、下一次交互从 QuestRecord 节点继续、重新建立身份、旧 UI 输入拒绝和交互资源释放。
- 切片 A 不得宣称奖励防重复、世界对象恢复或通用异步 Operation 崩溃安全。

## 8. 切片 B 边界

Receipt、Outbox、ResultLedger、Reward 与非幂等 World Result 只属于切片 B，本契约不声明其已实现。切片 B 必须另行冻结 World Result Receipt/Recovery 契约并提升 SnapshotSchemaVersion。
