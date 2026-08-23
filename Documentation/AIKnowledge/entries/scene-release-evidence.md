# 测试场景、发布验收与证据等级

`KnowledgeId`: `es.project.scene-release-evidence.v1`  
`Authority`: `Source + AIWarnings + Skill contract`  
`RouteKeys`: `scene-validation`, `guide`, `builder`, `acceptance`, `release`, `evidence`, `receipt`, `profiler`, `unity`  
`ContentHash`: `2a4766a6a66a445b7c7739f0226ce6fd8ce711cb79dbe1465d1a171c1f2a180b`

## Guide 的职责边界

`ESSceneValidationGuide` 是每个测试场景自己的导视和诊断组件：展示步骤、路线、预期和实时检查。它没有全局单例，不读取/改写玩家输入，不向 Entity/Vehicle/Camera Prefab 注入组件；自定义检查必须拿到该 Guide 实例并调用 `ReportCheck`。

内建检查读取 ESGameManager、Input、LocalControl、Camera 输出和显式目标。外部结果写入后立即可读；常规 Evaluate 负责重放状态，`latchPass` 可保持已通过结果。HUD 只在状态/展示失效时重建文本，路线标记在 LateUpdate 读取显式相机和观察者。源码上的这些节流不能直接证明目标平台“零 GC”。

## Builder 权威与生成资产

场景 Builder 是可重建测试 Fixture 的权威来源；生成 `.unity` 只是输出。修改生成场景而不同步 Builder，下一次生成会覆盖修补。覆盖审计要区分：Builder 源码、项目内生成场景、外部临时副本/备份。项目外备份不能作为仓库可复现证据，也不能冒充正式资产。

Guide 的 `ConfigureForAuthoring` 是显式编辑器配置入口，工具不应反射私有字段。标准化 Guide 允许不同场景复用诊断语义，但每个场景仍需配置自己的路线、目标和机器可读检查。

## 证据阶梯

静态阅读和哈希只证明“当前文件是什么”；编译证明程序集边界和语法；EditMode 证明纯合同；PlayMode 证明 Unity 生命周期与场景交互；Profiler/目标平台证明性能预算；发布回执证明指定入口、环境、产物和结果被实际执行。低等级证据不能冒充高等级结论。

`es-release-acceptance` 要求先确定 acceptance level，再按 evidence matrix 运行对应入口，并把命令/入口、环境、退出状态、关键产物与失败项写入 receipt。验收规则是 required checks 全部有可验证证据且无 blocker；“源码看起来正确”“测试文件存在”“有人曾经截图”都不是有效回执。

## 发布非宣称与恢复

- Guide 全绿只说明配置的检查在本次场景运行中成立，不代表整个项目可发布。
- Editor 测试通过不能替代 Player/目标平台；Profiler 预算不能由代码审阅推断。
- 场景生成或测试会写项目状态时，必须先审计 dirty worktree、精确记录影响范围并保留可恢复路径。
- receipt 缺字段、SourceRef 漂移或某层证据不可运行时，结论保持“未验收/受阻”，不能降级措辞为“基本通过”。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md` (`ab0c4852c76d57c727405cc8a4da597bfeb38a77875ff0b5c23abb1df06b1e8e`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md` (`3bb8490dfdf42399110309ada24f51926fdd6b6894a7373f0ef583ec90c52cbc`)
- `Documentation/ES_SCENE_VALIDATION_GUIDE_STANDARD.md` (`2debe25a8da6d854270a17304291a600efe587251d9a7f4773b56eaa367d737b`)
- `Assets/Scripts/ESLogic/Runtime/Developer/Diagnostics/ESSceneValidationGuide.cs` (`f6858785179a66d09857f051ee9fa5c66d8fb9b3123ca4c3c01f6898de02d6d5`)
- `.agents/skills/es-release-acceptance/SKILL.md` (`3ef346c8be19390311e3c5f6b6feb079b528ee7b191d10d834724435cac8d901`)
- `.agents/skills/es-release-acceptance/references/evidence-matrix.md` (`b4e9b8e1c4614adbef1f52c0758e47728253374b4d43bb9c38d7a2b1a23e3d85`)
- `.agents/skills/es-release-acceptance/references/evidence-receipt-contract.md` (`316be0b6ca77daca05dc4e823043ef5f203f3a821e34c9123e34ed533c2df173`)

`EvidenceLevel`: `S1`; `StaleWhen`: Scene Guide、Builder 权威、证据矩阵、receipt 合同或发布入口变化。
