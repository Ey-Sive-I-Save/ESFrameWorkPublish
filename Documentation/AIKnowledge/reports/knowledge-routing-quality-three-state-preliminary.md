# Knowledge Routing Quality 三态初步实验

状态：`single-model-isolated-preliminary`

本报告记录一次三态决策实验的可复用观察，不把本次结果升级为 `Three-State-Experiment-Validated`，
也不证明 Unity、Runtime、网络或发布行为。

## 目标

- KnowledgeId：`es.knowledge.routing-quality.v1`
- 条目静态状态：`static-passed`
- runtimeStatus：`runtime-not-run`
- 场景：SourceRef 可能漂移，同时路由探针出现零命中或误命中；AI 需要决定是否继续使用旧 route-pack，
  或停止并重新规划。

## 条件

- A：项目源码、配置、测试和合同上下文；禁止 Knowledge、AIWarnings、Skill 和前组答案。
- B：A 的可读范围加目标 Knowledge 与声明的 RequiredReads。
- C：B 加当前版本匹配的本地一手验证合同、验证器和稳定刷新来源；本轮没有联网。

三组由同一模型体系的三个新隔离子代理生成，使用同一任务和 JSON 输出结构；协调评分者在结果冻结后
进行比较。因此本报告是单模型隔离初步结果，不是多模型独立盲评。

## 可复用观察

1. A 能给出通用的哈希、停止、回滚、幂等和并发安全建议，但不会自然产生 ESFramework 的
   `NoKnowledgeRoute`、canonical 1～3、Top-3、planHash/CAS 等项目动作边界。
2. B 的新增证据改变了正确动作：零命中必须报告 `NoKnowledgeRoute`，误命中必须停用旧 route-pack，
   SourceRef 漂移必须 stale 并重新规划，稳定刷新必须在 apply-time 再校验来源。
3. C 的主要增益是验证与修复分离、StaleWhen、写入授权、整批回滚和静态/Runtime 证据边界；
   它没有证明网络搜索增益，因为本轮未联网。

## 门禁状态

```text
IsolationCheck: true (three fresh contexts; single-model)
RubricFreeze: true
BlindScoring: false (coordinator knew condition labels)
NegativeCaseParity: unknown (single bounded scenario)
EvidenceBindings: partial (A is project-context baseline; B/C bind project paths)
CounterfactualResult: not-run
TelemetryCompleteness: false
ReadCost: uncalculated
ReadEfficiency: uncalculated
Total: uncalculated
RuntimeStatus: runtime-not-run
```

## 结论边界

本轮足以支持“目标 Knowledge 在该场景中改变了项目可执行决策”的初步观察；不足以支持正式三态验收、
网络增益、成本收益或 Runtime 成功声明。后续正式实验必须补齐隐藏负例、反事实对照、匿名盲评和成本遥测。
