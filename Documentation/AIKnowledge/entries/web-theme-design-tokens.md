# Web 主题与设计 Token 知识

`KnowledgeId`: `es.project.web-theme-design-tokens.v1`
`Authority`: 当前 WebPageStudio 生成器与 Quality/Accessibility 验证器
`RouteKeys`: `web-theme`, `web-design-token`, `web-color`, `forced-colors`, `responsive`
`ContentHash`: `637fc259e9c17a339a38399bde9afccaf5dc6aac4fb89475987d9fb8ae7708c3`
`EvidenceLevel`: `S1`
`StaleWhen`: 主题 Token、媒体规则或验证器变化。

颜色使用语义角色；页面同时声明浅色、暗色和 forced-colors 行为，并覆盖文本、背景、边框和焦点色。仅设置 color-scheme 不算主题完成。

外部校准：MDN 说明 forced-colors 默认由用户代理调整；`forced-color-adjust` 只能为对比度需求局部 opt-out，不能阻止用户的高对比度选择，并且必须保留兼容性 fallback。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
