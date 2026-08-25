# AssetDatabase 写入、导入与批处理事务

`KnowledgeId`: `es.unity.editor-assetdatabase-import-transaction.v1`

`Topic`: Unity Editor 资产文件写入、AssetDatabase 导入、批处理提交与失败恢复

`Authority`: `Current project source + AIWarnings P0 + versioned Unity reference snapshot`

`RouteKeys`: `editor`, `asset-database`, `create-asset`, `move-asset`, `delete-asset`, `import-asset`, `reimport`, `refresh`, `batch-asset-editing`, `meta-guid`, `partial-failure`, `rollback`, `idempotency`

`ContentHash`: `2cbb0551889e5088bfc57318f5e80dc5fc875461bb14aaf249689fbc18011a9d`

`EvidenceLevel`: `S1`

`RuntimeAcceptance`: `runtime-not-run`

`StaleWhen`: Unity 版本、AssetDatabase/AssetImporter 合同、路径策略、批处理配对、暂存/备份/提交/回滚实现、生成文件写入实现、GUID/local file ID 参考快照或任一 SourceRef 哈希变化。

`RelatedSkills`: `es-editor-tooling`, `es-api-contract-review`, `es-security-input-audit`

`RequiredReads`: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`、`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`

## Scope

本条目负责 Unity Editor 中普通资产路径的创建、文件写入、导入/重导入、移动、删除和批处理事务。它要求 AI 分开判断磁盘字节、AssetDatabase 可见对象、Importer 状态、持久身份和业务后置条件，防止把其中任一层的成功冒充整个事务成功。

本条目不负责：

- Prefab 内容和 `SaveAsPrefabAsset` 事务；由 `es.unity.editor-prefab-asset-transaction.v1` 负责。
- `SerializedObject`、多目标 Undo/Dirty；由 `es.unity.editor-serialized-undo-dirty.v1` 负责。
- GUID/local file ID 的通用身份语义；由 `es.unity.serialization-prefab-identity.v1` 负责。
- GameCore/AssetLibrary 内容注册、CAS 和 Consumer 同步；由 `esframework.project.gamecore-content-registration-transaction.v1` 负责。
- AssetPackage 业务分类、资源 Bake、Bundle、Manifest、Provider 或发布。

## Trigger and routing

- 自然语言触发：创建 Unity 资产、写入 Assets 文件、强制导入、刷新 AssetDatabase、修改 Importer、批量移动/删除/覆盖资产、保留 `.meta`/GUID、导入失败恢复、重复生成资产。
- 精确 routeKeys：`asset-database`, `create-asset`, `move-asset`, `delete-asset`, `import-asset`, `reimport`, `refresh`, `batch-asset-editing`, `meta-guid`, `partial-failure`。
- 预期命中：本条目加最多两个与目标对象直接相关的 canonical 条目。
- 相邻误命中：`prefab` 应转到 Prefab 事务；`content-registration` 应转到 GameCore 注册事务；`resource-plan`/`manifest` 应转到资源管线；纯字段编辑应转到 SerializedProperty 条目。
- 回退：目标到底是磁盘文件、Unity 主资产、Importer、Prefab、注册表还是发布产物无法唯一判断时，停止写入，回读调用源码和对应领域规则，不以文件扩展名猜测。

## Decision rules

### 可以继续

仅当以下条件同时成立时进入写入阶段：

1. 目标是规范化后的项目相对 `Assets/...` 路径，解析后仍位于项目 `Assets` 根内；没有 `..`、绝对路径、重解析点逃逸或隐式外部目录。
2. 已明确权威对象和操作类型：新建、覆盖、移动、删除、磁盘文件生成、Importer 修改或批处理。
3. 已记录目标是否存在、当前 GUID、相关 `.meta` 状态、必要的文件哈希和未提交工作树重叠；覆盖/删除必须另有恢复策略。
4. 已定义成功后置条件，至少包含重新按目标路径加载、类型核对和业务身份/内容核对，而不是只检查文件存在。
5. 批处理已定义 `StartAssetEditing` 的唯一 owner，并保证所有异常、取消和提前返回最终到达 `StopAssetEditing`。

### 必须先读取额外来源

- Prefab、Scene、ScriptableObject sub-asset、模型/纹理/音频 Importer、AssetPackage、GameCore 注册或资源发布任务，必须追加各自 canonical 条目、AIWarnings 和当前实现。
- 使用 `SaveAndReimport`、Importer 特有字段或版本敏感导入选项前，必须回读当前 Unity 版本对应的官方 API/包源码；本条目不把通用规则扩张为所有 Importer 的相同合同。
- 修改已有目标前，必须读取生成器/作者工具，确认它是可重建输出还是人工权威资产。

### 必须停止

- 目标身份、现有改动归属、覆盖授权、恢复来源或并行写入状态不明。
- 路径越界、目标 `.meta` 缺失/异常、预期 GUID 或文件哈希在提交前发生变化。
- `StartAssetEditing` 已进入但无法证明退出路径；先恢复批处理状态，不开始新事务。
- 暂存、导入、重载、类型或后置内容任一步失败；不得继续处理剩余目标并报告全量成功。
- 删除或覆盖没有可验证备份，或者回滚会覆盖事务开始后出现的外部修改。

### Stale、Deferred 和 Blocked

- 任一 SourceRef 或当前 Unity 版本变化：`stale`，重新读取并计算 ContentHash。
- 只完成静态设计、未启动 Unity：保持 `runtime-not-run`，需要导入/重开证据的结论为 `Deferred`。
- 没有当前用户修改指令，或计划目标超出其范围：`Blocked`；不得借 Skill、菜单存在或用户可见按钮扩大权限。AICommand/TaskContract 缺失只阻断选中的受管通道。
- `planTask` 能力不可用时标记 `PlanTaskUnavailable`，不能改写为 `NoMatchingCommand`。

## Operation selection matrix

| 目标动作 | 选择前必须确认 | 完成后必须证明 | 禁止替代 |
|---|---|---|---|
| `AssetDatabase.CreateAsset` 新建主资产 | 路径空闲、类型正确、父目录存在、重复执行策略 | `LoadAssetAtPath` 返回预期类型，身份和内容符合后置条件 | 文件存在、对象仍在内存 |
| 直接写入 `Assets/...` 文件 | 路径边界、编码/二进制格式、临时文件与替换策略 | 写入内容核对；需要 Unity 消费时再执行并验证导入 | 把写盘成功当导入成功 |
| `ImportAsset` | 精确目标路径和所需选项 | 目标按路径可加载，Importer/类型/内容符合预期 | `Refresh` 代替精确导入后的核对 |
| `Refresh` | 确实需要扫描磁盘变化，且影响范围可接受 | 只声明刷新请求已发出；仍核对目标对象 | 把 Refresh 当保存、编译或业务提交 |
| `SaveAssetIfDirty`/`SaveAssets` | 哪些 Unity 对象实际 Dirty、保存范围是否过宽 | 明确目标重新加载后的值 | 把保存当重导入或注册成功 |
| `SaveAndReimport` | 当前 Importer 类型、设置变化和版本合同 | 重新读取 Importer 与导入资产后置条件 | 无差别用于普通对象保存 |
| `MoveAsset`/`DeleteAsset` | 目标 GUID/哈希、引用影响、恢复和并发基线 | 新旧路径、GUID、引用和残留状态核对 | `File.Move/Delete` 绕开 AssetDatabase |
| `StartAssetEditing` 批处理 | 单 owner、有限目标、取消与异常路径 | `StopAssetEditing` 已执行，再刷新并逐项验证 | 无 `finally` 的成对调用 |

## Verified facts

| 静态事实 | SourceRef |
|---|---|
| `SimpleToolsSafetyUtility.RunAssetEditing` 在 `try/finally` 中配对 `StartAssetEditing`/`StopAssetEditing`，退出后调用 Refresh。 | `SimpleToolsSafetyUtility.cs` |
| AssetPackage 导出实现先在事务目录创建 staged/backup，停止批处理并强制刷新后验证暂存资产；提交前再次核对目标冲突、GUID 和文件哈希。 | `ESAssetPackageBakeData.cs` |
| 同一导出实现把回滚结果区分为 Complete/Partial；回滚时只删除仍由当前事务身份和哈希拥有的提交目标，避免覆盖外部变化。 | `ESAssetPackageBakeData.cs` |
| `ESGeneratedSourceFile` 验证目标仍位于项目根下的 `Assets/`，比较现有内容，通过临时文件替换，并把 AssetDatabase 导入设为独立选项。 | `ESGeneratedSourceFile.cs` |
| 当前版本化参考快照区分资产 GUID、local file ID 和会话 Instance ID，并记录 `TryGetGUIDAndLocalFileIdentifier` 的版本来源。 | `official-source-snapshot.md` |

以上只证明当前源码和快照中存在这些合同；不证明所有项目资产工具都已采用，也不证明 Unity 中实际执行成功。

## Common AI failure modes

| 错误行为 | 典型症状 | 根因 | 预防检查 | 正确动作 | 失败恢复 | 缺少的证据 |
|---|---|---|---|---|---|---|
| 把文件存在当导入完成 | 磁盘有文件但 `LoadAssetAtPath` 为 null/类型错误 | 混淆文件系统与 AssetDatabase 状态 | 分开记录 `fileWritten`、`importRequested`、`assetReloaded` | 精确导入并按路径重载核对 | 保留源文件和错误，停止后续消费者 | 当前 Unity 导入回执 |
| 用 `Refresh` 冒充保存/重导入 | 调用后直接报告完成 | 把全局扫描入口当事务提交 | 列出真正 Dirty 对象和 Importer | 使用匹配 API，再验证目标 | 回读磁盘、对象和 Importer 状态 | 重载后的对象/Importer 值 |
| 批处理中异常未退出 | 后续资产长期不可见或导入状态异常 | 无单 owner 和 `finally` | 静态检查所有控制流到 `StopAssetEditing` | `try/finally` 配对，退出后再刷新 | 优先恢复退出；本批标记失败 | Editor 当前刷新/导入状态 |
| 覆盖/移动破坏 GUID | 引用丢失、目标成为新资产 | 绕过 AssetDatabase 或忽略 `.meta` | 记录操作前 GUID/哈希和目标存在状态 | 通过 AssetDatabase 执行并做身份后检 | 从验证过的 before 恢复；不覆盖外部变化 | 引用图和重开验证 |
| 批量部分成功仍报全成功 | 一部分目标已提交，另一部分缺失 | 只有全局布尔值，没有逐项状态 | 每项记录 planned/staged/imported/committed/rolledBack | 返回逐项目结果和总体 Partial/Failed | 逆序回滚已拥有目标；单列回滚失败 | 每项目后置核对 |
| 重复执行制造重复内容 | 重复资产、子对象或身份变化 | 无稳定目标和幂等检查 | 运行前按稳定路径/身份读取现状 | Current 则 no-op；冲突则停止 | 标记冲突，不自动删除未知对象 | 第二次运行零意外差异 |

## Execution checklist

```text
开始前
[ ] 读 Start / CurrentStatus / RuleIndex 和领域规则
[ ] 验证 SourceRefs / ContentHash / Unity 版本
[ ] 明确目标权威、路径、类型、操作、权限和并行写入状态
[ ] 记录 existing/GUID/meta/hash/dirty 与恢复来源

实施中
[ ] 先完成全量预检，再开始第一项写入
[ ] 批处理只有一个 owner，并用 finally 保证 StopAssetEditing
[ ] 逐项记录 staged/imported/committed 状态和异常
[ ] 提交前重验目标身份与外部漂移
[ ] 取消只在定义的安全点生效

完成后
[ ] StopAssetEditing 已确认执行
[ ] 对精确目标执行必要的导入/刷新，不用调用存在代替结果
[ ] LoadAssetAtPath 重载并核对类型、身份和业务内容
[ ] 重复执行一次应 no-op 或产生可解释的稳定结果
[ ] Partial、rollback failure 和残留事务目录单独报告

禁止
[ ] 不用 File.Move/Delete 绕开 Unity 资产身份
[ ] 不把 SaveAssets、Refresh、文件存在、测试源码存在写成事务成功
[ ] 不在恢复时覆盖事务开始后出现的外部修改
```

## Evidence boundary

### Static 可以证明

- 本条目引用的当前源码包含路径检查、批处理配对、暂存/备份、身份复核、逐阶段刷新和部分回滚设计。
- SourceRef 哈希和 ContentHash 可以由 Knowledge 验证器确定性重算。

### Runtime 尚未证明

- 未启动 Unity，未真实执行 Create/Move/Delete/Import/Refresh/SaveAndReimport。
- 未验证 Domain Reload、编译触发、Importer 回调、并行导入、取消、磁盘锁、崩溃恢复或跨平台文件替换。
- 未执行重复批处理、故障注入、引用图检查、Editor 重开、Test Runner、Profiler、Player、IL2CPP 或发布。

因此，文件存在、API 调用返回、日志文本、按钮存在和测试定义都不得被提升为 Unity 资产事务已通过。

## EvidenceRefs

- 当前仅有 S1 源码和规则证据；Unity/Editor 执行证据为 `runtime-not-run`。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/SimpleToolsSafetyUtility.cs` (`408c3bb4c901b3a841895c3d84b9f311fe7d82b073c264db42dd5cbc7d75feeb`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESAssetPackageBakeData.cs` (`0d8195e1992cd734fc4df7f6c2e706cbd033b795eb8adfb5d9ba991d05544e21`)
- `Assets/Scripts/ESLogic/Editor/Generation/ESGeneratedSourceFile.cs` (`36add64de85a4f6d670763d649ac7c637672b51282431af71d0a9b8336453661`)
- `Documentation/AIKnowledge/UnitySerializationPrefab/official-source-snapshot.md` (`bc7aea27f30ad1e7a5af08747f30519979fef0227d1557d7f9f2543c26cf611b`)
