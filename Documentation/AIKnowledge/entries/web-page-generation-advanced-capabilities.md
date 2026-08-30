# ES WebPageStudio 高级网页生成能力与稳定性路由

`KnowledgeId`: `es.project.web-page-generation-advanced-capabilities.v1`
`Authority`: Current WebPageStudio source, contracts, validators and acceptance matrix
`RouteKeys`: `web-page-generation`, `webpagestudio`, `static-html-css`, `dynamic-backend-contract`, `responsive`, `theme`, `forced-colors`, `seo`, `microdata`, `pwa`, `localization`, `freshness`, `evidence-boundary`
`ContentHash`: `9abb149ba5bcd75f2993991f82a71e634a7035685cf37ab41bb0afca3de0c16f`
`EvidenceLevel`: `S1`
`StaleWhen`: WebPageStudio generator, request/generation/backend schema, validator behavior, acceptance matrix, or any SourceRef hash changes.

## 目的与边界

当前唯一工件格式为 `html-css-esm`，由同一 Page IR 选择 `renderMode=static|dynamic`。静态模式输出 HTML/CSS；动态模式只挂载同源 runtime ESM。静态合同、Quality、Accessibility 和 Freshness 证据不能证明浏览器、网络、Unity 或生产部署运行。

## 已验证项目事实

- 静态生成包含响应式 profile、loading/empty/error 状态、dark/light Token、reduced-motion 与 forced-colors。
- SEO 输出包括 canonical、Open Graph、Twitter Card、Microdata、Manifest、robots、Sitemap 和 hreflang。
- 动态 Backend Contract 约束只读方法、Host allowlist、超时/响应大小预算、重试、取消和脱敏。
- 内核 schema、请求编译器、生成器与验证器保证格式、主题、脚本策略、输出路径和哈希闭合。

## 生成规则

默认网络关闭并标记 `runtime-not-run`；需要动态数据时先生成 Backend Contract，再由用户单独授权 adapter。来源或生成器哈希漂移时旧 Freshness 证据 stale，必须重新生成。

## SourceRefs

- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioStatic.ps1` (`5ffed0b5d13734e27db46a3fc5457a6909fd2c0235c1003dce34200de3c47000`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioAccessibility.ps1` (`6a63553015e05a7e45aecb4ffa3ea73ce7e506948da8ae77519c58b8ec3aa93b`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioContract.ps1` (`e5d277c8353fb7073edbee53ae15f7503516264746b8f2e8a417dcc749ffa587`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`2ac243a6670aea28409412228375614720327a158139a351211b985e9ca70650`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioKernel.ps1` (`7d12b4d1fcc1c312b08b4b653f156e44dfd7a9c8bea5461533978b69b826d52c`)