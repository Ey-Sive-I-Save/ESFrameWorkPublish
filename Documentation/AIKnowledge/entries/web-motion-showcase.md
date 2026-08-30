# Web 动效展示与视觉叙事知识

`KnowledgeId`: `es.project.web-motion-showcase.v1`
`Authority`: WebPageStudio Page IR、Quality/Accessibility 合同与官方 Web 标准校准快照
`RouteKeys`: `web-motion-showcase`, `web-scroll-animation`, `web-view-transition`, `web-hero-animation`, `web-3d-motion`, `web-performance-motion`, `web-motion-fallback`
`ContentHash`: `637fc259e9c17a339a38399bde9afccaf5dc6aac4fb89475987d9fb8ae7708c3`
`EvidenceLevel`: `S1`
`StaleWhen`: 动效合同、浏览器标准快照、Quality/Accessibility 验证器或 SourceRef 哈希变化。

## 动效编排

- 用 `hero → reveal → focus → micro` 四层叙事：首屏建立品牌张力，滚动揭示结构，焦点动画解释关系，微交互反馈操作；每层设置时长、easing、stagger 和可取消条件。
- 滚动叙事优先 CSS `scroll-timeline`/`view-timeline`/`animation-range`，并以 `@supports` 提供静态或低动效回退；不要在 scroll 事件中持续读写布局。
- 页面导航使用 View Transition API 时，为连续元素分配稳定名字；表单提交、错误态、长任务和不支持 API 的浏览器应跳过复杂转场。
- 3D 展示采用有限深度层、统一光向、阴影和景深；移动端减少层数、模糊、粒子和大面积滤镜，默认 transform/opacity 合成。
- 动效必须服务于空间、因果或状态，不用无限循环、闪烁和同时入场制造噪音；提供暂停、跳过和 `prefers-reduced-motion` 路径。

## 案例抽象

将高质量创意站点常见结构归一为 `ScrollFilm`（章节时间线）、`ProductOrbit`（有限 3D 轨道）、`GalleryMorph`（图片/卡片转场）和 `KineticEditorial`（文字节奏）。这些是生成配方，不是复制某一站点；素材来源、许可证和运行时支持仍需单独验证。

Chrome 对 Tokopedia、redBus、Policybazaar 的案例总结表明，声明式滚动动画可以替代大量主线程 scroll JavaScript，并让动画与滚动保持同步；NRK 案例则把滚动工具接入 CMS，使编辑人员可重复编排叙事。可迁移经验是：让内容作者控制时间线参数，但由生成器限制属性白名单、动画范围和降级路径。

案例来源：https://developer.chrome.com/blog/css-ui-ecommerce-sda?hl=en；https://developer.chrome.com/blog/nrk-casestudy

## 失败恢复

`WEB-MOTION-001` 全屏同时运动：降级为单焦点、分层 stagger；`WEB-MOTION-002` 滚动卡顿：移除布局动画并改 transform/opacity；`WEB-MOTION-003` 3D 遮挡：减少 translateZ、补 backface 和 2D fallback；`WEB-MOTION-004` 动效不支持：使用 `@supports` 静态首帧；`WEB-MOTION-005` 运动敏感：关闭非必要动画并保留功能反馈。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
- https://developer.mozilla.org/en-US/docs/Web/CSS/Guides/Scroll-driven_animations
- https://developer.mozilla.org/en-US/docs/Web/API/View_Transition_API
- https://www.w3.org/TR/css-transforms-2/
