# AIWarnings 全量迁移覆盖与整改需求

`KnowledgeId`: `es.aiwarnings.full-coverage.v1`  
`Authority`: `AIWarnings + current project source`  
`RouteKeys`: `aiwarnings`, `migration`, `coverage`, `p0`, `architecture`, `runtime`, `editor`, `validation`, `handover`, `archive`, `knowledge`, `evidence`, `stale`, `authority`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `c0d1c03a15b9ac32bb817613d682685cc1061b6ad4a26752d492d8c5b905782f`  
`SourceSetHash`: `c0d1c03a15b9ac32bb817613d682685cc1061b6ad4a26752d492d8c5b905782f`  
`EntryBodyHash`: `3698e916d94a986ea2f806e2add8028b2f82b72b6566f16e56769931d0e3ecce`  
`StaleWhen`: 任一 AIWarnings Markdown、Start 链、RuleIndex、RouteCatalog、KnowledgeIndex、迁移验证脚本或 SourceRef 哈希变化。

## 目的

本条目是 AIWarnings 全量迁移的聚合覆盖入口。它不取代任何 Warning、源码或用户授权；详细长期约束仍以对应 AIWarnings 原文为权威，Knowledge 仅提供可追溯导航、迁移要求和证据边界。

## 完整整改需求

- 保留 AIWarnings 的长期约束、P0、禁止事项、权限边界和证据边界。
- Knowledge 承载详细事实、历史迁移、导航和 SourceRefs，但不得取代 AIWarnings、源码或用户授权。
- 每条 Warning 检查 StableId、Authority、RouteKeys、EvidenceRef、StaleWhen、Knowledge 指针和机器路由闭环。
- RouteKeys 使用英文机器键；中文输入通过别名和路由解析器进入。
- RouteCatalog 与 GeneratedInventory 都是派生投影，必须回读 Markdown 和当前源码。
- `lastModifiedDate` 只表示哈希/文件系统变化，不等于语义复核日期；`ageDays > 7` 只是 stale 信号。
- 静态验证不得升级为 Unity、Runtime、PlayMode、Profiler、Player、IL2CPP 或发布通过。

## 覆盖范围

当前 AIWarnings 共 95 份 Markdown 文件；以下 SourceRefs 为本次全量覆盖基线。

## 对象级迁移审计

审计口径：逐文件枚举当前 AIWarnings Markdown，与本条目的 SourceRefs 做路径和规范化 SHA-256 双向比对；再对照 Start/RuleIndex 和 AIWarningsRouteCatalog 检查可达性。该审计不把自由格式旧 Warning 强行改写成不存在的元数据协议。

- 文件覆盖：95/95，缺失 0，哈希不一致 0。
- P0 覆盖：29/29，均保留原 Warning 作为约束权威。
- RouteCatalog 直接 `mustRead`：12 份；其余 83 份由 Start/RuleIndex 的领域路由进入，RouteCatalog 仍是增量机器投影，不替代人工规则索引。
- 语义状态：旧 Warning 的 StableId、Authority、EvidenceRef、StaleWhen 等字段并非统一格式；本批不伪造字段，语义复核以原文、当前源码和后续任务证据为准，当前声明为 `semantic-review-unproven`。

Knowledge 细节覆盖分布（排除本聚合条目）：40/95 份 Warning 已被至少一个独立 Knowledge 条目引用；55/95 份目前只有本聚合条目的来源绑定。后者不是事实缺失：任务命中时仍必须回读对应 Warning 原文，再决定是否创建专门 Knowledge 条目；聚合条目不把它们伪装成已完成的详细语义提炼。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`c1fc2f3dd03713d0bedf4c12c4e95190613033af55cc28eb79b075976501c31b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`2aa56abe81352fd79ad59b1364ffa7381d70b26674a1676b8439173a515d9b6c`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md` (`af48ba0543b77cfcd97bd9515576f06d7e09c41038bf5094f18c1ca167e0bcd6`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md` (`156190de624ca1df4cdbdbebc41076ecef47b0cc8da7f83f0624537db7c588a7`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_P0_Info必须对应Group_Pack非默认聚合_AI协作警告.md` (`18c53d9c66bf892b1dcad0a8c7d24268fe6461224ab0ac6218322559ebdf2ba4`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md` (`1ff7130253a32b13220afde5099c89060c255f8181670065902fcdeb99a44478`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_P0_编辑器交付体验与下一步可发现性_AI协作警告.md` (`7d08260bc02b1839a812196e0f108fb13476dcd3d2b03f2b4f7b5972d52d623f`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md` (`b7c986c498ce3f25a03afdd3c5dbd684e5913382f45d49012d3eae5195ccad28`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编码与文本（Encoding）/项目最高警告_P0_UTF8唯一编码_禁止AI默认代码页覆写与机械转码_AI协作警告.md` (`2ce3e5d9368f286204014c308d3890b7a0705f8efeae04f070658d710dc3a9e0`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）/项目最高警告_IL2CPP工具链注册_禁止以编译器文件存在代替Unity可检测_AI协作警告.md` (`b2e6c750f781806c676b62922b7ba351e4efe1e83e03888535e62344ec68b4fc`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` (`40d6e8f476a7a9246af75b35f48573c2769d8ad5b4a699305f605b3abf93905a`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_统一内容注册唯一入口与事务边界_AI协作警告.md` (`93365ba8696696d492931a3376b3aee5877e27b93b48bf4ea8944ab9343ca9eb`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md` (`47ceca5ccf3d9dd967c6668c052fd09059f3f76d82bf65f013463b372d54b5a2`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_长期序列化与成熟核心泛型容器具体类型边界_AI协作警告.md` (`4be8a11146dcaf71a308f98e5ac946e9f068d8898ab586b42dbf9e4bec35732d`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_配置双键与Inspector分层_AI协作警告.md` (`de5f1baf93a2c98a186d2c323846bc9d1b2028e5cb5d09511b554343dfe81dd8`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`a6e1424e0d2f4ece7c51869f7cf8e41c5d6e5e9ef5f37a26ccdf258229c0de42`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/PrimeTween_DOTween_迁移_P0_AI协作警告.md` (`ae47d72f6cbd36fb956b1e1a1fbcf610732b1675179c5c52671c7f748ddcda4a`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_ProjectileWeapon热路径冻结合同_初始化预热与无分配门禁_AI协作警告.md` (`38751e2e809d1e885ad2d8d0ffae04b4dc63c155d4c4cfb29044ee927c44d330`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2e9933512b183976b29b712ab0aeb885a17c8b5b14f79417aac380781ae92edc`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md` (`ee23d930bb006f56c6c6517072e556f2f9942368bd5dd8e235c65bb00d390b9a`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源工具链_四阶段严格隔离_AI协作警告.md` (`3ef18687efa69035b1952318581c0f4b4df7c08ac1f69bae8f32c2f3a0107251`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md` (`6ee72697e24d9dc57a3e6bc8c644f72e9b26b979d4a32ef47bbc7c49a895615d`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_Codex核心上下文总纲_状态机IK标签调度LOD_AI协作警告.md` (`01f6ed792f732746ece9d853fda41c131ea289d3a8c114daa445a30ab2427d65`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_AI协作历程与本地Session兜底恢复_AI协作警告.md` (`d6fd3966b7a74d7683509b3a9519278253941913c5ad3665d77ff1e25575f46d`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_CombatModule武器定义迁移边界与验收门禁_AI协作警告.md` (`85dffa1ae38dbfbc0d11ddd53744e229e5cd9e1421bcf7fd5fa6cc199edc105b`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ESDialog跨宿主唯一合同与Presenter注册边界_AI协作警告.md` (`7af8d226be1e85ae8d00f557ff146883e0f228d2051560064e21c103648c844b`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md` (`16daf5464a5c30913b6ceeefd224c7b01d1d0403bf5fe662d588d287e7ae032d`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_Profile装配权威_Feature目录与池化边界_AI协作警告.md` (`965fd0a20b81b7f06bcfb4c0cebca6454197d27aceae8246848eebf9df561ce8`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_代码结构规范_Internal前缀与new成员隐藏边界_AI协作警告.md` (`bbfdf40d223c489ecd39235f65496bcac71b69e89969fc3a4c9ffcaf1b26fb48`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_公共协议与元数据声明分层_AI协作警告.md` (`02eeab93dcac836e2ef0aca604f2f561f8e96976dc3be3c216c45cab88dc3c1d`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md` (`92894b62cf1af0cc26e7ee7d2de31bfd88ad88377b64499f759f721f27621d85`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/Buff职责边界_被动持续机制_AI协作警告.md` (`2baf4921b912a745ad9ff70bc7fdc7632139658bacbb5022c3634a553c0c0a31`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/属性数值与ValueChange边界_AI协作警告.md` (`5ced4eac1aae28b177afad9ff378042d524126de7ba5ee148f66f12b92b6ded5`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色Prefab职责与DataInfo入口_AI协作警告.md` (`4e1a75e52b673a57f10f8a53c2b566c44e60246b9f5bcb03cc8e9bf05d9bb306`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色通用架构验证_MMO开放世界角色切换剧情RPG战斗_AI协作说明.md` (`0e8fac01764793306187df82ed6ca6a18e6c8ebbbd6a7bd55ba8dde2c06d98be`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/模型重构_插件依赖边界_AI协作说明.md` (`a97ad637f95a0af8565a43aa54df53d55cd07616db61b31c40366be31515f6e1`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/模型重构_今日修正_CoreDomain与AI域控制_AI协作警告.md` (`4d167fd7670ba3cb848fb70dcd27dc3b373538d02fd639a32714afbd246426a2`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/玩家对象模型重构_AI协作说明.md` (`4398333388e4f0dff371e014b5f2227126fe3fd2993d3b135da1e20baa76f1de`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/装备定义与装配推进路线_AI协作说明.md` (`91a80a2cd812798de343698ccf54e9b8c36049d2ad60897632893eebe0529121`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/GameManager与存档（GameManagerSave）/架构体系_ESGameManager_SaveSystem_AI协作警告.md` (`5985dece2a14e5c9c6fe9ce66e42a01f43da242b1a70654c7d428b2f4ff69554`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md` (`842bc5d46a045f3e2f226426f005afb8f7114ba56646e623d245ea0f99a04166`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AI自动化内容身份与GameCoreKey迁移_AI协作警告.md` (`efad4b628b85820da047b070b6ac9d5f5c6a8c2c9140b7ebd0674e2fc52ab8f5`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAdvancedDialog通用编辑器输入边界_AI协作警告.md` (`cb8da4bd465c56291bd88c6ec5477a3252666f7bd0b3bbd219a7326b161d1935`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`6f7998bac62c988384030ea434dc1166d0b5fa11c05f880baf6705321ea27485`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESDeveloperCockpit_ArchitectureContract.md` (`90a6a6d3cc442dd7c288d1c8a70ecd1d3f05cc66d9e0813ffe27bec0fe1f248f`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/GameCoreGlobalData与AICommands_AI协作警告.md` (`a7fba2b0d2acc0990c6e2c74dd1d3f3a4a1f88df2537c5e0e0e1d1c47d3aae3a`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/模块成熟度与未完成实现治理_AI协作警告.md` (`b9289ed941f167c16441a89fead23dab77c79e7fd1737c21141f28859b9d8d91`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）/交互运行时_Interactable占用生命周期与结束原因_AI协作警告.md` (`3ef8e2244e8dc1301d31c6ee19e246bb272474304e76c556e0308f62c930ace2`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）/输入与交互入口_AI协作警告.md` (`0a85740e13e2c3f50324ce36c17146861ecf9ed70978687e78c64d9d3085b984`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/Contextitecture上下文系统_所有权生命周期与类型边界_AI协作警告.md` (`e230624a6d6ef6f9c646eb105f051ed9dd416232b1aadac23aa2bb15be10a2e9`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/通用架构理解_跨系统纠偏_AI协作警告.md` (`cd4c04a5bb3cd6e6852f3f0d706fce06bed579e6203ce9cd78d39d1cf79e860d`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/音频（Audio）/音频播放与资源边界_AI协作警告.md` (`9a208ede3cd065ab6d014d79dfaf1950c4cf745e306e9f734f3e7b2259ccb712`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/状态机与IK（StateIK）/AI协作职责_状态机与IK上层_Buff边界说明.md` (`c86832d48e0eefbbeed6cba0fe85ff607cd861e4b3fa1ee05c5ae312ad1ee3fc`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/玩家运动_PlayerMotion_AI协作说明.md` (`723540e47e96cd52678ed7949887e79adae5820fcf328d9f1e83215cb8f903c4`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/载具运动与骑乘职责_VehicleMotion_AI协作警告.md` (`95f6f749012c7cbc73f4475cd94f5d3748b5deba0eef5602fa468b0d3c307ca7`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Item与Shot物理（ItemShotPhysics）/OpTargetPack_Item整合与池化重设_AI协作警告.md` (`50e7e43b3a08f39c38fae5a011cd7f798324a376e2cffac0902ef698341ced5e`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Item与Shot物理（ItemShotPhysics）/物理架构_简单起步_AI协作警告.md` (`1ea45196d6a575cede11bd1f30737f4d1f53112cba03852055fffb306e83aeb9`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Item与Shot物理（ItemShotPhysics）/运动职责_Shot必中与Item运动_AI协作警告.md` (`983548866f65055b193d3d22cc26e6030489762e547c4a448d4650cd904c3947`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md` (`9215f8a2f89b0bcb8e9acda6c03e8a53d13f58a39d7e2c42f80d89cce9859196`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md` (`ea2de85ef1f62d2d43d1910711e449919ebe5ad2d6681ebd72133a72f4427581`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md` (`8c2c59d6d08a738eae2e073c4668f9946214fb0a77470d224ce611a1b52c348c`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md` (`2d81b217e424c9170625025664ee00db2716a8b2071133cdc3dc0e6f4f21f960`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md` (`e831fd0ac59c1840b958dd1a5345beb60f45ffa8e2f83adc2391f64c8a49882f`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/特效（VFX）/VFX运行时与制作边界_AI协作警告.md` (`a6531ed0d60c4e137dad6c18db4481ee50d6f771b9649a5932b61fae887a1118`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/SO表格（SOTable）/SO表格工具_AI协作说明.md` (`65430c28c5a7b968abe4b5bf16aa538f2e13991231831bd86f0ac5aaef2a8129`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ESCompositeShader_URP职责与材质检查器验收边界_AI协作警告.md` (`ee28160bdcf928982f5a743ee9e670c529942f6eb819296bec6eecef2668d004`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ES编辑器绘制与序列化套件_PropertyTree多目标与迁移边界_AI协作警告.md` (`63d3c0c60146e0f89ad75347907c9e30adc6dfe13f0767add81f4dff1449c8d1`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/GraphView与NodeRunner_数据权威稳定身份与重构门禁_AI协作警告.md` (`4a1bde6f96bad3461178fc0385d3e4b26eb7184ea7efc92de3879abb9f042d44`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/P2_编辑器菜单根必须使用【ES】_AI协作警告.md` (`f31ed883e4cdf3e30fb3f375b322e9b6814e83ccdbfee1d8d58e2f379a818dc7`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md` (`5a102b615d65c97d036d8e837a6778a3f61fee73b1b0a50d390db9d303442e52`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器窗口迁移_ESMenuTreeWindowAB适配_AI协作警告.md` (`7278694b4f706ea2bd82c92fc30c0eb9d5db445a35e06784b8961bea7591a488`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`f8b5dd538e5747a9fe5914fa30df168801db051911082dbfc397ddf767a439ce`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/第三方编辑器包嵌入与菜单补丁_AI协作警告.md` (`f0103c753440abc81acf3d722d5f2f5e9772faa555dbd578292a5359f0495fae`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/生成产物快速打开入口_AI协作警告.md` (`d480f3019fe6944fadc580adfa0c79cb9dfbbf26d5e798aecbd8c2a639682140`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/程序集与稳定性（AssemblyStability）/编辑器匿名函数与程序集流稳定性_AI协作警告.md` (`551f6b031775e20b222fe1fd50373a87d6d716ee43aa8494845a49ab5fe05c0a`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/简单工具（SimpleTools）/Codex_工具重写_商业级验证协作上下文.md` (`255af45a5c8a7275636c6e273b253328859b7e63b1b1cdcbe22c44cae9a4b84f`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/编辑器预览系统生命周期_AI协作警告.md` (`da41da7f309bdc11783f6febe99ae824cfb7858ebcb09f12ea797e3b5a9bddfc`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/内存泄露与编辑器生命周期_AI协作警告.md` (`8b867e93038fcf467efdf81f6803487e2e20ae4e23082c725ca2e195b9b7c95e`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/专业工作台（Workbench）/专业工作台与World作者工具_贡献注册与正式资产边界_AI协作警告.md` (`d2b7808cec44ced0c899fe393e5f283a3e2063d01098447811bff1689a868b2d`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/资产包分离（AssetPackage）/资产包分离窗口_预览与导出链路_AI协作警告.md` (`ff0539d8769216b873190dfbba402e5a933524fb0d355969a65178f656e3d9aa`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/AI协作治理验收（AICollaborationAcceptance）/AI协作历程与模块审计_商业可行性验收标准.md` (`cac7f1746d29499373d6d715689ffbcc484b9313685ba2de61370c8f3d970558`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/ResourcePlan扩展协议_强制约束.md` (`8a1d85e8542b86e9b99608084e7ffcff7b335180431bc34edd04b809900112de`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md` (`a55d464d511718c8e7f3024e75fbd14d34037ae9a6d9a35423ca0f61a6845e8e`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md` (`55533cb848d5153ef9a62c9e63f6c7fbfcae24544685c79f2b65471ba53c7556`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/资源计划验收（ResourcePlanAcceptance）/资源计划_Scope生命周期绑定_商业项目验收标准.md` (`27962ad8eb6b2674e1b759448708afc316b913fcf20945b80a83fc44111b5acf`)
- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/历史上下文（HistoricalContext）/2026-08-16_当前状态快照_活跃索引迁移前.md` (`3c5ae1c332ea7bcd148f4eb65cc033a97408304938f23072dfc725929f31c755`)
- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/历史上下文（HistoricalContext）/AIPersonas与AI顶级目录边界_AI协作警告.md` (`f366285980c1fcbd1e1c282cec96dc2b75d07b940ac9faad1e022b4f46937abb`)
- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/历史上下文（HistoricalContext）/资源加载底层_RuntimeKey稳定与旧输入坏引用_给其他AI阅读.md` (`b41596dc1a7df77ca7a384a0937f3d8389022b7757202e60a50a6ea9e3420d14`)
- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/失败复盘（Retrospectives）/ESCmdAgent_失败复盘与后续禁止事项_AI协作警告.md` (`1c4e98300cab7531038f48e062dc3b1f1973aaa8f3144a4167f18bf2a63cbe41`)
- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/项目总交接（ProjectHandover）/Codex_当前项目总交接_模型重构与编辑器稳定_AI协作警告.md` (`3e8beeaf530b5ca541491f37a35bab85f991b3fa3e0efd9a6c8212dc1f8671d1`)
- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/Entity固有Tag_DataInfo权威_Prefab入口与池化闭环_提案.md` (`bf8d9667929ce892e31f82f4cccfcd69f418199f6e0db505313ba360ae163ee4`)
- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/UnityMCP_AI工程验收代理与自动化能力路线图_预备案提案.md` (`368b625bad7e7dee513f5fedcc1eac22c8bc9b389f953d32492a45b47ad609cb`)
- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/音频系统_双后端与发布资源生命周期_待验收提案.md` (`68aaf9f2d9318bd72fe7ae93b50afda43e308c1835ed06be1e9b9626177ea0a4`)
- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/资源计划_Scope生命周期绑定_待验收提案.md` (`26b9879ca154921479a2e6f6574a932f8b6beef4d519c567efc59c7e5d50257f`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/AIWarningsRouteCatalog.json` (`f340924035d800f3b485a75f868ed9184bbe00634cb624e2d09f986536ae12d3`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`67794270442817648d4894f45766bf83d44aabc25e06f944f96717eda2462ddc`)

## RequiredReads

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/AIWarningsRouteCatalog.json`
- `Documentation/AIKnowledge/AIWarningsDomainInventory.yaml`
- `Documentation/AIKnowledge/AIWarningsGeneratedInventory.json`
- `Documentation/AIKnowledge/entries/aiwarnings-domain-map.md`
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json`
- `.agents/skills/es-aiwarning-authoring/SKILL.md`
- `.agents/skills/es-ai-knowledge-curation/SKILL.md`
- `.agents/skills/es-knowledge-validator/SKILL.md`
- `.agents/skills/es-aibrain-route-authoring/SKILL.md`
- `.agents/skills/es-utf8-guard/SKILL.md`
- `.agents/skills/es-worktree-audit/SKILL.md`

## 非声明

- 本条目不声称所有 Warning 的语义已经完成逐条人工复核。
- 本条目不声称 Unity、Runtime、Profiler、Player、IL2CPP、网络或发布行为已验证。
- 任何 SourceRef 漂移都使本条目及其依赖计划 stale，必须回读当前来源并重新计算哈希。
