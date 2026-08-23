# GameObject Pool、Operation 与 Skill Track 生命周期

`KnowledgeId`: `es.project.pool-operation-skill-lifecycle.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `pool`, `prewarm`, `spawn`, `despawn`, `operation`, `skill-track`, `stop`, `lease`, `zero-gc`  
`ContentHash`: `ba322ef7b7a727cf9d086f8c01d8b7f111378770a5443633bc2c70b107ebb57b`

## Pool 真实合同

`ESGameObjectPoolModule` 按 poolKey/prefab 管分组；配置包含预热量、空闲/总量上限、是否扩容、溢出销毁、自动修补、归还清父级、Particle/Trail 清理和自动归还。`ESPooledGameObject` 保存 owner、源 Prefab、IsSpawned、Version 和 auto-return；Version 用于拒绝旧归还请求影响重新借出的实例。

Spawn/Despawn 不只是 SetActive：实现 `IESGameObjectPoolLifecycle` 的组件收到生命周期回调，`ESGenericLife` 统一组织清理。回调中请求归还会延迟到 spawn dispatch 完成，避免重入破坏分组状态。

预热可以异步并受取消控制；“预热完成”只证明池已有指定空闲实例，不证明后续负载永不扩容。0 GC 声明仍需要固定容量、稳态采样和溢出路径证据。

## Operation 默认无 Stop

`IOperation` 描述一次运行时作用；只有确实创建持续资源/句柄的 Operation 才需要可撤销生命周期。默认给所有 Operation 增加 Stop 会制造虚假对称：同步 Set/Log/触发类操作没有可停止对象，而 Audio/VFX/ValueChange 等持续效果必须把真实 Handle/Lease 放在 `ESOpSupport` 所有权树中。

`ESOpSupport` 区分 Host、Buff、State/Skill 等 owner，集中持有并清理 Audio/VFX/ValueChange/Permit/Tag 等效果 Lease。Stop 的对象是一次具体运行产生的 Lease，不是 Operation 定义本身。

## Skill Track

Operation Track 将作者 Clip 编译为 runtime player。Track/Clip runtime state 实现 owned user data，进入、采样、退出和销毁分别管理本次执行状态；Editor Preview 的 Start/Stop 与 Runtime player 不能共用未隔离的 Handle。技能取消、State 退出或池化回收必须沿 owner tree 清理，而不是遍历全局系统猜测来源。

## 失败模式

- 重复归还旧 Version：应拒绝。
- Pool 回收只 SetActive(false)：会遗留粒子、Trail、输入/Tag/效果 Lease。
- 无界 allowExpand：不是性能方案；必须结合 maxTotal 与溢出策略。
- 同一个 Operation 定义保存运行实例 Handle：并发执行会互相覆盖。
- Editor Preview Stop 清理 Runtime Handle：跨宿主所有权错误。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md` (`6beb3f9d18ebf505170695a06e52c0065a49c0fd7628a800853bc529f355a633`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md` (`f88f17a86b2703c968ba19aefafacfc36b79c26c0b20d567dd0e69d10b7c25a3`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md` (`77b7554f3ca549c265f0e8fdd86be2ef6315b4d53fe4d9469bd6355f144f8704`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs` (`e5904b9119fed0902e25bb048a0c24682b4e372c0873e2637785a4355a53fe27`)
- `Assets/Plugins/ES/1_Design/Define/0Define-Operation/Operation-Define.cs` (`31fc492002fb8a7f74d2c1073353f791bc9bbb91beb0717d25d2305cde16d592`)
- `Assets/Scripts/ESLogic/Runtime/Operation/RuntimeServices/ESOpSupport.cs` (`4184f6c4264acbe5551af15140b7adaa1232400e1160182cc818e2d1936edc02`)
- `Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence/Tracks/SkillTrackItem_Operation.cs` (`ebfed0078c9830103a1e2482ce7800a4350868088ff356972cac26ba8c1b14c4`)

`EvidenceLevel`: `S1`; `StaleWhen`: Pool Version/回调、OpSupport ownership 或 Skill runtime player 生命周期变化。
