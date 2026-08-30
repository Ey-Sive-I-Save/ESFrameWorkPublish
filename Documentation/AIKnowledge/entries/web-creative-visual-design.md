# Web 创意视觉设计、动态表现与 3D 排版知识

`KnowledgeId`: `es.project.web-creative-visual-design.v1`
`Authority`: 当前 WebPageStudio Page IR、Quality/Accessibility 合同与官方 Web 标准校准快照
`RouteKeys`: `web-creative-design`, `web-motion`, `web-scroll-animation`, `web-view-transition`, `web-3d`, `web-typography`, `web-visual-hierarchy`, `web-reduced-motion`
`ContentHash`: `637fc259e9c17a339a38399bde9afccaf5dc6aac4fb89475987d9fb8ae7708c3`
`EvidenceLevel`: `S1`
`StaleWhen`: Page IR、视觉设计合同、Quality/Accessibility 验证器、浏览器标准快照或任一 SourceRef 哈希变化。

## 设计目标

网页的“绚丽”和“冲击力”必须由可解释的视觉系统生成，而不是随机堆叠特效。每个页面先建立视觉叙事：焦点（hero）、层级（section）、行动（CTA）、反馈（micro-interaction），再选择动效、材质、色彩和排版。静态输出必须可阅读、可降级；动态能力只能通过已声明的 runtime contract 接入。

## 高级视觉生成规则

1. **动效层级**：将动效分为入场（hero intro）、滚动叙事（section reveal）和微交互（hover/focus/press）。为每层定义时长、easing、stagger 上限与取消条件，禁止所有元素同时运动；默认只动画 `transform` 与 `opacity`。
2. **滚动驱动**：优先使用 CSS `scroll-timeline`/`view-timeline` 与 `animation-range`；用 `@supports` 提供静态或 IntersectionObserver 降级，禁止高频 scroll 事件直接改布局。滚动动画必须有进度边界，不能阻塞阅读和键盘操作。
3. **页面转场**：SPA/MPA 导航可使用 View Transition API，通过稳定的 `view-transition-name` 标识连续元素；对首屏、表单、错误恢复和不支持 API 的浏览器提供跳过或无动画路径。
4. **3D 化表现**：用 `perspective`、`transform-style: preserve-3d`、`translateZ/rotate` 构造有限深度层；同时明确光源方向、阴影、反射、景深和 `backface-visibility`。3D 只是空间错觉，不得改变语义顺序或造成内容遮挡；移动端与低性能设备应降低层数和模糊半径。
5. **创意配色**：先定义语义色角色（canvas/surface/text/muted/brand/accent/danger），再用渐变、噪点、玻璃或金属质感作为受控装饰。保持对比度、焦点可见性和暗/浅主题对称；`forced-colors` 下让系统颜色接管必要信息。
6. **精妙排版**：建立 `clamp()` 流体字号、明确 type scale、行高和最大阅读宽度；标题、正文、标注、数字和 CTA 形成至少四级层级。用网格、基线、留白和节奏控制密度，避免只靠字重/颜色表达信息；动感字体在 reduced-motion 下回归稳定排版。
7. **冲击力与可用性平衡**：每屏只保留一个主焦点和一个主行动；用尺寸、对比、空间、深度和时间差制造注意力路径。装饰不得遮挡文本、焦点环或触控目标，动画必须可暂停、跳过或关闭。
8. **性能预算**：将视觉效果映射到预算：首屏 LCP、交互 INP、布局稳定 CLS，以及纹理、阴影、滤镜和动画层数量。避免布局抖动、过度绘制和大面积 backdrop-filter；在生成结果中记录待运行时验证的指标。

## 生成前检查清单

- DesignSpec 是否声明焦点、层级、色彩角色、动效层级、3D 深度和排版比例？
- 是否提供 `prefers-reduced-motion: reduce`、键盘焦点、触摸和窄容器方案？
- CSS 新能力是否有 `@supports`/静态降级？3D 层是否有遮挡、backface 和性能上限？
- Quality、Accessibility、Contract、UTF-8 是否能在静态门禁中复现？浏览器、视觉回归和 Web Vitals 未运行时必须标记 `runtime-not-run`。

## 失败矩阵与恢复

| ID | 失败 | 恢复 |
|---|---|---|
| WEB-MOTION-001 | 动效没有层级或一次性全屏运动 | 删除非必要动画，分配 hero/section/micro 三层并限制 stagger |
| WEB-3D-002 | 3D 变换遮挡内容或导致抖动 | 减少深度层、补充 perspective/backface、回退为 2D 阴影 |
| WEB-TYPE-003 | 标题溢出、阅读宽度过长或层级不清 | 使用流体字号、最大行宽、基线网格和四级 type scale |
| WEB-REDUCE-004 | 未尊重 reduced-motion 或焦点不可见 | 关闭非必要运动，保留功能反馈，恢复稳定焦点样式 |
| WEB-PERF-005 | 大量滤镜/布局动画造成性能风险 | 仅保留 transform/opacity，降低模糊与层数，加入运行时预算验证 |

## 官方校准（截至 2026-08-29）

- MDN 的 scroll-driven animations 定义了基于滚动或视口可见度的时间线，适合把叙事进度交给 CSS；实现必须准备能力检测和降级。
- View Transition API 支持 SPA 与 MPA 的页面状态转场，连续元素应使用命名快照并允许跳过不合适的转场。
- W3C CSS Transforms Level 2 规定 perspective、preserve-3d、backface-visibility 等 3D 渲染模型；元素本质仍是二维平面，深度是渲染效果。
- MDN 建议用 `prefers-reduced-motion` 关闭或减少非必要移动，同时保留必要的功能反馈。
- web.dev Core Web Vitals 以 p75 的 LCP ≤ 2.5s、INP ≤ 200ms、CLS ≤ 0.1 作为常用目标；本条目只把它们作为预算，不把静态检查冒充运行时成绩。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)

