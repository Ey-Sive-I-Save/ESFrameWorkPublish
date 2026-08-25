# AIWarnings 文件级领域清单

`KnowledgeId`: `es.aiwarnings.domain-inventory.v1`
`Authority`: `Derived from AIWarnings`
`EvidenceLevel`: `S1`
`RouteKeys`: `aiwarnings`, `inventory`, `p0`, `architecture`, `runtime`, `editor`, `validation`, `handover`, `archive`
`ContentHash`: `acb2c9b728e28722ad67aeb3c80b3d999da9d3099cd0f85b56a7ebc67b80a4f8`
`StaleWhen`: AIWarnings 文件数量、目录边界、RuleIndex、生成脚本或任一 SourceRef 哈希变化。

## 用途

本条目是机器生成领域清单的可路由包装层。`AIWarningsDomainInventory.yaml` 保存领域级计数与 routeKeys，`AIWarningsGeneratedInventory.json` 保存文件级投影；两者都是派生导航，不替代 AIWarnings Start 链、P0 原文或当前源码。

使用时先读取 Start 链和 `aiwarnings-domain-map.md`，再按任务只选择对应领域。禁止为了建立上下文递归加载完整文件清单。

## SourceRefs

- `Documentation/AIKnowledge/AIWarningsDomainInventory.yaml` (`58b51a023cb972d03a77a4bf624b262ac050e9ce1b2bbe93e3b741587277ab67`)
- `Documentation/AIKnowledge/AIWarningsGeneratedInventory.json` (`f752f7c77af205e52697b4db38eb6f21726ed1eb9f82763bd8037522263519b3`)
- `Documentation/AIKnowledge/entries/aiwarnings-domain-map.md` (`89e79b682af9c820e6e53fba2f8fd7f8cc1a5d9fe4524a1c764a9b3af126c3e5`)
- `.agents/skills/es-ai-knowledge-curation/scripts/Build-ESAIWarningsInventory.ps1` (`ee78fa7f584f25447d4e072b9a750ca22efb446d1817aae9954be8167058d6f8`)
- `.agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIKnowledgeDiscovery.ps1` (`79439435e5f666ba54745c1bdefdce83a5bf8c8e3e1cba4a2c1ee42fc27a319d`)

## EvidenceRefs

- `.agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIKnowledgeDiscovery.ps1`
- `runtime-not-run`
