# 物理查询架构边界：保真 Knowledge
`KnowledgeId`: `es.aiwarning.runtime.physics-query-boundary.v1`  
`Authority`: `AIWarnings` 与当前 ESPhysicsQueryModule 实现  
`RouteKeys`: `aiwarnings`, `runtime`, `physics`, `query`, `collision`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `baff509239fd8cfd897b4672c4cb2b3674f755d743ac820fc28640f237682fd5`  
`SourceSetHash`: `baff509239fd8cfd897b4672c4cb2b3674f755d743ac820fc28640f237682fd5`  
`EntryBodyHash`: `52f218a8749d051e82867fb7d97b6ec56e6de2eca8a43d2b3ea2c264d6fe865b`  
`StaleWhen`: PhysicsQueryModule、查询 API 或任一 SourceRef 哈希变化。

## 迁移范围
Warning 保留最小公共查询层、模块获取语义、缓存并发边界和业务职责隔离；本条目承载源码入口、底层查询、语义 API、共享缓存约束和后续路线。Knowledge 不授予运行时修改权限。

## 模块与入口
实现为 `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESPhysicsQueryModule.cs`，入口为 `ESGameManager.PhysicsQueryModule`、`TryGetModule<ESPhysicsQueryModule>` 与初始化期 `GetOrCreateModule<ESPhysicsQueryModule>`；`autoCreatePhysicsQueryModule=true` 时由 GameManager 自动创建。`TryGetModule` 只查询不创建，`GetOrCreate` 仅明确初始化，旧 `GetModuleFast` 已废止。

## 查询层与语义层
底层提供 `RaycastNonAlloc`、`SphereCastNonAlloc`、`OverlapSphereNonAlloc`、`OverlapBoxNonAlloc`、LayerMask、共享缓存和溢出统计。语义入口包括 `ShotCast`（from/to，radius=0 用 Raycast，radius>0 用 SphereCast）、`TryGetNearestShotHit`、`TryFindBestInteraction`（Overlap 后按距离/朝向筛选）、`TrapOverlapSphere` 与 `TrapOverlapBox`。模块不是 `Physics.XNonAlloc` 薄包装，入口须保持可复用语义。

## 缓存与职责
高频/可重入调用优先传入 `RaycastHit[]` 或 `Collider[]` 显式缓存；低频可用 `RaycastShared`、`SphereCastShared`、`OverlapSphereShared`、`OverlapBoxShared`。共享缓存非并发安全，禁止嵌套共享查询后长期持有 `SharedRaycastHits/SharedColliders` 内容。模块不负责角色移动、伤害、Buff、Shot 飞行状态/必中、VFX、对象池、陷阱业务或武器释放。

## 路线与边界
建议顺序为交互接 OverlapSphere/Raycast、Shot solver 接 ShotCast、陷阱/区域接 Box/Sphere、近战接 SphereCast/Capsule，再评估分组 Tick、空间哈希、Job/Burst。不得先把 KCC、Item、Shot、Trap、Weapon 合成大 Domain；当前公共层只是查询服务。

## 原文快照与证据
迁移前台账：104 行、2505 字节，原始 SHA-256 `cc353b7cc11d1cfbead170eb8585e611c0f5cdd4ae3aa8cb0955920850f74a17`；本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Item与Shot物理（ItemShotPhysics）/物理架构_简单起步_AI协作警告.md` (`1ea45196d6a575cede11bd1f30737f4d1f53112cba03852055fffb306e83aeb9`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`90a4eec18e9952e4c13bdf8d6f1ebf3a4a88412f93c7d5403b3925afae1b0e9e`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-runtime-physics-query-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Item与Shot物理（ItemShotPhysics）/物理架构_简单起步_AI协作警告.md`
