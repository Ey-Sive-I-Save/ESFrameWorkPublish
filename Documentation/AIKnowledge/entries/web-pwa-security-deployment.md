# Web PWA、安全与部署边界知识

`KnowledgeId`: `es.project.web-pwa-security-deployment.v1`
`Authority`: 当前 WebPageStudio 输出合同与 Backend/Kernel 边界
`RouteKeys`: `web-pwa`, `web-offline`, `web-security`, `web-deployment`, `csp`
`ContentHash`: `637fc259e9c17a339a38399bde9afccaf5dc6aac4fb89475987d9fb8ae7708c3`
`EvidenceLevel`: `S1`
`StaleWhen`: Manifest、offline package、CSP、部署合同或验证器变化。

Manifest、离线包和 Service Worker 必须拥有版本化缓存身份、明确更新和回退策略。CSP、脚本来源、同源请求和 Host allowlist 必须显式声明。静态产物不代表可安装 PWA 或生产部署成功。

外部校准：Vite 的多页入口、base path 和静态资源分析规则说明构建输出必须绑定 public base；动态 `import.meta.url` 只有在可静态分析时才可靠，部署适配器应显式记录该限制。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
