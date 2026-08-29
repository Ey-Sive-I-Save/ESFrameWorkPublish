# 项目最高警告：核心热路径缺失依赖不判空
Status: current
StableId: es.aiwarning.p0.hotpath-missing-dependency-null-check.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, p0, runtime-performance, hotpath, dependency, null-check
Applicability: Entity、KCC、StateMachine、FinalIK Driver、Buff、AI、Interaction、对象池、Tag/ValueChange 与运行时调度器等高频链路
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-hotpath-missing-dependency-null-check.md
StaleWhen: 核心依赖初始化、预热、配置验证或 SourceRef 变化。

- 核心依赖必须在初始化、绑定、预热或配置验证阶段严格保证；初始化成功后热路径信任结果，缺失直接暴露，不得把错误转嫁给每帧回调。
- 可选能力在任务入口轻量判断并快速返回；诊断、日志和编辑器监控不得污染正式热路径。
- 禁止重复核心判空、每帧链式查找、以“安全”掩盖初始化错误，或在热路径引入 LINQ、反射、字符串拼接、临时集合和 Unity 查找。
- 看到判空不得盲删，先区分核心依赖与可选能力。Knowledge：`es.aiwarning.p0.hotpath-missing-dependency-null-check.v1`。
