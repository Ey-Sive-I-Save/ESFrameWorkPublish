# 测试场景、发布验收与证据等级

`KnowledgeId`: `es.project.scene-release-evidence.v1`  
`Authority`: `Source + AIWarnings + Skill contract`  
`RouteKeys`: `scene-validation`, `scene-guide`, `acceptance`, `release`, `evidence`, `receipt`, `profiler`, `unity`
`ContentHash`: `2f727bdfc9eca9625e0810da520bd4e32f3e6077710db3c63e6bfe3110d05f8d`

## Scope

本条目只负责 Scene Guide、验收层级、证据回执，以及 PlayMode、Profiler、Player 和发布结论的升级边界。它不负责 Builder 源码权威、Prefab Override、Fixture 布局或备份分层；这些事实由 `es.unity.editor.project-scene-builder-authority.v1` 持有。UI Fixture 与 ScreenSpec 由 `es.project.ui-automation-authoring.v1` 持有。

## Trigger and routing

- 自然语言触发：场景验收、Guide 检查、PlayMode 验证、Profiler 证据、Player 验收、发布回执。
- 精确路由：`scene-validation`、`scene-guide`、`acceptance`、`release`、`evidence`、`receipt`、`profiler`、`unity`。
- 预期最小命中：本条目；涉及 Builder/Override 时追加 Scene Builder，涉及 UI Fixture 时追加 UI Automation，总量不得超过 3。
- 邻近误路由：只有 `builder`、`fixture` 或 `backup` 时回退到 Scene Builder。只有通用 `unity` 或 `evidence` 时必须先补充场景上下文，不能直接宣称发布验收。

## Decision rules

1. 校验本条目、SourceRefs、requiredReads 和证据输入的新鲜度；任一漂移即标记 `stale` 并重新规划。
2. 先声明目标 acceptance level，再读取 evidence matrix 中该层的 required checks；没有明确层级时停止并请求验收范围。
3. 只有当前回执包含入口、环境、退出状态、产物、失败项和对应哈希，且 required checks 全部通过、无 blocker，才能输出该层“通过”。
4. 写 Scene、运行 Unity、Profiler、Player 或发布前，当前用户必须明确点名对应目标或动作；一旦点名即可在该范围直接实施。仅选用受管通道时才要求匹配 AICommand、AIBrain 计划和 TaskContract，缺失只阻断该通道。
5. 证据入口不可用但静态来源仍有效时标记 `Deferred/runtime-not-run`，不得降级为“基本通过”。

## Verified facts

- 当前源码定义 `ESSceneValidationGuide` 的显式配置与检查入口；这是源码事实，不是场景已运行事实。
- AIWarnings 规定 Guide 与场景验收的职责边界；这是 P0 规则，不是 PlayMode 回执。
- `es-release-acceptance` 及其 evidence matrix/receipt contract 定义验收层级与回执字段；Skill 合同不证明任何一次执行已经发生。
- 静态文件和哈希只能证明被读取版本；编译、EditMode、PlayMode、Profiler、Player 与发布必须分别由对应当前运行证据证明。

## Common AI failure modes

| 错误行为 | 症状与根因 | 预防与替代动作 | 恢复和缺失证据 |
|---|---|---|---|
| 把 Guide 全绿写成项目可发布 | 将局部运行状态扩大为全局结论 | 将结论限定到本次场景和已配置检查，再按 evidence matrix 升级 | 撤回发布结论；补 Player/发布回执 |
| 把测试源码或按钮存在写成已执行 | 把静态存在性当运行事件 | 要求当前 RunRecord、退出状态和产物哈希 | 标记 `runtime-not-run`；执行受权入口 |
| 用 Editor 结果替代 Player/Profiler | 混淆验收层级 | 每层只接受该层规定的证据 | 降回最后可证明层；补目标平台证据 |
| receipt 缺字段仍判通过 | 证据无法复现或绑定错误输入 | 验证 receipt contract、PlanHash 和输入哈希 | 判 `Blocked`；生成新计划和新回执 |
| 场景生成覆盖脏工作区 | 未审计写入范围与恢复路径 | 写前审计 dirty worktree、Owner、取消和回滚 | 停止执行；按记录恢复，不猜测文件归属 |

## Execution checklist

- 开始前：读取 AIWarnings Start、CurrentStatus、RuleIndex、本条目 requiredReads；校验 SourceRefs/ContentHash；确定验收层级和 Owner。
- 实施中：绑定稳定场景身份、入口、PlanHash、TaskContract、环境和超时；保留取消、失败、重复执行和恢复路径。
- 完成后：验证退出状态、必需产物、哈希、失败项和回执新鲜度；逐项对照 required checks。
- 不可跳过：PlayMode、Profiler、Player 或发布声明必须有对应当前运行证据。
- 禁止：用文件存在、按钮存在、测试源码存在、旧截图或旧回执冒充执行成功。

## Evidence boundary

Static 可证明当前源码、规则、合同与哈希内容；不能证明 Unity 已编译、Guide 已运行、PlayMode 已通过、Profiler 达标、Player 可用或发布完成。当前未附运行回执时统一报告 `runtime-not-run`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md` (`ab0c4852c76d57c727405cc8a4da597bfeb38a77875ff0b5c23abb1df06b1e8e`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md` (`3bb8490dfdf42399110309ada24f51926fdd6b6894a7373f0ef583ec90c52cbc`)
- `Documentation/ES_SCENE_VALIDATION_GUIDE_STANDARD.md` (`2debe25a8da6d854270a17304291a600efe587251d9a7f4773b56eaa367d737b`)
- `Assets/Scripts/ESLogic/Runtime/Developer/Diagnostics/ESSceneValidationGuide.cs` (`f6858785179a66d09857f051ee9fa5c66d8fb9b3123ca4c3c01f6898de02d6d5`)
- `.agents/skills/es-release-acceptance/SKILL.md` (`8cc50a64bf90c8c8302836255b7a022f2aa33040fb02065e1d4448755f8b27c6`)
- `.agents/skills/es-release-acceptance/references/evidence-matrix.md` (`b4e9b8e1c4614adbef1f52c0758e47728253374b4d43bb9c38d7a2b1a23e3d85`)
- `.agents/skills/es-release-acceptance/references/evidence-receipt-contract.md` (`6200d8178982010bbdbae30a19b9de92f53ea0ca2fea47aa0ccefa3777fc0d94`)

`EvidenceLevel`: `S1`; `StaleWhen`: Scene Guide、Builder 权威、证据矩阵、receipt 合同或发布入口变化。
