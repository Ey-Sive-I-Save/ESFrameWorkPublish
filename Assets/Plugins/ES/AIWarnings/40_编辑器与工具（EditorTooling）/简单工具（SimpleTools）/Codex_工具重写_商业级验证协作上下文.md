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
  - `Editor/ESGraphViewV2/ESStableGraphViewWindow.cs`: Stable Graph V2 图编辑器。
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

## 2026-08-01：编辑器工具工作台规范与首批目录迁移

完整页面规范位于：

```text
Documentation/ES_EDITOR_TOOL_WORKBENCH_STANDARD.md
```

它是 SimpleTools 后续改造的唯一排版和迁移基线。先确定用户任务、信息顺序、状态和风险，再改绘制；不要为了“统一风格”把工具行为一起重写。

### 已实施但尚未完成 Unity 人工验收的范围

本轮只进行了低功能风险的目录与公共排版迁移：

```text
SimpleToolsWindow
  移除“常用工具”根分类；改为观察与诊断、场景批处理、资产与发布、ES 配置与集成、维护与修复。

SimpleToolsPanelUtility
  DrawToolHeader / DrawSectionTitle 现在必须实际显示标题、副标题、状态、风险和细分隔线。
  普通摘要与普通结果不再默认套 helpBox；Warning / Error 仍保留明确语义。
  只有 Primary 是视觉主操作，Success / Warning / Danger 不能仅因名字不同变成多枚高饱和彩色主按钮。

ESEditorSectionNavigatorIMGUI
  是 ESEditorSectionNavigator 的 Window/IMGUI 配套实现：内容目录始终显示、窄窗口自动换行、选中仅用文字色和细下划线、选择用 SessionState 保存。
  它不是 Toolbar、Popup 或传统凸起 Tab；不可再把它改回 EnumToggleButtons。

Page_ObjectPool
  已移除 EnumToggleButtons 页面导航，改为运行时统计、预热配置审计、GameManager 接入、池组状态四个内容分区。

Page_TopToolbar
  已移除“管理内容”Popup 伪页面导航，改为场景快捷、资产快捷两个内容分区。
```

以上只改变导航、文案和通用绘制层，不得借机改变对象池接入、场景/资产快捷写入、Undo、确认、SaveAssets 或场景保存逻辑。

### 当前仍未迁移的内容

下列页面属于下一批，不得在未完成字段清点、预览签名和人工测试前粗暴替换：

```text
AssetReferenceChecker：TabGroup 配置字段要改为连续“范围 → 资源包入口 → 安全保护 → 高级”分区，不能 Hide 后漏掉入口。
PhysicsAlign：大型高风险场景写入工具，先梳理基础对齐、智能分布、尺寸匹配、布景整理、选区审计与预览。
MaterialReplacement：场景实例 / Prefab 资产工作区必须严格隔离；不得在 OnGUI 自动 CollectTargets 或写入。
ObjectPool：预热配置与 GameManager 接入页仍需把多操作收成明确状态机，并把查询改为显式刷新缓存。
```

### 硬性迁移门禁

```text
禁止 TabGroup、EnumToggleButtons 页面导航、Toolbar、Popup 伪页签。
禁止在打开页面、OnGUI、刷新或域重载时自动全盘扫描、写场景、写资产、自动发布。
自动扫描、资产写入、发布、场景修改必须由用户明确操作触发，且风险操作位于页面后段，有确认与结果反馈。
不要删除 legacy UI、旧菜单或数据字段，除非用户明确授权且已有迁移/回滚方案。
每页固定优先顺序：范围 → 规则 → 预览 → 一个主操作 → 结果 → 高级/历史。
连续配置使用 Title + 副标题 + 细分隔线 + Foldout 层级；配置目录只用于互斥的业务工作区。
```

### 验证状态

已完成的静态检查：

```text
ObjectPool / TopToolbar 不再命中 TabGroup、EnumToggleButtons 或“管理内容”Popup 伪页签。
本轮相关源码已通过 git diff --check。
```

`ES_Editor.csproj` 为 Unity 自动生成且被忽略；为进行本地编译验证，临时补入了当前工作区新建的编译单元。迁移自身的 `ESEditorSectionNavigatorItem` 缺失错误已消失。全量编译目前仍被无关的既有发布模块错误阻断：

```text
Assets/Plugins/ES/Editor/ESResPipeline/ESAssetBundlePublisher.cs(476,35)
CS0165：使用了未赋值的局部变量 page
```

尚未完成 Unity 人工视觉验收；不得因上述静态检查或局部编译通过而宣称“0 error”或“已完成商业级验收”。

### 四项硬验收的复核结论（首批样板，不是全量结论）

本轮依据源码重新核对：`SimpleToolsPanelUtility`、`Page_ObjectPool`、`Page_TopToolbar` 与 `ESEditorSectionNavigatorIMGUI`。

| 验收项 | 首批样板结论 | 已有证据 | 尚缺内容 |
| --- | --- | --- | --- |
| 风格一致 | 基础通过 | 公共标题真实显示用途、状态、风险和分隔线；两个页面共用内容目录和结果样式。 | 仍有未迁移旧页；ObjectPool 内部还有历史 `helpBox` 外框。 |
| 符合人类习惯 | 基础通过 | TopToolbar 顺序已是当前事实 → 工作区 → 操作 → 维护 → 结果 → 撤销；写入、移除、刷新均由明确按钮和确认触发。 | 批处理页尚未统一成“范围 → 规则 → 预览 → 应用”的状态机。 |
| 信息密度合理 | 部分通过 | 目录常显且自适应换行；摘要为单行事实；详情、结果和历史后置。 | ObjectPool 审计/接入区仍需表格化与移除无语义嵌套外框。 |
| 上手难度低 | 基础通过 | 空状态、缺失 GameManager、无效资产、写入确认均说明下一步或影响范围；技术术语放到副标题解释。 | 尚缺 Unity 人工测试，不能只凭源码认定新用户完全无障碍。 |

因此后续报告必须使用准确口径：**首批页面已建立可复用样板，整个 SimpleTools 尚未完成四项硬验收。**

### 2026-08-01：ObjectPool IMGUI Layout/Repaint 崩溃修复

复现异常：

```text
ArgumentException: Getting control 14's position in a group with only 14 controls when doing repaint
Page_ObjectPool.DrawPoolUsagePanel() line 214
```

原因不是对象池业务数据本身，而是运行时统计集合可能在 Layout 与 Repaint 之间变化。原实现每个事件都直接遍历 `globalGroup.Groups`，并依据实时集合决定 `VerticalScope`、折叠组和行数，导致两阶段生成不同数量的 GUILayout 控件。

修复约束：

```text
Layout 阶段建立池组、有效池条目、过滤结果和搜索框可见性的渲染快照。
Repaint / 鼠标事件复用同一快照，不直接遍历可能变化的运行时集合。
下一次 Layout 自动重新采样；不缓存到场景、资产或用户偏好。
保留原有搜索、折叠、统计和分析操作，不改变对象池运行时逻辑。
```

验证：

```text
dotnet build ES_Editor.csproj --no-restore -v:minimal -p:BuildProjectReferences=false
0 warning, 0 error

### 2026-08-01：SimpleTools ES 专属风格适配批次

本批完成的适配范围：

```text
RuntimeWatch
  接入 ES 标题、成熟度、风险和事实摘要；OnGUI 不再调用 TryAutoRefreshFromEditorTick，自动刷新由窗口 EditorApplication.update 宿主驱动。
  录制按钮去除红绿背景色，保留显式开始/停止和语义 Tooltip。

AssetReferenceChecker
  自绘体检台头部改为公共 ES 标题、风险和摘要；原 Odin TabGroup 配置分组改为 ESEditorSection。

AnimationBatchSetting / MaterialReplacement / PrefabManagement
  自绘头部改为公共 ES 标题、风险和摘要；材质头部不再为显示目标数调用 CollectTargets，避免打开页面隐式收集目标。
  MaterialReplacement 的隐藏 EnumToggleButtons 元数据移除，工作区继续由明确 Popup/自绘流程控制。

PhysicsAlign
  原 TabGroup 分区改为 ESEditorSection，保留字段、按钮、Undo 和预览数据；修正迁移中重复分区元数据导致的编译错误。

SceneOptimization
  Issue category / Severity 过滤器移除 EnumToggleButtons，恢复普通枚举字段显示，避免把字段选择器伪装成页面导航。

HierarchyTools 介绍页
  从 Odin Title/DisplayAsString 改为 ES 标题、工具目录和风险说明；只读介绍页不扫描、不写入。
```

本批静态门禁结果：SimpleToolsWindow 目录下不再存在 `TabGroup` 或 `EnumToggleButtons` 标记；`ES_Editor.csproj` 编译为 `0 warning, 0 error`。

仍未宣称全量商业级完成：ObjectPool 内部外框与显式缓存刷新、各批处理页唯一主操作、Unity 实际视觉验收和 Layout/Repaint 连续操作压力测试仍需逐页完成。

### 2026-08-01：去除 SimpleTools 双标题入口

所有当前活跃 SimpleTools 页面已移除类级 Odin `[Title]` 入口，并统一由 `SimpleToolsPanelUtility.DrawToolHeader` 绘制 ES 标题。这样页面不会再出现“旧 Odin 标题 + 新 ES 标题”重复堆叠。

当前活跃页面逐页具备公共 ES Header：

```text
AssetReferenceChecker
TextureSpriteTool
UnityPackageTool
ObjectPool
RuntimeWatch
TopToolbar
SceneTextRepair
AnimationBatchSetting
BatchRename
BatchStaticSetting
HierarchyTools
LightingSettings
MaterialReplacement
ParticleSystemAdjustment
PhysicsAlign
PrefabManagement
SceneOptimization
```

这个改动只移除旧标题绘制入口，不删除字段、数据、执行逻辑或 Undo 链路。类级标题语义已转移到公共 Header 的标题、用途、成熟度和风险四项中。
```

### 2026-08-01：全活跃页面第一轮 ES 入口门禁

对 `SimpleToolsWindow` 下当前活跃页面进行静态门禁：

```text
类级 Odin Title：0
TabGroup：0
EnumToggleButtons：0
GUIColor：0
公共 DrawToolHeader：每个活跃页面至少 1 个
```

这表示所有活跃工具已经有统一的 ES 页面入口，不再从类级 Title、TabGroup 或彩色 Odin Button 进入。仍保留的 FoldoutGroup 仅属于页面内部高级字段/历史数据承载，不代表页面导航；后续会按页面风险逐步收成 ESEditorSection 或 ES 连续分区。

本门禁不等于功能完成。每个工具仍必须继续验证唯一主操作、预览签名、写入确认、Undo/恢复、结果报告、缓存刷新和 Unity 实际视觉布局。

### ObjectPool 预热配置查询门禁

`Page_ObjectPool` 的 `PrefabPrewarmDataInfo` 查询已改为显式刷新缓存：

```text
打开页面、切换分区、搜索和重绘不再调用 ESEditorSO 查询。
用户点击“刷新配置事实”后，才从 ESSO/SoDataInfo 建立当前会话缓存。
搜索只过滤缓存，不重新扫描资产。
缓存未建立时显示“尚未刷新配置事实”，不会把空列表误报成项目没有配置。
```

这条规则适用于所有会读取 Project、场景或 ESSO 数据的 SimpleTools：查询必须由用户明确触发，页面只渲染缓存，并显示缓存是否已建立。

### 第二轮性能复核：未解决项（不得误报）

#### P1：RuntimeWatch 隐藏页后台自动采集

`RuntimeWatch` 已不在 OnGUI 中调用 `TryAutoRefreshFromEditorTick`，但 `SimpleToolsWindow` 仍常驻注册 `EditorApplication.update`。在 Play Mode 下，默认 `autoRefresh = true` 会使 `TryAutoRefreshFromEditorTick` 约每 0.25 秒执行一次 `CollectEntries()`，即使当前没有选中 RuntimeWatch 页面。

这不是一次性扫描。正确的第二轮门禁是：

```text
只有 RuntimeWatch 当前页面可见时允许自动刷新。
证据录制期间可继续后台采样，因为这是用户明确开始的会话。
隐藏页面不主动采集；再次切回后由用户刷新或由可见页自动刷新。
```

#### P2：ObjectPool Layout 快照的编辑器 GC

ObjectPool 已通过 Layout 快照保证 Layout/Repaint 控件数量一致，但当前每个 Layout 仍创建分组快照、每组 `List<string>` 和过滤列表。这不会进入游戏运行时，也只发生在对象池统计页绘制期间；但它不是一次性分配，持续显示时会造成 Editor GC。

第二轮优化方向：复用快照对象和内部 List 容量，只有数据签名、搜索条件或折叠/排序条件变化时重建；再通过 Unity Profiler 记录 Editor GC。

#### 编译口径

已通过的命令是：

```text
dotnet build ES_Editor.csproj --no-restore -v:minimal -p:BuildProjectReferences=false -p:UseSharedCompilation=false
```

因此 `0 warning, 0 error` 的准确含义是：**ES_Editor 的不构建项目依赖局部编译通过**。它不能代表全依赖项目构建通过；全依赖构建的当前外部阻断项必须单独记录，不得混写为本轮 SimpleTools 结果。

### 2026-08-01：第二轮性能门禁完成状态修正

上一节的 P1/P2 是实施前的风险记录，保留用于追溯；当前代码状态如下：

#### P1：RuntimeWatch 隐藏页后台自动采集——前台焦点门禁已补齐

`SimpleToolsWindow.TickRuntimeWatch()` 现在通过 `IsRuntimeWatchPageVisible()` 同时判断：RuntimeWatch 是否为当前目录页、SimpleTools 是否拥有焦点、窗口矩形是否有效，以及（可取得时）`EditorWindow.focusedWindow` 是否仍为该窗口。后台 Dock 标签不再因为仍保留菜单选中状态而采集；窗口销毁时在 `OnDestroy()` 中取消 `EditorApplication.update` 注册并清空窗口宿主引用。

当前语义为：

```text
SimpleTools 未打开：零 RuntimeWatch 采集
SimpleTools 已打开但 RuntimeWatch 未选中：零采集
RuntimeWatch 页面选中但 SimpleTools 位于后台 Dock 标签：零采集
RuntimeWatch 位于前台且拥有焦点：按页面设置执行自动刷新
程序集构建/域重载：只完成类型初始化与注册，不扫描实例
```

Unity 公共 API 无法可靠判断其他窗口对其进行的像素级遮挡，因此这里的“可见”准确指“前台、拥有焦点、窗口矩形有效”。本轮没有恢复后台录制采样，避免在用户未主动使用工具时产生编辑器和 Play Mode 损耗。

#### P2：ObjectPool Layout 快照编辑器 GC——代码优化已完成

`Page_ObjectPool` 已复用 `PoolGroupRenderSnapshot`、分组字符串列表及其容量，并使用 `poolGroupRenderSignature` 仅在统计值、分组内容或搜索词发生变化时重建渲染快照。Layout/Repaint 使用同一份稳定快照，避免运行时集合变化造成控件数量不一致。

仍需在 Unity Profiler 中对持续显示统计进行一次 Editor GC 实测；这属于验收数据，不是代码阻断项。

#### P2-补充：ObjectPool 控件数异常修复

此前快照只冻结了分组行；运行时 `GlobalStatisticsGroup` 在 Layout/Repaint 间变化时，统计区前置的“空状态 / 汇总 / 搜索栏”仍可能改变 GUILayout 控件数量，导致 `Getting control ... position in a group`。

现已改为整个运行时统计区的 Layout 快照：

```text
统计组是否存在
有效池总数、活跃数、池中数、丢弃数
搜索栏是否出现及其当帧过滤文本
每个分组的标题、行文本、展开状态
```

Repaint 不再直接读取 `GlobalStatisticsGroup`。折叠点击只写入下一帧状态并请求重绘，下一次 Layout 才改变子行数量，避免 MouseUp/Repaint 动态增减控件。

该修复已通过 ES_Editor 局部编译；P2 的连续 Layout/Repaint 和 Profiler 仍需 Unity 运行证据后签收。

#### P1 签收结论

RuntimeWatch 的前台焦点门禁已通过代码复核和 ES_Editor 局部编译验收。接受以下明确行为：

```text
RuntimeWatch 选中且 SimpleTools 聚焦：自动刷新运行
用户点击 Game / Scene / Inspector：自动刷新暂停
重新聚焦 SimpleTools：若自动刷新开启且当前模式允许，立即执行一次前台续采集，随后按间隔刷新
RuntimeWatch 仅在后台 Dock 标签可见但未聚焦：不采集
```

这属于性能保护语义，不是运行异常；Unity Dock 切换实测作为运行证据保留，不再作为静态代码阻断项。

#### 本轮剩余人工验收

```text
1. 关闭 SimpleTools，进入 Play Mode，确认 RuntimeWatch 不采样。
2. 打开 SimpleTools，切换到其他页面，确认 RuntimeWatch 不采样。
3. 保持 RuntimeWatch 选中，把 SimpleTools 切到后台 Dock 标签，确认 RuntimeWatch 不采样。
4. 将 SimpleTools 切回前台，确认 autoRefresh 按设置恢复，手动刷新仍可用。
5. ObjectPool 持续显示运行时统计，连续 Layout/Repaint 不出现 GUILayout control count 异常。
6. Unity Profiler 记录 ObjectPool 页面 Editor GC，确认快照复用符合预期。
```

### 2026-08-01：预览分页热路径降分配

公共层新增 `SimpleToolsPanelUtility.GetPageRange`。TextureSprite、UnityPackage、Lighting、ParticleSystem 四个预览页改为按起止索引直接绘制，不再在每次 Layout/Repaint 通过 `PageItems` 创建临时 `List`。原 `PageItems` API 保留给非热路径调用，分页大小、页码夹取和空数据语义不变。

这项优化只减少编辑器重绘分配，不改变扫描、写入、Undo 或业务结果；仍需在 Unity Profiler 中以实际页面数据确认 Editor GC 曲线。

公共 Header 的 `DrawSummary` 也改为复用编辑器主线程临时缓冲，移除每次重绘的 LINQ 过滤器与闭包；摘要文本和空状态语义保持不变。

### 2026-08-01：SimpleTools 工作台排版整改

本轮整改目标不是再增加页面装饰，而是修正实际绘制顺序：旧页面把 `OnInspectorGUI(PropertyOrder = 100)` 中的 Header、预览和结果放在 Odin 默认字段之后，导致用户先看到零散字段/提示/旧分组，最后才看到工具用途和操作。

#### 已实施的共同工作台规则

```text
右侧首屏：由 SimpleToolsWindow 根据当前目录绘制唯一工具标题、用途、成熟度与真实风险。
页面内容：宿主绘制期间抑制旧页面内部 DrawToolHeader，避免底部标题和双标题。
默认字段：16 个活跃工具页标记为 ESSimpleToolsLayout。
旧 Odin 装饰：动态移除直接页面成员上的 InfoBox、BoxGroup、TitleGroup、TabGroup 和 PropertySpace。
保留内容：HorizontalGroup / VerticalGroup 的稳定列宽、FoldoutGroup 高级层级、ESEditorSection 配置目录，以及嵌套结果表格。
内容容器：活跃页的普通 helpBox 外框改为无边框内容作用域；真正空状态、警告和错误仍使用语义提示。
```

这意味着页面的基础顺序统一为：

```text
工具标题与风险 -> 配置/规则 -> 预览或审计 -> 主操作 -> 最近结果 -> 高级/历史
```

`Page_SceneTextRepair` 同时完成了基准迁移：默认 Odin 扫描报告字段已隐藏，页面改为“扫描与修复 -> 扫描报告 -> 最近结果”，不再让历史输出抢占操作入口。

`Page_AssetReferenceChecker` 移除了目录后的重复“常用设置”编辑区：完整配置继续由“目标设置 / 资源包分离 / 安全保护 / 高级选项”目录负责，操作区改为只读的当前范围快照，再进入分析、结果和隔离动作。

#### 静态复核与边界

```text
ESSimpleToolsLayout 覆盖：16 个活跃工具页
活跃 SimpleTools 源码：无 TabGroup / EnumToggleButtons / GUIColor
普通 EditorStyles.helpBox 容器：0（不含状态提示 HelpBox）
ES_Editor 局部编译：0 warning / 0 error
git diff --check：通过，仅有仓库既有的 LF/CRLF 提示
```

仍待 Unity 视觉验收，不能提前签收：窄宽度、长字段名、PhysicsAlign 的多分区目录、Material/Prefab 的大结果表、RuntimeWatch 持续刷新以及 ObjectPool 的连续 Layout/Repaint。

#### PhysicsAlign 旧组路径兼容

`Page_PhysicsAlign` 的旧 `HorizontalGroup` / `VerticalGroup` 使用了 `对齐/...` 路径，而其父 `TitleGroup("对齐")` 已被 `ESSimpleToolsLayout` 按公共排版规则移除。保留子路径会使 Odin 在构建 PropertyTree 时报告“expected a group with the name 对齐 to exist”，并中止页面绘制。

该页已将旧路径改为简洁的本地组名，例如 `BasicSettings -> Left/Right`、`DistributionSettings -> Left/Right`、`DressingActions` 和 `AuditToolbar`。`ESEditorSectionAttributeProcessor` 会在 Odin 建 PropertyTree 前把它们重写为“当前分区 -> 本地组”的真实路径，因此横排/双列实际位于选中分区内部，而不是与分区并列。

旧页面常只在一行的第一个成员声明 Section，后续列成员只写 `VerticalGroup("DressingSurface/Right")`。处理器现会从同类型、先前已经归属 Section 的布局组根回溯推导这些后续成员的分区，再同时注入 Section 与重写后的组 ID。它只处理带 Odin `PropertyGroupAttribute` 的成员，不会把普通未标注字段自动塞入分区。首屏重复的快捷按钮已移除，操作只在各自业务分区出现；选区审计使用 `ESEditorBeginSection + [ESEditorSection]` 保持连续分区。后续新增组不得再手写旧 `对齐/...` 父路径，使用本地根名即可，并做 Unity 窄宽度验收。

### 2026-08-01：RuntimeWatch 前台续采集与响应式观察布局

RuntimeWatch 保持“前台才采集”的性能边界：`SimpleToolsWindow` 只在 RuntimeWatch 从非前台恢复为当前聚焦页面时调用 `RequestForegroundRefresh()`。该调用仅将下一次自动采样提前到当前 Editor tick；`autoRefresh` 关闭、手动暂停、Edit Mode 未开启编辑器扫描时不采集。窗口失焦、切页、后台 Dock 和普通 OnGUI 重绘仍为零采集。

观察区域采用固定信息顺序：

```text
观察范围与筛选 -> 自动刷新与高级规则 -> 观察条目工具栏 -> 分类标题 -> 对象标题 -> 成员条目 -> 证据回放 -> 诊断
```

分类和对象使用轻量标题带、左侧色标和细分隔线建立边界，不使用多层 Box。窄于约 720px 时筛选、分页和证据回放自动拆行；窄于约 760px 时成员右侧操作移至条目下方并横向扩展。不得重新引入固定 240px 右侧操作列或把完整工具栏硬塞在单行。

### 2026-08-02：常见案例 ReadMe 接入规则

`ESReadMeNote` 是挂在场景或 Prefab 对象上的 Inspector 说明组件，不是单独的 Markdown 文档，也不参与运行时业务逻辑。常见案例的说明必须出现在用户实际会选中的入口对象上，至少包含：用途、最短使用步骤、必须保留项、风险/性能边界、所属系统和更新时间。

批量接入入口：

```text
【ES】/自动化与开发/文档与示例/编辑器案例/接入或更新常见案例 ReadMe
```

该命令只在用户确认后更新下列指定资产：SimpleTools 基础场景、RuntimeWatch 场景、ItemMotion 场景、Asset + GameCore 热更新场景、资源引用 Prefab，并创建或更新 `ES_EditorExtension_Demo.unity`。最后一个场景为 ESEditorSection、双配置目录、ESPolymorphicReference 和多目标边界验证提供正式可打开的案例入口；四个实际案例对象各自挂有 ReadMe。

严禁将这类接入放到 `InitializeOnLoad`、窗口打开、目录切换、普通 OnGUI 或域重载回调中。场景使用 Additive 模式临时打开、保存后关闭；不会替换或关闭用户当前已打开的场景。Prefab 使用 `LoadPrefabContents` / `SaveAsPrefabAsset` 作用于唯一指定路径。任何新增案例都应扩展这个显式清单，不能扫描整个 Examples 目录后擅自写入。

#### 说明必须跟随测试职责对象

场景根 ReadMe 只解释整体流程，不能替代测试对象自身的职责说明。下列对象必须自带 `ESReadMeNote`：

```text
RuntimeWatch：RW_01_基础类型、RW_02_方法调用、RW_03_筛选嵌套、RW_04_Unity类型
ItemMotion：发射流程入口、命中目标、基础地面、可选弹道阻挡
AssetFlow：ES Asset + GameCore Flow Test 控制器
编辑器扩展案例：Section、双目录、多态引用、多目标边界四个实际案例对象
多目标边界菜单创建的根和每个独立子对象
```

ReadMe 至少回答：**当前对象验证什么、必须保留什么、修改它会改变哪个测试条件、有哪些风险边界**。不要给 Main Camera、Directional Light、纯模型骨骼、纯装饰物和自动生成的内部节点泛滥添加 ReadMe；它们不承担用户需要独立理解的测试职责。场景结构变化导致预期对象找不到时，接入工具必须输出警告而非创建同名替代物，以免掩盖测试场景本身已经失真。

### 2026-08-02：RuntimeWatch 视频发布前的快照与操作规则

RuntimeWatch 的页面顺序固定为：

```text
1. 选择范围并刷新
2. 刷新与显示
3. 观察结果与操作
4. 运行证据（仅录制会话存在时）
5. 诊断与链路报告（后置）
```

首屏唯一视觉主操作是“刷新快照”。范围菜单会在用户明确切换为当前场景、选中对象或选中对象及子层级后立即刷新；搜索、分类、脚本、对象和“显示”视图只筛选当前快照，绝不能因此触发扫描。范围、Tag、ShowIf 或 GetMoudle 规则与当前采样不一致时，必须显示“范围或规则已变 · 请刷新”，不能把旧数据伪装为新结果。

`WatchEntry.RefreshSample()` 只能由以下受控路径调用：手动刷新、前台 RuntimeWatch 的自动刷新、录制基线、用户明确设值/执行操作后的延迟刷新。`OnGUI`、Layout/Repaint、复制观察项、填写内联输入控件和证据回放必须只调用 `GetCachedValue()`；严禁重新执行属性、无参方法或反射 Getter。慢读取与读取失败状态因此代表一次真实采样，不会因编辑器重绘倍增。

采样列表 `entries` 与 IMGUI 渲染列表 `renderEntries` 必须分离：采样可以在 Editor update 中更新 `entries`，但 `renderEntries` 只允许在下一次 `EventType.Layout` 整体替换。分组、对象 Foldout、分页和空状态一律以 `renderEntries` 为准，禁止在 Layout/Repaint 中直接遍历会变化的采样列表。这是防止 `Getting control ... position in a group` 的 RuntimeWatch 稳定性边界。

同一原则适用于证据回放：证据事件、录制状态、范围文本和事件数量必须复制到渲染快照后再绘制，不能在 Layout/Repaint 直接读取会由自动采样改变的录制缓冲。用户操作后的逐条反馈应延迟到下一帧 Layout 才插入界面，避免 MouseUp 事件在旧布局树中临时增加 GUILayout 控件。

方法、可写字段和可写属性仍是明确操作：按钮附近显示最近操作的成功/失败反馈，详情后置到诊断区；方法二次确认默认开启。录制按钮在 Edit Mode 必须禁用并明确标为“Play 后录制”，不得点击后才报错。窄窗口下，首屏操作、筛选、分页分别拆行；不得把右侧操作或分页控件硬挤到不可见区域。
