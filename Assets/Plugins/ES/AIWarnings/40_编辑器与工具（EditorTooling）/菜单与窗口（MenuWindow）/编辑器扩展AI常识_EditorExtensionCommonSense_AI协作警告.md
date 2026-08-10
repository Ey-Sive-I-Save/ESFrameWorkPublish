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

## 2. EditorWindow 定位顺序

正确的首开定位顺序：

1. 先 `ShowUtility()` / `Show()`。
2. 再 `Focus()`。
3. 先计算并写入窗口尺寸。
4. 再按主窗口位置居中。

居中必须使用：

```csharp
Rect main = EditorGUIUtility.GetMainWindowPosition();
```

禁止用 `Screen.currentResolution` 做 Editor 窗口居中，它不代表 Unity Editor
主窗口在显示器上的实际坐标。

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
| 面板左上显示 | 使用 Screen.currentResolution 定位 | 用 GetMainWindowPosition 居中 |

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
- Inspector 字段变更不得在每个事件阶段重复保存、预览重建或全量刷新；必须区分编辑中、提交、失焦和明确保存。
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

### 11.7 变更验收门禁

编辑器 UI 改动必须按以下顺序验收：

1. Unity Editor Compile；
2. 固定 Inspector 与弹出 Inspector 分别打开 Track、Clip；
3. 点击字段、滚轮、按钮、对象选择和文本输入，确认不会关闭或丢目标；
4. 窄窗口、长中文、高 DPI 下检查无横向滚动、重叠和越界；
5. 连续拖动时间轴、滚动 Inspector、修改字段，检查响应和 GC/重绘异常；
6. Domain Reload、窗口关闭重开、Undo/Redo 后复核目标和状态；
7. 最后才允许评估颜色、动效和 Graph 风格一致性。

`.csproj` 编译、UTF-8 Guard 和 `git diff --check` 只能作为源码辅助证据，不能替代上述 Unity 实机验收。

### 11.8 本轮问题登记

以下问题已经发生，后续实施 AI 不得重复：

- 用每次 IMGUI 重绘创建/销毁 Skin、Texture 和 GUIStyle，造成 Track 卡顿和大量 GC；
- 用 `GUI.skin` 误判为可以覆盖 Odin `EditorStyles`，导致“改了外壳但字段仍是默认灰黑”；
- 时间轴根 PointerDown 清理规则穿透 Inspector，点击字段即清空 Clip 并关闭面板；
- 使用多层 ScrollView/IMGUI 滚动造成横向条、黑色条带、内容越界和字段重叠；
- 固定按钮宽度和错误 Flex 结构导致标题、状态、按钮纵向挤压与文字重叠；
- 弹出路由读取旧 Track 状态，导致 Clip 弹出实际进入 Track Inspector；
- 只做静态编译就宣称 Inspector 可用，未先用 Unity 截图和交互矩阵验证；
- 在基础布局未确认前继续叠加动效、分栏、滚动和视觉适配，扩大返工范围。
