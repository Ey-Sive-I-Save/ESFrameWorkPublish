# AIBrain Route Contract

Route 行必须含 routeKey、state、match、KnowledgeIds、relatedSkills、mcpCapabilities、requiredEvidence、nonClaims、owner、staleWhen。选择 `ManagedAIBrain` 执行 lane 的 route 必须有 AICommand 与 TaskContract；`CurrentUserDirect` lane 只要求目标属于当前用户明确范围，不以这些受管协议为前置条件。
