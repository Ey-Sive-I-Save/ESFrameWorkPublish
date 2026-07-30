# 项目最高警告：资源加载底层，Library 只属 Editor，Runtime 只认 Manifest/Table

最后核对：2026-07-29

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

业务层最终只应该这样用：

```csharp
var prefab = ESGameManager.AssetModule.Load<GameObject>(key);
var handle = ESGameManager.AssetModule.LoadAsync<AudioClip>(key);
ESGameManager.AssetModule.Release(handle);
```

业务层不应该关心：

- GUID
- AssetDatabase
- BundleName
- StreamingAssets
- 远端 URL
- 本地缓存路径

这些都由 `RunMode + Manifest + Loader` 解决。

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
