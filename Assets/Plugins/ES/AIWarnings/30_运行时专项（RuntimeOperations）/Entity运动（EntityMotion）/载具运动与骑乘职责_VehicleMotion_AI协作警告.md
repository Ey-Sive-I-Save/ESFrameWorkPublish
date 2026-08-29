# 载具运动与骑乘职责
Status: current
StableId: es.aiwarning.runtime.vehicle-motion-boundary.v1
Authority: AIWarnings；详见 Knowledge
RouteKeys: aiwarnings, runtime, vehicle, mount, motion
Applicability: VehicleController、EntityMountable 与骑乘输入/运动
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-vehicle-motion-boundary.md
StaleWhen: Controller、Mountable、Rigidbody/KCC 或 SourceRef 变化。
- `VehicleController` 是载具唯一运动写入者；`EntityMountable` 只管 rider、座位/挂点、对齐和驾驶输入转交。
- 座位输入必须经 `TrySetDriverInput(seat, driver, ...)`，每物理步仅一个仲裁来源；失效或过帧快照须清空意图。
- 车辆专用能力只改候选值并由 Controller 提交；禁止座位、镜头、AI、网络或动画直接写载具 Transform/物理组件。
- Rigidbody 在 `FixedUpdate` 写入；KCC 仅在回调写入，二者不得并用；调度遍历用 `TryGetAlive`，单能力异常不得阻断提交。
- `EntityBasicMountModule` 只接管骑手；非生命体载具不因运动添加 Entity/角色域。完整后端选择与验收见 Knowledge。
Knowledge：`es.aiwarning.runtime.vehicle-motion-boundary.v1`
