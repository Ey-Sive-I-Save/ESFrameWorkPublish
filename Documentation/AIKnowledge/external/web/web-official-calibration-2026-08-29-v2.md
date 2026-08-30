# Web 官方资料校准快照 v2（2026-08-29）

本快照在同日复核官方页面后建立，取代 v1 作为当前推荐校准集。仅记录可验证的稳定信号和设计影响，不声称全网绝对最佳。

## 当前稳定基线

- W3C WAI 推荐使用 WCAG 2.2；规范性成功标准与 ACT/Technique 支持资料分层，项目自动检查不能替代辅助技术实测。
- web.dev 当前 Core Web Vitals 为 LCP、INP、CLS，建议目标分别为 2.5 秒、200 毫秒、0.1，按移动/桌面第 75 百分位评估；指标集合可能演进，因此必须记录快照日期。
- MDN CSS Containment 将 container queries 定义为基于特定容器尺寸/样式的查询；媒体查询继续负责颜色方案、减少动效等用户偏好。
- MDN `forced-color-adjust` 默认尊重用户代理强制配色，局部 `none` 只能用于改善对比度，并存在兼容性差异。
- WAI-ARIA Authoring Practices（APG）要求自定义组件同时定义语义、状态和键盘行为；优先使用原生 HTML 控件，只有在语义不足时才增加 ARIA。
- MDN PWA 缓存指南将 stale-while-revalidate 定义为“先返回缓存、再用网络响应刷新”，生成器必须显式区分缓存命中、重新验证和离线回退三种状态。

## 搜索与发布

- Google Search Central 将重定向和 `rel=canonical` 作为强信号，Sitemap 为较弱建议信号；HTTPS 与互惠 hreflang 会影响 canonical 选择。
- Google Sitemap 要求 UTF-8 和绝对 URL；大型站点需要拆分 Sitemap 或使用 sitemap index。

## 框架机制对比

- Nuxt 4 Route Rules 可按路径选择 prerender、SWR、ISR、SSR/CSR 与缓存策略，体现 route-level rendering policy。
- Next.js 16 文档中的 Cache Components（`cacheComponents`、`use cache`、`cacheLife`）允许静态 shell 与缓存/动态片段组合；静态导出不等价于该运行时能力。
- Vite 当前构建支持多 HTML 入口、`base` 公共路径和资源哈希；动态 `import.meta.url` 必须可静态分析，SSR 语义不同。

## 对 ES WebPageStudio 的采用规则

1. Page IR 默认 `responsiveBasis=container`，viewport/media query 仅处理页面级断点和用户偏好。
2. `renderPolicy` 可表达 `prerender|cached|dynamic|client-only`，但仍映射到 ES 的 static/dynamic 合同，不能直接复制框架 runtime。
3. PerformanceEvidence 记录 LCP/INP/CLS 目标、设备分组、百分位和采集日期；静态检查只证明预算声明。
4. CanonicalEvidence 分离 redirect、link、sitemap、hreflang 信号强度。
5. AssetContract 记录 base path、静态可分析性和 SSR 限制。
6. AccessibilityEvidence 分离 WCAG 规范、ACT 自动规则、人工辅助技术和浏览器运行证据。
7. InteractionContract 为每个自定义控件记录 role/state、焦点顺序、Enter/Space/Escape/Arrow 键行为和不可用状态；未记录时只能标为 review。
8. CacheEvidence 记录 cache key、版本/ETag、命中后 revalidate、网络失败回退和失效原因；不得把“生成了 Service Worker”当作缓存正确性的证明。

## 官方来源

- https://developer.mozilla.org/en-US/docs/Web/CSS/Guides/Containment
- https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/forced-color-adjust
- https://www.w3.org/WAI/standards-guidelines/wcag/
- https://web.dev/articles/vitals
- https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls
- https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap
- https://nuxt.com/docs/4.x/guide/concepts/rendering
- https://nextjs.org/docs/app/api-reference/config/next-config-js/cacheComponents
- https://nextjs.org/docs/app/api-reference/directives/use-cache
- https://vite.dev/guide/build.html
- https://vite.dev/guide/assets.html
- https://www.w3.org/WAI/ARIA/apg/
- https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Caching

许可证：各来源按其官方站点许可使用；本文件为事实摘要，不再分发原文或第三方代码。
证据边界：本快照不证明浏览器、网络、PWA、性能或生产部署行为。
