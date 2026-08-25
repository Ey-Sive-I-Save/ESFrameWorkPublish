# Unity UI 自动布局、滚动与裁剪（2022.3.45f1）

`KnowledgeId`: `es.unity.ui-layout-clipping.v1`  
`Authority`: `Unity 2022.3 UGUI package documentation + package source`  
`RouteKeys`: `ui-automation`, `ui`, `ui-unity`, `ui-layout`, `responsive`, `ui-responsive`, `ui-clipping`, `scroll-rect`
`ContentHash`: `6755927e1fc9f018a08d690ca1fbd35aee13b2d0308ba955cd10323c140e6d47`

## Scope

本条目覆盖 UGUI 自动布局、尺寸驱动、ScrollRect 和 Mask 的组合边界，目标是为 Prefab 与
ScreenSpec 设计提供可检查的层级合同。依据项目锁定的 `com.unity.ugui@1.0.0` 文档及源码；
没有执行 Unity 场景或运行时验收。

## Verified facts

- LayoutGroup 根据子 RectTransform 的 preferred/min/flexible 尺寸分配位置；Horizontal、
  Vertical 和 Grid 的轴向、间距、padding 与 child alignment 语义不同。LayoutElement 可以
  覆盖子项的 min/preferred/flexible 尺寸参与布局。
- ContentSizeFitter 通过布局系统驱动自身 RectTransform 的尺寸。它与同一 RectTransform
  上控制该轴尺寸的 LayoutGroup/其他驱动器组合时可能形成循环；布局系统会重复标脏并重新
  建造，不能把循环当作稳定的自适应方案。
- LayoutRebuilder 在 CanvasUpdateRegistry 的布局阶段重建布局；尺寸或层级变化会使相关
  Graphic/Layout 元素标脏。批量改动应集中在一次布局更新前完成，避免逐项强制重建。
- ScrollRect 通常由 `ScrollRect root -> Viewport -> Content` 组成。Viewport 负责可见窗口，
  Content 是可滚动 RectTransform；水平/垂直滚动、movement type、inertia、elasticity 和
  scrollbar 引用共同决定行为。Content 的尺寸必须能表达其子项总尺寸。
- Mask 使用图形 stencil 边界，要求有效 Graphic；RectMask2D 使用矩形裁剪并受 Canvas
  渲染路径约束。两者都只影响后代可见性，不会替代布局或改变子项的逻辑尺寸。

## Authoring rules

1. 自动布局链必须有单向尺寸所有权：内容项提供 preferred/min，LayoutGroup 分配位置，
   ContentSizeFitter 只在没有上游尺寸驱动的轴上使用。
2. ScrollRect Prefab 固定声明 Root、Viewport、Content 三个角色；Viewport 的 Mask/RectMask2D
   与 Content 的布局职责分离，滚动条只作为可选控制器接入。
3. 列表项需要可变文本或异步资源时，先更新内容尺寸，再在同一批次刷新布局；不要通过每帧
   `ForceRebuildLayoutImmediate` 掩盖结构错误。
4. 选择 Mask 还是 RectMask2D 时记录裁剪需求：非矩形形状/软边界才引入 Mask；矩形列表优先
   评估 RectMask2D，并以实际材质/批次证据确认收益。

## Failure patterns

- 父 LayoutGroup 与子 ContentSizeFitter 在同一轴互相写尺寸，造成布局循环或跳动。
- ScrollRect 的 Viewport 未覆盖 Content、Content 与 Viewport 同级、或 Content anchor 使其
  尺寸无法增长，表现为无法滚动或裁剪区域错误。
- 把 Mask 当作安全区/布局容器，导致内容仍占据错误尺寸；裁剪和布局必须分别验证。

## Assumptions and non-claims

本条目假定普通 UGUI Canvas 渲染，未覆盖 UI Toolkit、第三方虚拟列表或项目自定义布局器。
静态源码和文档阅读没有证明任何具体 Prefab 的视觉、触摸、性能或内存结果。

## Official documentation

- https://docs.unity3d.com/2022.3/Documentation/Manual/comp-UIAutoLayout.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/script-ScrollRect.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/script-ContentSizeFitter.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/script-Mask.html
- https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Packages/packages-lock.json` (`6db87482785cd1b498aeb7386723c5b8f23fe7f79c8f3e2d409bf0206b48796f`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/comp-UIAutoLayout.md` (`727a23e183b10546fed7b29a65916302ec9d0386b9494f99711fb9abf21726fe`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-ContentSizeFitter.md` (`8f1f65b685e15ea1d8a7c685ec392d077721f80d276404e89e685c3c35411f31`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-LayoutElement.md` (`98d8973554a581f8985d9f1462fb682d48aa99ee494b840db1dc0b9bd69e1814`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-HorizontalLayoutGroup.md` (`65590135b4a9e036de0219e5afea313291b66129e8076264b258a8689307f2fc`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-VerticalLayoutGroup.md` (`81471ad089571275ff6f2b0b746a4b6b2d0a0189a1905f50f83d382c6ebbbf21`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-GridLayoutGroup.md` (`8fec0a9bb9839adec5731772f35c86ce5c877b8eb04ac33b9659f93eca1a1bdc`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-ScrollRect.md` (`03b1deeea7249d36526d7ab1badd33aaf0da0da68cc90c8ef116060b3fa866f8`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-Mask.md` (`a5009501c35ea1263a7159f68eda4bf0d6145e8e407666d1804fdf569e02e5cb`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-RectMask2D.md` (`2cc94772682ff36040a4f70aa95616bafdb9c64c3030c6a0b51c49bcdf4b3e92`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Layout/LayoutRebuilder.cs` (`a716457d2bd3539145a02a9ef2184582db066bf437648cbd2a6d42b7c60b42fe`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Layout/ContentSizeFitter.cs` (`a6b30a3b6e697524ce16f07e1807e870faf8d0c3e8c43004474eb5b8bb3c887b`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/ScrollRect.cs` (`1e576cf5927c3aa8e55184154e0aa807f038407769a9e88cae3f5915b9935228`)

`EvidenceLevel`: `S2`  
`StaleWhen`: UGUI 布局/裁剪/ScrollRect 代码或文档、项目包锁定版本、布局器实现、Prefab 层级合同或任一 SourceRef 哈希变化。
