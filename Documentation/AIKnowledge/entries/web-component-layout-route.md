# Web 组件、布局与路由知识

`KnowledgeId`: `es.project.web-component-layout-route.v1`
`Authority`: 当前 WebPageStudio Kernel 与 Quality 合同
`RouteKeys`: `web-component`, `web-layout`, `web-route`, `web-page-generation`, `responsive`
`ContentHash`: `0da6afdd9ef1d6768ba21ca7c7a5c11a7656117d6b263c85e7bb9805bfbd04f0`
`EvidenceLevel`: `S1`
`StaleWhen`: Kernel schema、编译器、Quality 验证器或页面 IR 变化。

页面由稳定 nodeId/parentId 组成的 Page IR 生成；布局、组件和路由需可追溯到输入规格。响应式变化通过 profile 与 Token 表达，静态路由闭合到输出白名单，动态路由只产生合同。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`2ac243a6670aea28409412228375614720327a158139a351211b985e9ca70650`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)