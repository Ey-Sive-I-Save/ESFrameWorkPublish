# 多步状态与能力闭环

`state-machine.binding.json` 是本 Skill 的状态投影合同；实际生命周期仍由 `TaskContextRuntime` 负责，ABCD `InnovationRun` 负责验证阶段和最终收敛。Skill 不创建第二个任务状态机。

状态顺序为：`intake → authority-locked → snapshot-frozen → decomposed → fanout-running → evidence-joined → adversarial-reviewed → converged → final-decision → closed`。

每次转移必须有非空输出和可重读 receipt。来源哈希漂移回到 `snapshot-frozen`；子 Agent 冲突回到 `authority-locked` 重新规划；取消或迟到结果必须隔离；缺少任一结构/实现/表现/性能必需 receipt 时只能降级。

能力闭环：A Intent 冻结目标，ABCC 协商 B 能力，TaskContext 绑定身份和 CAS，子 Agent 产生 EvidenceReceipt，ABCD 做分支评估、恢复和收敛，最终由 `ABCD-final-decision` 输出分层结论。
