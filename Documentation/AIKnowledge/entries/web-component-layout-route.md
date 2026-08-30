# Web 组件、布局与路由知识

`KnowledgeId`: `es.project.web-component-layout-route.v1`
`Authority`: 当前 WebPageStudio Kernel 与 Quality 合同
`RouteKeys`: `web-component`, `web-layout`, `web-route`, `web-page-generation`, `responsive`
`ContentHash`: `26aa765675ae87656e63444894a2208f4e97c65a7bef60c14ee9bba173ae755f`
`EvidenceLevel`: `S1`
`StaleWhen`: Kernel schema、编译器、Quality 验证器或页面 IR 变化。

页面由稳定 nodeId/parentId 组成的 Page IR 生成；布局、组件和路由需可追溯到输入规格。响应式变化通过 profile 与 Token 表达，静态路由闭合到输出白名单，动态路由只产生合同。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)