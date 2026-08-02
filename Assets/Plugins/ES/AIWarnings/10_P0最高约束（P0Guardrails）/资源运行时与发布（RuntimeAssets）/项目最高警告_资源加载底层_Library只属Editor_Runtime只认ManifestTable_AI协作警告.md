# 项目最高警告：资源加载底层，Library 只属 Editor，Runtime 只认 Manifest/Table

最后核对：2026-08-03

职责：这是 ESFramework 给后续 AI 的项目最高警告。当前正在搭建资源加载底层，任何资源系统、GameManager 启动流程、AssetRegistry、AssetLibrary、RuntimeKey、Manifest/Table 相关改动都必须先读本文件。

## 核心边界

### 当前发布文件索引权威（2026-07-25 最高约束）

当前正式发布链的物理文件定位只认 `ESAssetReleaseBundleIndex.json`。

完整寻址分层为：

```text
EnumKey / StringKey
  -> 当前类型 Catalog / AssetTable
  -> GUID 或 GUID + LocalFileId
  -> Library AssetBundleManifest 中的 AssetBundleKey + InternalName
  -> ESAssetReleaseBundleIndex 中的文件位置、Hash、CRC、Size、Dependencies
```

`ESAssetReleaseBundleIndex` 是运行时 `AssetBundleKey -> 物理文件` 的唯一权威，记录：

- `libraryFolder`；
- `fileUrl`；
- `localRelativePath`；
- `sha256`；
- `crc`；
- `size`；
- `dependencies`。

运行时禁止：

- 根据 Library 名称自行猜测或拼接 AB 文件名；
- 根据 `AssetBundleKey` 自行生成 CDN URL；
- 绕过全局 Bundle 索引直接相信 Library Manifest 中的物理路径；
- 索引冲突、GUID 冲突或依赖缺失时记录日志后继续运行。

Library Manifest 只负责 `GUID -> AssetBundleKey + InternalName`，其 Bundle 文件字段只用于和全局索引交叉校验。下载、缓存、重试及 RuntimeMap 构建必须以全局 Bundle 索引为准。

本地已验证文件索引必须携带 `releaseVersion`。发布版本不匹配时不得直接信任旧验证结果，必须重新校验文件 Hash。

`ESAssetLibrary` 只属于编辑器。

它的职责是：

- 给开发者拖资产。
- 按 Book/Page 分类。
- 自动收集指定文件夹。
- 校验重复 key。
- 生成/维护编辑器侧配置。
- 烘焙导出运行时 Manifest/Table。
- 服务 ResWindow/AssetWindow 等编辑器工具。

它不是运行时资产数据库本体。不要让正式运行时依赖 `ESAssetLibrary`。

运行时只认：

- `ESAssetManifest`
- `ESAssetTable`
- `ESAssetRecord`
- `runtimeKey`
- 当前 `ESAssetRunMode`
- 当前 Loader

正确链路：

```text
Editor:
ESAssetLibrary / Book / Page
  -> 收集资产、分类、校验、生成 key
  -> 烘焙导出 Runtime Manifest / AssetTable

Runtime:
ESAssetManifest / ESAssetTable
  -> runtimeKey 查表
  -> Loader 按当前 ESAssetRunMode 加载
```

## 四种加载模式

`EditorDirect`

- 中文名：编辑器直连模式。
- 机制：`editorGuid -> AssetDatabase.GUIDToAssetPath -> LoadAssetAtPath`。
- 用途：开发期最快跑起来，不要求构建资源包。
- 限制：只在 Editor 可用，真机构建不能走。

`EditorSimulateBuild`

- 中文名：编辑器模拟发布模式。
- 机制：`(AssetKind, runtimeKey) -> Typed Manifest/Table -> 模拟构建后地址规则`。
- 用途：在 Editor 中提前验证发布版 key、路径、依赖、包规则。
- 限制：仍不是真机加载，但可以暴露大部分发布链路错误。

`LocalBuild`

- 中文名：本地构建资源模式。
- 机制：`(AssetKind, runtimeKey) -> Typed Manifest/Table -> StreamingAssets/本地包`。
- 用途：无热更或首包内资源加载。
- 限制：只信任本地构建产物。

`HotUpdate`

- 中文名：热更新资源模式。
- 机制：`runtimeKey -> RemoteManifest/LocalCache -> 校验/下载/加载`。
- 用途：正式商业发布。优先使用本地缓存，缺失或版本不符再下载远端资源。
- 限制：启动阶段必须先完成清单校验。

## GameManager 启动准备

GameManager 启动资源系统时，不应该持有 Library 作为运行时入口。

错误方向：

```csharp
ESGameManager.AssetModule.ActiveLibrary = someLibrary;
```

正确方向：

```csharp
ESGameManager.AssetModule.Initialize(runMode, manifest);
```

启动链路建议：

```text
BootScene
  -> 创建 ESGameManager
  -> 读取 ESGlobalResSetting.AssetRunMode
  -> 加载 Runtime Manifest
  -> 构建 ESAssetTable
  -> 创建对应 ESAssetLoader
  -> 资源系统 Ready
  -> RuntimeData / Input / Save 初始化
  -> SceneModule 加载 MainMenu 或首个游戏场景
```

业务层按实际所有权选择入口，不得把共享缓存驱逐、引用计数和独立租期混为同一套 `Release`：

```csharp
// 无显式所有权：Resident Scope 常驻到统一安全点，业务不 Release。
var prefab = await ESAssets.LoadAsync(prefabRefer);

// Unity Owner：Owner 销毁时结束自己的 Scope。
var clip = await ESAssets.LoadAsync(clipRefer, owner);

// 短期独立租期：仅这个 Lease 的 Dispose 生效一次。
using ESAssetTemporaryLease<TextAsset> lease =
    await ESAssets.TemporaryScope.LoadAsyncLease(rawRefer);
```

业务层不应该关心：

- GUID
- AssetDatabase
- BundleName
- StreamingAssets
- 远端 URL
- 本地缓存路径

这些都由 `RunMode + Manifest + Loader` 解决。

## P0：资源租期与 Scope 语义

资源 API 的返回资源相同，不代表持有语义相同。必须按以下规则使用：

| 入口 | 所有权 | Active Plan 命中规则 | 释放规则 |
| --- | --- | --- | --- |
| `ESAssets.LoadAsync(refer)` | 无独立 Owner；Resident/统一安全点语义 | 可以只读借用活动 Plan；未命中才进入 Resident Scope | 业务不调用 `Release()`；统一资源安全点才允许卸载。 |
| `ESAssets.LoadAsync(refer, owner)` | `owner` 对应的独立 Scope | 禁止只读借用；必须由 Owner Scope 自己取得 Provider Lease | Owner 销毁时由 `ESAssetOwnerTracker` 自动释放。 |
| `refer.TryLoad(owner, out asset)` / `ESAssets.TryGetOwned(...)` | 只观察该 Owner 现有 Scope 已持有的资产 | 禁止查询 Provider 全局缓存或活动 Plan | 未命中不得创建 Tracker、Scope 或加载；命中资产继续服从该 Owner Scope。 |
| `ESAssetTemporaryScope.LoadAsync(refer)` | Temporary Scope 的一次引用计数 | 禁止借用；必须保留独立 Temporary 持有 | 每次成功调用都必须同一 Scope、同一 identity 调用一次 `Release(refer)`；它不是“本次调用幂等”。 |
| `ESAssetTemporaryScope.LoadAsyncLease(refer)` | 独立 Lease Token | 禁止借用；必须保留该 Token 的独立租期 | 每次成功调用有独立租期；复制或重复 `Dispose()` 只会归还一次。 |
| `TryGetActivePlanAsset(identity, out asset)` | 明确的活动 Plan 只读借用 | 只允许已经接入 Plan 所有权结束通知的框架系统使用 | 借用者不得 Release；必须在 `ActivePlanAssetOwnershipEnding` 返回前停止使用。 |

强制约束：

- `ReferenceCount` 与 `LeaseCount` 是两条独立计数；任一计数尚未归零时都不得释放底层 Scope 持有。
- 同一普通 `ESAssetScope` 内，同一 identity 最多持有一次；它是 Owner 聚合，不是 Temporary Scope 的逐调用引用计数器。
- 安全点与 Provider 切换会推进 Temporary Lease generation 并清空旧 Token；旧 Lease 不得影响新一代 Scope 或新 Provider。
- Provider 切换期间新请求必须被阻止，旧 Scope 与旧 Provider 的迟到结果不得写回新状态。
- `ESAssets.TemporaryScope` 是全局框架域。业务可以取得它完成短期请求，但不得自行 `Dispose()`；生命周期服务才有权切换、失效或销毁它。
- `ESAssetScope.CreateScope`、Scope 生命周期与底层 `Release` 是 ResourcePlan/框架高级边界；普通业务优先使用无 Owner、Owner 或 Lease 三种入口。
- 不同 Owner 对同一 Identity 可以拥有不同的 Owner Scope 持有；这表达不同生命周期所有权，不是把每次函数调用转换成引用计数。同一 Owner Scope 内重复请求同一 Identity 仍只持有一次。
- Owner 命中 Provider 已缓存资产时允许复用同一个 Unity 对象和底层加载结果，但必须取得属于该 Owner Scope 的独立 Provider Lease；缓存命中不等于借用活动 Plan。
- 禁止因为 Owner 只需要一个资产就自动 retain 整个 ResourcePlan。资源加载 API 不得隐式延长地图、区域、剧情或模式的上层生命周期。
- `refer.TryLoad(out asset)` 只是不建立所有权的 Provider 缓存观察口，只能用于诊断或同一调用栈内的即时只读检查；禁止把返回资产写入组件字段、UI、播放状态、对象池实例或其他长期状态。需要保存结果时必须使用 Owner、Plan、Resident 或 Temporary/Lease 中一种明确语义。
- Owner 的同步热路径只能查询该 Owner 自己已经存在的 Scope；不得为了同步查询命中率退回 Provider 全局缓存，也不得在未命中时偷偷创建 Scope 或发起异步加载。
- `ESAssetRefer<T>` 只保存稳定资产身份与编辑器配置，不得自行保存运行时 Handle、充当隐式 Owner 或提供“Refer.Release() 释放资源”的新版语义。运行时持有必须落在 Resident、Owner Scope、ResourcePlan 或 Temporary/Lease 四类正式边界之一。

错误做法：把 `LoadAsyncLease` 当作普通计数入口、给 Resident 资源手工 Release、在 Provider 切换后继续使用旧 Scope/Lease、或为了“方便”销毁全局 `TemporaryScope`。

P2 记录：Lease Token 使用递增 `long` 并规避当前活动 Token 碰撞，实际回绕概率极低；它不是已证明的无限身份空间。极限回绕仍应保留为底层测试项，不得据此弱化 generation 校验或改回可复用公开 Handle。

## P0：瞬时使用者不得冒充资源 Owner，ActiveLink 必须实际驱动 Plan 与 Scope

ES 必须区分三层责任，不能因为最终取得的是同一个 Unity 资产，就把业务使用次数、生命周期持有和 RuntimeBackend 的实际资源引用计数混为一层：

```text
ActiveLinkList / Binder / 明确生命周期 Owner
  -> 持有 ResourcePlan retain 或 Owner Scope
  -> ResourcePlan 持有内部 ESAssetScope
  -> ESAssetScope 按 ESAssetIdentity 最多持有一次 Provider Lease
  -> Voice / VFX / 渲染请求 / 投射物 / 短时技能执行只借用已持有资产
```

RuntimeBackend 仍是资产、Bundle 与依赖实际引用计数的唯一权威。业务并发数量可以用于预算、仲裁、抢占、诊断和安全停止，但不得直接决定 Provider Lease 的增减或底层资源释放。

强制约束：

- 瞬时使用实例默认不是资源 Owner。禁止因为每次播放、渲染、发射、命中或短时执行而创建完整 `ESAssetScope`、增加 Temporary 引用、取得独立 Lease，或建立业务私有缓存/引用计数。
- 只有调用方确实拥有独立资源生命周期和独立释放权时，才允许使用 Owner Scope、Temporary 引用或独立 Lease；不得用本条误杀合法的短期独立租期。
- Voice generation、VFX generation、对象池版本等只负责隔离旧异步续体和复用串线，不得被解释成资源引用计数。
- Owner、Plan 或 Provider 生命周期结束前，相关系统必须先拒绝新请求，取消或隔离在途请求，停止或失效仍在借用资产的运行实例，再归还 Scope/Plan 持有。Scope `Dispose` 与 Provider 安全点实际卸载是两层动作，不得混写成同步销毁。

Scope 池化只允许复用内部重型容器状态，不得直接复用对外暴露的 `ESAssetScope` class 外壳。调用方可能长期保存已 Dispose 的 Scope 引用；若整个对象再次租给新 Owner，旧引用会越代访问新 Owner 的资源，形成 ABA 串线。Scope 外壳 Dispose 后必须永久失效；内部状态只有在全部在途请求完成、Provider Lease 已归还、生命周期监听已清空且已从 live/transition 集合摘除后才允许回池。池化不得改变 Scope 所有权粒度，也不得用于合理化每 Voice、每 VFX 或每次短时使用创建 Scope。

### ActiveLink 生命周期门禁

`ActiveLinkList` 是 ResourcePlan 的正式生命周期 Owner 之一，不是仅供展示的活动状态：

- 每次来自任意 `ActiveLinkList` 的真实激活必须对应一次 Plan retain；同一列表重复激活同一 Plan 必须幂等，不得重复 retain。
- 同一 Plan 同时存在于 `Core`、`Game`、`Override` 或其他不同列表时，每个列表拥有独立 retain；停用一个列表只能归还自己的持有，不得影响 Binder、直接调用、生命周期 Scope 或其他列表的 retain。
- 最后一个 Plan retain 归零后，才允许释放 Plan 内部 Scope。Plan Scope 内同一 Identity 最多持有一次，不按资产具体使用次数累加。
- Provider 尚未 Ready 时，ActiveLink 激活必须保留逻辑持有；Provider Ready 后必须应用对应 Plan retain，不能要求业务重新切换 ActiveLink 才生效。
- Provider Transition 必须保留 ActiveLink 的逻辑成员和准确 retain 数量，销毁旧 Plan Context、旧 Scope 与旧资产引用；新 Provider Ready 后按保留的 retain 数重新 Prepare，禁止复用旧 Provider 的 Scope、Lease、资产实例或迟到结果。
- ActiveLink Prepare 失败必须保留可诊断失败状态并允许统一恢复或显式重试；禁止出现“列表仍显示 Active，但资源永远未准备且没有恢复入口”的假激活状态。

### 活动 Plan 只读借用只属于无独立所有权入口

只有明确声明“没有独立所有权、生命周期服从活动 Plan 或统一安全点”的借用入口，才允许先查询当前活动 ResourcePlan 已登记的资产：

```text
查询 Active Plan Asset
  -> 借用入口命中：直接借用，不新增资源持有
  -> 借用入口未命中：再按该 API 明确声明的 Resident 语义处理
  -> Owner / Temporary / Lease 入口：不得进入此分支，始终建立自己的独立持有
```

因此，若新增 `ESAssets.LoadResolvedResidentAsync<T>(identity, token)` 或等价的无 Owner Resident 入口，它可以先执行与 `TryGetActivePlanAsset(identity, out asset)` 等价的活动 Plan 查询。若新增的是 Owner、Temporary 或独立 Lease 入口，则必须跳过活动 Plan 只读借用，并通过自己的 Scope/Token 建立独立持有。

禁止在 `ESAssets.LoadAsync(refer, owner)` 中命中 Active Plan 后直接返回资产。通用 API 无法证明 `owner` 生命周期短于 Plan；直接返回会导致 Owner 仍存活而 Plan Scope 已释放。也禁止为了解决该问题自动 retain 整个 Plan：同一资产可能属于多个 Plan，而且一个小资源请求不得隐式阻止整张地图或剧情资源计划退出。

Direct Clip、Prefab 序列化材质/贴图等 Unity 直接引用不建立使用实例侧资源持有。它们继承承载 Scene、Prefab 及其上层 ResourcePlan/Scope 的依赖闭包；承载 Owner 必须覆盖对象实例和实际使用期。Unity 字段仍指向资产，不代表允许在借用者存活时执行破坏性卸载。

最低验收门禁：

- 同一列表重复 Activate 不重复 retain；不同列表持有同一 Plan 时，释放一侧不影响另一侧。
- 活动 Plan 已持有的同一资产被受控借用 100 次，不新增 Resident、Temporary 或独立 Lease 持有。
- 同一 Owner 重复加载同一 Identity 100 次，Owner Scope 仍只持有一次；不同 Owner 各自拥有独立持有。
- Plan 释放后，仍存活 Owner 的资产持有继续有效；直到 Owner 销毁才归还自己的 Provider Lease。
- 无 Owner Resident/借用入口可以优先命中 Active Plan；Owner、Temporary 与 Lease 入口不得借用 Active Plan。
- Owner 同步查询只命中自己的既有 Scope；未命中不创建 Tracker/Scope。Provider 缓存即使已有同一对象，也不能被持久业务当作 Owner 已持有。
- Provider 未 Ready 时发生的激活，在 Ready 后能够应用；Provider Transition 后按准确 retain 数恢复。
- 旧 Scope、旧资产和迟到异步结果不能进入新 Provider；ActiveLink Prepare 失败可诊断、可恢复、可重试。

错误做法：每个 Voice 创建 Scope、用 `ActiveVoiceCount` 控制 Clip Provider Lease、ResourcePlan 已命中后仍追加 Resident 持有、Provider 重建时清空有效 ActiveLink 业务状态，或只恢复一次 Apply 而丢失多个列表的独立 retain。

## P0：Raw 资源边界

第一波 Raw 资源统一以 Unity `TextAsset` 承载，主要服务 `.bytes`、二进制配置及可由 `TextAsset` 导入的文本数据：

```text
ESAssetReferRawConfigKey
  -> ESAssetReferRawConfigData(TextAsset)
  -> ESRuntimeDataAsset.RawAssets / ResourcePlan.rawAssets
  -> 既有 Catalog、Bundle、Consumer、下载、缓存与 Scope
```

禁止为 Raw 新建平行下载器、缓存目录、Manifest、引用计数或 Provider。Raw 必须复用既有资源寻址、发布和生命周期链。

业务若需要在 `TextAsset`/Bundle 释放后继续读取内容，必须在自己的明确生命周期内读取 `TextAsset.bytes` 并持有独立 `byte[]`；不得缓存已失效的 `TextAsset` 或假定 Provider 一直保留其内部缓冲。Raw 的完整集成测试（Catalog -> Bundle/Remote -> Scope -> bytes 生命周期）仍未完成，不能写成运行时验收已通过。

## RuntimeKey 进程内边界（P0，最高约束）

`RuntimeKey` 只允许由当前进程中已经初始化的强类型 `ESConfigKeyTable<TData>` 自动生成。它只是当前类型表、当前表生命周期内的热路径加速索引，不是资产身份，也不是构建数据。

只保证：

- 同一个强类型表、同一次 `BeginBuild/Clear` 生命周期内，业务键解析得到一致的 RuntimeKey；
- RuntimeKey 只能交给产生它的同一张表解释；
- 表清理、重建、进程重启后 RuntimeKey 允许变化，调用方必须通过 EnumKey/StringKey 重新解析。

注入完成后的合法获取方式：

- 不需要了解 RuntimeData 的普通调用者优先使用领域表 `int runtimeKey = Table.InjectWith(...)`，允许失败时使用 `TryInjectWith(..., out int runtimeKey)`；
- GameCore 领域 Table 和根 SO 作者必须先 `AcquireRetained`，把全部准备逻辑放入 `try`，再调用 `CommitRetained/TryCommitRetained`；准备或提交失败必须 `AbandonRetained`；
- 既有 `Inject/TryInject/RegisterAndGetRuntimeKey` 只作为稳定 API 兼容入口继续具备提交回滚语义；新 GameCore 代码不得把它们当作事务模板，也不得手工写入 RuntimeKey；
- 批量注入器可以在外层统一 `BeginBuild/EndBuild`，但每条 GameCore 数据仍必须独立完成 Acquire、准备、Commit/Abandon；
- RuntimeData 的 `runtimeKey` 由 Table 在成功提交时自动写入，并保证早于 `Ready=true`；调用方只接收返回值，不得指定、恢复或持久化该字段；
- 已持有 RuntimeData 时直接读取 `data.runtimeKey`；
- 只持有 ConfigKey 时调用同一强类型表的 `GetRuntimeKey(configKey)`，不确定是否完成注入时使用 `TryGetRuntimeKey(configKey, out int)`；
- 上述值只允许缓存到当前表生命周期内的运行对象，不得写回 ConfigKey 或任何持久数据。

GameCore RuntimeData 的完整事务、稳定外壳、`Ready` 与载荷释放规则，以
`项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md` 为唯一权威；本节不得被解释为允许 Upsert、换实例或只设置 `Ready=false` 而保留重量级载荷。

禁止持久化到任何位置，包括但不限于：

- `ESAssetPage`、`ESAssetLibrary`、Book/Page 编辑器配置；
- Catalog、Manifest、GlobalAssetRuntimeMap、JSON、ScriptableObject 构建缓存；
- `ESAssetConfigKey`、`ESGameCoreConfigKey` 等 ConfigKey 定义；
- 任意 `SoDataInfo`、`SoDataGroup`、玩法配置或资源计划；
- 玩家存档、网络协议、服务端数据、跨进程通信与跨版本数据。

编辑器侧必须遵守：

- 普通 Inspector、Library 页面、拖拽 Solver 和配置复制工具不得显示、编辑或写入 RuntimeKey；
- 禁止提供“手工指定 30000+ RuntimeKey”、重绑定或恢复旧 RuntimeKey 的入口；
- 如确需诊断，只能在运行时表已构建后，以只读高级调试信息展示，并明确标注“当前进程临时值”；
- `ESAssetRegistry` 只镜像 EnumKey/StringKey/GUID+LocalFileId，不生成、快照、比较或冲突检查 RuntimeKey。

跨进程重新解析入口只能是：类别内 EnumKey/StringKey，或资产物理身份 GUID/GUID+LocalFileId。AssetTable 将业务键解析为资产身份；GlobalAssetRuntimeMap 再将资产身份解析为当前版本的 AssetBundle 物理位置。

## AssetTable 稳定外壳与资产释放边界（P0）

`ESAssetConfigKeyTable<TConfigData, TAsset>` 按类别内 EnumKey/StringKey 稳定驻留的是轻量配置外壳，不是实际 Unity 资产，也不是 AssetBundle Handle。

Key→稳定实例的共同机制由 ES 标准底层 `ESRetainedConfigKeyTable<TData>` 提供；AssetTable 必须继承该标准表，
只扩展 Unity 资产、Loader、Handle 与请求合并生命周期。禁止在 AssetTable 重新声明驻留字典或复制 `AcquireRetained` 算法。
标准底层不认识 Unity 资产和 AssetBundle，因此不得把 Loader/Handle 反向下沉到该基类。

Runtime Catalog/Page 注入必须通过强类型 `IESAssetConfigDataInitializer<TKey>` 与 `ESAssetConfigRecord` 完成；
禁止使用 `GetField`、`GetMethod`、`Invoke`、`Enum.ToObject`、表达式树或运行时生成委托写入分类 Key/Data，避免 IL2CPP 裁剪、AOT 和字段改名风险。

同一轮 AssetTable 构建中，任一 EnumKey/StringKey 别名已进入活动表后，后续记录必须按冲突跳过并保留首条权威记录；
禁止再次取得该活动外壳后先覆盖 Key、GUID、LocalFileId 或诊断字段，再依赖 Register 判重。重复项不得抛出普通业务异常、不得形成半覆盖状态。
稳定外壳跨 Catalog 重建时，初始化器必须完整刷新所有有来源字段，并明确清空无来源的旧字段，禁止旧版本残值穿透新 Catalog。

Catalog/Page 全量重建必须先经过两阶段输入预检门禁：在隔离预演表中完成全部 17 类的强类型 Key 转换、完整字段初始化、重复键判定、别名绑定与 RuntimeKey 注册；
只有预演全部成功后，正式表才允许进入 `BeginBuild(true)`、释放旧载荷并提交新记录。禁止先清正式活动表再逐条校验输入；任一能在预检阶段发现的非法记录或初始化异常发生时，
旧 Catalog 的稳定外壳、活动映射、RuntimeKey、Ready 状态、loadedAsset 与 Loader Handle 必须保持原样。

该能力正式命名为“预检保护重建”，不得宣称为任意提交故障下可回滚的严格原子事务。当前稳定外壳允许业务缓存具体 `TConfigData` 引用，Loader Handle 又属于外部 Provider；
在没有引入可交换 TableState、可交换 Payload 和交换后 Handle 回收协议前，正式提交阶段的 Loader.Release、再次初始化或多表顺序提交异常不具备回滚保证。
后续 AI 禁止仅靠 `try/finally`、吞异常或复制字典宣称补齐严格原子性。

普通业务只需学习业务 Key 直达 API：`TryGetReady(key, out asset)`、`GetOrLoadAsync(key, callback)`。
`Release(key)` 的正式语义是“驱逐该 Table 槽位的共享资产缓存”，只允许 ResourcePlan、Scope 或统一内存管理服务调用；
它不是调用者私有引用的 `Dispose`，禁止普通业务组件自行调用并影响其他消费者。
`int runtimeKey` 重载仅供初始化后缓存的高频路径使用，不得要求普通调用者先理解、持久化或手工管理 RuntimeKey。

稳定外壳保留：

- 类别内业务键及其别名；
- GUID 或 GUID + LocalFileId；
- Catalog 烘焙得到的轻量配置；
- 当前进程内重新注册后得到的 RuntimeKey。

可释放载荷包括：

- `loadedAsset` 强引用；
- `loadedAssetReady/Ready` 状态；
- Loader 持有的底层资源 Handle、AssetBundle 引用计数和依赖租约。

强制生命周期：

```text
首次 Catalog 注册
  -> 按 EnumKey/StringKey AcquireRetained 配置外壳
  -> 注册当前表并得到进程内 RuntimeKey
  -> Ready=false

GetOrLoadAsync
  -> Ready=true 时直接返回
  -> Ready=false 时由 Loader 加载，同 Key 合并请求
  -> 成功后写 loadedAsset 并置 Ready=true

Release
  -> 先断开 loadedAsset 并置 Ready=false
  -> 再释放 Loader Handle
  -> 不删除业务键映射，不回收或替换配置外壳

下一次 GetOrLoadAsync
  -> 使用同一配置外壳和当前 GUID 身份重新加载

Provider/Catalog 重建
  -> 在资源安全点等待请求结束
  -> 释放实际资产和旧 Provider Handle
  -> 清活动槽位但保留业务键对应的配置外壳
  -> 新 Catalog 对同 Key 必须复用原外壳并覆盖当前版本配置
```

禁止：

- 因 `Release` 删除 AssetTable 配置条目或换成新配置实例；
- 仅设置 `Ready=false`，却继续保留 `loadedAsset` 或 Loader Handle；
- 给 GameCore/Asset 定义外壳增加对象池，或把 Unity 资产、AssetBundle、Handle 放入任何定义外壳；
- Catalog 重建时对每个既有 Key 无条件 `new TConfigData()`；
- 把外壳驻留解释成资产永久常驻内存。

加载尚未完成时收到 `Release`，必须在完成回调处立即释放新取得的 Handle，保持 `Ready=false`，不得把迟到结果重新写回缓存。

Provider/Loader 切换时，`ResetLoader` 必须摘除所有 PendingLoad，并对每个合并请求回调一次明确的取消错误；
禁止直接 `pendingLoads.Clear()` 导致等待方永久悬挂。旧 Loader 必须先与 Table 解绑并 Dispose，迟到结果不得恢复 Ready 或重复回调。

调用 `Loader.LoadAsync` 必须位于 Table 的同步异常事务边界内；若后端在返回前抛出异常，必须立即通过统一完成路径摘除 PendingLoad、
通知全部合并请求并允许同 Key 重试。每个业务回调必须独立捕获异常，单个回调失败不得阻断后续等待者。

异步完成回调必须捕获本次 `PendingLoad` 实例和发起请求时的 Loader。完成时除 RuntimeKey 外还必须以 `ReferenceEquals` 校验当前 Pending
仍是该实例；Provider 切换后旧请求与同 Key 新请求不得串代。旧请求迟到返回的资产只允许交还给旧 Loader，禁止触碰新 Pending、Ready 或新回调。

## Consumer / Library 增量激活与内容入口

Consumer 与 Library 的按需激活必须复用 `MODULE_ESRuntimeDataModule` 的单一串行 Gate 和 active `HashSet`：

```text
进入 Gate
  -> 已在 active 集合：直接返回
  -> 下载并 ActivateReleaseAdditionAsync
  -> 成功后才加入 active 集合
  -> 失败：不加入，下一次允许重试
```

禁止为同一 Consumer/Library 另造共享 `UniTask` 等待字典、平行下载管理器或“失败也登记 active”的伪幂等。串行 Gate 的职责是保证 Provider/Catalog 合并期间没有竞争；它不应被替换为无状态并发请求堆叠。

当前实现的门禁字段是 `onDemandReleaseGate`、`activeConsumerIds` 和 `activeLibraryKeys`。
进入 Gate 后必须再次检查对应集合；只有 `ActivateReleaseAdditionAsync` 成功完成后才允许
加入集合。下载、版本校验或激活失败时不得写入 active 集合，下一次调用必须仍可重试。
Library 的去重键是规范化后的 `consumerId + "/" + libraryFolder`，不是裸文件路径。

`ESContentResourceBinding` 是已经存在的薄内容到资源声明，不是 `LevelDataInfo` 的替身，也不是地图、模式或区域模型：

```text
Editor：Consumer SO 是作者权威，Bake 写入稳定 Consumer ID。
Player：EnterContentResourcesAsync / LeaveContentResourcesAsync 只使用 bakedConsumerId + 调用方持有的 ESAssetScope。
Plan：binding.ActivePlan 由统一 ResourcePlan Apply/Release 持有。
```

- `EnterContentResourcesAsync` 进入的是 `binding.ActivePlan` 的一次 Plan retain；
  `LeaveContentResourcesAsync` 只归还这个 Plan retain，不直接删除 Consumer 或 Library 的下载缓存。
- `binding.ExitTransitionPlan` 是独立的退出过渡计划，必须通过
  `EnterExitTransitionResourcesAsync` 和独立的 transition Scope 进入；不能把它混入
  `ActivePlan`，也不能用内容生命周期 Scope 假装退出过渡 Scope。
- Consumer 文件离开内容后仍可留在当前发布版本缓存中，后续内容重新进入时复用；是否清理由
  发布版本、缓存策略和统一资源安全点决定，不能在 `LeaveContentResourcesAsync` 中直接删除。
- `Consumer` 引用缺失但 `requiresConsumer=false` 时，内容可以只依赖 baked Plan；若
  `requiresConsumer=true` 而 `bakedConsumerId` 缺失，必须阻断并要求重新 Bake。

`LevelDataInfo` 已撤销封存；Map、Mode、Region 尚未形成正式 Runtime 模型。后续 AI 不得为填补命名空白提前创建 Level/Map/Mode/Region 数据层、资源下载层或生命周期入口。

## AssetRegistry 现状

当前编辑器侧 `ESAssetRegistry` 是 Editor 配置镜像和查询表，不等价于最终 Runtime AssetTable。

它可以做：

- 编辑器注入。
- key 冲突提示。
- GUID 权威去重。
- 编辑器配置查询。
- 为后续 Manifest/Table 烘焙提供中间状态。

它不应该做：

- 真机资源加载。
- 热更新下载。
- 运行时直接依赖 Library。
- 生成、持久化、比较或恢复 RuntimeKey。

## 场景管理与资源系统边界

SceneModule 不应该直接知道 AssetBundle、Addressables、EditorDirect 细节。

合理调用：

```csharp
ESGameManager.Asset.LoadSceneAsync(sceneKey);
ESGameManager.Asset.Preload(groupKey);
ESGameManager.Asset.ReleaseScope(sceneScope);
```

资源 Scope 建议：

- `Global`
- `GameSession`
- `Scene`
- `UI`
- `Temporary`

场景结束时按 Scope 释放，而不是到处散落释放。

## 旧输入模块结论

旧类型：

- `EntityAIInputSystemModule`
- `EntityInputStateModule`

不再补兼容壳。不要为了旧场景/Prefab 的 Missing Type 恢复旧类型。

正确处理：

- 清理场景/Prefab 上的 SerializeReference 坏引用。
- 使用当前链路：`EntityAIDomain.inputState + EntityPlayerInputWriteModule + EntityAIInputDispatchModule`。
- 如果 Unity 报 `serialized array of [SerializeReference] objects is missing entry`，通过打开场景、移除坏引用、重新保存解决，不通过恢复旧代码解决。

## 管理员权限警告

Unity 管理员权限警告不是项目代码问题。

但开发时不建议用管理员权限启动 Unity，因为 Unity 会执行项目脚本和第三方二进制，管理员权限会放大误操作风险。

## 给后续 AI 的结论

资源系统当前最高原则：

```text
Library 是 Editor 编排源。
Manifest/Table 是 Runtime 查询源。
ESAssetReleaseBundleIndex 是 AssetBundle 物理文件定位权威。
RunMode 决定 Loader 行为。
RuntimeKey 仅在当前进程、当前强类型表、当前表生命周期内有效。
旧垃圾不恢复，坏引用清序列化。
```

任何试图让运行时直接依赖 `ESAssetLibrary`、持久化或手工指定 RuntimeKey、恢复旧输入兼容壳、或在业务层手写 AssetDatabase/Bundle 路径的方案，都应停止并重审。
