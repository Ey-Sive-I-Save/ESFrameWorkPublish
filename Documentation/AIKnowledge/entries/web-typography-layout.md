# Web 排版系统与视觉层级知识

`KnowledgeId`: `es.project.web-typography-layout.v1`
`Authority`: WebPageStudio Page IR、Quality/Accessibility 合同与官方 Web 标准校准快照
`RouteKeys`: `web-layout-system`, `web-typography`, `web-type-scale`, `web-editorial-grid`, `web-content-hierarchy`, `web-fluid-type`, `web-design-system`
`ContentHash`: `637fc259e9c17a339a38399bde9afccaf5dc6aac4fb89475987d9fb8ae7708c3`
`EvidenceLevel`: `S1`
`StaleWhen`: Page IR、排版/主题合同、Quality/Accessibility 验证器或 SourceRef 哈希变化。

## 生成方法

- 先定义内容意图、阅读路径和主焦点，再建立 4 级 type scale（display/title/body/meta）、行高、字距和最大行宽；不要先选字体再硬塞内容。
- 使用 `clamp()` 建立流体字号，使用容器查询适配组件宽度；窄容器优先重排而不是缩小到不可读。
- 以 8pt/4pt 间距节奏、基线网格、模块留白和对齐线控制密度；每屏一个主标题、一个主行动和一条视觉锚线。
- 中文与拉丁文字分别校正字重、标点挤压、断行、数字等宽和 fallback；不可依赖单一网络字体才能保持语义。
- 杂志/展览风格可使用不对称网格、超大标题、边注、编号和跨栏图像，但必须保留 DOM 语义顺序、键盘顺序和移动端线性阅读。
- 动态字体、逐字入场和变量轴变化只作为装饰；`prefers-reduced-motion` 下回归稳定字号和静态层级。

## 案例抽象

从 Awwwards 的编辑型案例与 W3C/MDN 标准中抽象出可复用模式：`Editorial`（大标题+窄正文+边注）、`ArtDirection`（非对称网格+单一焦点）、`ProductClarity`（高密度信息+强 CTA）和 `DataStory`（数字层级+可扫描卡片）。案例只提供灵感，不能替代 ES 合同、许可证审查或浏览器证据。

近期 CSS Design Awards 2025 榜单中的 Exat Typeface、Dropbox Brand、The Monolith Project 等案例显示：高分作品通常把字体/品牌资产、产品叙事和交互节奏作为一个系统，而不是孤立的漂亮首屏。生成时应先提取“内容密度—网格—动作”的关系，再决定装饰强度；Chrome 的案例研究也强调滚动区域应保持聚焦、减少杂乱。

案例来源：https://www.cssdesignawards.com/blog/2025-website-of-the-year-winners/430/；https://developer.chrome.com/blog/css-ui-ecommerce-sda?hl=en

## 失败恢复

`WEB-TYPE-001` 溢出：缩短 measure、启用 `clamp` 和断点重排；`WEB-TYPE-002` 层级混乱：减少字号档位并恢复单一主焦点；`WEB-TYPE-003` 中文断行异常：补充语言 fallback 与标点规则；`WEB-TYPE-004` 装饰压过内容：移除非必要字效并保留语义顺序。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
- https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_containment/Container_queries
