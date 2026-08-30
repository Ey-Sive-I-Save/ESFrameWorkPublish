# 多子 Agent 内部编排绑定

本 Skill 采用 task-scoped fan-out/fan-in。四个必需分支是 `structure`、`implementation`、`presentation`、`performance`；可追加 `adversarial` 与 `evidence-audit`，但不能替代必需分支。

每个分支必须返回 EvidenceReceipt，至少包含 taskId、agentId、layer、entryPoint、expected、observed、status、sourceHash、runtimeStatus 和 claimsNotProven。自然语言摘要不能作为汇聚输入。

ABCC 负责能力协商、权限与证据归一化；ABCD Dynamic 负责状态迁移、分支评估、冲突检测、失败恢复和 final-decision。同一字段出现不同 observed 时进入 `replan`，不得多数投票压平冲突。缺少必需 receipt 只能输出 `partial` 或 `unverifiable`。

静态回放只证明合同与确定性编排。Unity、PlayMode、Profiler、Player、网络和发布结论必须由显式授权的独立运行 Agent 产生新鲜回执。
