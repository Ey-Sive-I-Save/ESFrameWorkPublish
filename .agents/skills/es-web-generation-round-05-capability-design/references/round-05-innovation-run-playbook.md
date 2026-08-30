# InnovationRun 执行要点

使用 `ES/Automation/ABCD/ESABCInnovationRun.psm1` 的真实状态机。Round 05 的执行输入为 TaskContext/KnowledgeRoute；外部 seed 只能作为约束或候选，`selectionAuthority` 必须为 `ABCD`。

必须保留 13 个阶段：requirement-facts、player-outcomes、lexical-deanchor、seed-divergence、tree-expansion、global-convergence、interaction-graph、adaptive-weighting、player-replay、counterplay-audit、complexity-prune、candidate-tournament、final-decision。

每个阶段都必须有非空输出；树扩展至少记录 12 轮 `iterationTrace`，每轮包含 parent、具体变化、playerAcceptability、interactionDelta、keep/discard、discardReason、当前权重和 stageUsage。每轮前重新计算权重并让实际 fan-out 介于 2–4。

预算是实际消耗，不是装饰字段：总量受 `maxModelCalls=128`、`maxEvaluations=512`、`maxBranches=256` 约束，超限立即停止并返回明确 reason code。
