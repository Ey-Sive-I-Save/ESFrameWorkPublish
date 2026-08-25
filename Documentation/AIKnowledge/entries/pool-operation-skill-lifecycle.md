# GameObject Pool、Operation 与 Skill Track 生命周期

`KnowledgeId`: `es.project.pool-operation-skill-lifecycle.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `pool`, `prewarm`, `spawn`, `despawn`, `operation`, `skill-track`, `stop`, `lease`, `zero-gc`  
`ContentHash`: `32560f3c64cd0bfaf89e1144e51d469aab851cad55608acced8b18e048388b4b`

## Pool 真实合同

`ESGameObjectPoolModule` 按 poolKey/prefab 管分组；配置包含预热量、空闲/总量上限、是否扩容、溢出销毁、自动修补、归还清父级、Particle/Trail 清理和自动归还。`ESPooledGameObject` 保存 owner、源 Prefab、IsSpawned、Version 和 auto-return。当前 `PushToPool(GameObject)` 接口不接收调用方预期的 Version/Lease，只检查对象仍为 spawned 且存在于 active 集合；因此 Version 不能单独拒绝一个旧异步回调对重新借出实例的归还。需要跨异步边界时，调用方必须自持并校验代号/租约，或由池 API 增加带 Version 的归还入口。

Spawn/Despawn 不只是 SetActive：实现 `IESGameObjectPoolLifecycle` 的组件收到生命周期回调，`ESGenericLife` 统一组织清理。回调中请求归还会延迟到 spawn dispatch 完成，避免重入破坏分组状态。

预热可以异步并受取消控制；“预热完成”只证明池已有指定空闲实例，不证明后续负载永不扩容。0 GC 声明仍需要固定容量、稳态采样和溢出路径证据。

## Operation 默认无 Stop

`IOperation` 描述一次运行时作用；只有确实创建持续资源/句柄的 Operation 才需要可撤销生命周期。默认给所有 Operation 增加 Stop 会制造虚假对称：同步 Set/Log/触发类操作没有可停止对象，而 Audio/VFX/ValueChange 等持续效果必须把真实 Handle/Lease 放在 `ESOpSupport` 所有权树中。

`ESOpSupport` 区分 Host、Buff、State/Skill 等 owner，集中持有并清理 Audio/VFX/ValueChange/Permit/Tag 等效果 Lease。Stop 的对象是一次具体运行产生的 Lease，不是 Operation 定义本身。

## Skill Track

Operation Track 将作者 Clip 编译为 runtime player。Track/Clip runtime state 实现 owned user data，进入、采样、退出和销毁分别管理本次执行状态；Editor Preview 的 Start/Stop 与 Runtime player 不能共用未隔离的 Handle。技能取消、State 退出或池化回收必须沿 owner tree 清理，而不是遍历全局系统猜测来源。

## 失败模式

- 旧异步归还：当前裸 `PushToPool(GameObject)` 无法证明会拒绝旧持有者，必须按风险处理。
- 当前无带 Version/Lease 参数的 `PushToPool(GameObject)` 不能证明上述旧异步归还已被拒绝；该风险必须由调用方代号校验或扩展后的池 API 闭合。
- Pool 回收只 SetActive(false)：会遗留粒子、Trail、输入/Tag/效果 Lease。
- 无界 allowExpand：不是性能方案；必须结合 maxTotal 与溢出策略。
- 同一个 Operation 定义保存运行实例 Handle：并发执行会互相覆盖。
- Editor Preview Stop 清理 Runtime Handle：跨宿主所有权错误。
- 异步预热跨代：同一 Scene/Space 在取消卸载后重新预热时，旧 `PrewarmAsync` 回滚必须校验 context identity/generation；旧回调不得删除或回滚新一代 context。
- 修补统计：`AutoRepairAll` 的 `repairCount` 必须按 `CreateInactive` 实际成功数记录，不能直接把需求量 `need` 当成成功修补数。

## 静态测试证据

当前仓库已有针对上述失败面的 EditMode 测试：

- `ESGenericLifePoolTests.cs` 覆盖 Spawn/Despawn 异常、失败基线补偿、Clear/重入与 Spawn 内延迟清理。
- `SkillTrackLifecycleIsolationTests.cs` 覆盖按 `NeedsStop` 决定 Stop、Stop 异常时的目标与运行态回收、Version 防旧归还。
- `SkillSequenceRuntimeCacheLifecycleTests.cs` 覆盖 Skill runtime cache 的释放、失效、清空和池回收交互。

这些是静态源码与测试绑定；没有运行 Test Runner，因此不声明测试实际执行通过。

`StaleWhen`: Pool、Operation、Skill Track、Lease/GC 合同或任一 SourceRef 哈希变化。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md` (`6beb3f9d18ebf505170695a06e52c0065a49c0fd7628a800853bc529f355a633`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md` (`f88f17a86b2703c968ba19aefafacfc36b79c26c0b20d567dd0e69d10b7c25a3`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md` (`77b7554f3ca549c265f0e8fdd86be2ef6315b4d53fe4d9469bd6355f144f8704`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs` (`e5904b9119fed0902e25bb048a0c24682b4e372c0873e2637785a4355a53fe27`)
- `Assets/Plugins/ES/1_Design/Define/0Define-Operation/Operation-Define.cs` (`aad9580cc5bc64864b478ea371dc303b1c5c27e264421a13daed4d073b387e60`)
- `Assets/Scripts/ESLogic/Runtime/Operation/RuntimeServices/ESOpSupport.cs` (`4184f6c4264acbe5551af15140b7adaa1232400e1160182cc818e2d1936edc02`)
- `Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence/Tracks/SkillTrackItem_Operation.cs` (`cf1f75ce54902dd936dab6c74118e1c4a1fa92c5b329119042c4998cb749f899`)
- `Assets/Plugins/ES/1_Design/Tests/ESGenericLifePoolTests.cs` (`57f1260c75da436d7f8e9c9cc0befc3332c8c4107f52e7ef60cbc4d8878c47cc`)
- `Assets/Plugins/ES/1_Design/Tests/SkillTrackLifecycleIsolationTests.cs` (`fda8013177ed153d1b275735f9dbb210b436f7b3f0302909b05cfad658c691d2`)
- `Assets/Plugins/ES/1_Design/Tests/SkillSequenceRuntimeCacheLifecycleTests.cs` (`9bac1c9e2a352414698bf5c4b0058df4cfdef89ac9f8e31264f6e659b7c8eba5`)

`EvidenceLevel`: `S1`; `StaleWhen`: Pool Version/回调、OpSupport ownership 或 Skill runtime player 生命周期变化。
