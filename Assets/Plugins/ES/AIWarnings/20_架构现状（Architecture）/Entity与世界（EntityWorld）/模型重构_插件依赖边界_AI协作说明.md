# 模型重构：插件依赖边界

Status: current
StableId: es.aiwarning.arch.entity-plugin-dependency-boundary
Authority: AIWarnings；Packages/asmdef/当前源码为事实权威。
RouteKeys: aiwarnings, architecture, entity, dependency, asmdef, runtime, editor, adapter
Applicability: Entity 五域、ES_Stand/Design/Logic、KCC、Input System、FinalIK、Cinemachine、EasySave、Tween 与编辑器工具依赖。
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-architecture-entity-plugin-dependency-boundary.md#evidence`
Owner: ES architecture/dependency owners。
StaleWhen: Packages/manifest、asmdef、Entity 五域或第三方插件引用变化。

## 长期约束

- 角色核心沿用 `Entity + EntityCharacterIdentity + Core → Domain → Module`；禁止新增 `CharacterActor`、平行 Player 根或 Facade 替代既有 Entity。
- 纯协议/领域层不得依赖 KCC、FinalIK、Cinemachine、EasySave、DOTween、TMP 等第三方组件；第三方能力只能通过明确 Adapter/表现层接入。
- 输入系统只产生统一 `CharacterIntent`，KCC 只实现运动 Adapter，FinalIK/Cinemachine 只负责表现/目标绑定，EasySave 只消费快照；任何插件不得取得角色移动、战斗、剧情或存档权威。
- ES_Logic/ES_Design 的 asmdef 引用必须保持显式、可审计；运行时代码不得引用编辑器/层级辅助工具，遗留插件不得通过改名或目录删除“简化”依赖。
- 角色主链保留 Basic、AI、Buff、Equipment、State 五域；输入、网络、回放、剧情均写入同一意图/权限链，不建立第二套控制器。
- 详细插件地图、版本、当前耦合、四层依赖建议、Prefab 模板边界和过时点由专用 Knowledge 承接；Knowledge 不授予依赖改造或删除权限。
