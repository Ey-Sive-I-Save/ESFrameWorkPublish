# 项目最高警告：GameCore RuntimeData 稳定驻留与事务注入

> 级别：P0。适用于所有 GameCore RuntimeData、强类型 Table、`InjectWith*`、根 SO 注入、Consumer 重载和资源安全点修改。

## 最高结论

GameCore RuntimeData 的标准闭环固定为：

```text
EnumKey/StringKey
→ AcquireRetained：按业务 Key 获取稳定外壳
→ 在 try 内准备全部载荷
→ CommitRetained/TryCommitRetained：原子提交
→ Table 写入实际 runtimeKey
→ 最后 Ready=true
→ O(1) 查询
→ Clear/Remove/Consumer 切换
→ Ready=false
→ ReleaseRuntimePayload：断开重量级引用
→ 同 Key 下一次注入复用原外壳
```

稳定的是 RuntimeData 对象身份，不是它持有的资源载荷。缓存旧引用是允许的，但每次读取业务字段前必须检查 `Ready`。

稳定外壳的 Key→实例绑定由 ES 标准底层 `ESRetainedConfigKeyTable<TData>` 统一提供。
`ESGameCoreConfigKeyTable<TData>` 必须继承该标准表，只扩展 GameCore 的事务提交、`Ready/runtimeKey` 与载荷回滚；
禁止在 GameCore 领域重新声明 `retainedByEnumKey/retainedByStringKey` 或复制驻留算法。

GameCore RuntimeData 是定义外壳，不是短生命周期运行实例，禁止实现 `IPoolableAuto` 或接入对象池。
首次出现业务 Key 时只创建一次外壳；之后 Clear/Remove/Consumer 切换都保留该实例，仅释放载荷并置 `Ready=false`。

## 固定类型模板

新增 GameCore 类别必须使用：

```csharp
public sealed class ESCategoryRuntimeData
    : ESGameCoreRuntimeData
{
    protected override void ReleaseRuntimePayload() { /* 断开重量级引用 */ }
}

public sealed class ESCategoryConfigKeyTable
    : ESGameCoreConfigKeyTable<ESCategoryRuntimeData>
{
    public ESCategoryConfigKeyTable(int capacity)
        : base(capacity, "GameCore.Category") { }
}
```

禁止以普通 `ESConfigKeyTable<T>`、Upsert 替换模型或每次重建新 RuntimeData 作为新 GameCore 类别模板。

## 三层使用边界

普通业务与策划代码：

- SO 由 Consumer 自动加载并注入；业务只通过强类型 EnumKey/StringKey 查询。
- 最小读取方式固定为 `Table.TryGet(key, out data)`，成功后检查 `data.Ready`；普通业务不调用 Acquire/Commit/Abandon。
- 动态权威数据使用领域表 `InjectWith/TryInjectWith`。
- 从领域默认对象生成次级定义使用 `InjectWithDefaults/TryInjectWithDefaults`。
- 不直接调用 `AcquireRetained`，不手工创建 RuntimeData，不手工维护 `Ready`。
- RuntimeKey 只是初始化后可选的热路径缓存；不了解 RuntimeKey 也能完成全部正常 GameCore 工作流。

领域 Table 与根 SO 作者：

- `AcquireRetained/TryAcquireRetained` 后必须立刻进入 `try`。
- `CreateRuntimeData`、默认值解析、filler、SO 字段复制、业务校验和 Commit 全部位于该 `try` 内。
- 所有异常必须在 `catch` 中调用 `AbandonRetained(data)` 后重新抛出。
- Try API 在准备阶段判定失败并返回 `false` 前，也必须先调用 `AbandonRetained(data)`。

底层 Table 扩展者：

- 成功使用 `CommitRetained`；可失败流程使用 `TryCommitRetained`。
- `CommitRetained/TryCommitRetained` 负责提交阶段的冲突与异常回滚。
- `AbandonRetained` 幂等，只清理尚未进入活动槽位的数据；不得破坏已提交记录。
- 既有 `Inject/TryInject/RegisterAndGetRuntimeKey` 仅为稳定 API 兼容入口；新 GameCore 注入实现必须显式使用 Commit API。
- `ESGameCoreConfigKeyTable<TData>` 不得使用 `new` 隐藏基表写入口来伪装访问控制。普通业务只使用领域 `InjectWith*` 入口；仍需保留但不面向普通业务的 ES 自有底层入口，应在权威声明处直接改为 `Internal_` 前缀。禁止仅为了“不让用户调用”而强制改成组合、只读 View、内部外壳或拆分程序集；`Internal_` 是使用边界标识，不是编译器权限控制。存量隐藏成员按触达调用链迁移，不得宣称已经强制封闭，也不得无授权全仓机械改名。

## 强制事务模板

```csharp
TData data = Table.AcquireRetained(key);
try
{
    PrepareAllPayload(data);
    return Table.CommitRetained(key, data, debugName);
}
catch
{
    Table.AbandonRetained(data);
    throw;
}
```

Try 入口必须遵循：

```csharp
if (!Table.TryAcquireRetained(key, out TData data))
    return false;

try
{
    if (!TryPrepareAllPayload(data))
    {
        Table.AbandonRetained(data);
        return false;
    }

    return Table.TryCommitRetained(key, data, out runtimeKey, debugName);
}
catch
{
    Table.AbandonRetained(data);
    throw;
}
```

重复 Abandon 是安全的。禁止只写 `catch { throw; }`，也禁止把 `CreateRuntimeData`、filler 或默认值解析放在 `try` 外。

## Ready 与载荷释放

- Table 成功注册后必须先把实际槽位 RuntimeKey 写入 `data.runtimeKey`，最后设置 `Ready=true`。
- `MarkNotReady` 必须先设置 `Ready=false`，再调用 `ReleaseRuntimePayload`。
- `ReleaseRuntimePayload` 必须断开 SO、SharedData、ExtraAsset、集合、轨道、状态配置等允许存在的重量级强引用。旧 RuntimeData 若仍有 Prefab/GameObject 字段，清理时也必须断开，但该字段属于待迁移遗留，禁止在新类别中继续使用；GameCore 与 Prefab 的 P0 单向依赖以“GameCore 根 SO 注入边界”警告为准。
- 底层 Asset Lease/Handle 仍由 `AssetScope` 安全点统一 Dispose；RuntimeData 不得直接重复调用 Loader Release。
- `runtimeKey`、Key 描述等轻量元数据允许保留，保证缓存引用可诊断；`Ready=false` 时禁止读取业务载荷。
- GameCore 定义外壳不得实现池接口、归还池或分配给其他业务 Key；需要池化的是 Buff 实例、技能执行上下文、Shot 实例等独立运行对象。

## RuntimeKey 规则

- RuntimeKey 仅属于当前强类型表、当前表生命周期和当前进程。
- RuntimeKey 由 Table 提交时自动写入；调用方不得在提交前指定、恢复或持久化。
- StringKey 后补 EnumKey 别名时，必须返回既有活动槽位的实际 RuntimeKey，不能返回本次临时 Bake 值。
- 存档、网络、构建产物、Manifest、Catalog 和 SO 一律保存 EnumKey/StringKey 或资产 GUID 身份，不保存 RuntimeKey。

## 禁止 Upsert 与换实例

GameCore 稳定表不采用“新实例覆盖旧实例”的 Upsert 模型：

- 同 Key 已 Ready：重复同一来源允许幂等返回；不同来源必须明确失败。
- Clear/Remove 后：同 Key 必须重新取得原稳定外壳并填充新载荷。
- 禁止把同 Key 绑定到新的 RuntimeData 实例。
- 禁止通过 Generation Handle 替代 `Ready` 热路径纪律，除非用户明确改变冻结设计。

## 性能边界

- EnumKey/StringKey/RuntimeKey 查询继续使用强类型字典 O(1)。
- 标准驻留层只增加初始化/重建冷路径上的 EnumKey/StringKey 字典查询；正常 RuntimeKey 查询不经过驻留字典。
- `Ready` 检查是缓存引用的唯一热路径附加成本。
- `AbandonRetained` 的活动槽位引用扫描只允许发生在失败或显式放弃冷路径，不得进入 Update/FixedTick。
- 禁止为注入事务引入协程、反射、中央工厂、每次注入分配的事务 class 或热路径委托分配。

## 权威入口文件

```text
Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs
Assets/Scripts/ESLogic/Data/GameCoreConfigKey/
Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/
Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs
```

## 验收清单

1. 六类或新增类别的 `ReleaseRuntimePayload` 覆盖全部重量级引用。
2. 所有 Acquire 后的准备逻辑都在 try 内。
3. 所有准备异常、filler 异常和 Try 提前失败都执行 Abandon。
4. 提交冲突后 `Ready=false`、载荷为空、同 Key 再 Acquire 仍是同一实例。
5. 成功提交后 `data.runtimeKey` 等于 Table 实际映射且 `Ready=true`。
6. Clear/Remove 后旧引用仍存在，但 `Ready=false` 且重量级载荷为空。
7. 编译 `ES_Design.csproj` 与 `ES_Logic.csproj`，运行 Unity EditMode 测试。
8. 检查 UTF-8、典型乱码和 `git diff --check`。

任何后续 AI 修改 GameCore RuntimeData 或注入链前，都必须先读本文件并回读当前源码；不得根据旧文档恢复 Register/Upsert/手工 RuntimeKey 方案。
