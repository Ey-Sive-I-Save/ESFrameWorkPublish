# Skill Resource Index Contract

状态：现行组合规范。权威索引：`.agents/SKILL_RESOURCE_INDEX.yaml`。

## Composition

每个正式 Skill 必须通过 Resource Index 声明 `routeKeys`、`references`、`scripts`、`mcp` 和证据案例。`SKILL.md` 只保留触发、决策和主流程；稳定事实、项目路径、脚本合同和 MCP 能力说明分层放入资源文件。

## Authority

Resource Index 是导航投影，不拥有源码事实或权限。AIWarnings 是长期规则，AICommand 是单次权限合同，AIBrain 负责 route/plan/PlanHash，MCP 只提供已鉴权的宿主能力。

## Staleness and acceptance

Skill、治理元数据、AICommand、Knowledge、MCP 能力或引用路径改变后，必须重新计算哈希并重新规划。缺少引用、脚本合同、MCP 能力状态或必需证据时，Skill 必须阻断而不是自动降级。
