# 资源加载底层：RuntimeKey 稳定与旧输入坏引用

记录时间：2026-07-22

职责：这是 Codex 针对当前资源加载底层和 Unity 日志的协作记录，给后续 AI 快速接上上下文。

## 本轮 Unity 日志拆解

### 1. Missing Type：旧输入模块

Unity 日志：

```text
Missing types referenced from component Entity on game object :
ES.EntityAIInputSystemModule, ES_Logic

Could not update a managed instance value at property path 'managedReferences[...]',
with value 'ES_Logic ES.EntityInputStateModule'

The serialized array of [SerializeReference] objects is missing entry
```

结论：

- 这是旧场景/Prefab 里还保存着旧 SerializeReference 类型。
- 当前代码里不再保留 `EntityAIInputSystemModule`、`EntityInputStateModule`。
- 曾短暂考虑过加兼容壳，但已明确否决。
- 后续 AI 不要恢复这两个类型，不要写兼容壳。

正确处理：

- 打开对应场景/Prefab。
- 清理 Entity 上的坏 SerializeReference 模块引用。
- 保存场景/Prefab。
- 使用当前链路：`EntityAIDomain.inputState + EntityPlayerInputWriteModule + EntityAIInputDispatchModule`。

### 2. RuntimeKey changed by asset self override

Unity 日志示例：

```text
[ESAssetRegistry] RuntimeKey changed by asset self override
A=Other/runtime=30001/string=SingletonSeriMono/guid=...
B=Other/runtime=30007/string=SingletonSeriMono/guid=...
```

结论：

- 这是资源表 key 稳定性问题，不是普通噪音。
- 同一个 GUID 和同一个 StringKey 重复注入时不应生成新 runtimeKey。
- 根因是字符串 key 路径最终走了 `nextStringRuntimeKey++`，没有优先复用同 GUID 的旧 runtimeKey。

当前已做：

- `ESAssetRegistry.BakeRuntimeKey` 已改为以 `ESAssetPage` 为输入。
- 优先级改为：
  1. `enumKey != 0` 时使用 enumKey。
  2. 同 GUID 快照存在且 stringKey 没变时复用旧 runtimeKey。
  3. page 自身已有有效 runtimeKey 时复用。
  4. 当前查询表已有同 stringKey 时复用。
  5. 最后才自增分配。

后续验证：

- 重新触发 `ESAssetLibrary.InjectToAssetRegistryEditor()`。
- 观察是否还出现同 GUID 从 `30001` 变 `30007` 的 warning。
- 如果还出现，继续查 `RemovePagesBySourceLibrary`、`RememberSnapshot`、`SourceLibrary` 是否在注入前后被清掉。

### 3. Unity 管理员权限警告

结论：

- 不是项目代码问题。
- 但建议不要用管理员权限启动 Unity。

## 当前资源架构强制理解

`ESAssetLibrary` 是编辑器资产组织源，不是运行时表。

运行时目标链路：

```text
ESAssetLibrary / Book / Page
  -> Editor 收集、校验、烘焙
  -> ESAssetManifest / ESAssetTable
  -> GameManager AssetModule 初始化
  -> Loader 按 ESAssetRunMode 加载
```

四种模式：

- `EditorDirect`：GUID -> AssetDatabase。
- `EditorSimulateBuild`：(AssetKind, runtimeKey) -> Typed Manifest/Table -> 模拟发布地址。
- `LocalBuild`：(AssetKind, runtimeKey) -> Typed Manifest/Table -> StreamingAssets/本地包。
- `HotUpdate`：runtimeKey -> RemoteManifest/LocalCache -> 校验/下载/加载。

## 不要做的事

- 不要让运行时直接依赖 `ESAssetLibrary`。
- 不要恢复 Runtime AssemblyStream。
- 不要恢复旧输入模块类型。
- 不要把 RuntimeKey 抖动 warning 当作正常。
- 不要把 EditorDirect 的 GUID 加载扩散到真机运行时。
- 不要在业务层手写 AssetDatabase、BundleName、远端 URL。

## 推荐下一步

1. 在 Unity 中清理旧场景/Prefab 的 `EntityAIInputSystemModule`、`EntityInputStateModule` 坏引用。
2. 重跑资源 Library 注入，确认 RuntimeKey 不再抖动。
3. 准备正式 `ESAssetManifest / ESAssetTable / ESAssetLoader` 的最小数据结构。
4. 让 `ESGameManager.AssetModule.Initialize(runMode, manifest)` 成为资源启动入口。
