# Codex Tool Rewrite Context

> Role: this Codex pass is responsible for ES editor-tool rewrite support and commercial-grade validation, not for redefining the whole gameplay architecture.
> Purpose: give future AI collaborators a dense, verifiable starting point before modifying or rewriting ES Framework tools.
> Scope: observations from the local project at `F:\aaProject\ESFrameWorkPublish` on 2026-07-17. Treat this as a tool-rewrite context map, not as product documentation.

## Responsibility Boundary

- Primary responsibility: validate, harden, and when requested rewrite small ES tools to a commercial standard.
- Main working surface: `Assets/Plugins/ES/Editor`, tool-related code in `Assets/Plugins/ES/0_Stand`, `Assets/Plugins/ES/1_Design`, and tool-facing data/assets they directly read or write.
- Secondary responsibility: identify architecture assumptions that affect tools, then verify them locally before changing behavior.
- Out of default scope: broad player runtime redesign, GameManager domain redesign, StateMachine/IK/Buff rewrites, or generated-data redesign unless the requested tool depends on them.
- Collaboration rule: if a tool rewrite touches runtime systems, first read the relevant `AIWarnings` note and the source code it names; do not infer from this file alone.

## Project Baseline

- Unity version is `2022.3.57f1c1`, from `ProjectSettings/ProjectVersion.txt`.
- Primary plugin root is `Assets/Plugins/ES`.
- Third-party plugins under `Assets/Plugins` include DOTween, Easy Save 3, RootMotion, and Sirenix/Odin Inspector.
- Main ES folders:
  - `0_Stand`: base framework layer, value types, containers, SO support, editor-safe utilities, AssemblyStream.
  - `1_Design`: design/runtime abstractions, input system definitions/services, domain/link/runtime mode tools.
  - `2_Feature`: 已迁空；不要在这里新增项目功能。`ESCommand` 已迁到 `Assets/Scripts/ESLogic/Runtime/Command`。
  - `Editor`: commercial validation focus; contains installer, menu-tree windows, drawers, GraphView, TrackView, resource and SO-data tools.
  - `3_Examples`: examples and test scenarios. Do not treat examples as production behavior without checking references.
  - `Generated`: generated Luban outputs.
  - `Obsolete`: legacy/preview code. Read only when current code references it or the user explicitly asks.

## Assembly Boundaries

- `Assets/Plugins/ES/0_Stand/ES_Stand.asmdef` has name `ES_Stand`.
- `Assets/Plugins/ES/1_Design/ES_Design.asmdef` references `ES_Stand`, `Sirenix`, and `Unity.InputSystem` by asmdef/GUID/package reference.
- `ES_Feature.asmdef` 已删除。`ESCommand` 当前随 `ES_Logic` 编译。
- `Assets/Plugins/ES/Editor/Installer/ESInstaller.asmdef` is Editor-only and `autoReferenced=false`; verify actual Unity compilation/availability before assuming it can be used from other assemblies.
- Many editor scripts are not in a dedicated visible asmdef under `Assets/Plugins/ES/Editor`; expect Unity's default editor assembly behavior unless a nearby asmdef is found.

## Menu and Window Entry Points

- Main menu path constants live in `Assets/Plugins/ES/0_Stand/Stand_Tools/OnlyEditor/MenuItemPathDefine.cs`.
- The root menu is `【ES】`.
- Core editor windows observed through `MenuItem` search:
  - `Editor/ESMenuTreeWindow/ResWindow/ESResWindow.cs`: `【资源管理】窗口`.
  - `Editor/ESMenuTreeWindow/SODataInfoWindow/ESSODataInfoWindow.cs`: `【SO】数据窗口`.
  - `Editor/ESMenuTreeWindow/SimpleToolsWindow/SimpleToolsWindow.cs`: `简单工具集成`.
  - `Editor/ESGraphView/Graphview-Define/ESGraphViewWindow.cs`: `【图】编辑器`.
  - `Editor/Installer/ESInstaller.cs`: dependency/install manager and dependency check menu items.
- Shared Odin menu-window base is `Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs`.

## Current Worktree Warning

- The repository already had many modified, deleted, and untracked files before this note was written.
- Important touched areas observed in `git status --short` include:
  - `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/EditorOnly/InfoType/*`
  - `Assets/Plugins/ES/1_Design/Input/*`
  - `Assets/Plugins/ES/Editor/ESDrawer/Normal/*`
  - `Assets/Plugins/ES/Editor/ESMenuTreeWindow/*`
  - `Assets/Scripts/ESLogic/*`
  - deleted `Assets/Plugins/ES/2_Feature/*`
- Do not revert, clean, or normalize these changes unless the user explicitly requests it.
- Before editing a file in a dirty area, inspect the file and its diff first. Assume changes belong to the user or another AI.

## Encoding Warning

- Use UTF-8 when reading source files with Chinese comments or menu strings.
- PowerShell default output can show mojibake for these files. Example: `Get-Content -Encoding UTF8`.
- Do not "fix" readable Chinese strings based only on garbled terminal output.

## Commercial Validation Priorities

For small-tool validation, prioritize in this order:

1. Compile and assembly availability: missing references, Editor-only leakage into runtime assemblies, asmdef dependency mistakes.
2. Tool entry reliability: `MenuItem` paths, window creation, initialization order, null static state, stale singleton/window references.
3. Data safety: asset writes, generated files, `.meta` preservation, destructive batch actions, path assumptions, dirty asset persistence.
4. Unity lifecycle: `InitializeOnLoad`, `delayCall`, `OnDestroy`, `OnDisable`, domain reload, play mode transition.
5. UX correctness for production use: clear errors, undo support, progress/cancel behavior, selection handling, disabled states, no silent partial success.
6. Dependency handling: Package Manager async request status, class-existence checks, optional vs required packages, offline/network failure behavior.

## Do Not Assume

- Do not assume `Obsolete` code is inactive; confirm references before deleting or ignoring behavior.
- Do not assume `Generated/Luban` files are hand-editable.
- Do not assume menu strings are duplicated bugs; some paths may intentionally expose legacy or test entries.
- Do not assume Odin is optional; several editor windows inherit Odin editor classes.
- Do not assume Easy Save 3 is part of ES core; it is a third-party plugin newly present in this workspace.

## Suggested First Checks For Any Tool

- Search exact class and menu path with `rg`.
- Read nearby `.asmdef` files and `using UnityEditor` placement.
- Check `git status --short -- <path>` before edits.
- If the tool writes assets, identify every path it can write before running it.
- Prefer narrow validation artifacts: one focused note, one focused test, or one small guard per issue.

## 2026-07-18 SimpleTools 核心推进收获与纠偏

职责重申：本文件作者当前负责“工具重写 / 商业级验证”。主要工作面是 `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow`，目标不是重做业务架构，而是把现有小工具从“能用的编辑器脚本”加固成可交付、可追责、可恢复、失败可见的生产工具。

### 今天已经验证过的事实

- `SimpleToolsWindow` 内置工具数量已经很多，不能按“单个按钮脚本”心态改。任何改动都要考虑 Undo、Dirty、`AssetDatabase`、场景脏标记、批处理失败摘要、中文 UI、路径边界和 Unity 版本 API。
- 局部编译验证命令有效：
  - `dotnet build Assembly-CSharp-Editor-firstpass.csproj --no-restore -v:minimal -p:BuildProjectReferences=false`
  - 截至本次记录，SimpleTools 相关修改用该命令通过，`0 warning, 0 error`。
- 整个项目完整编译曾被 SimpleTools 外的脏文件阻塞：`Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/EditorOnly/InfoType/ESSoTableDataRuleEditor.cs` 中存在 `DrawBatchFieldFilter` 未找到。不要把这个错误误归因到 SimpleTools。
- 新增的 `SimpleToolsSafetyUtility.cs` / `.meta` 是 SimpleTools 当前安全改造依赖，不是临时垃圾文件。不要清理。
- 当前工具重写策略已经从“直接执行清理”转为“预览、确认、隔离、保留失败项、可回滚、可复核”。

### 必须纠正的陈旧思想

- 不要再把“未使用资源”当成事实。现在应称为“疑似未使用资源”。引用分析无法证明代码动态加载、Addressables、Resources、反射、运行时拼路径等隐式入口。
- 不要再做永久删除。`AssetReferenceChecker` 的清理路径应走 `Assets/_ESToolQuarantine` 隔离区，并写清单；失败项必须留在列表里，不能因为部分成功就 `Clear()` 全部结果。
- 不要把 `FindObjectsOfType<T>()` 当成可靠场景扫描。它默认容易漏未激活对象，也可能造成分析口径和执行口径不一致。场景工具应优先从目标 Scene 的 root 递归 `GetComponentsInChildren(..., true)` 收集。
- 不要让 UI 文案比真实行为更激进。例如工具只是从快捷列表移除引用，就不要写“删除”；会修改路径就必须明确“会改变资源路径，可能影响引用”。
- 不要把导入设置修改当成普通场景优化。Texture/Audio Importer 是项目资产级变更，不只影响当前场景；必须有独立开关、预览、确认和回滚 JSON。
- 不要相信“成功弹窗”就代表全部成功。批量工具必须报告成功数、失败数、跳过数，并展示失败路径预览。

### 今日重点落地过的安全方向

- `AssetReferenceChecker`
  - 从永久删除改为隔离移动。
  - 保护入口资产和低置信度资源。
  - 批量选中/跳转时显示加载失败路径。
  - 隔离后只移除成功移动项，失败项保留给人工复核。
- `SceneOptimization`
  - 场景对象和组件收集统一为当前激活场景 root 递归，包含未激活对象。
  - 空对象、丢失脚本、静态标记、LOD、阴影、粒子、Collider、Renderer/Audio 反查不应再各自用不同扫描口径。
  - LOD 层级需要钳制，避免配置超出内部数组导致异常。
  - 项目资产导入设置必须走显式允许、预览、记录、回滚。
- `LightingSettings`
  - “所有灯光”应覆盖已加载场景内的未激活灯光，不应只看激活对象。
- `PrefabManagement`
  - 替换根对象时要保持原 Scene。否则多场景编辑下新 Prefab 可能被实例化到当前激活场景。
- `SceneManager`
  - 快捷资产列表的移除行为不是删除资产，按钮应叫“移除”。

### 后续 AI 修改 SimpleTools 前的硬性检查

1. 先跑高风险扫描：
   - `rg -n "DeleteAsset\\(|StartAssetEditing\\(|StopAssetEditing\\(|SaveAndReimport\\(|DestroyImmediate\\(|Undo\\.DestroyObjectImmediate\\(|AssetDatabase\\.ExportPackage\\(|File\\.WriteAllText\\(|File\\.WriteAllBytes\\(|File\\.ReadAllText\\(|File\\.WriteAllLines\\(|new StreamWriter\\(" Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow -S`
2. 再查场景扫描口径：
   - `rg -n "FindObjectsOfType<" Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow -S`
3. 改完至少跑局部编译：
   - `dotnet build Assembly-CSharp-Editor-firstpass.csproj --no-restore -v:minimal -p:BuildProjectReferences=false`
4. 发现中文乱码时，不要复制乱码扩散。用 `Get-Content -Encoding UTF8` 读取，必要时按原语义重写为正常中文。
5. 任何会写文件、移动资产、重导入、销毁场景对象的工具，都必须满足：预览、确认、Undo 或可恢复策略、Dirty/SaveAssets/MarkSceneDirty、失败摘要、路径边界检查。

## 2026-07-19 RuntimeWatch 核心理解与警告

职责重申：RuntimeWatch 属于本轮“工具重写 / 商业级验证”的重点工具。后续 AI 不要把它理解成单纯反射小面板，它现在的核心价值是用程序集注册数据和路径方案，把运行时可观察字段稳定、低损耗地投射到编辑器面板。

### 必须保留的设计事实

- RuntimeWatch 的能力边界不是两个示例类，而是“所有可由注册表解析出宿主链路的观察字段”：
  - 任意普通 `MonoBehaviour` 脚本：支持 Mono 直接字段，也支持 Mono 内部普通 C# 对象/可序列化对象的嵌套字段。
  - 任意 ES `Domain/Module`：支持 Module 直接字段，也支持 Module 内部普通 C# 对象/可序列化对象的嵌套字段。
  - 示例只是验证入口：普通 Mono 示例在 `Assets/Scripts/ESLogic/Samples/ESRuntimeWatchPlayground/RuntimeWatchPlaygroundMono.cs`；Entity Basic Module 示例在 `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicRuntimeWatchModule.cs`。
- 普通 Mono 脚本不是次级示例，`EntityBasicRuntimeWatchModule` 也不是唯一 Module 示例目标。后续任何业务 Mono、Domain、Module 只要字段打了 `[ESRuntimeWatch]` 且能被路径图追溯到宿主 Mono，都应纳入 RuntimeWatch。
- Module 不是靠全场景递归硬找出来的。`ESRuntimeWatchRegistry` 应通过 `IESHosting<TModule>` 建立 Module 到宿主 Mono 的快速链路，再由面板在宿主实例上读取 `ModulesIEnumable` 获得模块实例。
- Mono 只能作为根拥有者终点，不应作为字段路径中间节点继续向上递归。不要让 `Rigidbody`、`Transform`、`ScriptableObject` 或其他 `UnityEngine.Object` 原生对象进入中间路径图。
- 注册阶段必须轻量：`ER_ESRuntimeWatch` 通过编辑器程序集流发现带 `ESRuntimeWatchAttribute` 的字段后，只调用 `ESRuntimeWatchRegistry.RegisterField(attribute, fieldInfo)` 记录字段元数据。
- 重反射和路径方案构建必须延迟到面板真正访问时触发，例如访问 `Entries`、`OwnerTypes` 或 `GetEntriesForOwnerType(...)`。不要在 Unity 启动、域重载或普通运行时主动扫大图。
- 面板扫描只应针对注册表给出的 owner 类型，在当前场景 root 下找匹配 Mono；不应遍历所有 Active 脚本后再深度反射所有字段。
- Tag 过滤语义是“根对象 Tag 必须匹配 `requiredTag`”，不是被观察字段所在内部对象的 Tag。
- Odin `showIf` 表达式应在被观察字段的上下文对象上求值：普通 Mono 直字段是 Mono 实例，嵌套字段是嵌套对象，Module 字段是模块实例或模块内部对象。

### 不要回退到这些错误方案

- 不要把 RuntimeWatch 改回“每次刷新递归扫描所有组件字段”的方案；这会把性能成本从一次方案构建变成持续运行时损耗。
- 不要在面板打开时对 `parentEdgesByChildType.Keys` 或所有 Module 类型全量预计算 owner scheme。2026-07-19 已确认这会在大项目里组合爆炸，表现为打开 RuntimeWatch 面板后内存上涨、编辑器长时间卡死。正确做法是只对已注册 `[ESRuntimeWatch]` 字段的声明类型按需解析，并设置每个目标类型的路径方案上限。
- 不要为了支持 Module 而递归穿透所有对象图。Module 应走独立快速链路，普通嵌套对象才走有限、可缓存的字段路径方案。
- 不要把当前场景中所有 `MonoBehaviour` 都当作候选 owner。候选类型应来自注册表，面板再按类型查实例。
- 不要把 `ESRuntimeWatchAttribute.requiredTag` 做成全局面板过滤器。它是字段级约束，并且检查的是宿主 Mono 的 `transform.root.tag`。
- 不要把 RuntimeWatch 示例或支持范围只写成 EntityBasicRuntimeWatchModule。必须保留普通 Mono 示例，并明确所有业务 Mono、所有 Module/Domain 及其普通嵌套对象字段都属于目标能力。

### 当前验证入口

- 注册器核心：`Assets/Plugins/ES/0_Stand/Attributes/FlagOrTag/ESRuntimeWatchRegistry.cs`
- 属性定义：`Assets/Plugins/ES/0_Stand/Attributes/FlagOrTag/ESRuntimeWatchAttribute.cs`
- 编辑器程序集流注册：`Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/ESTools/ER_ESRuntimeWatch.cs`
- 面板实现：`Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/ESTools/Simple_ESTool_Page_RuntimeWatch.cs`
- 普通 Mono 示例：`Assets/Scripts/ESLogic/Samples/ESRuntimeWatchPlayground/RuntimeWatchPlaygroundMono.cs`
- Entity Basic Module 示例：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicRuntimeWatchModule.cs`

### 修改后的最低验证

- `dotnet build ES_Stand.csproj -v:minimal --no-dependencies`
- `dotnet build ES_Logic.csproj -v:minimal --no-dependencies`
- `dotnet build Assembly-CSharp-Editor-firstpass.csproj -v:minimal --no-dependencies`

如果完整依赖编译失败，先确认是否仍是 `Assets/Scripts/ESLogic` 中缺失或删除文件导致，不要直接归因到 RuntimeWatch。

## 2026-07-22 ObjectPool 工具重写与 SimpleTools 后续警告

职责重申：本段仍属于 Codex 的“工具重写 / 商业级验证”职责，不是对象池运行时架构重定义。后续 AI 修改 `SimpleToolsWindow` 时，应优先保持工具真实、可验证、低误导，而不是堆新按钮。

### ObjectPool 工具当前边界

当前工具文件：

```text
Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/ESTools/Simple_ESTool_Page_ObjectPool.cs
```

当前只允许收成四个真实页签：

```text
运行时统计
PrefabPrewarmDataInfo审计
GameManager接入
PlayMode池组状态
```

不要再恢复这些伪入口：

```text
预制件池化
预热条目录入
Selection.objects 读取 Project Prefab
Selection.objects 读取 Hierarchy Prefab 实例
GetNearestPrefabInstanceRoot
GetCorrespondingObjectFromSource
从当前选中 Prefab 自动加入预热配置
```

原因：Project/Hierarchy 选中 Prefab 只能说明“当前选择了一个 Prefab 资产或实例”，不能说明它已经进入 ES 根池化体系。把它做成“预制件池化入口”会误导开发者，以为录入行为等价于 GameManager 对象池接入。

### PrefabPrewarmDataInfo 的正确查询方式

`PrefabPrewarmDataInfo` 是 `SoDataInfo/ESSO` 链路资产。编辑器工具查询它时应走 SOS 高速通道：

```csharp
ESEditorSO.SOS.GetNewGroupOfType<PrefabPrewarmDataInfo>()
```

不要再用：

```csharp
AssetDatabase.FindAssets("t:PrefabPrewarmDataInfo")
```

更广义规则：只有明确继承 `ESSO` 的类型才享受 SOS 快速返回。接口类型、普通 `ScriptableObject`、不确定继承链的类型，不能因为某些实现类可能是 ESSO 就直接跳 SOS 早退，否则会漏掉非 ESSO 实现。

已经确认过的 ESSO/SOS 修正方向包括：

```text
PrefabPrewarmDataInfo
StateMachineConfig
ESInputConfig
ESSoTableDataRule
ESAssetPackageBakeData
ESAssetLibrary
ESGlobalProjectAssetGuideData
```

后续如果发现 `FindAllSOAssetsQuickly`、`Quick_InitAsset<T>()`、`LoadAssetAtPath` 包装层内部绕过 SOS，要先判断目标类型是否真是 ESSO 子类。是 ESSO 子类才改高速通道；接口和普通 SO 不要乱改。

### GameManager 接入页的真实语义

`GameManager接入` 页只应做以下事情：

```text
定位当前场景 ESGameManager
获取或创建 ESGameObjectPoolModule
把当前目标 PrefabPrewarmDataInfo 接入 pool.prewarmSources
从 pool.prewarmSources 移除当前目标 PrefabPrewarmDataInfo
PlayMode 下调用运行时加载/刷新/卸载入口
显示当前 GameManager 已接入的预热配置列表
```

会写入配置的操作必须保留确认、`Undo.RecordObject`、`EditorUtility.SetDirty`、`EditorSceneManager.MarkSceneDirty`。编辑模式只写配置关系，不实例化池对象；真正预热和池组创建发生在 PlayMode 或模块运行时入口中。

不要在 `GameManager接入` 页新增“帮你扫描 Project Prefab 并自动接入”的功能。正确流程是：先由 SO 数据系统维护 `PrefabPrewarmDataInfo`，再由本页把配置资产接入 GameManager。

### PlayMode 池组状态页

`PlayMode池组状态` 是只读运行时诊断页，只能查看当前 `ESGameObjectPoolModule` 已经创建的池组统计：

```text
key
activeCount
inactiveCount
totalCount
createdCount
rentCount
returnCount
missCount
repairCount
overflowDestroyCount
prewarmSourceCount
```

此页不得创建对象、不得预热、不得回收、不得写配置。当前实现通过编辑器反射读取 `ESGameObjectPoolModule` 私有 `groupsByKey`，再调用公开 `TryGetStats(key, out stats)` 获得统计。这个方案可用但不是最终理想形态；更好的后续方向是在运行时模块中提供正式只读 API，例如 `GetAllStats()` 或 `CopyStatsTo(List<ESGameObjectPoolStats>)`，让编辑器不用反射私有字段。

如果要新增正式 API，注意它必须是只读、低分配、不会暴露内部可变字典引用。

### 运行时统计页和 GameManager 池组页不要混淆

`运行时统计` 读取的是旧的/通用的 `PoolStatistics.GlobalStatisticsGroup` 口径，偏对象池统计汇总。

`PlayMode池组状态` 读取的是 `ESGameObjectPoolModule` 当前 GameObject 池组口径，偏 GameManager 模块实例状态。

二者可以同时存在，但 UI 文案必须写清楚，不要让开发者误以为它们是同一个数据源。

### SimpleTools 商业级改造铁律

后续 AI 继续强化 SimpleTools 时，先按工具价值和风险排序，不要均匀撒改动。优先处理：

```text
高频使用
会写资产或场景
可能批量破坏数据
文案与真实行为不一致
无预览、无确认、无报告
按钮堆叠导致误操作
```

每个工具应尽量收成统一工作流：

```text
范围
规则
预览
执行
报告
历史/复查（可选）
```

不要让工具 UI 变成按钮清单。中文界面要口语化但准确，尤其要写清楚“会改什么、不改什么、失败后如何复查”。空状态要告诉用户下一步做什么，而不是占大块面积写无效说明。

### 高风险工具的最低安全线

任何批量工具只要会改对象、资源、Prefab、Importer、场景、文件，都必须至少满足：

```text
执行前可预览
执行前有明确确认
只执行当前预览签名对应的结果
执行项和跳过项可区分
失败项保留并可复制/定位
场景对象修改走 Undo 和 MarkSceneDirty
资产修改走 SetDirty / SaveAssets / Importer 保存
路径写入有边界检查
大批量操作有数量提示和必要截断
```

不要把“点按钮后弹成功”当成报告。报告至少要包含成功数、失败数、跳过数、风险项和关键路径预览。

### 当前验证命令

本轮 ObjectPool 工具收口后，以下编译通过，均为 `0 warning, 0 error`：

```text
dotnet build ES_Design.csproj --no-restore /p:BuildProjectReferences=false
dotnet build ES_Logic.csproj --no-restore /p:BuildProjectReferences=false
dotnet build ES_Editor.csproj --no-restore /p:BuildProjectReferences=false
```

以后修改 `Simple_ESTool_Page_ObjectPool.cs` 后，至少跑 `ES_Editor.csproj`。如果涉及 `ESGameObjectPoolModule` 或 `PrefabPrewarmDataInfo`，同时跑 `ES_Logic.csproj`。

### 不要误读当前工具成熟度

ObjectPool 工具当前已经从“伪入口混杂”收回到真实链路，但仍不是最终形态。最值得继续做的是：

```text
给 ESGameObjectPoolModule 增加正式只读池组统计 API，替代编辑器反射私有 groupsByKey
PrefabPrewarmDataInfo 审计表格进一步商业化，展示每个条目的 Key、Prefab、数量、启用、Scene/Space 条件和风险
GameManager 接入页把编辑模式写配置、PlayMode 加载运行时状态分得更明显
把 SimpleTools 总入口按商业价值重排，减少低价值旧工具干扰
```

但不要为了“更强”重新加入从 Selection 直接录入 Prefab 的入口。那是伪池化，不是 ES GameManager 根池化。
