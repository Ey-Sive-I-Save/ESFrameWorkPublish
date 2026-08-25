# Skill Resource Index Contract

状态：现行组合规范。权威索引：`.agents/SKILL_RESOURCE_INDEX.yaml`。

## Composition

每个正式 Skill 必须通过 Resource Index 声明 `routeKeys`、`references`、`scripts`、`mcp` 和证据案例。`SKILL.md` 只保留触发、决策和主流程；稳定事实、项目路径、脚本合同和 MCP 能力说明分层放入资源文件。

## Authority

Resource Index 是导航投影，不拥有源码事实或权限。当前用户明确指令提供项目动作授权；AIWarnings 是长期规则，AICommand 与 AIBrain route/plan/PlanHash 是受管通道合同，MCP 只提供宿主能力。

## Staleness and acceptance

Skill、治理元数据、AICommand、Knowledge、MCP 能力或引用路径改变后，必须使相关缓存和受管计划 stale。缺少引用、脚本合同、MCP 能力状态或必需证据时，只阻断依赖它的能力/证据结论；不得撤销当前用户对直接项目工作的授权。

`.agents/SKILL_REGISTRY.manifest.json` 必须在 `metadata` 中记录当前 `.agents/SKILL_RESOURCE_INDEX.yaml` 的 SHA-256；`Test-ESSkillArchitecture.ps1` 重新计算并比较该值。Resource Index 只保存 Manifest 的项目相对路径，不反向嵌入 Manifest 哈希，因此不存在自引用哈希循环。任一侧变化后都必须重建 Manifest，旧受管计划随即 stale。
