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
