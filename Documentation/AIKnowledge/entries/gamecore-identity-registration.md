# GameCore、稳定身份与内容注册完整机制

`KnowledgeId`: `es.project.gamecore-identity-registration.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `gamecore`, `config-key`, `stable-key`, `runtime-key`, `sodata`, `content-registration`, `transaction`  
`ContentHash`: `34ed50917d6a9d221642260efdd2158e025448e870125c510296308fd34ab335`

## AIWarnings 提示所对应的真实问题

AIWarnings 对“稳定 Key”“GameCore RuntimeData 驻留”“Info/Group/Pack”与“统一内容注册”的简述，实际约束的是四种不能混用的身份：策划组织键、业务稳定键、资产精确身份和进程内加速索引。源码没有提供一个可以互相回退的万能字符串键；它刻意把四层拆开。

## 四层身份

| 层 | 源码载体 | 生命周期与用途 | 禁止用途 |
|---|---|---|---|
| 策划组织键 | `SoDataInfo.KeyName` | Group 字典、SO 表格、编辑器定位、策划命名 | RuntimeKey、存档、网络协议、跨进程身份、ConfigKey 回退 |
| 业务稳定键 | `ESGameCoreConfigKey<T>.enumKey/stringKey`、`ESStableKey` | 同一强类型表中的业务身份；烘焙与运行时解析 | 用 `KeyName` 自动补全；跨类型表混查 |
| 资产精确身份 | GUID + LocalFileId + type/path | 精确定位主资产或子资产；预检/提交一致性 | 用显示名、路径猜测或首个同类型资产代替 |
| 运行时索引 | `runtimeKey` | 当前类型表、当前进程和当前活动槽位内的加速索引 | 持久化、联网、跨进程、Ready=false 时访问载荷 |

`ESGameCoreConfigKey<T>` 同时保存 `definitionGuid/definitionLocalFileId/definitionTypeName`，但运行时查表仍以 `enumKey/stringKey` 为业务键；定义资产身份用于编辑器选择与烘焙精确性，不改变运行时键语义。

## Info、Group、Pack 的职责

- `SoDataInfo` 是单条策划数据；`KeyName` 仅属于编辑期组织。
- `SoDataGroup<T>` 是同类型 Info 的作者字典。重复 Key 被拒绝；其 `InjectGameCoreTables` 只转发实现 `IGameCoreSO` 的条目，不反射猜测。
- `SoDataPack<T>` 是多个 Group 的显式聚合快照。它检查 Group 的 Info 类型，并按 Key 合并；重复键不会静默覆盖有效对象。
- Group 和 Pack 都不是“根 GameCore 的替代品”。根资产、依赖资产与 Consumer 快照由内容注册与烘焙链显式建立。

## RuntimeData 稳定驻留机制

`ESRetainedConfigKeyTable<T>` 为 EnumKey 和 StringKey 保留对象引用：

1. `AcquireRetained` 首次由工厂创建；后续同一业务键返回同一对象。
2. EnumKey 与 StringKey 若已指向不同对象，返回失败或抛出，不做静默合并。
3. `Clear/Remove` 只移除活动槽位；驻留映射保留。
4. `ESGameCoreRuntimeData.MarkReady(runtimeKey)` 只在提交成功后发生。
5. 退出活动表时 `MarkNotReady` 并调用 `ReleaseRuntimePayload`，释放重量级业务载荷，但不销毁稳定外壳。
6. `CommitRetained/TryCommitRetained` 失败时调用 `AbandonRetained`，回滚本次载荷；已提交对象不会被误释放。

因此“稳定驻留”指对象身份稳定，不代表资源 Handle、Unity 资产或领域载荷永久常驻，也不代表进入对象池。

## 内容注册事务

`ESContentRegistrationAuthoring` 是编辑器侧统一分发入口，覆盖普通资产注册、AssetKey 迁移、GameCore 注册、GameCore 根注册、Consumer 同步与 Bake。写入采用两阶段合同：

```text
preview(commit=false)
  -> 精确解析 GUID/LocalFileId
  -> 读取目标 revision、当前 key、dirty 状态
  -> 返回 expected* 资格
commit=true
  -> 再校验身份、revision CAS、当前 key 与 clean target
  -> Undo.RecordObject
  -> 写入并验证后置条件
  -> SaveAssets
  -> 失败时恢复原对象/集合并再次落盘
```

提交不能只凭路径；必须携带预检返回的 GUID、LocalFileId、revision，Key 迁移还必须携带当前 EnumKey/StringKey。目标在预检后变化时返回 `concurrency_conflict`，已 Dirty 时返回 `target_dirty`。重复提交按幂等结果处理，不允许第二次制造重复页或重复根引用。

## 验证入口

`ESContentRegistrationTests` 覆盖 preview/commit/replay、并行预检后的 revision 冲突、DryRun 不写、Dirty 目标阻断、AssetKey CAS、GameCore 根注册与 Consumer 快照。它是 Editor 测试源码证据；没有本次 Unity Test Runner 结果时，不得宣称这些测试已经运行。

## 失败诊断

- 同一 RuntimeData 被两个稳定 StringKey 绑定：身份冲突，不能改写键规避。
- 使用 `KeyName` 作为运行时 fallback：跨层污染，应回到显式 ConfigKey 定义。
- Commit 没有 preview 资格：拒绝写入。
- Consumer 或源资产 revision 变化：重新 preview，不复用旧资格。
- RuntimeData 引用存在但 `Ready=false`：只允许重新装载/提交，不得读取旧载荷。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md` (`c6960fac99de98e02d304bca863a312314f065268f54f961f35cf61f68a847c7`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md` (`974957efac661db1bbbb7c578f5b4aa49c9a389a1e5a483ffd83ea766a436381`)
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs` (`ffe397fec6d57cbec8c8a7d388f45fbd8bf6705cd4b9ced389843830ebe4ad82`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/0-SoDataInfo.cs` (`4ceec7012317ad6ca0deeb410f111e8446eaf09f84410217f3bebb4313cc4d20`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/1-SoDataGroup.cs` (`d805db516c2fbc9f3471978c42dc32d77f9a015358e39bf92061b4647acaae07`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/2-SoDataPack.cs` (`e0ebdc30c11c6496107adfeba4d2d0a4af2bde02234ba6bbc555eb819ea2feda`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESContentRegistrationAuthoring.cs` (`2184f8b6e14f4cb557e59cf813e34750105838c7155b9efbf973bb2abb9539ac`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/Tests/ESContentRegistrationTests.cs` (`c773e81fa71707fd13fa49acbeff9ceec1f9ffb0a996308b1d53c4505fbd0eb0`)

`EvidenceLevel`: `S1`（源码与测试定义已检查，未运行 Unity Test Runner）  
`StaleWhen`: ConfigKey 表、RuntimeData 生命周期、Info/Group/Pack、内容注册事务或任一 SourceRef 变化。
