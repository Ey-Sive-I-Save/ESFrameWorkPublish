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
| 新增或修改 SoDataInfo、SoDataGroup、SoDataPack、内容库或 Consumer 聚合 | `10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_P0_Info必须对应Group_Pack非默认聚合_AI协作警告.md`、`项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md`；涉及 SO 表格时再读 `40_编辑器与工具（EditorTooling）/SO表格（SOTable）` |
| 修改资源加载、Manifest、AssetBundle、ResourcePlan、Scope Registry 或 `ESAssetDomain` | `10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）`、`50_验证与发布（ValidationRelease）`；默认枚举 Scope 的唯一权威定义位于资源运行时 P0 的“ESAssetDomain 权威语义”章节 |
| 修改编辑器初始化、扫描、预览、窗口或任何用户交付入口 | `10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）`、`40_编辑器与工具（EditorTooling）`；生成报告、日志、配置、快照、审计或交接产物时必读 `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/生成产物快速打开入口_AI协作警告.md` |
| 修改 Entity、角色、输入、控制或世界系统 | `20_架构现状（Architecture）/Entity与世界（EntityWorld）`、`输入与交互（InputInteraction）`、`通用架构（GeneralArchitecture）`；涉及角色 Prefab、DataInfo、挂点、武器或模板时必须先读 `角色Prefab职责与DataInfo入口_AI协作警告.md` 与 `Documentation/CHARACTER_PREFAB_CONTRACT.md` |
| 修改 ContextPool、ContextValue 或 ContextOperation | `20_架构现状（Architecture）/通用架构（GeneralArchitecture）/Contextitecture上下文系统_所有权生命周期与类型边界_AI协作警告.md` |
| 修改 ESCommandPlayer、Runner、虚拟输入命令或 RuntimeMode 命令 | `30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md`、`Operation默认无Stop_AI协作警告.md` |
| 修改 ESInteractable、Entity 交互模块、IK 交互写入或 Tag Zone | `20_架构现状（Architecture）/输入与交互（InputInteraction）/交互运行时_Interactable占用生命周期与结束原因_AI协作警告.md`、`输入与交互入口_AI协作警告.md` |
| 修改 StateMachine、FinalIK 或 Buff 表现 | `20_架构现状（Architecture）/状态机与IK（StateIK）`、`10_P0最高约束（P0Guardrails）/总体架构（Architecture）` |
| 新增或修改请求仲裁、镜头、控制权、UI 焦点或音频抢占 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md`；再阅读对应领域现状文档 |
| 编写或修改具体业务逻辑、角色行为、AICommand、输入、相机、交互、视觉表现或性能，并需要判断是否真正可用 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md`；必须补齐真实操作、表现、性能和运行验收证据 |
| 修改音频、AudioCue、AudioSource、Voice、音频资源或音频抢占 | `20_架构现状（Architecture）/音频（Audio）/音频播放与资源边界_AI协作警告.md`；涉及请求抢占时再读上一行的 P0；涉及资源加载时再读资源运行时 P0 与 `50_验证与发布（ValidationRelease）` |
| 修改 Buff、Tag、ValueChange、Permit | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_Codex核心上下文总纲_状态机IK标签调度LOD_AI协作警告.md`、`20_架构现状（Architecture）/通用架构（GeneralArchitecture）`、`20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/Buff职责边界_被动持续机制_AI协作警告.md`、`20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/属性数值与ValueChange边界_AI协作警告.md`、`10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）` |
| 修改 Pool、Item、Shot、运动或物理 | `30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md`、`Documentation/ES_GENERIC_LIFE.md`；涉及 Pool 回调命名和 Extension 注入时再读 `10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` |
| 新增、修改或迁移 XxxProfile、Profile Extension、Profile 生命周期转发、可选 Runtime Data、Profile Workbench、Prefab/场景能力装配或 Profile 池化接线 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_Profile装配权威_Feature目录与池化边界_AI协作警告.md`；涉及领域时再读对应 Audio、Camera、Entity、Pool、ResourcePlan 或 SoDataInfo 规则 |
| 修改 Skill Track、Operation 或其 Start/Stop 生命周期 | `30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md`、`Documentation/SKILL_OPERATION_LIFECYCLE.md` |
| 修改 SO 表格、资产包窗口或 SimpleTools | `40_编辑器与工具（EditorTooling）` |
| 修改 ESEditorSection、多态引用 Drawer、类型目录、PropertyTree 或序列化迁移 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ES编辑器绘制与序列化套件_PropertyTree多目标与迁移边界_AI协作警告.md` |
| 新增、迁移或评审 public interface、公共协议、Attribute、Drawer 共用契约 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_公共协议与元数据声明分层_AI协作警告.md`；涉及 Drawer 或序列化展示时再读上一行编辑器专项 |
| 修改 ESGraphView、NodeRunner 或图资产 | `40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/GraphView与NodeRunner_数据权威稳定身份与重构门禁_AI协作警告.md` |
| 新建或改造测试场景的操作引导、验收路线、运行态诊断、键位说明或区域导视 | `50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md`、`Documentation/ES_SCENE_VALIDATION_GUIDE_STANDARD.md`；优先复用 `ESSceneValidationGuide`，不得新建一次性 OnGUI 或污染正式 Prefab |
| 进行发布、IL2CPP、性能或资源生命周期验收 | `50_验证与发布（ValidationRelease）`、`10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）` |
| 用户说“审计”“审计并记录”或“继续审计”；判断模块未开始、开发中、待集成、待验收、稳定、废弃或归档；审计半成品渗透 | `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/模块成熟度与未完成实现治理_AI协作警告.md`、`Assets/Plugins/ES/AICommands/检查_模块成熟度与半成品影响_AI命令.md`、`ES/Documentation/Status/MODULE_AUDIT_STATE.md`；再按目标模块读取对应 P0、领域专项和当前源码 |
| 选择、执行或维护 AICommand；新增、修改或调用 Agent Skill | `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md`、`Assets/Plugins/ES/AICommands/README.md`；修改 Skill 时同时读取 UTF-8 P0，涉及 Unity 验收时再读 `50_验证与发布（ValidationRelease）` |
| 定位 Codex session、恢复失联窗口、维护 AI 协作历程、完成模块审计工作流、评估治理商业可行性或生成跨 AI 交接文案 | `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_AI协作历程与本地Session兜底恢复_AI协作警告.md`、`50_验证与发布（ValidationRelease）/AI协作治理验收（AICollaborationAcceptance）/AI协作历程与模块审计_商业可行性验收标准.md`、`ES/AI协作历程（Codex）/README.md` |
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
| Unity 编译、测试、Profiler、Player、IL2CPP、资源发布验收 | `$es-release-acceptance` |
| “审计”“审计并记录”“继续审计”、未开始/半成品模块状态、依赖渗透、成熟度跃迁与续接检查点 | `$es-module-lifecycle` |
| 新建、恢复、分叉或初始化 Codex 项目会话 | `$es-codex-session-bootstrap` |

领域 Skill 只负责执行工作流和导航，仍必须按上表读取对应 AIWarnings，并由用户要求与唯一选中的 AICommand 决定本次权限。

`80_交接与复盘（Handover）` 用于补充背景；`90_提案与废止（Archive）` 中的文件不能作为新增实现的唯一依据。
