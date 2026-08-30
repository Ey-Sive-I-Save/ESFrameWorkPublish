# ABC 生成链资源审计索引

本目录只保存协作索引，不复制 Automation 合同、源码或验证回执正文。

## 权威来源

- 生成模式合同：`ES/Automation/Contracts/es-ai-abc-generation-mode-v1.json`
- 生成响应合同：`ES/Automation/Contracts/es-ai-abc-generation-response-v1.schema.json`
- Provider 适配器：`Assets/Plugins/ES/Editor/ESAutomation/ESABCModelProviderAdapter.cs`
- 发散与候选选择：`ES/Automation/ABCD/ESABCDDivergence.psm1`
- 补丁计划：`ES/Automation/ABCD/ESABCDPatchPlanning.psm1`
- TaskContext 生命周期：`ES/Automation/TaskContextRuntime/`

## 当前策略

- 创新候选先返回轻量方案；只有排序选中的候选才允许物化 `proposedChanges`。
- 整体回执、候选补丁总字符数和总文件数受合同预算约束。
- 所有候选保持 `candidate-only`，最终裁决仍由 ABCD 高危验收和 TaskContext 证据链完成。
- 本索引不代表 Unity、Runtime、Profiler 或发布验收已完成。

## 归属说明

设计正文、候选包、PatchPlan 和机器回执继续保留在各自 Automation/AIKnowledge 权威目录；
AISpace 只提供跨 Agent 可发现入口，避免形成第二份事实源。
