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
