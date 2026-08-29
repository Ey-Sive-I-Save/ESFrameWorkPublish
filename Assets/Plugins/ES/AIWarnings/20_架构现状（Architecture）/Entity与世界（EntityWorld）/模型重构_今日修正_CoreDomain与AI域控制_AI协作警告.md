# 模型重构：CoreDomain 与 AI 域控制边界

Status: current
StableId: es.aiwarning.entity-core-domain-ai-control-correction.v1
Authority: AIWarnings（当前架构纠偏）；详细职责与历史语义见 Knowledge
RouteKeys: aiwarnings, architecture, entity, core, domain, module, ai-control, control-request, facade
Applicability: Entity、Core/Domain/Module、AI 控制来源、角色外壳、Buff/Equipment 域和场景模板
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-entity-core-domain-ai-control-correction.md
StaleWhen: Core/Domain/Module 生命周期、EntityAIDomain、控制请求合同或 SourceRef 哈希变化。

## 长期约束

- Core 可以有总入口和调度，Domain 可以有域级协调逻辑，Module 负责具体功能；不得把 Domain 当纯容器，也不得膨胀成巨型实现类。
- 角色外壳只管结构、引用、统一入口和桥接，不接管全部玩家/AI/剧情/网络控制。控制来源优先在 AI 域收集/仲裁，再输出本帧请求给 Basic/State/Skill/战斗模块执行。
- 第一阶段保持最小增量：薄外壳（可选）、控制来源模块和轻量请求数据；未经验证不得铺设大量控制脚本或新 Domain。
- Buff 与 Equipment 保持各自域职责，不回流 Combat 或按空域重建；层级模板只是说明结构，不等于可玩工业 Prefab。
- 热路径禁止每帧 Find、反射、字符串查找和动态取模块；初始化缓存引用，控制请求优先 struct/复用对象，避免每帧分配。

## Knowledge 导航

详细纠偏背景、Core/Domain/Module 职责、AI 控制链、场景模板、性能边界和历史结论见 `es.aiwarning.entity-core-domain-ai-control-correction.v1`。本 Warning 不授权新增脚本、Domain、Prefab、源码或运行时改造。
