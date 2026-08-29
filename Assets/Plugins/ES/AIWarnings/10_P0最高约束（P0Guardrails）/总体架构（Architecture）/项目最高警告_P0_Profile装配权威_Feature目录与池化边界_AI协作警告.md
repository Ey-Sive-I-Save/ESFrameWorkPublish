# 项目最高警告：P0 Profile 装配权威、Feature 目录与池化边界

Status: current
StableId: es.aiwarning.p0.profile-assembly-feature-pool-boundary.v1
Authority: AIWarnings（长期 Profile/Pool 约束）；详细规范见 Knowledge
RouteKeys: aiwarnings, p0, profile, assembly, feature, pool, extension, runtime-context, editor
Applicability: XxxProfile、Prefab/场景装配、Profile Editor、Extension 生命周期和对象池
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-profile-assembly-feature-pool-boundary.md
StaleWhen: Profile Header/Settings/Extension/RuntimeContext、ESGenericLife、Feature 目录或 SourceRef 哈希变化。

## P0 长期约束

- Profile 是 Prefab/场景对象的能力装配和默认策略权威，不是 Definition/Catalog、资源 Owner、Runtime Service、动态状态或第二个 Pool Root；标准结构为 Header、强类型 Settings、单层 Extension 列表和非序列化 RuntimeContext。
- `Profile` 术语必须满足稳定 Key/Schema、Settings、Extension、RuntimeContext 和完整生命周期边界；普通 Config、Preset、Policy、Identity、Plan 不得借名，空壳具体类型不合格。
- Extension 只在生命周期边缘按稳定顺序正序开始、逆序结束；不得嵌套 Domain、在热路径遍历、反射或按类型名动态分派。Runtime 不依赖 Editor Registry/迁移器。
- Profile 迁移只能由 Editor 显式事务执行并可整体恢复；禁止 Drawer、OnGUI、OnValidate、Awake 或 Player 静默迁移。Settings 不得保存动态业务状态，RuntimeContext 不得成为第二权威。
- `ESGenericLife` 是 Pool Root、Generation 和 Spawn/Despawn 唯一权威；Profile 只能作为 Entity 的 Extension。Profile 不拥有资源、不创建 Scope、不扫描子树，新代不得继承旧 Handle/注册状态。
- `Runtime/Profile` 仅放装配声明；Feature 放使用侧组件/桥接，Module 负责全局服务/仲裁。静态检查不能证明 Unity 序列化、运行时、Profiler 或发布。

## Knowledge 导航

详细 Profile 结构、命名门禁、迁移事务、目录语义、Extension 生命周期、Pool 和资源规则见 `es.aiwarning.p0.profile-assembly-feature-pool-boundary.v1`。本 Warning 不授权新增、迁移、删除、Git、运行时或发布操作。
