# Web 动态运行时与数据合同知识

`KnowledgeId`: `es.project.web-runtime-data-contract.v1`
`Authority`: 当前 Backend Contract schema、生成器与验证器
`RouteKeys`: `web-runtime`, `web-data`, `dynamic-backend-contract`, `web-security`
`ContentHash`: `74486c6530399b1b45ebb2ad7b8eecc0ae3cb63ff4f621a6b32a1a8b7538e42d`
`EvidenceLevel`: `S1`
`StaleWhen`: Backend schema、动态编译器或 allowlist 变化。

动态页面从同一 Page IR 生成，运行时代码只使用同源 ESM。请求必须声明方法、Host allowlist、超时、响应大小、幂等重试、取消和脱敏规则。静态验证不能证明真实网络执行。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`0a958512aa4ead4dfcfb1b70ca19b62f12433862f6830bee0eef089a6475838b`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
