# Shot 必中与 Item 运动职责：保真 Knowledge

`KnowledgeId`: `es.aiwarning.runtime.shot-motion-responsibility.v1`  
`Authority`: `AIWarnings` + current Shot/Item source  
`RouteKeys`: `aiwarnings`, `runtime`, `item`, `shot`, `motion`, `must-hit`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `c2becd3de7838b548678ffc606aae37732b7aab860af1b3875ea189b904aa11b`  
`SourceSetHash`: `c2becd3de7838b548678ffc606aae37732b7aab860af1b3875ea189b904aa11b`  
`EntryBodyHash`: `a371854a27c88f51546c42fc861a3a587015e01c42e58fbb4b74b17f18c03d1a`  
`StaleWhen`: ShotMotion、ItemDataInfo、碰撞查询、Tick 策略、Operation 或配置合同变化。

## 迁移范围

Warning 从 130 行、5941 字节压缩为长期边界与导航；本条目接纳详细参数、接口替换点、配置映射、失败面和原 Warning 摘要。原迁移前 SHA-256 为 `5cf969b8565bda238aada61c68fb0d0e7545a68bcb9dc341ebbe3587d1791f1b`。

## 当前事实

- Entity 管生命体；Item 管飞行物、门、机关、塔、陷阱、拾取物、武器、平台和区域等世界逻辑体；Shot 是 Item 的飞行能力，不是伤害、特效、对象池或技能总入口。
- 运动模块负责发射延迟、预热、加速/限速、锁头时间窗、转向、飞行、碰撞/到达、过期和停止，并输出事件或命中候选。`ShotMotionTypes`、`ShotMotionSolver`、`ItemBasicModules` 与 `ItemDataInfo` 是主要事实入口。
- `MustHit` 表示战斗/技能层已经决定命中，Shot 到达目标点时产生命中候选，即使没有真实物理碰撞；`Free`、`Target`、`Scan` 保持不同语义。`WorldOnly` 当前仍是数据语义，不能声称已完成 Layer 过滤。
- `IItemShotHitSolver` 默认使用 `Physics.SphereCastNonAlloc`，允许未来替换为空间哈希、简化碰撞或 Job；`IItemShotTickPolicy` 只回答本帧是否 Tick，分组额度、排序和更新保护应复用调度能力，不得伪装成完整调度器。
- `ItemDataInfo` 是统一配置入口。Shared 保存基础弹道、命中、寿命和必中许可；Variable 保存 seed、倍率、强制必中、延迟覆盖、目标偏移和散射角。`ItemShotModule.ApplyShotData(shared, variable)` 是运行时入口，不得把每发状态写回共享资产。

## 职责和性能边界

伤害、Buff、VFX、音效、Pool、复杂剧情、命中后分裂/连锁/换目标等由外层 Operation/Support 消费事件。高频 Tick 禁止 Op 链、反射、LINQ、字符串查找、每帧 new 数组、每帧 GetComponent；不得恢复旧 `Runtime/Movement`、`ESMotionBody` 或散落 MonoBehaviour 大根。

## 失败面与证据

重点防止把必中当作碰撞 hack、把阻挡/阵营/伤害塞入运动模块、把每帧 Tick 策略称为调度器、混用 Shared/Variable、提前锁死 Unity Physics 或把所有飞行物锁死为每帧全量更新。当前仅有源码静态证据；Unity、Runtime、Profiler、Player、IL2CPP 与发布行为未验证。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Item与Shot物理（ItemShotPhysics）/运动职责_Shot必中与Item运动_AI协作警告.md` (`983548866f65055b193d3d22cc26e6030489762e547c4a448d4650cd904c3947`)
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ShotMotionTypes.cs` (`4ddf21250e3635d644c02f5dd7e19f218f6beef1fe59d02aea052add8cf99102`)
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ShotMotionSolver.cs` (`1173c9f65a7270ec85203ea0af66de67c398bd2e531b578ab5fa16488c45dd0f`)
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ItemBasicModules.cs` (`256801bf1b996ab3735f52792867226ed3733be1a6dac3f03cfef0088e7147c5`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ItemDataInfo.cs` (`aad779ccdaba27ea91ca976ad731150aba4b27a90a7ca25487d4f799ff2abb00`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-runtime-shot-motion-responsibility.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ShotMotionSolver.cs`
