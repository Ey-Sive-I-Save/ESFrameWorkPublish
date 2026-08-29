# 配置双键与 Inspector 分层：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.config-dual-key-inspector-layer.v1`  
`Authority`: `AIWarnings` 与当前 ConfigKey/Inspector/RuntimeKey 合同  
`RouteKeys`: `aiwarnings`, `p0`, `identity`, `config-key`, `enum-key`, `string-key`, `inspector`, `runtime-key`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `0622f9e35e9447f77e18ee956a78b656f38ea6b8a832709018b4f64a893b047e`  
`SourceSetHash`: `0622f9e35e9447f77e18ee956a78b656f38ea6b8a832709018b4f64a893b047e`  
`EntryBodyHash`: `7e25e35dd75136487d229153dea096c2b5b2a0c091ee3807640ab849f0c6276a`  
`StaleWhen`: ConfigKey/RuntimeKey、Inspector 分类、热路径规则或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留双键选择、Inspector 分层与热路径边界；本条目承载示例、身份语义、转换时机和禁止误区。Knowledge 仅导航，不授予配置写入或运行时修改权限。

## 双键与展示

枚举键具备强类型和编译期检查，适合核心高频对象；字符串键适合扩展、热更新、外部表格和非核心低频配置。两者可共享 Inspector 的 `分类/名称` 展示，例如 `[InspectorName("控制/冰冻")]`，但展示路径不是运行时最高身份，也不应默认变成 `Buff.控制.冰冻` 运行时字符串路径。

核心默认类型包括 `ESGameTag : ushort`、`ESBuffKey : ushort`、`ESSkillKey : ushort`、`ESStateKey : ushort`。扩展字符串（如 `"控制/冰冻"`）必须在编辑器、烘焙或初始化阶段转换为缓存 Key，不能在热路径转换。

## 三种身份边界

`ESBuffKey` 表示 Buff 配置是谁；`ESGameTag` 表示实体当前拥有什么状态事实；`RuntimeKey` 表示当前进程对应强类型 AssetTable 的运行索引，由 ConfigKey 解析得到。RuntimeKey 必须与 AssetKind/EnumType 一起解释，不能把裸 int 当作跨资产类型或跨进程身份。三者可协作但不可混成一个身份。

## 禁止与推荐运行时

禁止为 Inspector 分层强造多层类、多层资产或多层字典；禁止高频 Buff/Tag/State 查询依赖字符串；禁止在 Update、KCC、StateMachine Evaluate、IK 求解或 Buff Tick 中做字符串查找/转 Key；禁止混淆枚举中文名、Inspector 显示名、RuntimeKey 与 GameTag。推荐运行时直接使用 `HasGameTag(ESGameTag.控制类_眩晕)`、`HasBuff(ESBuffKey.控制类_眩晕)` 等强类型调用，启动后才可缓存当前表 RuntimeKey。

## 原文快照

迁移前台账快照：117 行、3287 字节，原始 SHA-256 `4bad90b1d4ae5f12b9de612c47457887dbea27565e0feb18610807e415fed3d5`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_配置双键与Inspector分层_AI协作警告.md` (`de5f1baf93a2c98a186d2c323846bc9d1b2028e5cb5d09511b554343dfe81dd8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`1db86389d4c7786b508fd116ac76d5e70e0abc33445f94e79ed009689438e8b7`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-config-dual-key-inspector-layer.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_配置双键与Inspector分层_AI协作警告.md`
