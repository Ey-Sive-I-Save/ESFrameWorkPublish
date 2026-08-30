# Web 内容、SEO 与站点发现知识

`KnowledgeId`: `es.project.web-content-seo-discovery.v1`
`Authority`: 当前 WebPageStudio 静态生成器与 Quality 合同
`RouteKeys`: `web-content`, `web-seo`, `web-sitemap`, `web-localization`, `microdata`
`ContentHash`: `3e1d53b96a8093398c56a0870aa08202ad8cb717d23a442690d466a87d5a0fdb`
`EvidenceLevel`: `S1`
`StaleWhen`: SEO 输出、locale bundle、Sitemap 或验证器变化。

canonical、Open Graph、Twitter Card、可见 Microdata、robots、Sitemap 和 hreflang 必须引用实际生成路径；外部 URL 只能来自显式 HTTPS PublicBaseUrl。

外部校准：Google 将重定向和 `rel=canonical`视为强信号，而 Sitemap 是较弱信号；Sitemap 必须 UTF-8 且使用绝对 URL，故生成器不能仅凭 Sitemap 宣称 canonical 已确定。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`21d9ba62d4b55d67a42518b1cb3cbf53b8d2f0340122e1bc0ceac1341c723d40`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
