# AIWarnings 规则索引

本索引是 AIWarnings 的任务路由入口，不是全目录阅读清单。所有任务先读 README 与 CurrentStatus，再按下表选择最小权威规则集；禁止为了“熟悉项目”递归读取全部 AIWarnings。

加载规则：

1. 所有文本或源码修改先读 `10_P0最高约束（P0Guardrails）/编码与文本（Encoding）`。
2. 再读取任务行命中的 P0 与领域专项原文；一个任务命中多行时按领域分批读取。
3. `80_交接与复盘（Handover）` 只在需要直接决策背景、失败复盘或窗口交接时读取相关文件。
4. `90_提案与废止（Archive）` 只在评审对应提案、迁移或废止方向时读取，不能作为已实现事实。
5. 普通任务约 1～2 万字符、复杂跨系统任务约 2～5 万字符只是预算建议；不得为了满足预算跳过命中的 P0、现行状态或专项原文。
6. 跨系统分批摘要必须保留规则路径、状态、结论、禁止事项和证据入口；摘要不能冒充已逐条复核的规则原文。

| 任务 | 必读目录或文档 |
|---|---|
| 修改 GameCore、ConfigKey、RuntimeData | `10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）`、`配置与稳定身份（IdentityConfig）` |
| 新增或修改继承、成员可见性、`new` 成员隐藏、内部入口命名、只读 View、组合包装或程序集收口 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_代码结构规范_Internal前缀与new成员隐藏边界_AI协作警告.md`；禁止仅为了让普通用户不使用某方法而改变代码结构，ES 自有非普通用户入口使用 `Internal_` 前缀 |
| 新增或重命名 Program、Compiler、Runner、Snapshot、Scheduler、Dispatcher、Router、Selector、Policy、Definition、Template、Binding、Table、Registry、Catalog 等架构类型 | `10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md`；`Program` 当前且唯一保留给 `ESBehaviorTreeProgram`，`Scheduler/调度器` 语义由 ES 调度框架独占，普通分支选择或 `ShouldTick` 判断不得占用 |
| ES 自定义泛型容器进入 Unity/Odin 长期序列化字段，或成为成熟大系统长期持有的权威表、稳定公共 API、跨模块缓存或核心生命周期合同 | `10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_长期序列化与成熟核心泛型容器具体类型边界_AI协作警告.md`、`项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md`；保留并复用通用泛型底座，仅长期合同先取得 `sealed` 具体类型；不得误读为删除、禁用或全局替换泛型容器，也不得扩大到局部变量、短期缓存、普通非序列化字段或所有泛型 |
| AI 生成或迁移 GameCore 内容定义、Action、Weapon、Skill、SkillTrack 或跨定义稳定引用 | `10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）`、`20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AI自动化内容身份与GameCoreKey迁移_AI协作警告.md`；涉及资源时再读资源运行时 P0，涉及 Tag 时再读 Buff/Tag 领域规则 |
| 注册普通资产、修改 AssetKey、注册 GameCore、同步 Consumer，或接入 Inspector、资源窗口、MCP、C# 自动化注册入口 | `10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_统一内容注册唯一入口与事务边界_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）`；GameCore 同时读取 GameCore 边界规则 |
| 新增或修改 SoDataInfo、SoDataGroup、SoDataPack、内容库或 Consumer 聚合 | `10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_P0_Info必须对应Group_Pack非默认聚合_AI协作警告.md`、`项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md`；涉及 SO 表格时再读 `40_编辑器与工具（EditorTooling）/SO表格（SOTable）` |
| 修改资源加载、Manifest、AssetBundle、ResourcePlan、Scope Registry 或 `ESAssetDomain` | `10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）`、`50_验证与发布（ValidationRelease）`；默认枚举 Scope 的唯一权威定义位于资源运行时 P0 的“ESAssetDomain 权威语义”章节 |
| 修改 UI 图标、SpriteAtlas、`ESDynamicAtlasGraphic` 或运行时纹理分流 | `30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md`；涉及资源加载、Provider、ResourcePlan 或发布时再读资源运行时 P0 与 `50_验证与发布（ValidationRelease）` |
| 修改编辑器初始化、扫描、预览、窗口或任何用户交付入口 | `10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）`、`40_编辑器与工具（EditorTooling）`；生成报告、日志、配置、快照、审计或交接产物时必读 `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/生成产物快速打开入口_AI协作警告.md` |
| 新增或修改 EditorWindow、EditorGUI、GUIStyle、工具栏、菜单、命令面板或编辑器扩展 UI | `10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）`、`40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`（Project Authority / P0，优先于普通 UI 建议和未验收历史方案）；涉及 Unity 菜单路径、分类、快捷投影或菜单引用迁移时同时读取 `P2_编辑器菜单根必须使用【ES】_AI协作警告.md` |
| 修改 URP Shader/HLSL、ShaderGUI、材质 Inspector、材质预设、Shader Variant 或 `MaterialPropertyBlock` 写入 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ESCompositeShader_URP职责与材质检查器验收边界_AI协作警告.md`、同目录 `编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md`；涉及 Shader 编译、Console、Domain Reload 或视觉效果结论时同时读取 AI 交付声明 P0 与 `50_验证与发布（ValidationRelease）` |
| 修改 `ESWorkbenchContributionRegistry`、模块模板/裁剪/排序、工作台会话注入释放，或 Unity Terrain 正式输出后端 | `40_编辑器与工具（EditorTooling）/专业工作台（Workbench）/专业工作台与World作者工具_贡献注册与正式资产边界_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`；同时读取 EditorLifecycle 与编辑器扩展 AI 常识，涉及 UI Toolkit 作者布局/Draft/Undo 时再读本索引的 UGC Workbench 行，涉及资源收集/发布时再读资源运行时 P0 和 `50_验证与发布（ValidationRelease）` |
| 修改 ES 窗口半休眠、父子窗口关系、临时 Inspector、预览附属窗口或 `ESWindowFoundation.SetSleepOwner` | `10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）`、`40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` 第 11.10 节；必须执行显式 owner、稳定 ownerKey、PendingFollowOwner、关闭解绑和 Domain Reload 恢复规则 |
| 修改 `ESWindowActionHosts`、系统/全局/窗口/页面动作、标题栏动作槽位或休眠按钮注入 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` 第 11.11 节；标准基类创建宿主，派生窗口只追加或按显式契约隐藏，自定义标题栏显式传入宿主，缺少宿主时禁止绝对定位覆盖式注入 |
| 嵌入、修补或升级第三方 Unity 编辑器包，处理第三方 `[MenuItem]` 注册、PackageCache/Git 来源或依赖冻结 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/第三方编辑器包嵌入与菜单补丁_AI协作警告.md`；仍需按编辑器初始化、菜单路径和 UTF-8 规则完成对应验证 |
| 修改 `ESEditorPresentation`、全局 Editor 皮肤、`EditorStyles` 染色、全局 USS 或 ES 品牌字体 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` 第 12 节；同时读取 `10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）`，保持增量刷新、可逆恢复、纹理预算、中文字体范围和 Editor-only 分发边界 |
| Developer Cockpit、Developer Trace、Observation Run、开发时间线、因果链诊断、工作区恢复、实验运行或证据导出 | `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESDeveloperCockpit_ArchitectureContract.md`；该文件是现行设计契约，不是已实现事实。涉及 Runtime 公共协议时再读公共协议分层 P0，涉及 Editor 生命周期或证据产物时再读对应 EditorLifecycle 与生成产物规则 |
| 修改 Entity、角色、输入、控制或世界系统 | `20_架构现状（Architecture）/Entity与世界（EntityWorld）`、`输入与交互（InputInteraction）`、`通用架构（GeneralArchitecture）`；涉及角色 Prefab、DataInfo、挂点、武器或模板时必须先读 `角色Prefab职责与DataInfo入口_AI协作警告.md` 与 `Documentation/CHARACTER_PREFAB_CONTRACT.md` |
| 推进装备定义、武器 ItemDataInfo、武器槽位、Weapon Prefab、挂点装配或装备运行时消费 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_CombatModule武器定义迁移边界与验收门禁_AI协作警告.md`、`20_架构现状（Architecture）/Entity与世界（EntityWorld）/装备定义与装配推进路线_AI协作说明.md`、`角色Prefab职责与DataInfo入口_AI协作警告.md`；涉及 Shot/资源/输入时再读对应专项 |
| 修改 ContextPool、ContextValue 或 ContextOperation | `20_架构现状（Architecture）/通用架构（GeneralArchitecture）/Contextitecture上下文系统_所有权生命周期与类型边界_AI协作警告.md` |
| 修改 ESCommandPlayer、Runner、虚拟输入命令或 RuntimeMode 命令 | `30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md`、`Operation默认无Stop_AI协作警告.md` |
| 修改 ESInteractable、Entity 交互模块、IK 交互写入或 Tag Zone | `20_架构现状（Architecture）/输入与交互（InputInteraction）/交互运行时_Interactable占用生命周期与结束原因_AI协作警告.md`、`输入与交互入口_AI协作警告.md` |
| 修改 StateMachine、FinalIK 或 Buff 表现 | `20_架构现状（Architecture）/状态机与IK（StateIK）`、`10_P0最高约束（P0Guardrails）/总体架构（Architecture）` |
| 新增或修改请求仲裁、镜头、控制权、UI 焦点或音频抢占 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md`；再阅读对应领域现状文档 |
| 编写或修改具体业务逻辑、角色行为、AICommand、输入、相机、交互、视觉表现或性能，并需要判断是否真正可用 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md`；必须补齐真实操作、表现、性能和运行验收证据 |
| 任何 AI 交付、完成声明、可用性判断、未验证项披露或证据等级判定 | `10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`；必须区分源码、静态编译、Unity 实机、运行时和发布证据，主动报告未尽责任与阻断原因 |
| 修改 PrimeTween、DOTween、Tween Sequence 或其迁移与生命周期 | `10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/PrimeTween_DOTween_迁移_P0_AI协作警告.md`；涉及角色权威运动时再读 Entity 与实际可玩闭环 P0 |
| 修改音频、AudioCue、AudioSource、Voice、音频资源或音频抢占 | `20_架构现状（Architecture）/音频（Audio）/音频播放与资源边界_AI协作警告.md`；涉及请求抢占时再读上一行的 P0；涉及资源加载时再读资源运行时 P0 与 `50_验证与发布（ValidationRelease）` |
| 修改 VFX、`ESVfxInfo`、`ESVfxGroup`、`ESVfxModule`、ParticleSystem/VFX Graph 后端、特效事件、Exposed Property、完成判定或预算 | `30_运行时专项（RuntimeOperations）/特效（VFX）/VFX运行时与制作边界_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md`；涉及 GameCore/稳定 Key、资源、Pool 或表现编排时再读对应 GameCore、IdentityConfig、RuntimeAssets、Pool 与 State/IK 规则 |
| 修改 Buff、Tag、ValueChange、Permit | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_Codex核心上下文总纲_状态机IK标签调度LOD_AI协作警告.md`、`20_架构现状（Architecture）/通用架构（GeneralArchitecture）`、`20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/Buff职责边界_被动持续机制_AI协作警告.md`、`20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/属性数值与ValueChange边界_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）` |
| 修改 Pool、Item、Shot、运动或物理 | `30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md`、`Documentation/ES_GENERIC_LIFE.md`；涉及 Pool 回调命名和 Extension 注入时再读 `10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` |
| 新增、修改或迁移 XxxProfile、Profile Extension、Profile 生命周期转发、可选 Runtime Data、Profile Workbench、Prefab/场景能力装配或 Profile 池化接线 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_Profile装配权威_Feature目录与池化边界_AI协作警告.md`；涉及领域时再读对应 Audio、Camera、Entity、Pool、ResourcePlan 或 SoDataInfo 规则 |
| 修改 Skill Track、Operation 或其 Start/Stop 生命周期 | `30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md`、`Documentation/SKILL_OPERATION_LIFECYCLE.md` |
| 修改 AssetPackage 烘焙、分析、预览、固定分类路径、增量导出、链路修正、Resolution Snapshot、事务或回退 | `40_编辑器与工具（EditorTooling）/资产包分离（AssetPackage）/资产包分离窗口_预览与导出链路_AI协作警告.md`、`40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/编辑器预览系统生命周期_AI协作警告.md`；涉及依赖收集、运行时资源或发布时再读资源运行时 P0 与 `50_验证与发布（ValidationRelease）` |
| 修改 UGC Workbench、World 作者工作台、资源/层级/视口/Inspector 布局、拖放、锁定、Draft、Undo、外部漂移或事务提交 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md`、同目录 `编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`；涉及 World 地图、TerrainData、正式 Scene/Prefab 时严格执行交付契约第六节的五层权威对象门禁 |
| 修改 SO 表格或 SimpleTools | `40_编辑器与工具（EditorTooling）` |
| 修改 ESEditorSection、多态引用 Drawer、类型目录、PropertyTree 或序列化迁移 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ES编辑器绘制与序列化套件_PropertyTree多目标与迁移边界_AI协作警告.md` |
| 新增、迁移或评审 public interface、公共协议、Attribute、Drawer 共用契约 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_公共协议与元数据声明分层_AI协作警告.md`；涉及 Drawer 或序列化展示时再读上一行编辑器专项 |
| 新增、迁移或调用 ESDialog、跨宿主对话框、Editor/Runtime Presenter 或对话框 Host 注册 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ESDialog跨宿主唯一合同与Presenter注册边界_AI协作警告.md`；Editor 侧使用 `ESAdvancedDialog` 时同时读取 `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAdvancedDialog通用编辑器输入边界_AI协作警告.md`，Editor 初始化还须读取 EditorLifecycle 的 AssemblyStream P0，Runtime 注册禁止恢复 Runtime AssemblyStream |
| 修改 Stable Graph V2、图资产、`edge.order`、Branch/FanOut/Join、Agent 产物图、AISkill 执行图、消费者专属产物、BehaviorTree Program、Story Definition Snapshot，或评估恢复 Legacy Graph/NodeRunner | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/GraphView与NodeRunner_数据权威稳定身份与重构门禁_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md`；涉及 TaskContract、RunRecord、取消或恢复时再读 `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md`；Legacy 已删除，禁止恢复旧可变 SO Runner 方案 |
| 修改 Agent Authoring Graph、AICommand/AISkill 候选生成、AISkill 持久化执行、TaskContract、父子 Run、RunRecord、取消或恢复 | 上一行 Graph 专项规则、`20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md`、`AgentSkills与AICommands协作边界_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`；候选生成只能走 `$es-generate-agent-artifacts` 与人工 Diff Review/批准，源码存在不得冒充真实运行验收 |
| 新建或改造测试场景的操作引导、验收路线、运行态诊断、键位说明或区域导视 | `50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md`、`Documentation/ES_SCENE_VALIDATION_GUIDE_STANDARD.md`；优先复用 `ESSceneValidationGuide`，不得新建一次性 OnGUI 或污染正式 Prefab |
| 刷新测试场景、核查 Prefab override、处理构建器与场景不一致或归档变更前备份 | `50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md`；构建器是场景布局权威，备份统一归档到 `ES/Bak/Local` 或 `ES/Bak/Reviewed` |
| 进行发布、IL2CPP、性能或资源生命周期验收 | `50_验证与发布（ValidationRelease）`、`10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）` |
| 用户说“审计”“审计并记录”或“继续审计”；判断模块未开始、开发中、待集成、待验收、稳定、废弃或归档；审计半成品渗透 | `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/模块成熟度与未完成实现治理_AI协作警告.md`、`Assets/Plugins/ES/AICommands/检查_模块成熟度与半成品影响_AI命令.md`、`ES/Documentation/Status/MODULE_AUDIT_STATE.md`；再按目标模块读取对应 P0、领域专项和当前源码 |
| 选择、执行或维护 AICommand；新增、修改或调用 Agent Skill | `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md`、`Assets/Plugins/ES/AICommands/README.md`；修改 Skill 时同时读取 UTF-8 P0，涉及 Unity 验收时再读 `50_验证与发布（ValidationRelease）` |
| 根据 Agent Authoring Graph 生成 AICommand 或 Agent Skill 候选包 | `$es-generate-agent-artifacts`；产物只能写入隔离候选目录，仍须人工 Diff Review 与明确批准 |
| 启动、监控或中断 ESTEST / ESAITest | `$es-start-estest` |
| 向运行中的测试 AI 投递一次性提示 | `$es-publish-aitest-prompt` |
| 用户说“交接一下”“直接交接”“准备交接”“生成交接文案”“交给新窗口”“让新 AI 接手”“交接后关闭当前窗口”；或定位 Codex session、恢复失联窗口、维护 AI 协作历程、完成模块审计工作流、评估治理商业可行性 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_AI协作历程与本地Session兜底恢复_AI协作警告.md` 第 3.1 节、`50_验证与发布（ValidationRelease）/AI协作治理验收（AICollaborationAcceptance）/AI协作历程与模块审计_商业可行性验收标准.md`、`ES/AI协作历程（Codex）/README.md`；真实交接必须优先调用 `ES/AI协作历程（Codex）/Tools/Complete-ESCodexHandoff.ps1`，禁止用普通聊天总结静默替代 |
| 打开新 Codex、开启新对话、恢复/分叉会话、初始化 Codex 或接手项目 | `$es-codex-session-bootstrap`；恢复历史时再读取上一行的 session 恢复 P0，普通新会话只加载开始链与任务命中规则 |
| 新建自动化任务、Python/PowerShell Worker、发布物审计、上传、清理或发布门禁 | `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md`、`Documentation/ES_AUTOMATION_CENTER_STANDARD.md`；先检查 C# Editor 注册和任务合同，禁止先写散落脚本 |

## 领域 Skill 快速路由

| 任务 | 推荐 Skill |
|---|---|
| GameCore 根 SO、RuntimeData、全局索引、GameManager 模块 | `$es-gamecore-integration` |
| AssetLibrary、ResourcePlan、Manifest、Provider、Scope、资源发布 | `$es-resource-pipeline` |
| ESGameTag、ESTag、ConfigKey、Catalog、稳定身份 | `$es-tag-config` |
| Entity、角色 Prefab、DataInfo、部件、运动、池化生命周期 | `$es-entity-authoring` |
| 输入动作、绑定、Profile、RuntimeMode、玩家控制链 | `$es-input-action` |
| ESCommand、分类、Context、Player、Runner、Start/Stop | `$es-command-authoring` |
| EditorWindow、Drawer、ESEditorSection、SO 表格、ReloadDomain | `$es-editor-tooling` |
| ShaderGUI、材质 Inspector、材质预设与编辑器绘制 | `$es-editor-tooling` |
| Shader 导入/编译、Unity Console、Domain Reload 与错误证据 | `$es-unity-compile` |
| Unity 编译、测试、Profiler、Player、IL2CPP、资源发布验收 | `$es-release-acceptance` |
| “审计”“审计并记录”“继续审计”、未开始/半成品模块状态、依赖渗透、成熟度跃迁与续接检查点 | `$es-module-lifecycle` |
| 新建、恢复、分叉或初始化 Codex 项目会话 | `$es-codex-session-bootstrap` |

领域 Skill 只负责执行工作流和导航，仍必须按上表读取对应 AIWarnings，并由用户要求与唯一选中的 AICommand 决定本次权限。

`80_交接与复盘（Handover）` 用于补充背景；`90_提案与废止（Archive）` 中的文件不能作为新增实现的唯一依据。
