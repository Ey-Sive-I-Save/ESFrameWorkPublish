# GameCoreEditorGlobalData 与 AICommands：保真 Knowledge

KnowledgeId: es.aiwarning.arch.gamecore-editor-globaldata-aicommands.v1  
Authority: AIWarnings + current GameCore editor source/asset  
RouteKeys: aiwarnings, architecture, gamecore, aicommand, catalog  
EvidenceLevel: S1  
RuntimeEvidence: runtime-not-run  
HashSchema: v2  
ContentHash: 52aa7e95e9bcd2f5ed77fa2c60f3f5ddcbf3500b29f88666d13cab1fb7b657f9  
SourceSetHash: 52aa7e95e9bcd2f5ed77fa2c60f3f5ddcbf3500b29f88666d13cab1fb7b657f9  
`EntryBodyHash`: `b55ac48ec1a718bd4a24a0dab675d555f05a5ab1caafaecdd79d9a2dd7a3f9a9`
StaleWhen: GameCoreEditorGlobalData、菜单、Catalog/Bake、AICommands 或稳定身份合同变化。

## 迁移范围

Warning 从 155 行、6298 字节压缩为长期权威、权限和导航边界；本条目接纳详细职责、资产/菜单映射、生成限制、行为分层、旧入口迁移和原文事实。Knowledge 不取代编辑器 SO、运行时 Catalog 或用户授权。

## 当前事实

- GameCoreEditorGlobalData 通过 CreateAssetMenu「【ES】/配置/GameCore/编辑器全局数据」作为编辑期唯一配置入口，资产位于 Assets/ESNormalAssets/Data/GlobalData/GameCore/GameCoreEditorGlobalData.asset；菜单类提供打开/创建、推荐规则、GameTag、属性表、Bake、稳定 Key 审计和验证入口。
- 该 SO 集中维护 GameMode、GameModeTag、GameTag、角色/物品 Float/Permit Schema、Input 分类、物理层语义和 AI Command 模板；运行时不直接依赖它，具体 DataInfo/Catalog/Table 仍由各领域拥有。
- fixedApiName 仅允许为角色固定 HotSlot 生成确定性数组访问 API；普通 HotSlot、Sparse、Item 属性和 GameTag 不得被强行代码化。Schema、稳定身份或槽位结构变化才需要生成并等待 Unity 编译。
- 行为分层为编辑器语义/推荐名、领域 BehaviorProfile、Domain Module、单一 Policy/Strategy 和 StateMachine；配置只保存稳定 Key、版本和参数，运行时解析后缓存，热路径禁止字符串查表、反射和按帧创建策略对象。
- AICommands 是开发者复制给 AI 的受管命令模板，不是自动授权或盲写生成器。新增输入、Tag、属性、物理层、Shot 或 GameMode 时必须同时回读源码、资产、菜单、Bake、Catalog、验证和命令合同。

## 禁止与迁移边界

禁止把 StateMachineConfig 挂入编辑器全局数据，禁止保存 System.Type、程序集限定名、委托、RuntimeKey 或场景实例，禁止在业务脚本硬编码 LayerMask、绕过 RuntimeMode 过滤、把 Shot 每发变量写回 ItemDataInfo，禁止恢复旧 GameCoreGlobalData 类型/资产/菜单路径。新增运行时 Behavior Catalog 前必须声明 Domain、StableKey、Bake/校验、缺 Key 失败策略、池化重绑和发布证据；未实施只能标记提案。

## 原文事实摘要

原 Warning 还要求验证旧类型/旧资产路径零命中、确认 GameCoreEditorGlobalData.asset 唯一可定位、执行 GameTag 验证、Catalog Bake 和稳定 Key 审计；这些是验收要求，不代表本条目已经完成 Unity 菜单或发布验证。原迁移前 SHA-256：e1c1014a48e4f743d39a62daa219a2dd8e169fe80f071ea459865e778597b6ce。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/GameCoreGlobalData与AICommands_AI协作警告.md` (`a7fba2b0d2acc0990c6e2c74dd1d3f3a4a1f88df2537c5e0e0e1d1c47d3aae3a`)
- `Assets/Scripts/ESLogic/Runtime/Data/Normal/GameCoreEditorGlobalData.cs` (`ed2cdb9c9f836fc678e6790a87f8c98f21e60754c4679cf74b2981899bb604cd`)
- `Assets/Scripts/ESLogic/Editor/GameCoreEditorGlobalDataMenu.cs` (`e8b459df597b2c071eb591c7c9546e8510ca57c06687562196397baf8080e690`)
- `Assets/ESNormalAssets/Data/GlobalData/GameCore/GameCoreEditorGlobalData.asset` (`def912a30718549067a9e0167b8077771aee003f8687f46769f50ae8646b8405`)
- `Assets/Plugins/ES/AICommands/README.md` (`4af02fd8d89c7e85191027262afb869a6bb1e8e3ca4a362f571758a68a24e651`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-architecture-gamecore-editor-globaldata-aicommands.md`
- Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
- Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
- Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
- Assets/Scripts/ESLogic/Runtime/Data/Normal/GameCoreEditorGlobalData.cs
- Assets/Scripts/ESLogic/Editor/GameCoreEditorGlobalDataMenu.cs
