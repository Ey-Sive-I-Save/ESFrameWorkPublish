# Web AI 生成、失败挖掘与修复知识

`KnowledgeId`: `es.project.web-ai-generation-repair.v1`
`Authority`: 当前 WebPageStudio Page IR、Kernel 合同与 Revision 语义
`RouteKeys`: `web-ai-generation`, `web-prompt`, `web-repair`, `web-revision`, `web-knowledge`, `prompt-engineering`, `prompt-evaluation`
`ContentHash`: `6cdc11783208c739b23b86bb21e8875b3f8c39084243c3d2a2c9b68c537b8174`
`EvidenceLevel`: `S1`
`StaleWhen`: Page IR、生成请求、编译器、RevisionPatch 或验证合同变化。

## 规则

AI 输出先约束为 WebPageIntent、DesignSpec 和 Page IR，再生成 HTML/CSS/ESM。每个 finding 必须定位到节点或合同字段，并生成带 before/after hash、幂等键和回滚点的 RevisionPatch。无法证明的设计意图、资产来源和测量必须记录 uncertainty/knownLoss，不得静默丢失。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioKernel.ps1` (`7d12b4d1fcc1c312b08b4b653f156e44dfd7a9c8bea5461533978b69b826d52c`)
