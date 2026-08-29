# AI 自动化内容身份与 GameCoreKey 迁移

Status: current
StableId: es.aiwarnings.arch.ai-content-identity-gamecorekey-migration
Authority: AIWarnings
RouteKeys: aiwarnings, architecture, identity, gamecore, configkey, migration
Applicability: 设计或迁移 AI 生成内容、Action、Weapon、Skill、Audio、Prefab、Tag 和 GameCoreKey 时。
EvidenceRef: Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs -RouteId es.aiwarnings.arch.ai-content-identity-gamecorekey-migration
Owner: ES Architecture/GameCore
StaleWhen: ConfigKey/Catalog/RuntimeData/Provider、Action/Skill/Weapon 身份合同或迁移格式变化。
Knowledge: es.aiwarning.arch.ai-content-identity-gamecorekey-migration.v1

长期约束：
- AI 只产出/选择稳定 Key 与结构化参数；Key 标识可复用、可校验、可生成和可迁移的内容定义，不标识实例状态，也不兼任 Prefab、VFX、AudioClip、Bundle、地址或场景对象。
- Action、Weapon、Skill、Audio 等跨定义引用必须使用各自类型化 ConfigKey；禁止退回显示名、资产名、自由字符串、RuntimeKey、InstanceID、委托或 Unity 对象。
- RuntimeData/Player 通过领域 Table、Catalog 或 Provider 解析为 RuntimeData/Handle；字段存在、空表或占位 DTO 不等于内容链、资源发布或运行消费已完成。
- SkillTrack 是否独立成 Key 必须先裁决复用/版本化/迁移资格；State 不得在裁决前预建第二套身份体系。迁移必须提供旧值到新 Key 的可证明映射和失败策略。
- 不得机械删除作者引用、扩大万能 Behavior/Perception/Targeting Key，或让 RuntimeData 直接持有 Prefab/GameObject/SO 作为 Player 身份。
- 静态结构不能证明 Unity、PlayMode、Player、Profiler、IL2CPP 或发布闭环。
