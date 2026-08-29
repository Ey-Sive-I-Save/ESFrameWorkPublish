# 稳定 Key、Catalog 与 RuntimeKey：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.stable-key-catalog-runtimekey.v1`  
`Authority`: `AIWarnings` 原文与当前稳定身份/Catalog 合同  
`RouteKeys`: `aiwarnings`, `p0`, `stable-key`, `catalog`, `runtime-key`, `schema-hash`  
`HashSchema`: `v2`  
`ContentHash`: `5b0b8a79c9c7f58e9072eea8f243e504fe743b5cd1eed053647897eed56d8e7d`  
`SourceSetHash`: `5b0b8a79c9c7f58e9072eea8f243e504fe743b5cd1eed053647897eed56d8e7d`  
`EntryBodyHash`: `f68ac85f6df66d472cccab4dd5826d3f7ffe9729e96fc4753c3590f18aecbcdf`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: Catalog/Schema、RuntimeKey 生命周期、Key 迁移、AI 内容协议或任一 SourceRef 哈希变化。

## 迁移说明

Warning 保留 Scope/StableKey/Catalog/RuntimeKey 的核心身份规则、禁止持久化、确定性构建和 AI 内容准入；本条目承载完整模型、输入档案、当前实现入口、审计要求和原文语义。Knowledge 不授予 Catalog 烘焙或运行时修改权限。

## 详细合同

跨对象、版本、存档、网络、DLC/Mod 的身份使用带 Scope 的 EnumKey/StringKey，经 Catalog 按稳定身份确定性排序生成 RuntimeKey + SchemaHash。RuntimeKey 仅当前进程、当前 Catalog 生命周期有效，不是配置/存档/网络身份；HotSlot/Sparse 只是存储策略。别名须在同 Scope 同定义解析同 RuntimeKey，冲突构建失败。局部 Owner 容器键只有进入外部契约才升级。

禁止持久化或跨进程传 RuntimeKey，禁止依据注册/数组顺序、InstanceID、GUID、路径、Address、Bundle Hash、显示名恢复，禁止热路径裸 StringKey 查表、隐式创建、旧 RuntimeKey 双写/fallback 或跨 Catalog 解释。Key 定义必须声明 Scope、类型、Schema、存储、迁移和 owner；跨版本/云档案/联机导入比较 Catalog 名称、Scope、SchemaHash，不匹配时明确迁移或安全拒绝。

编辑器审计别名、类型、重复声明、未使用项、所有权、跨资产冲突，失败必须 fail-closed；不应把所有 Dictionary<string> 全局 Catalog 化，也不得为没有正式定义/Table/Consumer 的 Targeting/Behavior/Perception 预建万能空 Key。AI 内容只能使用强类型稳定 Key 和结构化参数；RuntimeKey、Handle、InstanceID、委托、自由字符串和裸 Unity 对象不是 AI/Player 权威。正式内容需 Info/Group→GameCore→强类型 RuntimeTable→Consumer，资源另接 AssetKey/ResourcePlan/Provider。

当前入口包括 `ESKeyCatalog`、`ESConfigKeyTable<T>`、`ESTagBakeTable`、`ESSuperAttributeCatalog/Table`、`ESInputSchemeCatalog`、`ESInputActionCatalog` 和稳定 Key 审计菜单。输入档案仅保存 configId、SchemaHash、bindingId、schemeId，不保存 RuntimeKey；不匹配拒绝迁移并使用当前默认键位。资源与 GameCore 的额外 P0 仍同时适用。

## 原文快照

迁移前完整 Warning（84 行、7819 字节）由以下 SourceRef 保留，原始 SHA-256 为 `c6960fac99de98e02d304bca863a312314f065268f54f961f35cf61f68a847c7`。

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md` (`47ceca5ccf3d9dd967c6668c052fd09059f3f76d82bf65f013463b372d54b5a2`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`50ee60a341d12ddf2bceb93df834962da8eb54e53e8e3bfcdddd1c472deed853`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-stable-key-catalog-runtimekey.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
