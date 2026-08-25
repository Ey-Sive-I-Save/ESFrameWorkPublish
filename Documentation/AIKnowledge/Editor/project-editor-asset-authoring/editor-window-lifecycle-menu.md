# Unity EditorWindow 生命周期与 ES 菜单边界

`KnowledgeId`: `es.unity.editor-window-lifecycle-menu.v1`

`Topic`: Unity EditorWindow 生命周期、ReloadDomain 恢复与 ES 菜单入口边界

`Summary`: 约束窗口单实例、状态所有权、回调释放、ReloadDomain 恢复和三棵 ES 菜单的信息架构。

`Authority`: `Unity 2022.3 official documentation + AIWarnings + current source`

`RouteKeys`: `editor`, `editor-window`, `reload-domain`, `session-state`, `menu-item`, `menu-architecture`, `single-instance`, `owner-lifecycle`

`ContentHash`: `9ef4104937115a51080f6240746173e26513377908a8657b3ba32b7002b76b46`

`EvidenceLevel`: `S1`

`StaleWhen`: Unity Editor 版本、任一 UnityOfficialReferences 响应内容哈希、EditorWindow/AssemblyReloadEvents/SessionState 合同、ESWindowFoundation、ES 菜单信息架构或任一 SourceRef 哈希变化。

`RuntimeAcceptance`: `runtime-not-run`

`RequiredReads`: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`、`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md`、`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md`、`Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`、`Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/P2_编辑器菜单根必须使用【ES】_AI协作警告.md`

## Scope

本条目负责 EditorWindow 的打开身份、状态所有权、回调释放、ReloadDomain 恢复和 ES 菜单入口。它不负责窗口视觉/交互验收、Workbench Draft 事务、SerializedProperty 写入或正式 Prefab 资产提交。

- 窗口可用性和实机证据归 `es.engineering.editor-availability-validation.v1`。
- SerializedObject、Undo、Dirty 和 Prefab override 写入归 `es.unity.editor-serialized-undo-dirty.v1`。
- 正式 Prefab Asset 保存和分阶段事务归 `es.unity.editor-prefab-asset-transaction.v1`。

## Trigger and routing

- 自然语言触发：新建或恢复 EditorWindow、窗口重复打开、父子窗口、owner、ReloadDomain、SessionState、EditorPrefs、GlobalObjectId、恢复选择/目标、菜单迁移、`【ES】/` 菜单根、窗口关闭重开。
- 精确 routeKeys：`editor-window`, `reload-domain`, `session-state`, `menu-item`, `menu-architecture`, `single-instance`, `owner-lifecycle`。
- 默认只命中本条目；任务要求证明窗口真实可用时再追加 `es.engineering.editor-availability-validation.v1`；涉及正式资产写入时改路由到对应事务条目。
- 误路由回退：若任务对象是运行时 UI Window、Workbench Draft、Inspector 字段或 Prefab Asset，不沿用本条目的窗口结论，回到 `KnowledgeIndex.yaml` 重新按对象与动作匹配。

## Decision rules

1. SourceRef 或官方响应哈希漂移、Unity 版本变化、窗口类别不明确时，标记 `stale` 并停止使用当前结论。
2. 继续前必须确定窗口类别、唯一打开入口、稳定 owner 身份、状态存储层和 ReloadDomain 恢复目标。
3. 普通生产窗口没有单实例入口、订阅无法对称释放、恢复依赖活动对象引用或初始化需要全项目扫描时，停止实现并标记 `Blocked`。
4. `FollowOwner` 的 owner 暂时不可解析时必须保持 `PendingFollowOwner`；只有父窗口真实关闭并形成可序列化脱离意图后，才允许转为 `Independent`。禁止把解析失败、恢复顺序变化或 ReloadDomain 当成真实关闭。
5. 恢复任何状态前先按下方五层模型分类。无法证明稳定身份、存储生命周期或恢复失败语义时，标记 `Deferred` 或 `Blocked`，不得用标题、同名对象、最近活动窗口或 InstanceId 猜测。
6. `[InitializeOnLoad]` / 静态构造阶段可能早于资产导入完成；需要资产解析的恢复必须延后到明确的窗口打开、用户动作或可证明的导入后阶段，不能在自动入口中直接扫盘或加载资产。
7. 仅修改 Knowledge 正文不授予窗口源码、菜单、Unity、Git 或发布动作。实际改动由当前用户明确指令授权；选用 AIBrain/Worker 通道时再匹配 AICommand 与 TaskContract。
8. 只有 Unity 中完成重复打开、关闭重开、ReloadDomain、父子顺序和菜单可达性验证，才能提升 Runtime 结论；静态源码只能维持 S1。

## Core conclusion

EditorWindow 的生产边界不是“能打开一个窗口”，而是打开身份、状态所有权、回调释放、ReloadDomain 恢复和菜单入口同时闭合。普通 ES 主窗口应复用同类型实例；瞬时 Popup/Dialog 的多实例行为必须由显式协调器约束，不能成为主窗口的默认模式。

## Unity 2022.3 基础机制

- `EditorWindow.GetWindow<T>()` 返回屏幕上第一个同类型窗口；不存在时才创建并显示。这是普通单实例窗口的直接基础。
- `OnEnable` 适合建立轻量绑定，`OnDisable` 用于对称清理资源和回调。`OnDisable` 也可能由脚本重载触发，因此不能无条件解释成“用户关闭”。
- `AssemblyReloadEvents.beforeAssemblyReload` / `afterAssemblyReload` 提供重载前后事件。订阅必须幂等，释放必须对称；活动 Unity 对象引用、窗口引用、鼠标捕获和临时播放状态不能作为恢复身份。
- `SessionState` 用于需要跨程序集重载、但不需要跨 Unity 进程保留的键值状态；Unity 退出后清空。跨 Editor 会话的用户偏好才考虑 `EditorPrefs`，项目资产事实不得藏在二者中。
- `[InitializeOnLoad]` 的静态构造器会在 Unity 启动和脚本重编译后执行，但官方文档明确提示此时资产导入可能尚未完成，资产加载可能失败。自动入口只能建立轻量、幂等的注册与延后恢复信号。
- Unity 字段序列化只覆盖受支持的实例字段；`static`、`const`、`readonly` 和不受支持的类型不能作为 Reload 恢复合同。序列化字段可保存恢复描述，不可保存仍需在新域继续使用的活动引用。
- `GlobalObjectId` 可为项目内持久 Unity 对象提供项目范围的持久身份；对象移动到另一个 Scene 时其 ID 会变化，因此解析失败必须进入目标失效/重新选择流程，不能按名称或旧 Scene 猜测替代对象。

## Reload 恢复的五层状态模型

| 层级 | 可保存内容 | 存储/恢复方式 | 必须停止或降级的情况 |
|---|---|---|---|
| ES 窗口关系身份 | `ownerKey`、链接模式、真实关闭后的脱离意图 | 窗口可序列化状态 + `PendingFollowOwner`/显式重绑定流程 | key 缺失、重复 pending、owner 暂不可解析时保持 Pending；禁止静默 Independent |
| 项目对象身份 | 已保存 Asset/Scene 对象的 `GlobalObjectId` 或项目现行稳定 ID | 新域中从当前项目权威重新解析 | managed、未保存对象、mixed selection 不得跨 Reload；对象移动 Scene 或解析失败时不得同名猜测 |
| Editor 会话状态 | 当前页、筛选、轻量工作区快照、恢复阶段等标量/可序列化 DTO | 带命名空间、稳定身份和 schema 的 `SessionState` | Unity 退出即失效；不得保存项目事实、活动对象引用或无版本任意 JSON |
| 跨会话用户偏好 | 机器/Editor 用户的显示和交互偏好 | `EditorPrefs` | 不得保存项目资产权威、owner 关系、协作状态或敏感数据 |
| 瞬时运行状态 | `EditorWindow`/Object 引用、鼠标捕获、Popup、拖动、动画、活动 Task/回调 | 不持久化；Reload 前取消/释放，新域按稳定描述重建 | 任何把瞬时对象写入 SessionState/EditorPrefs 或用旧引用继续执行的方案都必须阻止 |

恢复顺序固定为：读取可序列化恢复描述 -> 校验 schema/稳定身份 -> 重建轻量窗口外壳 -> 解析项目对象 -> 显式绑定 owner 或登记 Pending -> 恢复页面状态。任一步失败都保留可诊断状态并停止后续副作用，不能为了“窗口看起来恢复了”继续写资产或执行旧任务。

## ES 窗口合同

1. 打开入口先确定窗口类别。普通主窗口使用 `GetWindow<T>()` 或 ES 基类的等价复用入口；`CreateWindow<T>()` / `CreateInstance<T>()` 不是绕过单实例的替代 API。
2. 参与 ES Presentation 的窗口通过 `ESWindowFoundation.Bind` 或标准动作宿主入口显式绑定，并在关闭/重建时 `Unbind`。基础层不应猜测标题栏或给未知窗口覆盖注入按钮。
3. 父子窗口通过显式 owner、稳定 `ownerKey` 和 `SetSleepOwner` / `RegisterPendingSleepOwner` / `ResolvePendingSleepOwners` 流程恢复。父先恢复时直接显式绑定，子先恢复时登记 Pending；不得持久化 `EditorWindow`、`UnityEngine.Object` 或 `InstanceId` 作为长期身份。
4. ReloadDomain 前先停止瞬时视觉/交互、捕获可序列化偏好并恢复原生窗口几何；新域再按稳定身份重建。重载中断、编译失败和禁用 Domain Reload 是不同恢复路径。
5. 初始化阶段只做轻量、幂等的元数据注册。普通 ES Editor 初始化优先 AssemblyStream；禁止把 `[InitializeOnLoad]`、静态 `delayCall` 或 `update` 变成全项目扫描和资源加载入口。

## 菜单边界

- Unity `MenuItem` 只能标注静态方法；验证函数使用同一路径并把第二个参数设为 `true`。快捷键、优先级和宿主上下文是入口行为，不是业务稳定身份。
- ES 自有顶部菜单、`CreateAssetMenu.menuName` 与 `AddComponentMenu` 的根均为精确字面量 `【ES】/`，但三棵菜单按不同用户心智分别分类。
- 顶部菜单按任务域组织；`Assets/Create/【ES】` 按资产类型组织；`Add Component/【ES】` 按组件能力组织。不能机械共用一套一级分类。
- “常用窗口”只投影打开窗口动作，并复用正式入口；不能投影写资产、清理、修复或测试执行。
- 菜单路径迁移必须同步启动器、`ExecuteMenuItem`、测试断言和现行文档，但不改变序列化字段、GUID 或业务身份。

## Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作、恢复与缺失证据 |
|---|---|---|---|
| 用 `CreateInstance` 打开普通主窗口 | 重复点击出现多个实例；打开身份未集中 | 搜索全部打开入口并分类 | 改用正式复用入口；关闭历史重复实例前先报告，不猜测保留对象；仍需重复点击实测 |
| 把 `OnDisable` 当成用户关闭 | ReloadDomain 后 owner 或状态被错误清除 | 区分关闭、重载、目标失效 | 分开处理事件并保持清理幂等；用稳定身份重建；仍需重载回放 |
| 持久化窗口/Object/InstanceId | 重载后引用为空或绑定错误 | 检查恢复数据是否仅含稳定 key 和可序列化状态 | 丢弃瞬时引用，从当前权威对象解析；FollowOwner 解析失败保持 Pending，目标对象解析失败进入失效/重新选择或 Blocked |
| 把 owner 暂不可用当成真实关闭 | 子先恢复时永久脱离，稍后父窗口恢复也无法重绑 | 分别标注 Reload、真实关闭、目标失效三个事件 | Reload 只释放活动引用并保留 Pending；只有真实关闭写入脱离意图；补父先/子先恢复回放 |
| 用 `SessionState` 保存 managed、未保存或 mixed selection | Reload 后错误绑定、JSON 结构漂移或把临时选择冒充项目事实 | 为每个字段标注身份来源、schema 和存储生命周期 | 仅保存可重建描述；无法形成持久 ID 的选择留在当前域，Reload 后明确清空并提示重新选择 |
| 假设 `GlobalObjectId` 永不变化 | Scene 对象移动后旧 ID 解析失败，AI 按名称绑定了错误对象 | 覆盖移动 Scene、删除、重建和解析失败 | 停止自动恢复并请求重新选择/确认；不得使用名称、层级位置或最近选择兜底；仍需 Unity 实测 |
| 在 `[InitializeOnLoad]` 中立即加载资产恢复 | 启动/编译后偶发空对象、重复扫描或导入竞态 | 检查自动入口是否只注册元数据和延后信号 | 把解析移到明确窗口打开、用户动作或可证明的导入后阶段；补启动与重编译恢复证据 |
| 订阅不对称或初始化扫盘 | 重载后回调倍增、卡顿或资源泄漏 | 核对订阅/释放成对且注册阶段轻量 | 先释放旧订阅再注册；移除全盘扫描；仍需 Profiler/ReloadDomain 证据 |
| 菜单路径当成业务身份 | 路径迁移破坏启动器或测试 | 列出 MenuItem、验证函数、启动器和测试引用 | 同步入口投影但保持业务稳定身份；仍需 Unity 菜单可达性证据 |

## Execution checklist

```text
开始前：读 Start/CurrentStatus/RuleIndex 和命中 P0 -> 验证 SourceRef -> 审计全部打开入口 -> 为每份恢复状态选择五层之一
实施中：确定单实例/例外 -> 声明 ownerKey、对象身份、schema 与失效语义 -> 对称 Bind/Suspend/Close 和订阅 -> 定义父先/子先恢复
完成后：检查三棵菜单、验证函数、启动器和测试引用 -> 检查无全局扫描、同名猜测和瞬时身份持久化
不可跳过：Unity 中验证重复打开、父先/子先、真实关闭脱离、目标失效、ReloadDomain、移动 Scene 后解析失败和菜单可达性
禁止：用文件/按钮/测试源码存在冒充可用；用静态检查冒充 Unity 验收；在 InitializeOnLoad 直接加载资产；无权限修改窗口、菜单或资产
```

## Evidence boundary

### 已验证事实

- 当前项目版本文件声明 Unity `2022.3.45f1`。
- Unity 2022.3 官方文档确认了 `GetWindow` 的复用/创建语义、程序集重载事件、`SessionState`/`EditorPrefs` 的生命周期、受支持字段序列化边界、`InitializeOnLoad` 的资产导入时序风险、`GlobalObjectId` 的项目持久身份与 Scene 移动限制，以及 `MenuItem` 的静态入口合同。
- 当前 ES 源码存在显式 `Bind`/`Unbind`、稳定 ownerKey、待绑定 owner、ReloadDomain 前恢复，以及“五个正式业务域 + 常用窗口快捷投影”的菜单常量。

### 推导

- 可恢复窗口状态应拆成 ES owner 关系身份、项目对象身份、Editor 会话状态、跨会话用户偏好和瞬时状态五层；前四层只按各自生命周期恢复，瞬时状态必须丢弃并重建。
- 菜单只负责可发现入口。业务命令身份、写权限、Undo 和失败恢复必须由被调用流程独立保证。

### 非声明

- `runtime-not-run`：本次未启动 Unity、打开窗口、触发 Domain Reload 或点击菜单。
- 未证明多显示器位置、窄窗口、高 DPI、焦点、Popup、重复点击、编译失败恢复或动作宿主布局在 Unity 中可用。
- 源码和规则存在不等于 RuntimeAcceptance、ReleaseAcceptance 或商业可用性通过。

## UnityOfficialReferences

原有 EditorWindow/GetWindow/AssemblyReloadEvents/SessionState/MenuItem 响应在 2026-08-23 返回 HTTP 200；EditorPrefs/InitializeOnLoadAttribute/Serialization/GlobalObjectId 响应在 2026-08-24 返回 HTTP 200。响应内容哈希用于识别外部依据，不参与本地 `ContentHash`。

| Unity 2022.3 官方文档 | SHA-256 |
|---|---|
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorWindow.html | `5676fdb13b497446be79d1babdf6e53f7390a9254000aa40ed1e2ef69b803781` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorWindow.GetWindow.html | `04a3ae243ada217140ab4b125e5626d6f304c51f7705e5342044a9840fa17803` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssemblyReloadEvents.html | `bdfd802e9762aec3c08b46fd7ff1133b55d87285389d0332b00ddfd19a148437` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SessionState.html | `3cd28e66d7a85d1f3a6011f05da521b3d62703dcd89da9bc75cdee62a033afbe` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorPrefs.html | `e84d2fdc369f3e579a4d872e3cb58e4f6871104455c6e8fcc51f177a0c9b1cf2` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/InitializeOnLoadAttribute.html | `760c8e91bd8cb922ecad1935702cb42500e67caa5b88f3daed93e2b832834102` |
| https://docs.unity3d.com/2022.3/Documentation/Manual/script-Serialization.html | `f7dc82204a5c081b73114149199d07ce0e6304f3f182f995467d3bbf5ee0d0ad` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GlobalObjectId.html | `98a64e228b51080738dc7cf60edfd9e4cf719284db73601b755558609181c304` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MenuItem.html | `0cbb2da1b132889d62e310d75c91bd0a37bc75ad9db93674e5e6d90c157b3adc` |

## EvidenceRefs

- `StaticReview`: 已交叉读取 Unity 2022.3 官方文档、AIWarnings、ES 窗口基础层与菜单常量；本条目的 SourceRef/ContentHash 由只读验证器复核。
- `Runtime`: `runtime-not-run`；没有 Unity 窗口打开、ReloadDomain 或菜单交互回执。

## SourceRefs

- `Documentation/AIKnowledge/Editor/project-editor-asset-authoring/editor-window-official-source-lock.md` (`b582b8a4a5ce16929b1ea7e797dec3bc82c6e00ef4799a37cf541194e1900d2e`)
- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md` (`63054f018470f0c3a07ae63b78879cb6c24c39bcc982689890a7cab7990e9af5`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md` (`b25a7f0aa36852bfd4096033de5aca12e12cec730e0437873eeb673da68434df`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`0e7523fd7806a9be00a2bde8edb97a6b9f8e22c1830e1319a89a96e5ead0e00f`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/P2_编辑器菜单根必须使用【ES】_AI协作警告.md` (`1b813362d62255a3ed7e8910ef7cf3c3b2fcf344dbc2ceccf753ad825d08fd9a`)
- `Documentation/ES_EDITOR_WINDOW_PRODUCTION_STANDARD.md` (`88ce371c61194569d3a3738ec2c35e89b1ec5315d1e8b6d44e9c5313fb93b81e`)
- `Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs` (`e6bdd701c0fd202f3183099de131530bd16cf4eea09046441f306a353708d3a5`)
- `Assets/Plugins/ES/0_Stand/Stand_Tools/OnlyEditor/MenuItemPathDefine.cs` (`d83e91ef8456727b554c854d59405492202f342d841a4b3dde265ef7c6c06560`)
