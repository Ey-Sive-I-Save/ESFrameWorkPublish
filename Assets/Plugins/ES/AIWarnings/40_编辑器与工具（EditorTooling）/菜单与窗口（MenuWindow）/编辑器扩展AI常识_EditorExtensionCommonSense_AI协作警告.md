# 编辑器扩展 AI 常识（项目权威规范）

适用范围：新增或修改 EditorWindow、EditorGUI、GUIStyle、工具栏、菜单、
命令面板、预览窗口、自定义 Inspector 和任何 Editor-only UI。

> 权威级别：Project Authority / P0。
>
> 编辑器 UI 的可交互性、事件隔离、目标正确性和重绘性能高于视觉增强、动效、
> 复杂布局和新增功能。任何违反本文件的改动，不得以“能编译”或“看起来更像 Graph”
> 作为通过理由；必须先修复基础行为，再讨论视觉优化。
>
> 冲突处理：本文件优先于普通 UI 设计建议、实施 AI 的临时约定和未验收的历史方案；
> 若与更高层 P0、当前源码或 Unity 实机事实冲突，必须停止宣称完成并报告冲突。

## 1. GUI 只能在 OnGUI 调用

- `GUI`、`GUILayout`、`EditorGUILayout`、`GUI.skin`、`GUIStyle` 创建与绘制只能在 OnGUI 内执行。
- `OnEnable`、`Awake`、静态初始化、AssemblyStream 注册阶段禁止访问 GUI API。
- 样式可以延迟到首个 OnGUI 创建，`OnEnable` 只维护引用计数。
- 样式释放仍放 `OnDisable` / `OnDestroy`，通过引用计数保证不重复释放。

## 2. EditorWindow 定位与首开稳定性

- 普通独立窗口默认按 Unity 主窗口可用区域居中。
- Popup、上下文对话框、代码示例等依附交互应优先靠近显式 Owner、触发控件或指针；显式产品需求也可以使用 Owner 角落、屏幕锚点或自定义屏幕坐标。
- 所有定位都必须基于 Unity 主窗口、显式 Owner 或已捕获的 Editor GUI 屏幕坐标，并夹取到当前可用显示区域，保证标题栏、主要内容和确认/关闭入口可触达。
- `Show`、尺寸、定位和 `Focus` 的具体顺序由窗口种类与 Unity 行为决定，不规定所有窗口共用一种固定调用顺序；验收结果必须是首开位置正确、无明显跳变，并且重新打开和多显示器场景稳定。

主窗口坐标使用：

```csharp
Rect main = EditorGUIUtility.GetMainWindowPosition();
```

禁止用 `Screen.currentResolution` 推导 Editor 窗口位置，它不代表 Unity Editor
主窗口、Owner 或触发控件在多显示器桌面上的实际坐标。

窗口尺寸需要：

- 默认尺寸达到内容详情所需最小宽度。
- 按主窗口可用空间收窄。
- 保留明确最小尺寸。
- 中文按钮不要用固定宽度，改用 `MinWidth` 或 `CalcSize`。

## 3. IMGUI 布局一致性

- 同一段 OnGUI 在 Layout、Repaint 和其它事件中的控件数量必须一致。
- 禁止只在某个事件分支绘制控件，否则会出现“Getting control N's position”崩溃。
- 文本字段、按钮、页签、滚动条要保持固定顺序和固定逻辑路径。

### 3.1 Odin 滚动轴必须明确区分

- `OdinEditorWindow.UseScrollView` 是整体滚动开关；返回 `false` 会同时关闭竖向和横向滚动，不能表述为“只关闭横向”。
- 只覆写 `DrawEditors()` 不等于关闭 Odin 外层滚动；Odin 的宿主绘制仍可能在自定义内容外包裹滚动容器。
- 目标页完全不允许滚动时，必须同时关闭宿主滚动并保证内容在首屏、最小窗口和高 DPI 下不被裁切；禁止用“滚动条不明显”冒充关闭滚动。
- 目标是“保留竖向、禁止横向”时，不能依赖 `UseScrollView`；应在明确的自定义 OnGUI 绘制入口使用单轴滚动，并验证内容宽度不会触发横向溢出。
- 只对目标页关闭宿主滚动时，按当前菜单选中项路由开关；其他页面的滚动行为不得被无意改变。
- 任何“无滚动”或“仅竖向滚动”的结论，都必须通过 Unity 实机检查横向滚动条、窗口窄化、长中文字段和域重载后的实际行为；源码或 `.csproj` 编译不能替代视觉验收。

## 4. Editor 资源生命周期

- 运行时创建的 Editor 纹理、AudioClip、Material 等临时对象使用 `HideFlags.HideAndDontSave`。
- 必须缓存并统一释放，不能只清空引用。
- 释放使用 `DestroyImmediate`，并保证幂等。
- 切换主题、方案、刷新缓存和域重载前必须释放旧资源。

## 5. Domain Reload

- 不持久化 EditorWindow、UnityEngine.Object、SerializedObject 引用。
- 不保存 RuntimeHandle、InstanceId、Scene 对象引用作为恢复身份。
- AssemblyStream 注册阶段只做元数据注册，禁止全量扫盘、加载大资源、创建场景对象。
- 重操作延后到用户点击、窗口打开或明确范围操作。

## 6. 音频预览

- Editor 音效使用 `AudioUtil.PlayPreviewClip` 预览播放，禁止运行时 AudioSource。
- 反射降级必须逐候选方法尝试，全部失败才回退系统提示音。
- WAV 读取先做长度和格式准入，再分配字节数组。
- 方案配置和合法性应缓存，高频播放路径禁止反复文件 IO 或 JSON 反序列化。

## 7. 常见崩溃速查

| 错误 | 原因 | 处理 |
|---|---|---|
| You can only call GUI functions from inside OnGUI | OnEnable/静态初始化访问 GUI | 延迟到 OnGUI |
| NullReferenceException / GUIStyle null | 样式创建失败后仍被使用 | OnGUI 创建并校验 |
| Getting control N's position | Layout/Repaint 控件不一致 | 统一控件路径 |
| 按钮文本显示不全 | 固定宽度小于中文文本 | 使用 MinWidth / CalcSize |
| 面板左上显示 | 使用 Screen.currentResolution 定位 | 按主窗口或显式 Owner/触发点坐标定位并夹取 |

## 8. 命令面板/搜索入口不要视觉重复

同一个资产或同一目标，在搜索结果中默认只出现一条命令。

次级动作优先复用键盘或右键操作：

- 打开：Enter
- 定位：Ctrl+Enter
- 复制文本 / 路径：Ctrl+C

不要把“打开 + 定位 + 复制”拆成多条标题几乎相同的独立命令。
如果某个入口确实需要多条命令，标题必须带上明确动作前缀，例如
`打开 X`、`定位 X`、`复制 X 路径`，不能只靠描述区分。

已发生案例：GlobalData 同一资产曾同时生成“打开”和“定位”两条近同命令，
用户看起来像重复出现。已改为单条默认命令，定位继续由 Ctrl+Enter 提供。

## 9. Inspector Header 扩展：本次收敛结论

适用范围：`ER_ESEditorInspectorUser` 路由、`InspectorUser_ScriptQuickFilter`、
`InspectorUser_AssetQuickInfo`、`ESEditorInspectorContext`。

### 9.1 宿主识别

- 优先通过 `GUIView.current` + `HostView.actualView` 反射识别当前 Inspector/PropertyEditor。
- 反射失败时允许用 `mouseOverWindow / focusedWindow` 中的 Inspector/PropertyEditor 兜底。
- 仍无法识别时，`GameObject` 按主 Header 处理，`Component` 按独立组件 Inspector 处理，避免扩展被静默跳过。
- 这属于兼容路径，不是权威宿主判断；多 Inspector、锁定 Inspector、独立 Component Inspector 仍需 Unity 实机矩阵。

### 9.2 组件筛选口径

- 触发条件按总组件槽位计算：`TotalComponentSlots >= 2`。
- 面板包含 Transform、Renderer、Collider、MonoBehaviour、Missing Script。
- 原生组件显示 `UnityNative`，可选中、隐藏；MonoBehaviour 额外支持启停和打开脚本。
- 来源分类规则：
  - `Assets/Plugins/ES/` -> ES
  - `Packages/` -> ThirdParty
  - 其余 `Assets/` -> Project
  - 空路径或非 Assets/非 Packages -> Unknown
  - 非 MonoBehaviour 原生组件 -> UnityNative

### 9.3 显隐

- “隐”设置 `HideFlags.HideInInspector`，“显”恢复原 HideFlags。
- 为降低写入场景 YAML 的风险：
  - 保存场景前恢复；
  - Domain Reload 前恢复，Reload 后重新应用；
  - 进入 PlayMode 前恢复，回到 EditMode 后重新应用；
  - 退出 Unity 前恢复；
  - 保存/重载后重新隐藏。
- 必须实机验证保存后的 YAML 不出现 `m_ObjectHideFlags`；Prefab 保存和 Prefab 实例路径也需覆盖。

### 9.4 状态与缓存

- 隐藏记录保存原始 HideFlags，不使用 `hideFlags` 猜测。
- `GameObjectCaches` 上限 256，LRU 淘汰，销毁对象清理，按实际组件 ID 清除失效条目。
- provisional StableKey 重新解析时迁移搜索、分类、展开、面板可见性状态；
  迁移策略为新键已有值优先，旧键始终删除。
- 功能关闭时退订 hierarchy/sceneSaved/sceneSaving/Play/DomainReload/Quit 事件并清缓存。

### 9.5 当前成熟度

> Source Implemented / Isolated Compile Passed / Host Fallback Implemented /
> Component Filter Includes Unity Native Components / HideFlags Lifecycle Guard Implemented /
> Unity Inspector Behavior, Save YAML, Prefab and Profiler Verification Pending

## 10. ES 资源设置页显示完整性

- 资源设置页的模式、平台和阶段状态必须使用面向用户的显示名；禁止直接把枚举成员名（例如 `EditorDirect`、`HotUpdate`）作为主要界面文案。
- 资源目录不能因为布局重构被折叠到不可见或被删除。远端当前平台、本机测试、StreamingAssets、下载缓存、Staging、BuildCache、发布根目录、上传计划、Planned 和 Baked 等受管目录必须继续可见，并保留复制/打开入口。
- 关键目录和高风险发布入口继续使用金色强调，但颜色必须在绘制结束后恢复，不能污染后续控件。
- “完整设置”可以默认折叠；由资源管线生成的目录快捷入口属于运行状态和定位信息，不得默认丢失。空间不足时优先竖向排列或缩短显示文本，不得用横向撑宽解决。
- 页面重排只允许改变布局，不得减少既有配置字段、目录入口、模式显示名或关键视觉语义；静态编译不能代替 Unity 实机确认。

## 11. Inspector 与时间轴编辑器基础约束（P0）

本节由 Track Inspector 实机问题固化，适用于所有固定 Inspector、弹出 Inspector、
Odin PropertyTree、IMGUIContainer 和 UI Toolkit 容器。

### 11.1 事件边界必须先于选择清理规则

- 时间轴根节点的“点击空白清除选择”不能穿透 Inspector、Inspector 的 ScrollView、
  IMGUIContainer、按钮、文本框和 ObjectField。
- 根节点在 TrickleDown 阶段处理 Pointer/Key 事件时，必须先判断事件目标是否属于受保护的
  Inspector 子树；属于 Inspector 时不得执行 `ClearClipSelection()`、删除、切换目标或关闭抽屉。
- Inspector 内的键盘输入、滚轮、拖动、对象选择和按钮点击必须保持在 Inspector 上下文内。
- 任何“点击 Inspector 任意位置导致面板关闭/目标丢失”的问题都是 P0 事件路由故障，不能归类为普通 UI 问题。

### 11.2 Inspector 目标必须是单一、同代、可追踪的权威目标

- Track 和 Clip 必须使用明确的目标种类与目标引用，不能同时保留两个 Drawer 的业务对象。
- 绑定 Track 时清空 Clip Drawer；绑定 Clip 时清空 Track Drawer。
- 弹出按钮必须使用当前内置 Inspector 的绑定目标；不得优先读取旧的轨道选择、上一次弹窗或模糊的全局 Selection。
- Clip 的弹出入口必须确实创建 Clip Inspector；失败时必须报告原因，禁止静默回退成轨道 Inspector。
- 内置 Inspector、弹出 Inspector、重建、Undo/Redo 和 Domain Reload 后，目标恢复必须保持 Track/Clip 类型不串线。
- `OnDisable` 可能来自失焦、布局切换或 Domain Reload，不能直接等同于用户关闭；关闭回调必须区分主动关闭、重载和目标失效，并保证最多执行一次。
- 目标删除或稳定身份无法重新解析时，独立 Inspector 必须明确提示并自动关闭，禁止继续编辑旧托管引用或猜测绑定同名对象。

### 11.3 滚动容器必须单一且有明确方向

- 一个 Inspector 只能有一个权威滚动容器。
- 需要竖向滚动时使用明确的 UI Toolkit `ScrollView(ScrollViewMode.Vertical)`，隐藏横向滚动，
  IMGUI/Odin 字段绘制不得再创建第二层滚动。
- 不得用 `UseScrollView = false` 误称为“只关闭横向滚动”；该属性会同时影响 Odin 宿主滚动。
- IMGUIContainer 必须设置最小宽度为 0、受父容器宽度约束，不得把内容宽度反向撑大父窗口。
- 出现横向滚动条、异常黑色条带、内容越界或字段被裁切时，先定位滚动层和控件语义，不能继续盲目调宽度。

### 11.4 高频 OnGUI 路径禁止资源分配

- `OnGUI`、IMGUIContainer 回调和时间轴重绘路径禁止每帧创建/销毁 `GUISkin`、`GUIStyle`、
  `Texture2D`、`SerializedObject`、`OdinEditor` 或临时 `ScriptableObject`。
- 视觉 Skin/Texture 必须按主题或 SkinGeneration 缓存，仅在主题变化、程序集重载或明确销毁时重建。
- `DestroyImmediate` 不得出现在正常每帧绘制路径；只能出现在缓存失效、窗口销毁或域重载收口路径。
- Inspector 字段变更必须分层：Undo/Dirty 与必要投影即时处理，预览重建和校验空闲合并，资产落盘延迟或明确提交；禁止一个变更回调包办全部后处理。
- 数值拖动、滚轮和连续键盘输入期间，只允许即时更新必要的轻量投影；Preview Player 重建、全量校验等重操作必须在输入短暂停顿后合并执行，禁止改回每帧或逐次 `delayCall` 重建。
- 刷新必须选择最小有效粒度：字段、节点、局部分区、业务投影、预览缓存、整窗重建依次升级；能刷新单节点时不得重建整窗。
- 多个 Inspector 同时查看同一数据时，只重绘确实需要同步的区域，禁止一次输入强制所有 PropertyTree、摘要区、主窗口和预览系统重复绘制。
- 编辑器内部写入后应同步更新自身 revision/dirty 快照，资产监听不得把本窗口修改误判为外部冲突并触发全量恢复。
- 卡顿、滚动掉帧、按钮响应延迟优先检查 GC Alloc、全局皮肤切换、AssetDatabase、预览重建和重复 Repaint。

### 11.5 IMGUI/UI Toolkit 视觉适配边界

- `GUI.skin` 只影响部分 IMGUI 控件；Odin 的 `EditorGUI` 字段大量依赖 `EditorStyles`，不能宣称局部
  `GUISkin` 已实现 Graph 同源绘制。
- 全局 `EditorStyles` 适配器不得在每次字段绘制时 `TryApply/Restore`；如需启用，必须由主题生命周期一次性管理，
  并在退出、PlayMode、Domain Reload 时恢复。
- UI Toolkit 外壳与 Odin 字段桥接只能称为“视觉适配”，不能称为“Graph 完全一致”；像素级一致必须使用同源
  UI Toolkit 字段或明确接受差异。
- 自定义颜色、`GUI.color`、`GUI.backgroundColor`、`EditorGUIUtility.labelWidth`、`wideMode` 和 indent 必须
  使用可恢复 Scope，异常和提前返回也必须恢复。

### 11.6 控件语义必须可识别

- MinMaxSlider、Range、进度条、滚动条和分隔条必须有明确上下文、标签或视觉 Token；不能以黑色默认横条出现在业务字段中，
  让用户误以为是残留布局。
- 如果控件样式无法可靠适配，优先改为明确的数值输入或使用 ES 自绘控件，不能保留不可解释的默认控件。
- “弹出”“关闭”“保存”等操作必须各自有明确作用，按钮文本不能因固定宽度裁切或重叠。

### 11.7 同类可编辑数据的字段排版声明

- 对节点、模块、操作、轨道片段等“同一基类下持续扩展”的可编辑数据，应由该类别定义一份共享排版标准；基类和派生类不得各自发明分组名与排序。
- 推荐认知顺序为：概览 → 时间/范围（如有）→ 内容与资源 → 目标与上下文 → 行为参数 → 高级设置 → 预览/调试 → 集合编排。
- 可见序列化字段必须声明中文 `LabelText`、明确 `PropertyOrder` 和该类别认可的分组；嵌入对象确实不需要字段标题时才允许 `HideLabel`。
- 稳定身份、Schema 和纯迁移字段保持隐藏；仍允许编辑的旧兼容入口必须在中文标签中明确标注“兼容”。
- 排版特性只能改变展示，禁止顺带重命名字段、改变序列化类型或修改运行时语义；可扩展类别应增加反射测试，阻止后续字段绕开标准。

### 11.8 变更验收门禁

编辑器 UI 改动必须按以下顺序验收：

1. Unity Editor Compile；
2. 固定 Inspector 与弹出 Inspector 分别打开 Track、Clip；
3. 点击字段、滚轮、按钮、对象选择和文本输入，确认不会关闭或丢目标；
4. 窄窗口、长中文、高 DPI 下检查无横向滚动、重叠和越界；
5. 连续拖动时间轴、滚动 Inspector、修改字段，检查响应和 GC/重绘异常；
6. Domain Reload、窗口关闭重开、Undo/Redo 后复核目标和状态；
7. 最后才允许评估颜色、动效和 Graph 风格一致性。

`.csproj` 编译、UTF-8 Guard 和 `git diff --check` 只能作为源码辅助证据，不能替代上述 Unity 实机验收。

### 11.9 本轮问题登记

以下问题已经发生，后续实施 AI 不得重复：

- 用每次 IMGUI 重绘创建/销毁 Skin、Texture 和 GUIStyle，造成 Track 卡顿和大量 GC；
- 用 `GUI.skin` 误判为可以覆盖 Odin `EditorStyles`，导致“改了外壳但字段仍是默认灰黑”；
- 时间轴根 PointerDown 清理规则穿透 Inspector，点击字段即清空 Clip 并关闭面板；
- 使用多层 ScrollView/IMGUI 滚动造成横向条、黑色条带、内容越界和字段重叠；
- 固定按钮宽度和错误 Flex 结构导致标题、状态、按钮纵向挤压与文字重叠；
- 弹出路由读取旧 Track 状态，导致 Clip 弹出实际进入 Track Inspector；
- 只做静态编译就宣称 Inspector 可用，未先用 Unity 截图和交互矩阵验证；
- 在基础布局未确认前继续叠加动效、分栏、滚动和视觉适配，扩大返工范围。

## 11.10 ES 窗口父子休眠绑定契约（P0）

适用范围：所有使用 `ESEditorPresentation`、`ESWindowFoundation`、
`ESMenuTreeWindow`、`ESSinglePageWindow` 或独立 Inspector 外壳的辅助窗口，
尤其是由主窗口打开的临时 Inspector、预览、浮动工具和附属面板。

### 11.10.1 绑定必须由打开方明确指定

- 子窗口打开入口必须提供 `Open(EditorWindow owner)` 或
  `OpenFor(..., EditorWindow owner)` 形式的可选父窗口参数；业务窗口打开子窗口时必须传入 `this` 或已验证的主窗口实例。
- 不得只依赖 `ESWindow_SleepOwner => SomeWindow.window` 这种动态 getter 来猜测父窗口；getter 只能作为已明确契约的重载恢复兜底，不能代替打开时的 owner 传递。
- 绑定成功必须调用 `ESWindowFoundation.SetSleepOwner(child, owner, ESWindowSleepLinkMode.FollowOwner)`。
- `FollowOwner` 表示父窗口进入/退出半休眠时同步子窗口；`OwnedSurface` 表示内容属于宿主，不得创建独立休眠控件；`Independent` 才是完全独立窗口。

### 11.10.2 父窗口暂时不存在时禁止静默降级

- 子窗口先于父窗口创建、父窗口正在重载或父窗口尚未恢复时，必须登记基础层的 `PendingFollowOwner` 待绑定状态。
- 待绑定状态必须使用稳定字符串 `ownerKey`（例如 `ES.TrackView.Window`）关联父窗口；禁止保存 `EditorWindow`、`UnityEngine.Object`、InstanceId、窗口标题或屏幕坐标作为恢复身份。
- `ownerKey` 必须由窗口契约声明并可序列化恢复；`FollowOwner` 没有稳定 key 时必须报错并阻止待绑定登记，不得把窗口宣称为已绑定。
- 父窗口启用或 Domain Reload 恢复后，由父窗口主动调用稳定 key 解析接口完成绑定；禁止全局扫描所有 `EditorWindow`、按标题匹配或按“最近激活窗口”猜测父子关系。
- 显式 `SetSleepOwner(child, owner, ...)` 的优先级高于此前的 `PendingFollowOwner`；基础层必须在显式绑定成功前清理同一子窗口的旧 Pending 记录，防止宿主稍后恢复时反向覆盖已确认的 owner。
- `CreateInstance`/`OnEnable` 早于调用方传入 owner 时，打开方必须在窗口显示并完成 Presentation 绑定后再次提交显式 owner；不能只写入字段或依赖动态 getter 让关系“碰巧”成立。
- 同一子窗口只能存在一个活动 owner 或一个 pending 记录。重新登记前必须清理旧绑定和旧 pending，避免分叉状态。

### 11.10.3 关闭、重载和生命周期收口

- 父窗口真正关闭/销毁时，基础层必须解除其已绑定子窗口；被父窗口强制休眠的子窗口恢复为 `Independent`，不得留下隐藏的跟随关系。
- 父窗口销毁时必须清理该 `ownerKey` 下未解析的 pending 记录，防止下次打开时把旧子窗口意外重新绑定。
- Domain Reload 后只恢复稳定 `ownerKey` 和窗口自身序列化状态；不得恢复正在进行的拖动、Popup、鼠标捕获、动画计时或活动 `EditorWindow` 引用。
- Domain Reload 后若持久化状态表明窗口处于 `SleepTile`/`EdgeTab`，必须在同一恢复步骤中先写入对应的原生窗口 `position`、`minSize` 和锚点，再显示休眠覆盖层；禁止出现“覆盖层显示为休眠但原生外框仍是大窗口”的混合状态。Reload 恢复不播放从大窗口缩小的动画。
- `SleepTile` 的悬停只是发现和召回提示，不得重置 `SleepTile -> EdgeTab` 晋级计时；只有真实拖动、Busy、Popup、鼠标捕获或显式 `InteractionHold` 才能暂停该计时。休眠块拖动必须使用“起始指针 + 起始窗口矩形”计算绝对目标，再做工作区夹取，禁止把当前窗口坐标重复叠加到每个 PointerMove。
- `EdgeTab` 必须可沿其所属屏幕边缘拖动，拖动期间固定边缘锚点、方向、厚度和当前展开长度；点击仍直接召回 `ActivePanel`。点击/拖动必须设置防手抖阈值，页签伸出必须有短暂悬停意图判定，图标、标题等子元素的 Enter/Leave 不得重复重置计时。方块与页签的 `PointerMove` 只计算最新目标，原生 `EditorWindow.position` 由 Editor update 合并提交，禁止在指针回调内高频同步移动原生窗体并形成 CaptureOut/坐标回写抖动。
- 关闭窗口与半休眠不是同一操作：关闭必须真正解绑并退出生命周期，不能通过缩小、页签或休眠形态伪装关闭。

### 11.10.4 实现与验收边界

- 父子休眠同步属于 `ESEditorPresentation`/`ESWindowFoundation` 基础层；业务窗口只负责声明关系、传递 owner 和提供稳定 key，不得各自复制一套休眠状态机。
- 不得使用全局启动扫描、反射目录、标题猜测或任意窗口拓扑推断来建立关系；绑定必须是用户操作或窗口打开链路中的显式动作。
- 最少验证矩阵：父先开再开子、子先开再开父、父进入/退出半休眠、子 Busy/拖动时延迟同步、父关闭后子恢复独立、Domain Reload 后按 key 恢复、重复打开不产生第二个绑定实例。
- 源码静态检查、`.csproj` 编译、UTF-8 Guard 和 `git diff --check` 不能替代 Unity Editor 实际打开顺序、Domain Reload、窗口关闭重建和交互验收。

### 11.10.5 当前源码案例与依赖级别

当前源码中已存在的子窗口案例必须按以下依赖级别理解，后续 AI 不得仅凭“临时检查器”或窗口标题自行推断：

| 案例 | 打开入口 | 依赖模式 | 稳定 ownerKey | 规则级别 |
|---|---|---|---|---|
| `ESTrackItemTemporaryInspectorWindow` | `OpenFor(..., EditorWindow owner = null)` | `FollowOwner` | `ES.TrackView.Window` | P0 |
| `ESTrackClipTemporaryInspectorWindow` | `OpenFor(..., EditorWindow owner = null)` | `FollowOwner` | `ES.TrackView.Window` | P0 |
| `ESTrackSkillDataTemporaryInspectorWindow` | `OpenFor(..., EditorWindow owner = null)` | `FollowOwner` | `ES.TrackView.Window` | P0 |
| `ESAssetPackageRecordPreviewWindow` | `Open(bake, record, EditorWindow owner)` | `FollowOwner` | `ES.AssetPackageBake.Window` | P0 |
| `ESIndependentInspectorWindow<TWindow>` 的其他派生窗口 | `OpenIndependent(..., owner)` | 由派生类声明；未声明时只能是 `Independent` | 由窗口契约声明 | P0 |

- `owner` 为空不代表独立窗口：`FollowOwner` 必须登记 `PendingFollowOwner`，直到稳定 `ownerKey` 解析成功；不能静默降级。
- `OwnedSurface` 是“内容属于父窗口”的更强依赖级别，不得给子表面生成独立休眠按钮；它不是 `FollowOwner` 的别名。
- `Independent` 只表示休眠生命周期独立，不表示业务数据、关闭顺序或编辑目标独立；仍须按各自资源/目标生命周期收口。
- 父窗口关闭、目标资产失效和 Domain Reload 是三种不同事件，不能用一个 `OnDisable` 分支混为“用户关闭”。
- 父窗口关闭或从 Presentation 解绑时，基础层负责把活动 `FollowOwner` 子窗口恢复为 `Independent`；业务父窗口禁止再调用子窗口的 `Close()` 冒充解绑。只有子窗口自身任务已经失效、用户明确关闭，或独立的业务生命周期规则要求退出时，才允许真正关闭子窗口。
- “父窗口真实关闭后的独立状态”必须作为子窗口自身可序列化的脱离意图保留；后续页面重建或 Domain Reload 不得仅因类声明了 `FollowOwner` 就自动复活关系。只有显式再次调用 `Open(owner)`、`OpenFor(..., owner)` 或等价的 owner 重绑定入口，才能清除脱离标记。Domain Reload 只释放活动引用，不得写入该脱离标记。

以下窗口虽然可能从某个工作台触发，但不得因此机械建立 `FollowOwner`：

- `ESAdvancedDialogWindow`、`ESCompactChoicePopup`、`ESCreateSkillWindow`、`ESInputActionImportWindow`、`ESInputActionBindingImportWindow` 属于 Dialog、Popup 或短生命周期输入面，必须明确不参与半休眠；其中有明确 ES owner 的 Dialog/Popup 应在自身真实存活期持有 `ESWindowFoundation.HoldInteraction(owner)`，关闭、异常或取消时确定性释放，防止 owner 因焦点转移意外休眠；
- `ESAgentArtifactCandidateReviewWindow` 可由生成流程、Graph Inspector 或“打开最新候选”进入，任务本身能够独立完成；若后续接入 Presentation，依赖模式必须是 `Independent`；
- `ESProgressCenterWindow` 是跨任务全局进度聚合面，不属于任一业务窗口的子窗口，并保持不参与自动半休眠；
- 仅仅“由某窗口按钮打开”“打开位置靠近某窗口”或“编辑同一资产”都不足以证明父子依赖。
- `HoldInteraction` 只能暂停已经显式接入 ES Presentation 的窗口，不得因 owner 是原生 Inspector、SceneView 或第三方窗口而隐式执行 `BindWindow`、注入系统动作或取得半休眠所有权。

生产契约门禁位于 `ESMenuTreeCommercialTests.ProductionFollowOwnerWindowsExposeExplicitStableContracts`。新增明确子窗口时必须同步：业务打开调用、稳定 ownerKey、恢复入口、本文案例表和该门禁；不得只增加基类测试。

状态：现行 P0 约束；上述案例为源码事实，实际 Unity 打开顺序、父子同步和 ReloadDomain 交互仍需按 11.10.4 的矩阵验收。

## 11.11 ES 窗口动作宿主与注入边界（P0）

适用范围：`ESMenuTreeWindow<T>`、`ESSinglePageWindow<T>`、
`ESSinglePageIMGUIWindow<T>`、`ESOdinMenuTreeWindow<T>`，以及直接使用
`ESWindowFoundation.Bind` 的 TrackView、Stable Graph、Agent 等自定义窗口。

### 11.11.1 四层动作职责必须分离

- **系统动作**：由 ES 基础层拥有，只承载允许休眠、立即休眠/恢复、自动休眠、全局休眠开关等窗口生命周期能力。业务窗口不得替换、复制或重新实现系统状态机。
- **全局动作**：当前工具跨页面通用的业务动作，不依赖当前页面上下文；例如全局刷新、打开设置或共享诊断入口。
- **窗口动作**：只作用于当前窗口实例，但仍不依赖当前页面上下文；例如重建当前窗口外壳、恢复默认布局或窗口级导出。
- **页面动作**：只作用于当前选中页面，必须通过 `ESMenuTreePageAction` 或等价页面合同取得有效 `ESMenuTreePageContext`；页面切换、删除或局部重建后旧上下文必须失效。
- 四层动作不得塞入同一宿主后依靠按钮顺序区分。标准 MenuTree/SinglePage 外壳应按独立行布局，窄宽度时可分别折叠为菜单，但不得把系统动作与高风险业务动作混为一个无标识菜单。

### 11.11.2 宿主由基类创建，派生窗口只追加

- `ESMenuTreeWindow<T>`、`ESSinglePageWindow<T>` 和 `ESSinglePageIMGUIWindow<T>` 必须由基类创建系统、全局、窗口和页面动作区；派生窗口通过可重写入口追加动作，不需要也不得复制宿主结构。
- 继续使用 Odin 的窗口由 `ESOdinMenuTreeWindow<T>` 提供兼容宿主；Odin 兼容只保留序列化和 PropertyTree 能力，不得恢复第二套窗口生命周期或动作注入机制。
- 直接继承 `EditorWindow` / `OdinEditorWindow` 的自定义窗口若参与 ES Presentation，必须在自身标题栏/工具栏完成布局后构造 `ESWindowActionHosts`，并传给 `ESWindowFoundation.Bind`。每个宿主必须属于当前 `rootVisualElement`，System、Global、Window 不得复用同一 `VisualElement`。
- `OwnedSurface`、Popup、Dialog 和明确不参与半休眠的短生命周期窗口可以不提供系统休眠宿主；这必须由窗口契约显式声明，不能靠基础层猜测窗口标题、尺寸或父窗口。

### 11.11.3 禁止未知窗口覆盖式注入

- 找不到显式 System 宿主时，基础层不得在窗口右上角创建绝对定位按钮、不得 `BringToFront()` 覆盖自定义标题栏，也不得把任意 `Toolbar`、同名元素或 CSS class 当作布局授权。
- 缺少 System 宿主时的安全行为是：不注入系统按钮、保留可诊断状态，并要求该生产窗口完成标准宿主接入；不得静默降级成 GenericMenu 或只靠全局设置入口维持功能。
- 当前 `AttachSemiSleepControls` 找不到显式 System 宿主时直接返回，活跃源码不再创建 `ESWindowSystemActionsFallback`。现行测试 `SemiSleepControlsRequireDeclaredHostAndUseResponsiveOverflow` 固化了同一合同：无宿主不注入，有宿主才挂载；不得恢复旧 fallback 或复制其模式。
- TrackView、Stable Graph 和 Agent 已有显式宿主只能证明这些接入点存在；所有生产窗口、窄屏折叠和系统动作状态同步仍须分别验收。

### 11.11.4 验收门禁

1. System、Global、Window、Page 四层在宽窗口中分区清晰；
2. 窄窗口下动作按职责折叠，文本不裁切，危险动作不会因折叠失去确认语义；
3. 页面切换后仅页面动作变化，系统/全局/窗口动作实例和状态不重复注册；
4. 未声明宿主的自定义窗口不出现覆盖式按钮或无上下文 GenericMenu；
5. ReloadDomain、窗口重建和主题切换后宿主只存在一份，回调能够确定性释放；
6. 半休眠允许状态、立即休眠、自动/固定和全局开关在按钮、菜单与持久化状态间保持一致。

源码结构或反射测试只能证明合同形状；按钮位置、折叠手感、焦点、Popup、ContextMenu、ReloadDomain 和高 DPI 必须在 Unity 中实机验证。

## 12. ES Presentation 全局皮肤与品牌字体边界（P0）

适用范围：`ESEditorPresentation`、`ESGlobalEditorSkinExperiment`、
`ESGlobalEditorTheme`、全局 Editor USS、ES 自有窗口品牌字体及其资源。

当前源码与资源证据入口：

- `Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/GlobalEditorData/ESGlobalEditorTheme.cs`
- `Assets/Plugins/ES/Editor/ESPresentation/Styles/ESGlobalEditorDeepSkin.uss`
- `Assets/Plugins/ES/Editor/ESPresentation/Styles/ESBrandTypography.uss`
- `Assets/Plugins/ES/Editor/Resources/ESPresentation/Fonts/`

### 12.1 全局皮肤不得成为常驻重任务

- 全局皮肤只允许在显式启用、主题切换、用户刷新、返回 EditMode 或新窗口同步时工作；禁止在 `Update`、`OnGUI`、每帧 Repaint 中枚举全部窗口、反射 `EditorStyles`、读取纹理或重新挂载 USS。
- `EditorStyles` 反射字段必须按类型缓存；已处理的 `GUIStyle`、窗口根节点和 ES 窗口绑定必须使用哈希索引，禁止在交互热路径反复线性遍历。
- 多次主题/窗口同步必须通过单一 `delayCall` 合并。窗口同步只处理已挂 Panel 的活动根节点，并清理已关闭窗口记录；禁止把隐藏或失效窗口长期留在快照集合中。
- 用户点击“刷新”时，应先比较当前 `EditorStyles` 容器和 Pro/Light Skin。两者未变化时只增量同步窗口，不得先完整 `Restore` 再销毁、重建全部纹理。
- 未应用任何深度皮肤状态时，`Restore` 不得无意义触发 `InternalEditorUtility.RepaintAllViews()`。

### 12.2 纹理生成必须缓存、受预算约束并可逆

- 同一源纹理在一次应用过程内最多读取一次源像素；不同 `SkinTone` 复用该源像素，禁止每个状态都重复 `Graphics.Blit + ReadPixels`。
- 生成纹理必须继续使用 `HideFlags.HideAndDontSave`，按源纹理与 Tone 缓存，并保留数量、单纹理像素和总字节硬预算。扩大预算必须先取得 Unity Profiler 与显存证据。
- 临时源像素缓存只允许存在于一次应用过程，完成后立即清空；持久缓存只保存实际生成纹理及其可逆快照。
- 每个被修改的 `GUIStyleState` 必须保留原文字色、背景和缩放背景；停用、进入 PlayMode、受控卸载和域重载路径必须能够恢复原生状态并销毁生成纹理。
- 禁止为透明根节点或 `background == null` 的原生宿主强行填充不透明背景；深度皮肤只能调整已识别语义和安全内容容器。

### 12.3 ES 品牌字体只用于信息层级，不覆盖正文

- 品牌字体只允许应用于 ES 自有窗口的标题、数字、徽章、状态标识和短标签。
- 正文、输入框、日志、Console、代码、命令输出、文件名和路径必须继续使用 Unity 默认字体及其回退链。
- UI Toolkit 必须通过 `es-brand-typography` 根类和明确的品牌语义类选择器接入；禁止使用 `.unity-label`、`Label` 或窗口根继承等宽选择器覆盖全部文本。
- IMGUI 只允许在共享标题 `GUIStyle` 首次构建时应用字体；禁止每帧 `Resources.Load`、动态创建 `Font` 或逐控件复制字体资源。
- 字体必须具有完整简体中文字形、明确的可再分发授权，并位于 `Assets/Plugins/ES/Editor` 内随 ES 包分发。禁止依赖开发机系统字体、项目外绝对路径或要求使用者手工安装字体。
- 当前随包默认字体为 `ChillRoundGothic Medium（寒蝉圆黑体）`，许可证必须与字体文件一同保留。字体属于 Editor-only 资源，不得进入玩家运行时资源收集、AssetBundle 或发布清单。
- `ESGlobalEditorTheme.enableBrandTypography` 必须能够关闭品牌字体；关闭后标题自动回退 Unity 默认字体，不能影响正文布局和输入行为。

### 12.4 验收要求

1. 关闭深度皮肤时确认无全窗口扫描、无无效全局 Repaint；
2. 首次启用记录 Apply 尖峰、生成纹理数量和字节，确认未突破硬预算；
3. 连续点击刷新时确认 `EditorStyles` 未变化路径只执行窗口增量同步；
4. 同时打开多个 ES 与原生窗口，确认 USS 不重复挂载、关闭窗口后记录可清理；
5. 进入/退出 PlayMode、Domain Reload、切换深浅主题后确认原生样式可恢复且 ES 窗口可重新绑定；
6. 检查中文标题、数字和徽章使用品牌字体，同时输入框、日志、代码和路径仍保持 Unity 默认字体；
7. Unity Console 无 USS/字体加载错误，Profiler 无每帧纹理、样式、反射和窗口枚举分配。

静态编译只能证明 API 与语法成立，不能替代 Unity 实机的皮肤恢复、中文显示、
窗口生命周期和 Profiler 验收。
