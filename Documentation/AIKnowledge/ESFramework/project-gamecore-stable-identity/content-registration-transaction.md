# GameCore 内容注册事务

`KnowledgeId`: `esframework.project.gamecore-content-registration-transaction.v1`
`Authority`: `Source + AIWarnings + Unity official documentation`
`RouteKeys`: `gamecore`, `content-registration`, `preview`, `commit`, `cas`, `guid`, `local-file-id`, `consumer`, `rollback`, `idempotency`
`ContentHash`: `3e3684039f4ad5c681a311a1500d682735ff68a7296646e3111b9bd90235ac78`
`EvidenceLevel`: `S1 / runtime-not-run`

## Summary

`ESContentRegistrationAuthoring` 是普通资产、AssetKey、GameCore、GameCore 根与 Consumer 同步的统一编辑器入口。目标身份必须由 GUID + LocalFileId + 类型精确解析；显示名、路径或“首个同类型资产”不能替代精确身份。

写入是两阶段资格协议：

```text
preview(commit=false)
  -> 精确解析目标与来源身份
  -> 读取 revision、当前 key、dirty 状态
  -> 返回 expected identity/revision/key

commit=true
  -> 重验 identity、revision CAS、当前 key 与 clean target
  -> Undo.RecordObject
  -> 写入并检查后置条件
  -> 仅对明确目标执行 AssetDatabase.SaveAssetIfDirty
  -> 失败时恢复原对象或集合并再次保存
```

没有当前进程 preview 资格的 commit 必须拒绝。preview 后目标变化属于并发冲突，Dirty 目标必须阻断；重复同一提交只能返回幂等结果，不能制造重复 Page、Group、根引用或 Consumer 快照。GameCore 注册还必须维持根 SO 单向依赖，不能借注册流程把 Prefab 或场景对象反向塞入核心定义。

## 动作选择

| 目标 | Action | 关键边界 |
|---|---|---|
| 普通非 GameCore 资产进入 AssetLibrary | `RegisterAsset` | GameCore SO 不得进入普通 AssetTable |
| 修改已注册普通资产的稳定 Key | `UpdateAssetKey` | 必须带当前 Key CAS；不提供直接列表写入 |
| DataInfo 接入正式 Group 与 Consumer | `RegisterGameCore` | Info/Group 类型、唯一归属、显式 ConfigKey 必须成立 |
| 不属于 DataInfo/Group 的独立核心根 | `RegisterGameCoreRoot` | 只注册真正独立根；嵌套数据不能伪装根 |
| Consumer 快照同步 | `Synchronize` | revision/Dirty/精确目标门禁仍适用 |
| Catalog/ReferenceGraph Bake | `Bake` | 注册与 Bake 是两阶段；pending 不等于完成 |
| 移除、移动、复制、合并、批量清空 | 无正式事务 | 保持禁用；不得退回直接写列表 |

所有菜单、Inspector、Drawer、资源窗口、MCP 和 C# 自动化只能组装同一 `ESContentRegistrationRequest` 并调用 `ESContentRegistrationAuthoring.Execute`。禁止直接改 Page、Key、`ManualGameCoreAssets` 或调用旧收集 API。

## Preview 到 Commit 的精确协议

1. 在 Unity Editor 主线程发送 `commit=false`；请求没有 requestId 时由入口生成独立 preflight requestId。
2. 只在 preview `success=true` 后继续；保存返回的同一 requestId、GUID、LocalFileId、各目标 revision、当前 Key 和其他 expected 字段。
3. commit 使用相同业务输入和同一 requestId，只补回 preview 给出的 expected 字段并设 `commit=true`。Domain Reload、进程变化或业务输入变化后重新 preview。
4. commit 资格是当前 Unity Editor 进程内、单次消费的。真实 commit 尝试开始前资格即被移除；即使 CAS/Dirty/冲突失败，重试也必须重新 preview。
5. 同一 requestId 的已成功提交可按完全相同 fingerprint 幂等重放；requestId 对应不同输入时返回 `idempotency_conflict`，不得改字段硬凑。
6. Bake 期间普通提交冻结；同机多进程依靠 Mutex，跨机器没有分布式锁，必须依赖版本控制、revision/CAS 和合并后重新 preview。

## 失败状态与恢复动作

| 状态 | 含义 | 唯一安全动作 |
|---|---|---|
| `editor_thread_required` | 不在 Unity Editor 主线程 | 切回目标 Editor 主线程后重新 preview |
| `editor_busy` / `registration_busy` | Editor 状态或注册互斥锁不允许写入 | 等状态稳定，重新 preview；不绕过锁 |
| `preview_required` | 当前进程没有匹配资格，或 Domain Reload/输入变化 | 重新执行 `commit=false`，使用新回执 |
| `idempotency_conflict` | requestId 被不同语义复用 | 新建 requestId 并重新 preview |
| `identity_conflict` | GUID/LocalFileId/type 不匹配 | 重新解析精确资产；不得改用路径或名字 |
| `concurrency_conflict` | revision 或当前 Key 已变化 | 重新读取当前目标并重新 preview |
| `target_dirty` | 目标含未保存编辑 | 由用户处理/保存目标，再重新 preview；不得覆盖 |
| `key_conflict` | 稳定 Key 已占用或别名冲突 | 定位占用定义，明确取消或由用户选择未占用 Key |
| `bake_in_progress` | Bake 正在读取作者源 | 等待或取消 Bake，再重新 preview |
| `commit_failed` / `failed` | 写入或回滚异常 | 保留错误与 changedPaths，核对磁盘/内存目标；不得声称原子成功 |
| `pending` | 长任务已入队 | 按 runId 查询最终状态；不得声称 Bake 完成 |

### 持久化原子性边界

当前实现的多目标提交通过 `Undo.RecordObjects`、内存快照恢复和逐目标 `AssetDatabase.SaveAssetIfDirty` 组织补偿；它不是跨 source/group/consumer 的文件级事务。中途进程崩溃、磁盘写入失败或回滚保存失败时，可能留下部分持久化结果，现有静态测试也未证明 Undo 栈、磁盘重载和故障注入后的原子恢复。遇到这类故障只能保留 `commit_failed`/`rollback-failed` 证据，重新读取目标、GUID/LocalFileId、revision 和 Dirty 状态后再 preview；不得宣称“原子提交”或“完全回滚”。

StringKey 必须按 preview 输入原值回传；禁止 Trim、大小写归一化、自动替换或静默生成。主资产身份使用 GUID + LocalFileId(0)，子资产必须使用真实 LocalFileId。 

## 阶段与证据不得混称

```text
preview success  != commit success
commit success   != Bake complete
Bake pending     != Bake complete
源码/单测定义存在 != Unity Test Runner passed
Unity Handler 存在 != MCP client tools/list 可见
Editor 流程通过 != PlayMode / Player / 发布可用
```

MCP 客户端可用必须有真实客户端 `tools/list`、同一客户端 preview + commit + 幂等重放，以及精确 Unity 实例的 Server 日志。不得用 Unity Handler 单测或 legacy TCP 直发冒充 MCP 闭环。

## 提交前不可跳过检查

- [ ] 已选择唯一正确 Action；没有调用直接写列表、旧收集 API 或第二套入口。
- [ ] 当前目标 Unity Editor 实例和主线程已确认。
- [ ] preview 在当前进程成功，commit 使用同一 requestId 和相同语义输入。
- [ ] GUID、LocalFileId、type、revision、当前 Key 与所有 expected 字段来自本次 preview。
- [ ] 目标无 Dirty，Bake/注册锁空闲，StringKey 未归一化。
- [ ] GameCore 与普通资产正确分流；Info/Group/Root/Consumer 职责清楚。
- [ ] 写入只保存明确目标；失败路径恢复内存快照并报告回滚落盘异常。
- [ ] 幂等重放只复用完全相同 fingerprint；任一失败重试都重新 preview。
- [ ] pending、Handler、编译或测试定义没有被提升为运行/发布/MCP 验收。

## RequiredReads

- `Documentation/AIKnowledge/ESFramework/project-gamecore-stable-identity/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_统一内容注册唯一入口与事务边界_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md`
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESContentRegistrationContracts.cs`
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESContentRegistrationAuthoring.cs`
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESGameCoreRegistrationAuthoring.cs`
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESGameCoreRootRegistrationAuthoring.cs`

## RelatedSkills

- `es-gamecore-config-authoring`
- `es-gamecore-integration`
- `es-ai-knowledge-curation`
- `es-editor-tooling`

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_统一内容注册唯一入口与事务边界_AI协作警告.md` (`341c140c6745bacedaae0a0efb5d15e7b3e6f577ddcf13d7420c11c92ab071f7`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md` (`682d227e80853c3b66d758ffe23426711b05e29629c0faf9b3bf54de3dd89c88`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESContentRegistrationContracts.cs` (`f14296126acff7981e44f146b7fe51e0d1f2024d103e81794da999c6816ac0af`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESContentRegistrationAuthoring.cs` (`2184f8b6e14f4cb557e59cf813e34750105838c7155b9efbf973bb2abb9539ac`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESGameCoreRegistrationAuthoring.cs` (`2f7cbbfb99c43aeda0938d9165bdc88a7a264e8dd5efd0bcf2d721151a0730ed`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESGameCoreRootRegistrationAuthoring.cs` (`696ce1885acb43457bb2ba4653ea678c41d844dc62d5019f32b37ead57b385ba`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/Tests/ESContentRegistrationTests.cs` (`c773e81fa71707fd13fa49acbeff9ceec1f9ffb0a996308b1d53c4505fbd0eb0`)

## ExternalRefs

- Unity 2022.3 `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.TryGetGUIDAndLocalFileIdentifier.html` (retrieved SHA-256 `4305fbf00f83448784e0105e66ef6aa623ce58a5cfc6107efb45eb5932837baf`)
- Unity 2022.3 `Undo.RecordObject`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.RecordObject.html` (retrieved SHA-256 `64e22af38a58cc39f0ff2d8b1fe2723dad1a9cd147240ae772fd6b4cc721c107`)
- Unity 2022.3 `AssetDatabase.SaveAssetIfDirty`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.SaveAssetIfDirty.html` (retrieved SHA-256 `bb6000ff90682f6cecacb073436edd13c10a5b0886efc00db975bdde710d36aa`)

## EvidenceRefs

- `Assets/Plugins/ES/Editor/ESContentRegistration/Tests/ESContentRegistrationTests.cs` defines preview/commit/replay, dirty-target rejection, AssetKey CAS, GameCore root, GameCore group/consumer and MCP fail-closed cases; tests were not run in this task.

## StaleWhen

Any SourceRef hash changes; Unity version or cited editor API contract changes; preview qualification, identity resolution, revision/CAS, dirty-state policy, Undo/save/rollback, idempotency, root registration or Consumer synchronization changes; or current Unity evidence contradicts this S1 summary.
