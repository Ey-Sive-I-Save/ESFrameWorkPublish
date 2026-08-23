# Unity UI 事件、交互与图形更新（2022.3.45f1）

`KnowledgeId`: `es.unity.ui-interaction-rendering.v1`  
`Authority`: `Unity 2022.3 UGUI/Input System package documentation + package source`  
`RouteKeys`: `ui`, `ui-unity`, `ui-interaction`, `ui-rendering`, `ui-input`, `event-system`, `graphic-raycaster`, `selectable`  
`ContentHash`: `a4ebf91f9304527acefa98658194f87b93b0885e3354daec5d71907af72ee15d`

## Scope

本条目描述 UGUI 事件入口、GraphicRaycaster、Selectable 派生控件和图形/布局重建边界。
项目锁定 Unity `2022.3.45f1`、`com.unity.ugui@1.0.0` 与 `com.unity.inputsystem@1.11.2`；
Input System 的 UI 模块只作为事件输入适配层，不拥有业务命令权限。

## Verified facts

- EventSystem 需要一个活动的 Input Module 来产生指针、导航和提交事件；Raycaster 将输入
  位置投影到可命中的 UI/场景对象。一个事件系统不应在同一场景无意中并行运行多个互斥模块。
- GraphicRaycaster 对 Canvas 下的 Graphic 做命中测试；`raycastTarget` 为 false 的 Graphic
  不参与该图形命中。排序、阻挡对象和 Canvas 层级会影响最终命中顺序。
- Selectable 管理 Normal、Highlighted、Pressed、Selected、Disabled 等状态，并通过
  Color Tint、Sprite Swap 或 Animation Transition 反馈。Navigation 可以是 Automatic、
  Explicit 或关闭；键盘/手柄焦点依赖可导航对象和当前 EventSystem selection。
- Button 通过 click UnityEvent 发出一次提交；Toggle 维护 isOn 并可加入 ToggleGroup；Slider
  维护 value 与方向/范围。控件事件是 UI 层事实，业务层仍需明确授权、去重、取消和生命周期。
- Graphic 的材质、颜色、顶点或 RectTransform 变化会使 Graphic 标脏。CanvasUpdateRegistry
  在布局阶段和图形阶段批量处理重建队列；静态修改不等于已完成 GPU、批次或帧时序验证。
- Input System 包的 `InputSystemUIInputModule` 将 Input Actions 映射到 EventSystem 事件；
  它必须与场景中的 EventSystem、Canvas Raycaster 和已启用的 UI Action 资产一致，不能只因
  Action 资产存在就宣称控件可操作。

## Authoring rules

1. 每个交互屏幕明确唯一 EventSystem/Input Module 组合，并记录输入后端（旧 Input Manager
   或 Input System）及切换策略。
2. 仅装饰性的 Image/Text 关闭 `raycastTarget`；可交互 Graphic 保留命中并检查透明区域是否
   需要额外的 raycast filter，避免遮挡按钮。
3. 控件状态、Navigation 和业务命令分层：Selectable 只负责状态与事件分发，Presenter/命令
   层负责权限、重复提交、异步关闭和错误反馈。
4. 修改大量 UI 时集中变更后再观察一次 Canvas 更新；不要在 Update 中反复设置同一属性以
   规避布局或图形重建问题。

## Failure patterns

- EventSystem 存在但没有正确 Input Module，或旧模块与 Input System 模块同时处理同一输入。
- 全屏 Image 保持 raycastTarget，导致视觉上位于其后的按钮永远收不到点击。
- 只配置 Button.onClick 而未配置 Raycaster、EventSystem、可交互状态或输入 Action，造成
  编辑器层级看似完整但运行时无事件。

## Assumptions and non-claims

本条目不覆盖 XR 专用 TrackedDeviceRaycaster、多玩家 EventSystem 或第三方导航框架的完整
合同。未运行 Unity Editor、未注入真实设备输入、未采集帧调试/Profiler/截图，因此不声明
任何具体场景的交互、视觉、批次或性能验收已经通过。

## Official documentation

- https://docs.unity3d.com/2022.3/Documentation/Manual/UIInteractionComponents.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/EventSystem.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/Raycasters.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/script-GraphicRaycaster.html
- https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html
- https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Packages/packages-lock.json` (`6db87482785cd1b498aeb7386723c5b8f23fe7f79c8f3e2d409bf0206b48796f`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/EventSystem.md` (`1a2d06703dddc79aaf88e1ae799e3957ffa9f39c1b54029d243394b83dcc3961`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/Raycasters.md` (`18803aaac55431766143b9c5af3f25038e2bc30c217659a457fa1d3dbcdbfb49`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-GraphicRaycaster.md` (`4007fd76bd1188a697d5341868f40da1dabc0d20ebda6c15020daefe48949903`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-Selectable.md` (`f8a4d69666eb00ccc966aee745f3f1197ddfbb688e2a9cb1fe4b1f60e86cddde`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-Button.md` (`77eb38636d3b174c1213df32b13df6230beba2d84f4f393eade3ca7fbc749845`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-Toggle.md` (`1e3370fc88137c0395cf04fe027e46a0527e47cbe4d307da6788d8b2ec7d79d0`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-Slider.md` (`0c9456d509cc8defc9961e73a425495bd09b9c86ded7b32e8bad731865fc6646`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/CanvasUpdateRegistry.cs` (`6d74c8cfa3500ffc2f35e1dd6ebc991178e2227194e8994dffe9db74575171cb`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Graphic.cs` (`c23b303effecdb6693f791cbbe703f0c368fd92b1443934ae90d4d97c21dd9b0`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/GraphicRaycaster.cs` (`1625f39ea41156afec5995401d8aaab16eb931029cdf496963fddd4bbf0f66ca`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Selectable.cs` (`aa2703fa92ae0ec0386e309dcd06c3646c581ccc756f16a7735a19ab8fcb1a30`)
- `Library/PackageCache/com.unity.inputsystem@1.11.2/InputSystem/Plugins/UI/InputSystemUIInputModule.cs` (`6f7abbed16e134a5f9e0cae4de505bf5b7d49232bca80fb51bc69b2287e175d7`)

`EvidenceLevel`: `S2`  
`StaleWhen`: Unity/UGUI/Input System 版本、EventSystem/Raycaster/Selectable/Graphic 更新合同、项目输入资产或任一 SourceRef 哈希变化。
