# 场景构建器、Prefab Override、Fixture 布局与备份权威

`KnowledgeId`: `es.unity.editor.project-scene-builder-authority.v1`
`Authority`: `Source + AIWarnings + Unity 2022.3 official documentation`
`RouteKeys`: `scene-validation`, `scene-builder`, `prefab-override`, `scene-fixture`, `scene-layout`, `scene-backup`, `backup-manifest`
`ContentHash`: `d8b667c0c2d81449ad38b75f41cfab2da804c873cec4c85398869ddc0113b0cb`

## 适用范围

本条目只描述项目测试场景的生成权威、Prefab 实例覆盖识别、Fixture 布局身份和变更前备份边界。它不授权修改 Builder、Prefab、Scene、Git、AIWarnings 或共享 Knowledge 路由。

当前项目声明 Unity `2022.3.45f1`。当前 Unity 进程、`PATH` 与 Unity Installer 注册信息未发现该版本 Editor（注册信息只指向旧的 2020.3 安装），因此不声称已读取 2022.3 本机 Editor 源码；Unity API 语义采用 Unity 2022.3 官方文档，项目行为仍以当前项目源码和 AIWarnings 为准。

## AI 必读执行协议（规范性）

AI 处理测试 Scene、Builder、Prefab Override 或 Fixture 任务时，必须先执行本节，再使用后面的事实说明。任一步无法证明时停止写入，并明确报告缺少的证据；不得用经验、旧 Scene 或“当前没有报错”补齐空白。

### 0. 新鲜度与权限门禁

1. 重新计算全部 `SourceRefs` 的 SHA-256，并按排序后的哈希重算 `ContentHash`。任一来源缺失或漂移，本条目立即视为 stale；回读漂移来源并重新规划，不能继续引用旧结论。
2. 记录当前分支、HEAD、staged/unstaged/untracked 路径，并检查目标 Builder、Prefab、Scene 和备份目录是否与已有改动重叠。不能确认改动归属时停止覆盖。
3. 区分用户授权、AICommand、Skill 和工具可见性。读取本条目不授权写 Scene、Prefab、源码、Git、备份、审计或发布状态。
4. 任何资产写入前必须证明 Unity 已退出 PlayMode、编译/导入已结束且没有并行写入任务。无法确认时按“仍在运行”处理。

### 1. 权威选择表

| 变更意图 | 必须修改的权威 | 禁止替代路径 | 写后动作 |
|---|---|---|---|
| 测试区域数量、ID、位置、标题、出生点、设施或 Guide 装配 | 对应 Scene Builder 的 Spec/装配代码 | 直接手改生成 `.unity` | 重跑 Builder，检查生成 diff |
| 可跨场景复用的角色、载具、相机结构或默认配置 | 正式 Prefab/Variant/配置作者源 | 在测试 Scene 实例上 Apply 未审计 override | 重新生成/实例化，再审计实例 |
| Builder 明确拥有的实例变换、父子关系或引用 | Scene Builder | 把 Scene override 当作者基线 | 重跑 Builder，确认结果收敛 |
| 明确的场景私有配置 | 只有在 Builder 明确不拥有且不会覆盖时，才允许由 Scene 持有 | 未证明所有权就手改 Scene | 记录例外原因、路径和重建后保留证据 |
| 变更前回滚副本 | `ES/Bak/Local/<TaskKey>` 或 `ES/Bak/Reviewed/<TaskKey>` | 用户目录、项目外临时目录、变更后伪 before 副本 | 校验来源、大小、时间和 SHA-256 |

无法唯一选择权威时，不做“最可能正确”的写入；先输出候选权威及冲突证据，请求收口。

### 2. 确定性执行顺序

```text
Freshness/权限检查
  -> 工作树与 Unity 状态快照
  -> 定位 Builder + 正式 Prefab/配置作者源
  -> 创建并校验 before 备份（仅在已授权写入时）
  -> 只修改选定权威
  -> 运行官方 Builder
  -> 审查生成 diff
  -> Prefab Override 分类
  -> 静态诊断
  -> 按授权逐级运行 PlayMode / Profiler / Player
  -> 分层报告证据与未运行项
```

同一作者源和输入重复运行 Builder 应趋于无意外差异。第二次生成仍出现无法解释的变化时，视为非幂等或存在隐藏输入，停止 Apply、提交和更高层验收，保留两次 diff 作为诊断证据。

### 3. Override 分类与处置

| 分类 | 判定证据 | 默认处置 |
|---|---|---|
| Builder 明确生成 | Builder 中存在对应赋值/装配，重建可复现 | 保留；记录生成位置 |
| Prefab/Variant 基线 | `GetCorrespondingObjectFromSource` 可解析且作者源包含该内容 | 在作者源修复，不把实例改动反向冒充基线 |
| 场景私有且已声明 | Builder 明确不拥有，重建后仍按设计保留 | 保留；记录例外与所有者 |
| 可疑属性或组件 | 不在 Builder、Prefab 基线或声明例外中 | 不自动 Apply/Revert；逐项记录来源与影响 |
| Missing source/script/reference | 源对象、脚本或序列化引用无法解析 | 阻断正式验证，先恢复或明确移除 |
| Default override | `HasPrefabInstanceAnyOverrides(instance, false)` 可能忽略 | 不能用 `false` 结果证明零差异；按需要显式包含并检查 |

禁止批量 Apply All 或 Revert All 代替分类。清理可疑 override 后必须重新加载 Scene/Prefab，并复查 Missing、旧类型名、无效引用和 override 数量。

### 4. 硬停止条件

出现任一条件即停止资产写入或提高验收结论：

- SourceRef/ContentHash 漂移但尚未回读权威来源；
- Builder、Prefab 作者源或场景私有所有权无法唯一确定；
- 目标路径与未确认的 staged/unstaged/untracked 改动重叠；
- Unity PlayMode、编译、导入或并行自动化状态不明；
- before 备份来自变更后文件、项目外路径，或缺少可核对哈希；
- Prefab source、script 或序列化引用 Missing；
- Builder 重跑不收敛，或生成 diff 超出任务范围；
- 只有静态/MCP clean 证据，却准备宣称 PlayMode、Profiler、Player 或发布通过。

## 已验证事实

### 1. Builder 是生成布局权威，Scene 是输出

- `ESPlayerControllerTestSceneBuilder.CreateOrRefresh` 先读取并验证正式玩家、载具和相机前置资产，再创建临时 Scene、构造根对象/环境/玩家/相机/载具/Guide，最后通过 `EditorSceneManager.SaveScene` 写入固定 `ScenePath` 并强制导入。
- 该入口明确不隐式重建正式玩家或载具 Prefab；需要准备正式资产时必须走显式的 `PrepareAssetsAndCreateOrRefresh`。因此正式 Prefab 作者源与测试 Scene Builder 是两个不同权威层，不能用生成 Scene 的实例修改反向替代任一作者源。
- AIWarnings 将 Builder 定义为布局、标题、出生点和验收导视的唯一权威。生成 `.unity` 是可重建输出；只修生成 Scene 而不修 Builder，会在下一次生成时被覆盖。
- Unity 2022.3 `EditorSceneManager.SaveScene` 官方文档仅证明该 API 将指定 Scene 保存到项目相对路径并以布尔值报告成功；项目代码对 `false` 抛出异常并在保存后重新导入、重新加载。

权威链为：

```text
正式 Prefab / Camera 作者源
          +
Scene Builder 中的布局与装配规则
          |
          v
生成的测试 Scene（可重建输出）
          |
          v
静态诊断 -> PlayMode -> Profiler / Player（逐级独立证据）
```

### 2. Fixture 布局身份在 Builder 的 Spec 中

- 玩家控制器 Fixture 由 `CreateAreaSpecs` 定义有序区域；每个区域携带 `number`、稳定 `id`、标题、分类、状态、位置、目标、预期、失败提示和真实输入 Action。
- `CreateTestEnvironment` 按 Spec 创建区域对象和 `ESDemoTestAreaMarker`，并以 `id` 建立 ordinal 字典；`CreateAreaFacilities` 通过 `layout.Require(id)` 将设施绑定到区域。缺失 ID 会抛出异常，而不是静默落到邻近区域。
- 区域状态是证据边界：`Ready` 表示该区域接入了当前可操作设施，`Planned` 只表示布局占位。源码对高翻越、移动平台、交互、战斗、武器、技能、Permit、锁敌和池化等 Planned 区域明确写有“不宣称能力通过”的预期。
- `ESSceneValidationGuide` 只属于测试场景根或 Diagnostics 子节点。路线、目标和检查必须显式配置；`ManualObservation` 不能伪装成自动通过，Guide 也不能替代 PlayMode、Profiler 或发布验收。

因此，Fixture 的可复现身份至少由 Builder 入口、稳定区域 ID、Spec 状态、设施绑定和生成路径共同组成。Scene 层手工移动对象、改标题或改出生点不是新的 Fixture 权威。

### 3. Prefab Override 检测是筛查，不是完整裁决

- Builder 使用 `PrefabUtility.InstantiatePrefab(prefab, scene)` 创建玩家和载具实例。Unity 2022.3 官方文档说明该 API 会在目标 Scene 中实例化对象并保留 Prefab 连接。
- 项目 Prefab 管理页先用 `IsPartOfPrefabInstance` 限定实例，再用 `HasPrefabInstanceAnyOverrides(obj, false)` 标记“有修改”，通过 `GetCorrespondingObjectFromSource` 解析源资产，并单独记录 Variant 和源丢失状态。
- Unity 2022.3 官方文档说明 `HasPrefabInstanceAnyOverrides` 是“不需要知道具体 override 时”的快速检查；当前调用传入 `includeDefaultOverrides = false`，因此默认 override 不计入该布尔结果。
- Unity 2022.3 官方文档提供 `GetObjectOverrides(prefabInstance, includeDefaultOverrides)` 来返回对象级 override 信息，但当前项目页没有用它枚举具体属性，也没有在这段扫描中证明 added/removed component 的逐项来源。

所以 `hasModifications = false` 不能单独证明实例与作者基线在所有审计维度完全等价；`true` 也不能单独证明修改错误。正式覆盖审计仍需同时比较：

1. 对应 Prefab/Variant 作者基线；
2. Builder 明确生成的实例变换、父子关系和配置；
3. Scene 序列化结果中的属性、组件、引用与 Missing 状态；
4. 每个差异的来源、必要性、清理或保留结论。

### 4. 备份必须保留 before 身份与项目边界

- `ES/Bak/Local/<TaskKey>/` 用于机器本地短期回滚，当前 `.gitignore` 明确忽略 `/ES/Bak/Local/`；它不是可审阅验收证据。
- `ES/Bak/Reviewed/<TaskKey>/` 用于用户要求保留、需要审阅或可复现的变更前基线。AIWarnings 要求每个 Reviewed 目录携带 `BACKUP_MANIFEST.md`，记录源路径、时间、原因、大小和 SHA-256。
- 两层副本必须来自同一份变更前源文件。项目外用户目录、修改后副本或没有来源/哈希的文件不能冒充 before 基线。
- Local 是否被忽略要由 `git check-ignore` 证明；Reviewed 是否进入审阅范围要由 `git status`/diff 证明。目录命名本身不构成 Git 或验收证据。

## AI 最小交付记录

AI 的结果必须至少包含以下字段；没有证据的字段写 `not-run` 或 `unconfirmed`，不得省略后让读者自行推断：

```text
branch / HEAD:
targetPaths:
authoritySources: Builder + Prefab/Config + generated Scene
sourceHashes:
unityVersion:
playModeStopped: true / false / unconfirmed
concurrentWriterState: clear / blocked / unconfirmed
backupLayer / taskKey / beforeHashes:
builderEntrypoint:
firstGenerationDiff:
repeatGenerationDiff:
overrideInventory: generated / baseline / scene-private / suspicious / missing / default
staticDiagnostic:
unityImport:
playMode:
profiler:
player:
remainingBlockers:
nonClaims:
```

判定口径固定为：只有对应字段存在当前可复现证据时才能报告该层通过；下层通过不自动提升上层，`not-run` 不是失败，但必须阻止依赖该层的完成或发布声明。

## 派生路由与登记状态

本条目适合 `scene-builder`、`prefab-override`、`scene-fixture`、`scene-layout`、`scene-backup` 和 `backup-manifest` 路由，现已登记到共享 `KnowledgeIndex.yaml` 与 `AIBRAIN_ENTRY.md`。AIBrain 自动发现只证明路由命中，仍须校验 SourceRef、当前 Builder/Fixture 和工作树事实。

## Unity 官方参考

以下页面于 2026-08-23 只读访问并返回 HTTP 200；它们是外部版本化参考，不参与项目内 `ContentHash`：

- Unity 2022.3 `PrefabUtility.InstantiatePrefab`: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.InstantiatePrefab.html
- Unity 2022.3 `PrefabUtility.HasPrefabInstanceAnyOverrides`: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.HasPrefabInstanceAnyOverrides.html
- Unity 2022.3 `PrefabUtility.GetObjectOverrides`: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.GetObjectOverrides.html
- Unity 2022.3 `EditorSceneManager.SaveScene`: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager.SaveScene.html

## 非宣称

- `runtime-not-run`：本任务未启动 Unity、未运行 Builder、未加载或保存 Scene，也未执行 Prefab Apply/Revert。
- 静态源码和哈希不证明 Unity 编译、Domain Reload、Scene 导入、PlayMode 操作、Profiler、Player、IL2CPP 或发布通过。
- 本条目不证明当前生成 Scene 与 Builder 完全一致，也不证明任一现存 Local/Reviewed 备份内容有效；这些结论需要面向具体资产的当前审计证据。
- 本条目不把项目 Prefab 管理页的粗粒度布尔筛查描述为完整 Prefab Override 审计器。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md` (`3bb8490dfdf42399110309ada24f51926fdd6b6894a7373f0ef583ec90c52cbc`)
- `Assets/Scripts/ESLogic/Editor/CharacterTemplates/ESPlayerControllerTestSceneBuilder.cs` (`78354577b02f89838905e08ab966eff107981bd5b7c8520ee66ca70b52986b59`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/HierchyTools/Simple_HierchyTool_Page_PrefabManagement.cs` (`c7289857a54f188b8912ffe3f4fc2e4a8707746b2be08766eb8af38053eac43c`)
- `Documentation/ES_SCENE_VALIDATION_GUIDE_STANDARD.md` (`2debe25a8da6d854270a17304291a600efe587251d9a7f4773b56eaa367d737b`)
- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `.gitignore` (`f496ad693801e69ca15d6149d64fc8fabd8ed0a59bead920478c8b7ade8c0d61`)

`EvidenceLevel`: `S1`
`StaleWhen`: Unity 版本、Scene Builder/Fixture Spec、Prefab 审计实现、Scene Guide 规范、备份分层或任一 SourceRef 哈希变化；共享索引登记后还需同步复核 RouteKeys 与 requiredReads。
