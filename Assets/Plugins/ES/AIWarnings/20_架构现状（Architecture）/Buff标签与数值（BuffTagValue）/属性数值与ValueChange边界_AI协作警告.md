# 属性数值与 ValueChange 边界

Status: current
StableId: es.aiwarning.runtime.attribute-valuechange-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, runtime, attribute, valuechange, effect-lease, performance
Applicability: ESSuperAttributeTable/Catalog、ValueChange、Permit、Entity、Buff、装备与数值效果
Owner: ESFramework Attribute/Effect 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-attribute-valuechange-boundary.md
StaleWhen: 属性 Schema、Bake/Catalog、ValueChange、EffectLease 或热路径实现变化。

## 长期约束

- `ESSuperAttributeTable` 是唯一数值定义权威；Float 聚合只用 `ESFloatValueChangeSet`，Permit 只用 `ESPermitSet`。Tag 表达布尔事实，Buff 表达生命周期，Stat 表达数值，禁止复制第二套 Schema 或任意 Formula 字符串执行。
- `EnumKey`/`StringKey` 是唯一稳定身份；`fixedApiName` 只是生成访问名。Bake 先验证角色/物品源表，失败保留旧产物；RuntimeKey、Token、OwnerId、SourceId 仅是当前进程身份，不能进入配置/存档/网络。
- Modifier 必须持有 `ESEffectLease`/`ESValueChangeToken`；按 StringKey 扫描、裸清理、跨 Host 写入、旧 Lease 写新租户一律拒绝。池化推进 generation，写入瞬间校验 slot+generation 与宿主身份。
- ValueChange 先提交、观察者旁路；`BeginBatch` 只合并通知不是回滚。清理期间仅允许只读 TryGet/Snapshot，先归还外部 Lease 再清容器，旧引用不得跨池化生命周期。
- 高频角色读取用固定 API；热路径禁止 StringKey、Catalog/Resolver、分配、LINQ、闭包。任何“0 GC”或异常安全声明都必须有对应证据。
- 未完成重入、异常、Owner 释放、池化复用和 PlayMode 回归前，不得宣称运行时完整验收。

## Knowledge 导航

完整 Schema、Bake/生成门禁、Lease/generation、通知清理、性能和 P1 调试规则见 `es.aiwarning.runtime.attribute-valuechange-boundary.v1`。
