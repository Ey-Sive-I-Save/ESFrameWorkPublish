# Web 官方资料校准快照（2026-08-29）

本文件是一次受控、官方来源限定的项目内快照。内容为工程决策摘要，不复制长段落；原始页面、抓取日期和许可证说明保留用于复核。

## CSS 响应式与可访问配色

- MDN Container Queries：组件可以依据自身容器尺寸而非视口布局；媒体查询仍适合用户偏好（颜色方案、减少动效），因此 WebPageStudio 应将 container profile 与 preference media 分开建模。
- MDN `forced-color-adjust`：默认由用户代理调整强制配色；只有为满足对比度等需求时才允许局部 opt-out，不能用来阻止用户选择。该能力存在浏览器兼容性差异，生成器必须保留 fallback。

## 可访问性

- W3C WAI WCAG Overview：当前推荐使用 WCAG 2.2；规范文本是规范性依据，Technique/Understanding/ACT 资料是支持性依据。项目静态 Accessibility 检查不能替代 ACT 或辅助技术实测。

## 搜索发现

- Google Search Central canonical：重定向和 `rel=canonical` 是强信号，Sitemap 是较弱信号；HTTPS 和互惠 `hreflang` 会影响 canonical 选择。
- Google Sitemap：Sitemap 必须 UTF-8，URL 使用绝对地址；大型站点需拆分或使用 sitemap index。生成器应验证 PublicBaseUrl、路径闭合和编码。

## 框架机制校准

- Nuxt Rendering：Route Rules 可按路径选择 prerender、SWR、ISR、SSR 或 CSR，说明静态/动态不应是全站二选一，而应进入路由级策略。
- Next.js Cache Components：静态 shell 可以与缓存或动态片段组合；需要请求、网络或系统 API 的组件必须显式包裹动态边界，不能隐式混入静态输出。
- Vite Build/Assets：多 HTML 入口可产出多页静态站点；`base` 会影响资源路径；动态 `import.meta.url` 资产只有在可静态分析时才安全，SSR 语义不同。

## 对 ES WebPageStudio 的创新投影

1. 在 Page IR 增加 `responsiveBasis: viewport|container`，组件优先 container，页面级偏好仍走 media query。
2. 将 `renderPolicy` 细化为 route-level `prerender|cached|dynamic|client-only`，但必须映射回现有 static/dynamic 合同，不直接复制框架运行时。
3. 将 `canonicalEvidence` 分成 redirect、link、sitemap、hreflang 四类信号，避免把 Sitemap 单独当作强 canonical 证明。
4. 将 `accessibilityEvidence` 区分规范性 WCAG、自动 ACT、人工辅助技术和浏览器运行证据。
5. 将资源 URL 处理加入 `basePath` 和静态可分析性检查，阻止构建后路径漂移。

## 来源与许可证

- https://developer.mozilla.org/en-US/blog/getting-started-with-css-container-queries/ （MDN，2026-08-29，页面代码/内容按 MDN 站点许可）
- https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/forced-color-adjust （MDN，2026-08-29）
- https://www.w3.org/WAI/standards-guidelines/wcag/ （W3C WAI，2026-08-29）
- https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls （Google Search Central，2026-08-29，CC BY 4.0/代码 Apache 2.0）
- https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap （Google Search Central，2026-08-29）
- https://nuxt.com/docs/4.x/guide/concepts/rendering （Nuxt，2026-08-29）
- https://nextjs.org/docs/app/getting-started/partial-prerendering （Next.js，2026-08-29）
- https://vite.dev/guide/build.html 与 https://vite.dev/guide/assets.html （Vite，2026-08-29）

本快照不证明浏览器、网络、PWA、性能或生产部署行为；外部资料只用于校准设计和合同边界。
