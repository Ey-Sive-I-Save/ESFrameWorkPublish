# 项目最高警告：配置双键与 Inspector 分层

Status: current
StableId: es.aiwarning.p0.config-dual-key-inspector-layer.v1
Authority: AIWarnings（长期 P0 约束）；详细事实与示例见 Knowledge
RouteKeys: aiwarnings, p0, identity, config-key, enum-key, string-key, inspector, runtime-key
Applicability: Buff、Tag、State、Skill、Item、Camera、Mode 等可配置运行对象
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-config-dual-key-inspector-layer.md
StaleWhen: ConfigKey/RuntimeKey、Inspector 分类、热路径规则或任一 SourceRef 哈希变化。

## P0 长期约束

- 配置层可同时使用枚举键与字符串键：枚举键强类型、可编译期检查，适合核心高频对象；字符串键服务扩展、热更新、外部表格和非核心低频配置。两者都可在 Inspector 显示 `分类/名称`，但显示路径不是运行时身份。
- 核心对象使用强类型 `ESBuffKey`、`ESGameTag`、`ESSkillKey`、`ESStateKey` 等；Inspector 用 `[InspectorName("控制/眩晕")]` 分层展示。字符串如 `"控制/冰冻"` 必须在编辑器/烘焙/初始化阶段转换成缓存 Key。
- BuffKey 表示配置身份，GameTag 表示实体当前事实，RuntimeKey 表示当前进程对应 AssetTable 的运行索引；RuntimeKey 必须与 AssetKind/EnumType 一起解释，不得把裸 int 当跨资产或跨进程身份。
- 禁止把 `Buff.控制.冰冻` 等点号字符串作为核心运行时 Key；禁止为分层展示强造多层类、资产或字典；高频 Buff/Tag/State 查询不得字符串查找或在 Update、KCC、StateMachine Evaluate、IK、Buff Tick 中做字符串转 Key。
- 分类展示交给 Inspector，配置身份交给强类型 ConfigKey；启动后可解析为当前表 RuntimeKey，但字符串不进入高频判断。AI/Player 内容仍受稳定 Key、RuntimeKey 进程边界和相关 GameCore P0 约束。

## Knowledge 导航

完整示例、三种身份协作关系、扩展配置转换时机和原文快照见 `es.aiwarning.p0.config-dual-key-inspector-layer.v1`。本 Warning 不授予配置写入或运行时修改权限。
