# 六类机制闭环专用静态验收

## 范围

- Profile：`governance`
- Acceptance id：`mechanism-replication-closed-loop`
- 目的：验证六类机制到 ES RoutePlan/TaskContext/Knowledge/Evidence 合同的静态闭包。
- Runtime 边界：不启动 Unity、Player、网络或宿主进程；`runtime-not-run` 单独报告。

## 必须逐项覆盖

- `mechanism-coverage`：六个机制均有唯一身份、ES 投影和失败风险。
- `route-stage-chain`：四个 RouteStage 的 `requires -> produces` 链连续，失败码稳定。
- `branch-isolation`：ToT 分支使用不可变快照和独立上下文。
- `divergence-loop`：候选生成、分支隔离、剪枝和回溯均有事件记录，不共享可变状态。
- `audit-loop`：独立 auditor 绑定 EvidenceRefs，审计结论不由候选或模型自报替代。
- `iteration-loop`：轮次、预算、选择和下一轮引用可重放；CorrectionCycle 只作为内循环记录。
- `evidence-receipt-boundary`：EvidenceSet 经 Verifier，Receipt 不可变，completionDecision 与 deliveryAcceptance 分离。
- `source-drift-invalidation`：SourceRef、正文、requiredReads 或路由哈希变化会使缓存/计划失效。
- `permission-escalation`：写入、网络、Unity、外部进程和 handoff 替换在默认路径被拒绝。
- `discoverability-closure`：AIBRAIN_ENTRY、KnowledgeIndex、RouteProbe、RouteStage、Skill Catalog 和中文别名可互相追溯。
- `adaptive-learning-isolation`：train/validation/holdout 三分区按 case、snapshot 和 source group 隔离；候选谱系、Pareto/no-regression、预算、收敛和 promotion 门禁可重放。
- `external-source-lock`：固定仓库 commit、许可证边界、HTTPS host allowlist、响应大小/超时/严格 UTF-8 和内容哈希重验均闭合。
- `cross-process-cas`：跨进程 CAS 竞争、刷新版本重试、幂等单事件、原子事件发布和孤儿/损坏工件 fail-closed 均有回执。

## 裁决

全项有静态证据才能说“静态合同闭环”。`SOURCE_HASH_DRIFT`、正文哈希漂移、requiredReads 漂移或 Top3 竞争仍须按对象/字段报告；不能因新探针零 finding 或整体计数而压平。缺 Runtime 收据时交付最多为 `Implemented-Unverified`/`S2`。
