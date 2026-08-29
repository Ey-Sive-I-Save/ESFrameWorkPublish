# P0：保留通用泛型底座，长期合同使用领域具体类型

Status: current
StableId: es.aiwarning.p0.generic-container-serialization-boundary.v1
Authority: AIWarnings（长期 P0 约束）；详细判定与迁移门禁见 Knowledge
RouteKeys: aiwarnings, p0, identity, serialization, generic, container, unity, il2cpp, migration
Applicability: ES 自定义泛型进入 Unity/Odin 持久化、GameCore/RuntimeData 表、稳定 API 或 AOT 边界
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-generic-container-serialization-boundary.md
StaleWhen: Unity/Odin 序列化、ESEnumStringMirrorMap、领域具体类型、兼容阶段或 SourceRef 哈希变化。

## P0 长期约束

- 保留、复用并测试 `ESEnumStringMirrorMap<TEnum,TValue>` 等通用底座；本规则不禁止泛型，也不授权删除、禁用或复制底座。
- 进入长期序列化、GameCore/RuntimeData 权威表、跨模块公共 API 或 IL2CPP/AOT 稳定边界的闭合泛型，必须由承担真实职责的 `sealed` 领域具体类型承载；仅固定参数、改名或空壳包装不合格。
- 具体类型必须具备可验证的序列化/AOT/迁移身份、不变量/冲突策略、版本迁移、作者态隔离、领域 API/诊断或发布门禁之一。仅因限制继承 API 不得改成组合；真实内部入口使用 `Internal_`。
- 局部算法、非序列化缓存、短生命周期 Pool/Lease/Handle/Scheduler/Builder、普通 BCL 集合和底座自身默认不命中，禁止扫描后机械包装。
- 正式发布/玩家存档/UGC/外部协议等兼容数据变更前必须执行预检、备份、受控迁移、保存后重载等价验证和失败恢复；`FormerlySerializedAs` 不等于容器换型安全。
- 开发阶段框架旧数据可按明确政策破坏性重置，但这不证明新格式、Unity、PlayMode、Player、IL2CPP 或发布已通过；必须列出重置范围并验证新格式消费。

## Knowledge 导航

详细判定范围、继承/组合矩阵、迁移步骤、证据分层和 AI 禁令见 `es.aiwarning.p0.generic-container-serialization-boundary.v1`。本 Warning 不授予删除、迁移、Git、运行时或发布权限。
