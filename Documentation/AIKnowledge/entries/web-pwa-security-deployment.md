# Web PWA、安全与部署边界知识

`KnowledgeId`: `es.project.web-pwa-security-deployment.v1`
`Authority`: 当前 WebPageStudio 输出合同与 Backend/Kernel 边界
`RouteKeys`: `web-pwa`, `web-offline`, `web-security`, `web-deployment`, `csp`
`ContentHash`: `74486c6530399b1b45ebb2ad7b8eecc0ae3cb63ff4f621a6b32a1a8b7538e42d`
`EvidenceLevel`: `S1`
`StaleWhen`: Manifest、offline package、CSP、部署合同或验证器变化。

Manifest、离线包和 Service Worker 必须拥有版本化缓存身份、明确更新和回退策略。CSP、脚本来源、同源请求和 Host allowlist 必须显式声明。静态产物不代表可安装 PWA 或生产部署成功。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`0a958512aa4ead4dfcfb1b70ca19b62f12433862f6830bee0eef089a6475838b`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
