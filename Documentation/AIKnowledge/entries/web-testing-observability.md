# Web 测试、观测与视觉证据知识

`KnowledgeId`: `es.project.web-testing-observability.v1`
`Authority`: 当前 WebPageStudio Quality、Accessibility、Contract 验证器与接受矩阵
`RouteKeys`: `web-testing`, `web-observability`, `web-visual-regression`, `web-evidence`, `web-quality`
`ContentHash`: `fe0de56438b8f3c38247482b31745fbfd028de7c409ce6f9742a79841ca9a9d2`
`EvidenceLevel`: `S1`
`StaleWhen`: 任一网页验证器、验收矩阵、RunRecord 或输出合同变化。

## 规则

测试分为 Contract、Quality、Accessibility、Freshness、视觉回归和运行时 E2E 六层。每层必须绑定输入、输出和哈希；单张截图、文件存在或静态脚本检查不能替代浏览器执行、像素回归或生产性能证据。失败结果保留为可重放 finding，不得压平为通过。

## SourceRefs

- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioAccessibility.ps1` (`6a63553015e05a7e45aecb4ffa3ea73ce7e506948da8ae77519c58b8ec3aa93b`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioContract.ps1` (`e5d277c8353fb7073edbee53ae15f7503516264746b8f2e8a417dcc749ffa587`)
