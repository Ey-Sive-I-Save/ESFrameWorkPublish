# Web 可访问性、性能与证据知识

`KnowledgeId`: `es.project.web-accessibility-performance-evidence.v1`
`Authority`: 当前 Accessibility/Quality 验证器与接受矩阵
`RouteKeys`: `web-accessibility`, `web-performance`, `web-evidence`, `web-responsive`
`ContentHash`: `3bca732f5eb65a9ab4b6dc92f5d2502e84901d149ac19b01a8a33c4c30d9f3ba`
`EvidenceLevel`: `S1`
`StaleWhen`: Accessibility、Quality、性能预算或接受矩阵变化。

静态验收覆盖语义结构、焦点、键盘顺序、对比度、响应式和 reduced-motion。文件存在、静态脚本检查或单张截图不能证明浏览器性能与视觉质量。

外部校准：W3C WAI 当前推荐 WCAG 2.2；规范性成功标准与支持性 Technique/ACT 资料必须分层记录，不能把自动检查结果当作辅助技术实测。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioPreview.ps1` (`b66baf4ad8e6e33403263e4f7fc95dcc09c5559cc76ef19451fb9cecb0686012`)
- `ES/Automation/WebPageStudio/Compare-ESWebVisualPixels.ps1` (`a27fe0f7fb77b0d2edacd6ff6a371b20043b3e2376e1c8b82754a600b4d59aa6`)
- `ES/Automation/WebPageStudio/Invoke-ESWebVisualMatrix.ps1` (`1112396eb2660719a3421189ad600e1b96e9f1b69e77fa177fda4d76abb1d0256`)
- `ES/Automation/WebPageStudio/Measure-ESWebPageStudioPerformance.ps1` (`91d17870252bb1eb8859303dde52fdaa5a48362503152517cb00637bd025437c`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPerformanceBaseline.ps1` (`97810ba6eb84ba30dcab7e3621aa51136c95d75c67cf20d866479ce3c371c18e`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioStaticSignals.ps1` (`73b59b654e3d05af6d1f1a3efc07cdf1c2be357446798358c9886faebe1c5b64`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudio.ps1` (`b28deef00f7da00e4637e581d07fb1576e7ad067062e6f5f4db1f7ec62a49e63`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioStagingReadiness.ps1` (`3c673c06f8b1504c6bbef574ca5ddced74837becea25b7d5ea9ba313837f447f`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioArtifactIntegrity.ps1` (`a2e5c4e382ce0af70b6552e3eaeb1d2cdb3f3b1727f36ec3f68467418e4235d3`)
- `ES/Automation/WebPageStudio/performance-budget.yaml` (`755fee0cbd8e5cab80fd6de4da67a5631a0cd04b77f75af623dd4b9bba4bacbe`)
- `ES/Automation/WebPageStudio/ui-validation-matrix.yaml` (`20f6ae15dc23d0ee9bb17a29334a2b0124dbdeb462ddb19bc7ea9a8a785bf94b`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
