# GameCore 根 SO 与 RuntimeData 稳定驻留

`KnowledgeId`: `esframework.project.gamecore-root-runtime-data.v1`
`Authority`: `Source + AIWarnings + Unity official documentation`
`RouteKeys`: `gamecore`, `root-so`, `runtime-data`, `retained-shell`, `ready`, `sodata`, `group`, `pack`, `injection`
`ContentHash`: `89b987d312620d01caba6cefd47ae127c3d4bbd7984bf7f1f4533ec23016ef43`
`EvidenceLevel`: `S1 / runtime-not-run`

## Summary

GameCore 是启动期可独立加载的定义层。内容对象可以引用 GameCore 定义，但 GameCore 根 SO、嵌套配置和 RuntimeData 不得反向直接持有 Prefab、GameObject、Component 或场景对象；资源需求必须转换为稳定的类型化资产身份并由资源系统解析。

`IGameCoreSO` 只表示根 SO 可通过 `InjectGameCoreTables()` 注入核心表，不表示运行时表必须依赖 ScriptableObject。非 SO 的服务器、文件或程序生成来源仍可通过领域 `InjectWith/TryInjectWith` 进入相同强类型表。

`SoDataInfo.KeyName` 只服务编辑器组织、Group 字典和 SO 表格，不是 ConfigKey、RuntimeKey、存档或网络身份。`SoDataGroup<T>` 聚合同类型 Info，并只转发实现 `IGameCoreSO` 的条目；`SoDataPack<T>` 是多个 Group 的显式聚合，不是默认根，也不能用嵌套数据伪装独立核心定义。

## RuntimeData 生命周期

`ESRetainedConfigKeyTable<TData>` 按 EnumKey/StringKey 维持稳定对象外壳；`ESGameCoreConfigKeyTable<TData>` 在此基础上管理 `Ready`、实际 runtimeKey 和失败回滚：

```text
AcquireRetained
  -> try 内准备全部载荷
  -> CommitRetained / TryCommitRetained
  -> 写入实际 runtimeKey
  -> 最后 MarkReady
  -> Clear / Remove / Consumer 切换
  -> MarkNotReady
  -> ReleaseRuntimePayload
  -> 同一业务 Key 下次复用原外壳
```

稳定的是对象引用，不是重量级载荷。`Ready=false` 时旧引用只能用于诊断或重新装载，不能读取业务载荷。RuntimeData 不是短生命周期实例，不进入对象池；失败路径必须 `AbandonRetained`，且不得破坏已经提交的槽位。

当前已知实现缺口：`ESStoryDefinitionCatalog.BuildGeneration` 在逐条 `AcquireRetained` 后直接填充并提交，外层 `catch` 只结束 Build，未对当前候选逐条调用 `AbandonRetained`。若准备阶段赋值或提交前异常，不能把该路径描述成已闭合的 Acquire→try→Abandon 事务；应标记为 `transaction-gap`，修复或补充针对残留载荷的测试后再升级结论。

## AI 对象分类决策

| 观察到的对象 | 归属 | 正确动作 | 禁止动作 |
|---|---|---|---|
| 可独立复用、查询、版本化或迁移的正式内容定义 | GameCore 内容定义 | 设计显式 ConfigKey、强类型 Table 和 Consumer；若是可枚举 `SoDataInfo`，同时提供匹配 Group | 把冷却、耐久、弹药、目标等实例状态升级为全局 Key |
| 多条同类型、持续新增的 `SoDataInfo` | Info + `SoDataGroup<TInfo>` | 每个正式 Info 恰有一个主 Group；Group 只负责编辑器组织和启动聚合 | 复制第二套 RuntimeData/Key，或让一个 Info 拥有多个可编辑主 Group |
| 单例全局设置或 GameCore Root 唯一字段 | 明确根入口 | 可以不使用 Group，但必须有唯一、可验证的根所有者 | 为逃避 Group 规则而伪装成散落 `SoDataInfo` |
| 服务器、文件或程序生成的非 SO 权威数据 | 领域注入来源 | 使用强类型 Schema 与 `InjectWith/TryInjectWith` 进入同一 Table | 创建假的 SO 或让运行表依赖编辑器资产组织 |
| Buff 实例、技能执行上下文、Shot 实例等短生命周期状态 | 运行实例 | 由领域生命周期或 Pool 管理 | 继承 `ESGameCoreRuntimeData` 或进入 retained table |
| 跨 Group 检索、区域预热或发布下载 | 查询/ResourcePlan/Manifest | 分别使用只读索引、ResourcePlan、Manifest/Release Payload | 扩张通用 `SoDataPack<T>` 一次承担组织、启动、资源和发布 |

新业务依赖默认冻结 `SoDataPack<T>`。只有先明确唯一职责、成员权威、Consumer 归属、版本/差异/回退和自动化验证后，才可设计专用集合类型。

## 调用者分层

### 普通业务与 Consumer

```csharp
if (!Table.TryGet(stableKey, out TData data) || !data.Ready)
{
    // 未加载、已清理或配置缺失；按领域失败策略处理。
    return;
}

UsePayload(data);
```

- 只用强类型 EnumKey/StringKey 查询；可在初始化后缓存 runtimeKey 作为同一表生命周期内的加速。
- 不调用 Acquire/Commit/Abandon，不手工创建 RuntimeData，不手工设置 `Ready` 或 runtimeKey。
- 缓存 RuntimeData 引用后，每次读取载荷仍检查 `Ready`。

### 领域 Table 或根 SO 作者

```csharp
TData data = Table.AcquireRetained(key);
try
{
    PrepareAndValidateAllPayload(data);
    return Table.CommitRetained(key, data, debugName);
}
catch
{
    Table.AbandonRetained(data);
    throw;
}
```

- `AcquireRetained` 后立即进入 `try`；工厂、默认值、filler、SO 复制、业务校验和 Commit 全部在 `try` 内。
- Try 流程的任意提前 `false` 和任意异常都先 `AbandonRetained(data)`。
- 同 Key 已 `Ready` 时不得重新 Acquire；相同来源走幂等查询，不同来源明确冲突。

### 底层 Table 扩展者

- GameCore Table 继承 `ESGameCoreConfigKeyTable<TData>`；不得复制 retained 字典或改用普通 `ESConfigKeyTable<T>`/Upsert。
- 成功只由 Commit 路径先写实际 runtimeKey、最后 `Ready=true`。
- Clear/Remove 必须先 `Ready=false` 再释放重量级载荷；不得销毁或换绑稳定外壳。
- `ReleaseRuntimePayload` 断开 SO、集合、资源引用等载荷，但不重复释放由 AssetScope/Handle 所有的底层资源。

## 修改前不可跳过检查

- [ ] 对象是独立内容定义，不是实例状态或 Owner 内部局部槽位。
- [ ] GameCore 依赖方向始终为内容层引用 GameCore；核心层不反向直持 Prefab/场景对象。
- [ ] 可枚举 Info 有匹配的具体 Group、创建入口、唯一主 Group 和 Consumer 收集入口。
- [ ] `KeyName` 只用于编辑器组织；运行时身份来自显式 ConfigKey。
- [ ] RuntimeData 覆盖 `ReleaseRuntimePayload`，所有重量级引用都能在 NotReady 时断开。
- [ ] Acquire 后所有准备和提交都在 try 内，Try 提前失败与异常均 Abandon。
- [ ] 查询方检查 `TryGet` 和 `Ready`；没有依赖旧载荷、旧 runtimeKey 或对象池。
- [ ] 已定义冲突、重复注入、Clear/Remove、失败回滚和同 Key 重建测试；未运行时只声明测试定义存在。

## RequiredReads

- `Documentation/AIKnowledge/ESFramework/project-gamecore-stable-identity/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md`
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs`

## RelatedSkills

- `es-gamecore-integration`
- `es-gamecore-config-authoring`
- `es-ai-knowledge-curation`

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md` (`682d227e80853c3b66d758ffe23426711b05e29629c0faf9b3bf54de3dd89c88`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md` (`3d237b03c1b8acf59368e6293a374010e624ede948299351b0b6b268e432a34b`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_P0_Info必须对应Group_Pack非默认聚合_AI协作警告.md` (`39aa99c781fa08197c4c219c5d7d6310756fd1bf5a4f555691417728c1a90f2f`)
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs` (`08c4fda0e5ec09db552834ff2137314aec6244709ea7d40c9c0e276a9987c33e`)
- `Assets/Scripts/ESLogic/Runtime/Story/Definitions/ESStoryDefinitionCatalog.cs` (`df7be43d2e524d1c50a2bc3f6ab1c62831e64d6624b1c2d3ab0cf4f84db83231`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/0-SoDataInfo.cs` (`85bd3b3512aae56da1ebd0ef0bacbc98df8dbc2a742377c531fdb197ab7fe3ae`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/1-SoDataGroup.cs` (`899fbcd7cd7b989a1baa6ee5f829d1772cb56a8a6f80a066d6086bd6b27e2f6e`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/2-SoDataPack.cs` (`b07d4dfb9f53dfd0ea3b36e6c9d0e9a00acca34954d30e1315d2f189d846205c`)
- `Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs` (`94204e17e8fb557fa80e28d400a654cd2f711d3d42ca5e372d881a2033503bff`)

## ExternalRefs

- Unity 2022.3 `ScriptableObject`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ScriptableObject.html` (retrieved SHA-256 `fc8c46b1857a4927e9ccf2475a8681157eaf153f3241fa9a7c96eac3aecdabb5`)

## EvidenceRefs

- `Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs` contains retained-shell, commit ordering, clear/remove, rollback and alias-conflict test definitions; tests were not run in this task.

## StaleWhen

Any SourceRef hash changes; Unity version changes; `IGameCoreSO`, Info/Group/Pack aggregation, retained-table ownership, `Ready` ordering, payload release, or GameCore dependency direction changes; or new Unity runtime evidence contradicts this S1 summary.
