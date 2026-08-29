# 载具运动与骑乘职责：保真 Knowledge
`KnowledgeId`: `es.aiwarning.runtime.vehicle-motion-boundary.v1`  
`Authority`: `AIWarnings` 与当前载具运动实现  
`RouteKeys`: `aiwarnings`, `runtime`, `vehicle`, `mount`, `motion`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `98f96a5d90c1611a07a105a61f69fc79c35224a28f6b46755146624e0d8e7246`  
`SourceSetHash`: `98f96a5d90c1611a07a105a61f69fc79c35224a28f6b46755146624e0d8e7246`  
`EntryBodyHash`: `b3a25fafb31b6aad7e21984248ea220a1810036616db0feeb64b058c42a6a49f`  
`StaleWhen`: Controller、Mountable、Rigidbody/KCC 或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留载具运动唯一权威、输入仲裁、后端互斥、禁止旁路写入和异常隔离；本条目承载组件细节、后端选择、调度接口与验收清单。Knowledge 不授予运行时修改权限。

## 详细规则

- `VehicleController` 选择并验证 Rigidbody/KCC，保存仲裁驾驶意图，调度 `IVehicleBeforeMotion`、`IVehicleRotationMotion`、`IVehicleVelocityMotion`、`IVehicleAfterMotion`，并提交最终旋转/速度。
- `EntityMountable` 维护单一 rider、`matchPoint`、武器挂点和同步；仅 `allowInput` 驾驶座转交输入，乘客座可无 Controller，且组件位于可命中 Collider 的同节点或祖先。
- `EntityBasicMountModule` 仅管理骑乘状态、MatchTarget 和输入采样；KCC 回调只接管骑手，不接管载具。车辆能力注册到自身 `ESWorkScheduler`，不复用带 Entity 语义的 `IEntityKCC*Motion` 接口。
- Rigidbody 载具适用于受力/碰撞/关节对象，必须是非 Kinematic，并在 `FixedUpdate` 写 `MoveRotation`/`velocity`；KCC 载具只在 `ICharacterController` 回调写 `ref currentRotation`/`ref currentVelocity`，不得同时启用非 Kinematic Rigidbody。
- 输入路由、Tag 或快照失效须清空意图；Controller 禁用/启用时解绑/重绑 KCC，所有回调以 `IsReady` 为前提。调度遍历用 `TryGetAlive`，单能力异常只记录并跳过。
- 载具不是生命体时不应为运动添加 Entity、角色 Domain 或 Profile。验收需覆盖配置错误不回退 Transform、上/下车与池化清理、Rigidbody 固定步/碰撞、KCC 地面/斜坡/碰撞及 MatchTarget 中断重进；这些仍需 PlayMode/运行时证据。

## 原文快照

迁移前台账：51 行、3753 字节，原始 SHA-256 `67f5565428ff61c53d99890e1330e8d33598919089d09c1811634d2c3c05308b`。原文完整内容由迁移台账和本条目规则覆盖，未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/载具运动与骑乘职责_VehicleMotion_AI协作警告.md` (`95f6f749012c7cbc73f4475cd94f5d3748b5deba0eef5602fa468b0d3c307ca7`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`e617d33e89bea630efca9a6f92fbe572e470456a4e40a62f466ea14465768713`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-runtime-vehicle-motion-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/载具运动与骑乘职责_VehicleMotion_AI协作警告.md`
