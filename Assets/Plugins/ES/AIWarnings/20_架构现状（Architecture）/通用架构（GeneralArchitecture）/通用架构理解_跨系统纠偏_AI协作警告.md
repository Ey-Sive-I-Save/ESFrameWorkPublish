# 通用架构理解：跨系统纠偏

Status: current
StableId: es.aiwarning.general-architecture-cross-system-correction.v1
Authority: AIWarnings（长期跨系统边界）；详细纠偏与自检清单见 Knowledge
RouteKeys: aiwarnings, architecture, cross-system, domain, module, facade, runtime-state, hot-path, buff, input
Applicability: Entity、Input、ValueChange、Buff、StateMachine、Item、GameManager、AITalk 和编辑器边界
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-general-architecture-cross-system-correction.md
StaleWhen: Core/Domain/Module、Entity、Input、Buff、State、Item、GameManager 或跨系统边界变化。

## 长期原则

- 底层协议管规则，Domain 管域级协调，Module 管具体能力，Facade 管结构入口，Binding 管跨层翻译，Runtime State 管运行变量，DataInfo 管配置，Editor 管制作体验。
- 不建立“大一统”总系统：AI/控制来源、Basic 身体执行、State 表现、Buff 实例效果、Equipment 装备事实各守自己的域；外壳不接管全部控制。
- State 表现层不得拥有属性合成、Buff 叠层/驱散、权限、伤害、背包或装备逻辑；ValueChange、Expression、Op、Buff 按真实生命周期和热路径边界分工。
- 高频 Tick 只做纯运动、命中候选、事件和 NonAlloc 检测；禁止每帧反射、LINQ、字符串查找、动态组件/模块查找和临时集合分配。
- Domain 是大边界，能力优先放 Module；允许缓存的服务对象保持稳定，运行变量不得进入可复用 Player/Calculator/Data；Entity Inspector 不得摊平 Domain 内部职责。
- Buff 扩展必须复用既有实例、Permit、ValueChange、OpSupport 和 VisualBridge 链路；AITalk 交互必须遵守 Session、权限、公开/私密和持续轮询协议。

## Knowledge 导航

详细职责矩阵、跨系统纠偏、Buff/AITalk 规则和自检清单见 `es.aiwarning.general-architecture-cross-system-correction.v1`。本 Warning 不授权新增 Domain、脚本、源码、运行时或发布改造。
