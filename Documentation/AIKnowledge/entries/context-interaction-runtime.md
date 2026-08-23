# Contextitecture 与 Interaction 生命周期

`KnowledgeId`: `es.project.context-interaction-runtime.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `context`, `context-value`, `pool`, `interaction`, `interactable`, `occupancy`, `end-reason`, `ik`, `tag-zone`  
`ContentHash`: `c8a45d8cc467da2bf2d63ef9a408e4d148658f7eff33bf1cf76ff93d3baa7191`

## Context 是显式传播，不是全局状态袋

ContextPool 是值的接收/传播边界；Context Value 通过强类型枚举表达 Class、UnityObject、DynamicTag、Float、Int、Bool、String、Vector 等形态。值可以标记是否发送及是否持久，但这不赋予它跨场景全局所有权。

Value 由各类型对象池 `Rent`，使用前 `PrepareFromPool`，结束后按真实运行类型 `Release` 回对应池。池化对象的 Key、值、发送目标和接收池关联都属于单次租用状态；持有方必须在生命周期结束时解除接收关系并归还，不能缓存为永久引用。未知类型回退成 String 是当前兼容行为，不应被解释为类型安全扩展点。

ContextOperation 是对上下文值执行行为的边界。Operation 配置、运行时值实例与接收池所有权需分开，禁止让 ScriptableObject 配置直接保存一次运行时租用品。

## Interactable 的检查、占用与结束

`CanInteract` 只做资格检查，结果至少区分 Disabled、Cooldown、Occupied、距离/视角/状态/Tag 等拒绝原因；它不取得所有权。`TryAcquireInteraction` 才把 Entity 写为唯一 `_interactionOwner`。同一目标被其他 Entity 占用时拒绝，结束或失败路径必须释放同一个 owner。

生命周期为检查 -> 取得占用 -> Started/Update -> Ended。`ESInteractionEndReason` 区分用户取消、移动取消、目标无效、模块停用、Owner 丢失、BeginRejected 等终态；`success` 不能替代原因枚举。`OnInteractEnded` 会兼容调用旧的 Completed 钩子，派生类不能因此跳过基类清理。

`ESInteractionBinding` 由 token、generation、owner、target 组成，用于拒绝上一代或其他占用者的迟到提交。Story 等集成层应传递 binding，而不是只保存目标对象引用。

## IK、MatchTarget 与 Tag Zone

Interactable 只组装 IK 写入请求：Goal、目标 Transform、clamp 后权重、lerpingRate 和是否写旋转；实际 Driver/仲裁由交互运行时消费。权重与 lerpingRate 是不同语义，不能互换。MatchTarget 同样是请求配置，不拥有角色运动系统。

`ESTagApplyZone` 按 Entity 维护 Occupant，并给每个进入者持有独立 `ESTagLeaseSet`。重复 Collider 进入不能重复叠加同一租约；离开、实体失效、Zone Disable/Destroy 都必须释放。区域需要 Trigger Collider 和项目 Layer 规则，Tag 写入失败会保留诊断而非静默成功。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/Contextitecture上下文系统_所有权生命周期与类型边界_AI协作警告.md` (`ba38596bdf67ef81bb7179bb0f0345ef896a216e73d72e83e821df4e2dbc4f6e`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）/交互运行时_Interactable占用生命周期与结束原因_AI协作警告.md` (`4a08dcbd6972f2aa451259b92723aaebd1bf461580eec5d92f30025630be46d9`)
- `Assets/Scripts/ESLogic/Runtime/Context/Core/ContextPool.cs` (`263b1d76e646c8edced4a9750ca898a0deca0e0ca229b82f828a8aaa14c5d6db`)
- `Assets/Scripts/ESLogic/Runtime/Context/Values/SupportContextValue.cs` (`5a5461a1d6a038a5f1e49217089d9813a4e9d50aafa2e09694b8a22bfda8f875`)
- `Assets/Scripts/ESLogic/Runtime/Interaction/Core/ESInteractable.cs` (`bf76ca5b4ff1bbac2c07b255887c55fc592c99c039b8e0e3efb5c277b2393a8c`)

`EvidenceLevel`: `S1`; `StaleWhen`: Context Value 类型/池协议、Interactable 占用、Binding、结束原因或 Tag Lease 变化。
