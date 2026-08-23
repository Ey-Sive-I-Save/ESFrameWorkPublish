# Unity UI Canvas 与响应式布局（2022.3.45f1）

`KnowledgeId`: `es.unity.ui-canvas-layout.v1`  
`Authority`: `Unity 2022.3 package documentation + package source + project version`  
`RouteKeys`: `ui`, `ui-unity`, `ui-canvas`, `ui-layout`, `ui-responsive`, `rect-transform`, `canvas-scaler`  
`ContentHash`: `d9fb346e62ba470b2de600c78a2127d8a16d65d5641b68c73831bb003cf41e89`

## Scope

本条目为 ESFramework 当前 Unity 版本的 UGUI Canvas、RectTransform 和多分辨率布局提供
可追溯的基础事实。项目版本是 `2022.3.45f1`，Unity revision 为 `a13dfa44d684`；
相关包为 `com.unity.ugui@1.0.0`。内容来自该包的随包文档和运行时代码阅读，未把静态
阅读写成 Editor、PlayMode、Profiler 或发布验收证据。

## Verified facts

- Canvas 的 `Screen Space - Overlay` 不依赖场景相机；`Screen Space - Camera` 使用指定相机；
  `World Space` 将 UI 当作场景中的世界空间对象。Canvas 的层级和 sibling 顺序决定同一
  Canvas 内的绘制前后关系，嵌套 Canvas 才建立额外的批次/排序边界。
- RectTransform 的位置由父矩形、anchor、pivot 和 `sizeDelta` 共同决定。anchor 在父矩形
  中定义比例位置；pivot 是自身局部旋转/缩放中心；拉伸 anchor 时，`sizeDelta` 表示相对
  于 anchor 区间的偏移，而不是固定屏幕像素尺寸。
- CanvasScaler 的 `Scale With Screen Size` 以 Reference Resolution 为基准。`Match Width
  Or Height` 在宽高之间插值缩放；`Expand` 保持最小尺寸并扩展参考区域；`Shrink` 保持最大
  尺寸并裁减参考区域。`Constant Pixel Size` 和 `Constant Physical Size` 有不同的 DPI/像素
  假设，不能与参考分辨率语义混用。
- UGUI 的多分辨率方案要求把固定边距表达为 anchor/pivot 与布局约束，把屏幕边缘安全区
  作为运行时输入，而不是把单一分辨率下的绝对坐标复制到所有设备。

## Authoring rules

1. 每个运行时屏幕默认使用一个 root Canvas 和一个 CanvasScaler；只有需要独立排序、更新
   频率或渲染目标时才增加嵌套 Canvas，并记录其排序边界。
2. 先确定屏幕的 anchor/pivot 语义，再选择 LayoutGroup 或显式尺寸。不要同时用父级拉伸、
   子级绝对位置和互相驱动的自适应组件表达同一约束。
3. Reference Resolution、Match、方向和安全区必须作为 ScreenSpec/Prefab 的显式配置，不能
   只依赖当前编辑器 Game 视图看起来正确。
4. 需要适配刘海、圆角或系统栏时，在 Canvas 下建立安全区容器并以 `Screen.safeArea` 更新
   其 RectTransform；安全区策略不能改写业务面板的最小尺寸合同。

## Assumptions and non-claims

这些规则假定项目使用 UGUI（`UnityEngine.UI`）而非 UI Toolkit。尚未在 Unity Editor 中运行
场景、切换真实分辨率、验证字体/材质或采集 GPU 截图，因此本条目不声明视觉正确、运行时
输入可用、性能达标或发布通过。

## Official documentation

- https://docs.unity3d.com/2022.3/Documentation/Manual/UICanvas.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/UIBasicLayout.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/HOWTO-UIMultiResolution.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/script-CanvasScaler.html
- https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Packages/packages-lock.json` (`6db87482785cd1b498aeb7386723c5b8f23fe7f79c8f3e2d409bf0206b48796f`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/UICanvas.md` (`724607c892472f573d6b6475794ebc08a62df7384dbbacc4c1817a0f3d88e0c4`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/UIBasicLayout.md` (`9a2149d4eb669d0bab8cc3edab78940804a070ed34339ab5dd9fd40e4e1c8932`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/class-RectTransform.md` (`1203caf31b1f54a797cebc07336f96e0faa7d95e1496d7fca8b2255435172694`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-CanvasScaler.md` (`fb36337d6a4714789723165ea4d28c7c0448040667ba14d5fb867ed4e0a756b2`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/HOWTO-UIMultiResolution.md` (`fccc33bfb27d315db11d9c8c12e3e5758b2f05536d87de3c051894c67a013335`)

`EvidenceLevel`: `S2`  
`StaleWhen`: Unity major/minor/patch version、UGUI 包版本、Canvas/RectTransform/CanvasScaler 合同、项目安全区策略或任一 SourceRef 哈希变化。
