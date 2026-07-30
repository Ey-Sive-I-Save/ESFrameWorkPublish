# ES Asset Runtime Generation

状态：`Reserved`，未来架构预留，当前不实施  
当前正式方案：Catalog/Page 预检保护重建  
立项触发条件：玩法运行中需要无停顿资源热插拔，并且切换失败后必须继续使用完整旧版本

## 1. 当前结论

当前 AssetTable 只在启动、编辑器刷新、加载界面、Consumer/DLC 激活或明确资源安全点重建。现有预检门禁会先在隔离表中验证 17 类强类型 Key、配置初始化、重复键、别名和 RuntimeKey 注册，再允许正式重建。

它保证预检可发现的非法输入不会破坏旧表，但不承诺正式提交阶段任意异常均可回滚。当前产品需求未要求玩法中无停顿切换，因此不得提前引入 Generation/Payload。

## 2. 为什么未来可能需要

现有正式重建仍是顺序操作：

```text
预检成功
→ 17 张正式表依次 BeginBuild(true)
→ 释放旧资产和 Loader Handle
→ 再次初始化稳定外壳
→ 逐条注册新映射
```

若正式提交过程中发生 Loader 释放异常、初始化异常或多表中途失败，旧运行环境可能已经被部分修改。启动或加载界面可以通过重新 Bootstrap 处理；玩法中无停顿热切换则不能接受这种状态。

## 3. 正确的原子边界

原子单位必须覆盖完整资源运行环境，不能只覆盖 17 张 AssetTable：

```text
ESAssetRuntimeGeneration
├─ GenerationId
├─ RuntimeMap / Catalog Schema
├─ 17 类不可变 TableState
├─ Config Payloads
├─ Runtime Provider
├─ Loader 与 Handle
└─ Resident / GameCore 预加载激活状态
```

如果只切换 AssetTable，而 Provider 或 RuntimeMap 已提前变化，仍会产生“新表查询旧 Provider”或“旧表查询新 Provider”的混合状态。

## 4. 提交模型

```text
完整构造 NewGeneration
→ 校验 RuntimeMap、Catalog Schema、17 类 TableState 与 Payload
→ 建立 Provider、Loader 和预加载候选状态
→ Current 保持指向 OldGeneration
→ Interlocked.Exchange(ref Current, NewGeneration)
→ 新业务操作进入新代
→ 等待旧代读者退出
→ 回收 OldGeneration、旧 Loader 和旧 Handle
```

唯一提交动作只能是 `Current` 引用交换。构建第 1、8 或 17 张 TableState 时失败都属于提交前失败，必须直接丢弃 NewGeneration，Current 不得变化。

## 5. Stable Shell 与 Payload

稳定外壳不能逐个切换 Payload，否则 17 张表仍存在半新半旧窗口。外壳只保留稳定身份，通过一次捕获的 Generation 读取该代不可变 Payload：

```csharp
ESAssetRuntimeGeneration generation = Volatile.Read(ref current);
ESAssetPayload payload = generation.GetPayload(shell.StableSlot);
```

一次同步或异步业务操作必须只捕获一次 Generation。禁止在操作中途反复读取全局 Current，否则同一次业务调用仍可能跨代。

`StableSlot` 是当前进程内稳定外壳的索引，不是 RuntimeKey，不得写入 Catalog、存档或网络协议。

## 6. RuntimeKey 代际规则

RuntimeKey 不需要禁止数值复用，正确身份是：

```text
GenerationId + RuntimeKey
```

相同整数可以出现在不同 Generation，但缓存、异步请求和完成回调必须携带 GenerationId。代际不匹配时，只能把结果交还原 Generation 的 Loader，禁止写入 Current。

## 7. 旧代读者生命周期

交换 Current 不代表旧代立刻无人使用，必须选择并冻结一种回收策略：

1. 严格限定 Unity 主线程资源安全点交换；
2. Generation Reader Lease；
3. Epoch 或引用计数，最后一个读者退出后回收。

Loader.Release 异常只能进入旧代回收重试或泄漏告警，不能回滚已经成功的 Current 交换。

## 8. 性能与内存预算

- 普通查表预计只增加 Generation/Payload 间接读取，不得增加 Dictionary 次数或托管分配。
- 热路径必须保持每帧 `0 GC`，实际损耗必须使用 IL2CPP Player Profiler 验证，不以编辑器微基准代替。
- 构建阶段会同时持有新旧元数据，元数据峰值接近两份。
- 若新旧两代真实资产同时预加载，受影响资产峰值内存可能接近两倍；默认方案应只双持元数据，资产按需加载。

## 9. 主要风险

- Table、ConfigData、Provider、Loader、Scope 和 GameCore 激活生命周期同时变化，改动范围大。
- 公共 ConfigData 字段可能需要改为只读属性或 Payload 转发，存在源码迁移成本。
- 异步回调遗漏 GenerationId 会形成跨代写入。
- Reader Lease/Epoch 实现错误会造成旧代过早释放或永久泄漏。
- RuntimeKey 缓存若缺少 GenerationId 会读取错误布局。
- IL2CPP/AOT 必须保持全程强类型，禁止反射生成分类表或 Payload 访问器。

## 10. 验收门禁

1. 构建第 1、8、17 张 TableState 时分别失败，Current 始终保持旧引用。
2. RuntimeMap、Catalog Schema、Payload、Provider 或预加载准备失败时，旧代仍完整可用。
3. `Interlocked.Exchange` 后所有新业务操作只看到新代。
4. 已捕获旧代的业务操作可以完成，并由 Reader 生命周期保护旧 Handle。
5. 旧 Provider 迟到回调不能写入新代。
6. Loader.Release 异常进入旧代回收重试/泄漏告警，不回滚新代。
7. 同 Key 新旧 GUID 切换正确，被删除的 Key 在新代明确不可查。
8. `GenerationId + RuntimeKey` 校验覆盖缓存、请求、回调和诊断。
9. IL2CPP Player 的查表热路径保持 `0 GC`，并记录 CPU 与峰值内存基线。

## 11. 明确非目标

- 当前不修改 AssetTable、ConfigData、RuntimeMap、Provider 或 Loader。
- 当前不增加 Generation、Payload、Lease、Epoch 或兼容门面。
- 当前不改变普通用户的 `TryGetReady`、`GetOrLoadAsync` API。
- Consumer/DLC 只在加载界面激活、失败允许重新 Bootstrap 时，不触发本项目立项。

