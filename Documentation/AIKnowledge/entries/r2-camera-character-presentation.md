# R2 相机与角色表现知识包

`KnowledgeId`: `es.project.r2-camera-character-presentation.v1`
`Authority`: 当前 ES 相机/角色源码与合同；Unity 官方资料仅作版本校准
`RouteKeys`: [camera, camera-production, third-person-camera, camera-occlusion, camera-transition, entity, character, prefab, final-ik, state-machine, playable, socket, mapping]
`ContentHash`: `82af90fec035d717738e7ae58b75225dcd701ab138c1d1af8b0e2604aba54feb`
`EvidenceLevel`: `S1`
`StaleWhen`: 相机模块、角色 Prefab/IK/挂点合同、Cinemachine 2.10 或 PlayableGraph 生命周期语义、任一 SourceRef 哈希变化。

## 已验证项目事实

- 唯一业务链为 `ESCameraModule → ESCameraDirector → ESCameraCinemachine2ViewAdapter → CinemachineBrain/VCam`；业务只提交 `ESCameraRequest/Lease/Modifier`。
- `ESCameraDirector` 维护活跃请求集合并确定性仲裁；正常提交点是 `LateTick`，旧 Lease 由 ViewId/SceneEpoch/Generation 拒绝。
- `ESCameraSceneBinding` 独占 Output Camera、Brain、Definition/Rig Catalog、BlendSettings 与 RigRoot；RigRoot 不得成为 Output Camera 的父级反馈链。
- 角色根由 `Entity + EntityCharacterIdentity + EntityTransformMapping` 等职责组件构成；Identity 只能提供稳定 Camera Definition 意图，不能持有 VCam 或 Lease。
- `EntityTransformMapping` 是挂点缓存权威；正式 Socket 缺失必须拒绝装配，不回退到根节点或 Humanoid 手骨。
- `StateFinalIKDriver` 消费 State/Playable 汇总后的 Pose；Solver 缺失时必须显式关闭能力，禁止静默 no-op 或运行时 auto-add。

## 外部版本校准（非运行时证明）

- Cinemachine Collider 通过物理射线检查遮挡并调整镜头位置，障碍物必须有 Collider，且该检查具有性能成本。[Unity Cinemachine Collider](https://docs.unity.cn/Packages/com.unity.cinemachine@2.10/manual/CinemachineCollider.html)
- Unity PlayableGraph 负责 playable/output 生命周期，创建的图必须在结束时显式 `Destroy`；这支持 R2 预览会话的成对创建/释放约束。[Unity PlayableGraph](https://docs.unity3d.com/cn/current/Manual/Playables-Graph.html)

## 最小失败面与检查

| failure | 触发 | 防止/恢复 | 当前证据 |
|---|---|---|---|
| Camera-Occlusion | 墙角/窄门遮挡 | 以 Adapter 的 Collider/探针策略回缩；无可见点进入安全默认 | 无 Unity 回放 |
| Camera-Arbitration | 玩家与 Shot/Modifier 同帧竞争 | Director 按 priority/kind/submissionSequence 选唯一赢家 | Director 源码 |
| Target-Lifetime | Entity 销毁或换场景 | SceneEpoch/Generation 失效旧 Lease，冻结或切安全锚点 | 静态合同 |
| Prefab-Socket | Socket 缺失或错挂 | EntityTransformMapping/WeaponBinding 校验失败即拒绝装配 | 映射与绑定源码 |
| Playable-Leak | 预览关闭仍持有图 | 会话 Dispose 成对销毁 PlayableGraph 与 Lease | Unity 官方生命周期规则；未运行验证 |

## 非声明与缺口

- 本条目不证明 Unity 编译、Prefab 实际序列化、PlayMode、遮挡体感、IK Solver 生效、Profiler、Player、IL2CPP 或发布。
- `RENDERING_5_WINDOW_TASK_PACKET.md` 当前缺失；正式玩家/载具 Camera 资产是否生成并被场景引用仍待 Unity 核验。

## SourceRefs

- `Documentation/ES_CAMERA_RUNTIME_STANDARD.md` (`ce33403ccd724bcd16bb87e1050e0e0cbe3cff8e6bf37b59bee91a5a0d87e317`)
- `Documentation/CHARACTER_PREFAB_CONTRACT.md` (`28cee5135c6f8938ab701df21c469f80db15fbe9777b50bd4c6dbb54887438e9`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色Prefab职责与DataInfo入口_AI协作警告.md` (`4e1a75e52b673a57f10f8a53c2b566c44e60246b9f5bcb03cc8e9bf05d9bb306`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/状态机与IK（StateIK）/AI协作职责_状态机与IK上层_Buff边界说明.md` (`c86832d48e0eefbbeed6cba0fe85ff607cd861e4b3fa1ee05c5ae312ad1ee3fc`)
- `Documentation/AIKnowledge/ExternalSources/unity-cinemachine-210-r2-camera-provenance-20260830.json` (`4aa64b1a254ff836cc8cf0abd1ec83a28afb1a7895dd8c9d8282c80f967d554d`)
