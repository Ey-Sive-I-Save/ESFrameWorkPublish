# State、Buff、Tag、ValueChange 与请求仲裁机制

`KnowledgeId`: `es.project.state-buff-tag-value-arbitration.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `state`, `buff`, `tag`, `value-change`, `permit`, `lease`, `camera`, `arbitration`, `lifecycle`  
`ContentHash`: `588a4149c66d8fceaa100d2e31413bfeadd750fb56c18de69d7a97c4eeec4279`

## 三种“状态”不能混为一层

- StateMachine 管表现状态、层、过渡、动画/IK 和生命周期。
- Buff 管有来源、有持续时间/层数/等级的被动持续机制，并通过 Operation/Lease 施加效果。
- Tag、ValueChange、Permit 是通用运行时容器：分别聚合存在计数、数值修正和许可规则；它们不拥有 Buff 或 Entity 业务。

把短暂动作都做成 Buff、把长期被动都塞进 State，或让 Tag 直接执行玩法逻辑，都会破坏所有权和清理路径。

## Tag 的引用计数与 Lease

`ESTagCollection` 将高频 Tag 放在 64 位热集合，稀疏 Tag 放 Dictionary；同时维护 Host 自有贡献和外部 Lease 贡献。`SetTag` 只改变 Host 的单次贡献，外部系统必须使用 `ESTagLease/ESTagLeaseSet`。稳定引用先经 `ESTagRuntimeCatalog` 解析为进程内 key；Catalog 未绑定或 Key 未注册时拒绝，不按字符串临时创建。

Lease 带 generation；Collection Clear/Dispose 后旧 Lease 不能减少新一代计数。通知分 count changed 与 presence changed，并通过队列隔离重入；回调异常被诊断记录，不回滚已完成的 Tag 变更。

## ValueChange 与 Permit

`ESFloatValueChangeSet` 聚合 base value、modifier、最终不可撤销定义边界和缓存值；每个 Token 绑定 setId、tokenId/version、ownerId/sourceId。Set 可延迟创建索引，只有实际 modifier 到来时分配；同一 Set 绑定 EffectLease host 后不能迁移到另一 host。

`ESPermitSet` 使用同样的 Token/owner/source 生命周期，但输出是由 PermitLaw 解析的 bool 与诊断结果。Fallback 只在没有更高规则时使用，不能用一个全局 bool 替代多来源请求。批处理在最外层完成后统一通知，避免中间态泄漏。

## Buff 运行所有权

EntityBuffDomain 分离 active/inactive Buff，并拥有独立 OpSupport。公开 API 接受稳定 Enum/String ConfigKey 或定义引用；StringKey 只在应用时解析，活跃 Tick 保存进程内 runtime key。IndependentInstance 可能在同来源存在多实例，因此按 Key 操作若不唯一会拒绝，调用者必须保存 `ESActiveBuffRuntime` 精确句柄。

BuffFrame 是状态效果的命令缓冲事务：`BeginBuffFrame(owner)` 后声明本帧效果，`EndBuffFrame` 验证并原子应用，未在本帧重申的该 owner 效果被移除；失败时回滚本帧新建项，`CancelBuffFrame` 保留上一份已提交状态。普通计时 Buff 不受 BuffFrame 清理影响。

## 活跃请求仲裁

Camera、控制、UI Focus、音频 Voice 等“多来源争用单一出口”的系统应使用：稳定 owner/source、priority、generation-safe token、显式结束原因和可观察当前赢家。`ESCameraDirector` 是该模式的领域实例；它不授权其他领域复用 Camera 内部键。Permit 只回答“能否执行”，Arbiter 还要回答“谁是赢家”，两者不能合并。

`StaleWhen`: State/Buff/Tag/ValueChange/Permit 仲裁合同或任一 SourceRef 哈希变化。

## 静态测试证据

当前仓库已有部分失败面测试：

- `ESTagCatalogRuntimeTests.cs` 覆盖稳定 Tag Catalog 解析、未注册 key 拒绝和运行时 key 边界。
- `ESValueChangeSetTests.cs` 覆盖 stale/foreign token、generation、owner/source 批量释放、优先级仲裁、批处理通知和 HardDisable。

BuffFrame 的提交/回滚/取消仍缺专门测试；本条目不把该部分声明为已运行或已闭合。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/Buff职责边界_被动持续机制_AI协作警告.md` (`6f8518f81bb15330013bf7829237954c2f84f373523c3f149071f11052523f76`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/属性数值与ValueChange边界_AI协作警告.md` (`8b4db36fcf4a870ec2b7a67eaff3aa90478b374f1d4fc019575406355fc4d505`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_Codex核心上下文总纲_状态机IK标签调度LOD_AI协作警告.md` (`ab9dd8f419d3a37e79540f64add66b1b09af4f9bc7b6acfce8b8a946830542e2`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md` (`064642f794962c253c2504ae6516586d3232ce0002cdebf849433e6d0ba354ef`)
- `Assets/Scripts/ESLogic/Runtime/Tag/ESTagCollection.cs` (`77d172c9fb88a7ec84a60a67b0f7846fe38d79583da7e0b633c2e97cf4c2b980`)
- `Assets/Scripts/ESLogic/Runtime/Tag/ESTagLease.cs` (`fe2e9639dc42803a9c2d3ac3dbca31bf5be1eb55e50aaa9bb3c422fd6e41a612`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/ValueChange/SERVICE_ESFloatValueChangeSet.cs` (`6a00c1c7423aabc16a5f0539262f77f7a5e8dd5dd5c462a6203e4a59afbc7c9b`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/ValueChange/SERVICE_ESPermitSet.cs` (`2f2c243ab4cb12a886fe5ff4324be28aa82b722e8c249955c9b204cb70127bac`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Buff/_EntityBuffDomain.cs` (`95e0ae56541c0410867a8de6f18f67f70eb04ae2b0026d8a0f6a559a40305af4`)
- `Assets/Scripts/ESLogic/Runtime/State/Core/StateMachine/StateMachine.cs` (`53197774f0c98613af3bec8819dada81a2908e32715a23e032b6fff00507c9a8`)
- `Assets/Scripts/ESLogic/Runtime/Camera/Core/ESCameraDirector.cs` (`a0b7dffee4d1518c2a1c89e50f9f68dcf2f136f02a63c2642e9039199fd441c9`)
- `Assets/Plugins/ES/1_Design/Tests/ESTagCatalogRuntimeTests.cs` (`7a7e7ecba2fef233b2d487cd32d52cc4c3530b606fb0732e881e4416810f4c90`)
- `Assets/Plugins/ES/0_Stand/Tests/ValueChange/ESValueChangeSetTests.cs` (`320f307a37c81dabfccce10310839db035b70717dc21858e0d5952cb6c0238a6`)

`EvidenceLevel`: `S1`; `StaleWhen`: Token/Lease、BuffFrame、State 生命周期或请求仲裁协议变化。
