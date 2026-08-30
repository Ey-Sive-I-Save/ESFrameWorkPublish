# Web 动态运行时与数据合同知识

`KnowledgeId`: `es.project.web-runtime-data-contract.v1`
`Authority`: 当前 Backend Contract schema、生成器与验证器
`RouteKeys`: `web-runtime`, `web-data`, `dynamic-backend-contract`, `web-security`, `web-cache`, `cache-invalidation`, `stale-while-revalidate`
`ContentHash`: `fafa2e8a3c4463257f77fb08bafc446f5025814f431d6d65a455e96d2257c2ef`
`EvidenceLevel`: `S1`
`StaleWhen`: Backend schema、动态编译器或 allowlist 变化。

动态页面从同一 Page IR 生成，运行时代码只使用同源 ESM。请求必须声明方法、Host allowlist、超时、响应大小、幂等重试、取消和脱敏规则。路由还必须声明 `prerender|cached|dynamic|client-only`、缓存键、TTL、stale-while-revalidate、失效标签与 last-known-good 回退。静态验证不能证明真实网络执行或多节点缓存一致性。

外部校准：Nuxt Route Rules 和 Next Cache Components 都把静态、缓存和动态边界细化到路由/组件级；ES 只吸收该分层思想，不复制框架运行时或放宽网络授权。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
- `ES/Automation/Contracts/es-web-cache-policy-v1.schema.json` (`69adcf6c6bd706001d3fb3a1ff510949c32521877ccb363159feb5b91d62cf81`)
- `ES/Automation/WebPageStudio/New-ESWebCachePolicy.ps1` (`afe53ace80cc1728691df83b2b81f2d00122386290ed3ec633a8fc995a5c012e`)
- `ES/Automation/WebPageStudio/Test-ESWebCachePolicy.ps1` (`4e6b7040380b0e70978f1d9427bce8e7b0a824ebb28d3be38855b09882bcdc74`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioLocalAdapter.ps1` (`2b8e1ba9a718ad0db14ae6aa9ab7049a0073911fbd3350aa1659e04b709fc4bd`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioLocalAdapter.ps1` (`36ec0f8d6cbf22378349a0ee6eb2b4ca705e4be6daa8c63992d792062f142bd5`)
- `ES/Automation/WebPageStudio/Test-ESWebNetworkRuntimeReceipt.ps1` (`dd5fc404b504ea88aa7295beb000155cc41d305b8f6324642d5357d93fd1bce0`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudio.ps1` (`b28deef00f7da00e4637e581d07fb1576e7ad067062e6f5f4db1f7ec62a49e63`)
- `ES/Automation/WebPageStudio/fixtures/local-adapter-response.json` (`ac29ab0887064523a9b4975a6e3a31a5bb223ba4db23c96606bd024ff4cbeb03`)
