# 资源加载底层：RuntimeKey 旧结论废止与旧输入坏引用

记录时间：2026-07-22

职责：这是 Codex 针对当前资源加载底层和 Unity 日志的协作记录，给后续 AI 快速接上上下文。

> P0 更新（2026-07-28）：本文原先关于“持久化并稳定 RuntimeKey”的结论已经废止。RuntimeKey 现在只属于当前进程、当前强类型表、当前表生命周期；不得进入 ESAssetPage、Library、Catalog、Manifest、JSON 或 ConfigKey。

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

### 2. RuntimeKey changed by asset self override（历史日志，现已移除根因）

旧实现曾由 `ESAssetRegistry` 给页面分配并持久化 30000+ RuntimeKey，因此重复编辑器注入会出现抖动警告。这个设计本身就是错误边界，不应继续修补“稳定分配”。

当前结论：

- `ESAssetPage`、Library 注册表和编辑器查询表完全不拥有 RuntimeKey；
- `ESAssetRegistry.BakeRuntimeKey`、RuntimeKey 快照、冲突检查及手工改键入口必须不存在；
- RuntimeKey 仅由运行时强类型 `ESConfigKeyTable<TData>` 在当前表生命周期内自动生成；
- 表 `Clear/Rebuild` 或进程重启后允许变化；跨进程必须用 EnumKey/StringKey/GUID 重新解析；
- 后续若再看到页面序列化中的 `RuntimeKey:`，应删除旧字段，不得恢复兼容读取。

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
- `EditorSimulateBuild`：EnumKey/StringKey -> AssetTable -> GUID/GUID+LocalFileId -> 模拟构建产物。
- `LocalBuild`：EnumKey/StringKey -> AssetTable -> GlobalAssetRuntimeMap -> StreamingAssets/本地 AssetBundle。
- `HotUpdate`：EnumKey/StringKey -> 资产身份 -> RemoteManifest/LocalCache -> 校验/下载/加载。

## 不要做的事

- 不要让运行时直接依赖 `ESAssetLibrary`。
- 不要恢复 Runtime AssemblyStream。
- 不要恢复旧输入模块类型。
- 不要恢复页面 RuntimeKey、30000+ 手工入口或跨进程 RuntimeKey 兼容。
- 不要把 EditorDirect 的 GUID 加载扩散到真机运行时。
- 不要在业务层手写 AssetDatabase、BundleName、远端 URL。

## 推荐下一步

1. 在 Unity 中清理旧场景/Prefab 的 `EntityAIInputSystemModule`、`EntityInputStateModule` 坏引用。
2. 重跑资源 Library 注入，确认页面、Library 与 Catalog 不再产生 RuntimeKey。
3. 准备正式 `ESAssetManifest / ESAssetTable / ESAssetLoader` 的最小数据结构。
4. 让 `ESGameManager.AssetModule.Initialize(runMode, manifest)` 成为资源启动入口。
