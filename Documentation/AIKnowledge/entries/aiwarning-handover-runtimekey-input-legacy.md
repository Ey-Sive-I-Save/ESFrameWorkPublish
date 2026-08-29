# 历史交接：RuntimeKey 与旧输入 SerializeReference

`KnowledgeId`: `es.aiwarning.handover.runtimekey-input-legacy.v1`  
`Authority`: `AIWarnings historical handover + current resource/input source`  
`RouteKeys`: `aiwarnings`, `handover`, `historical`, `runtimekey`, `asset-pipeline`, `legacy-input`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `78f42301085e5447f8a496d8ddfad5bf552f1119c496aef34a4851bd2316ddf5`  
`SourceSetHash`: `78f42301085e5447f8a496d8ddfad5bf552f1119c496aef34a4851bd2316ddf5`  
`EntryBodyHash`: `d45cf2f91e7d493ff553190bb2f11a16af492804866e684f4be9c28afd59a53d`  
`StaleWhen`: `历史记录解释、当前资源 P0 Warning 或输入/资源源码变化。`

## 保真迁移

原 Warning 94 行、3,922 UTF-8 字节；现 Warning 仅保留历史性质、禁止恢复项和 Knowledge 导航。旧 Unity 日志拆解、SerializeReference 坏引用、RuntimeKey 设计根因及清理步骤保存在本条目，不将历史日志当作当前证据。

## 旧输入引用

- 历史日志中的 Missing Type 指向已删除的 `EntityAIInputSystemModule` 与 `EntityInputStateModule`，属于旧场景/Prefab 的 SerializeReference 残留，不应恢复兼容类。
- 正确处置是打开对应场景/Prefab，清理坏引用并保存；当前链路使用 `EntityAIDomain.inputState`、`EntityPlayerInputWriteModule` 与 `EntityAIInputDispatchModule`。是否清理成功必须由 Unity 实机重读验证。

## RuntimeKey 与资源链

- “页面稳定 RuntimeKey”曾由 Registry 分配并持久化，导致重复注入抖动；该设计已废止。RuntimeKey 由当前强类型 `ESConfigKeyTable<TData>` 在表生命周期内生成，Clear/Rebuild/进程重启可变化，跨进程使用 EnumKey/StringKey/GUID 重新解析。
- RuntimeKey 不得进入 ESAssetPage、Library、Catalog、Manifest、JSON、ConfigKey、存档或网络；再次看到页面序列化 RuntimeKey 应删除旧字段，不做兼容读取。
- 资源链为 `ESAssetLibrary/Book/Page (Editor) → ESAssetManifest/ESAssetTable → GameManager AssetModule → Loader/ESAssetRunMode`。EditorDirect 仅限 Editor；LocalBuild/HotUpdate 必须使用正式 Manifest/Release 索引和校验。
- Unity 管理员权限提示不是项目代码证据，建议普通权限启动；这不替代资源或场景验收。

## 后续验证

清理旧场景/Prefab 后重读；重跑 Library/Manifest 注入，确认页面、Library、Catalog 不再拥有 RuntimeKey；建立最小 Manifest/Table/Loader 并验证 `AssetModule.Initialize(runMode, manifest)`。当前均未运行 Unity/Player/发布。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/历史上下文（HistoricalContext）/资源加载底层_RuntimeKey稳定与旧输入坏引用_给其他AI阅读.md` (`b41596dc1a7df77ca7a384a0937f3d8389022b7757202e60a50a6ea9e3420d14`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md` (`6ee72697e24d9dc57a3e6bc8c644f72e9b26b979d4a32ef47bbc7c49a895615d`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs` (`28578ef54995dbcc085e7856e237bffb0292914d7b3bcae34b8152b470a99b05`)
