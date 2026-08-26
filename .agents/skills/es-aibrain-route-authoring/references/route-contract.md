# AIBrain Route Contract

Route 行必须含 routeKey、state、match、KnowledgeIds、relatedSkills、mcpCapabilities、requiredEvidence、nonClaims、owner、staleWhen。选择 `ManagedAIBrain` 执行 lane 的 route 必须有 AICommand 与 TaskContract；`CurrentUserDirect` lane 只要求目标属于当前用户明确范围，不以这些受管协议为前置条件。

## RoutePlan V1

`ES/Automation/Contracts/es-route-plan-v1.schema.json` 是组合路由输出合同，`es-route-stage.registry.json` 是阶段依赖与深度授权的中央权威。每个阶段必须由精确 `skillName + profile + routeKey` 唯一解析，并声明 `requires`、`produces`、`failureConditions`。阶段按依赖图确定性排序；core 深度为 0，默认扩展深度为 1，深度 2 还必须命中注册的定向 reason code。循环、缺失输入、重复产物、未注册阶段和无授权深度只阻断当前 RoutePlan Profile。

RoutePlan 必须绑定冻结 GoalRevision、规范化且按 ordinal 排序的精确 routeKeys、当前 Git HEAD、规范化 SourceRefs 集合、SourceRefs Hash 和 Registry Hash。`ES/Automation/RoutePlan/ESRoutePlanContract.psm1` 是 PowerShell canonical hash、工件重读、SourceRef、Registry 和深度关系的唯一实现；TaskContext、验证器和夹具不得复制字段序列。`executionEnabled` 固定为 `false`；`planTask` 只返回计划，不执行阶段。V1 以兼容投影附加到现有 `ESAIBrainPlan`，不得改变旧 `Ready/NoMatchingSkill/NoKnowledgeRoute/...` 状态，也不得宣称生产路由或全局 P0 已接入。

真实 `planTask` 构建路径只对 `profile=governance + scope=task-object` 生成 `shadowIntegration` 候选。它使用冻结 GoalRevision、路由三轴、阶段、问题和 snapshot 推导 `decisionHash/decisionId`，并记录 legacy status 的 before/after、状态未改变、回滚动作、生产路由未接管和全局 P0 未接入。C# 生产者不得自报 `matched` 或 `no-bypass`；PowerShell 工件消费者独立重算后才在验证结果中签出 `shadowDecisionIdMatched=true`、`shadowBypassDetected=false` 和 rollback 状态。其他 Profile 返回 `not-selected` 且不生成 decisionId。ID mismatch、legacy status 漂移、scope/Profile 扩大、rollback 不可用或 takeover 标志会拒绝当前 RoutePlan 工件，但不会扩大成项目级阻断。
