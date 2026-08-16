# AIWarnings 当前状态

最后核对：2026-08-16。本次已复核 Graph、Story、AISkill、镜像容器、VFX、AssetPackage 与 UGC Workbench 相关段落；其他段落继续以各自日期和证据为准，较早快照不得覆盖较新的局部证据。

## 2026-08-16 ES 编辑器窗口基础层

- 活跃窗口公共外壳已经收敛为 `ESMenuTreeWindow<T>`、`ESSinglePageWindow<T>`、`ESSinglePageIMGUIWindow<T>`、`ESOdinMenuTreeWindow<T>` 与 `ESIndependentInspectorWindow<T>`；已删除的 `ESMenuTreeWindowAB` 只允许出现在迁移历史或“已删除”说明中，不得继续作为现行类型权威。
- 半休眠使用显式形态 `ActivePanel / SleepTile / EdgeTab`；父子窗口使用显式 owner、稳定 `ownerKey`、`PendingFollowOwner` 和 `FollowOwner / OwnedSurface / Independent` 关系。Domain Reload 只恢复稳定身份、持久形态和几何，不恢复拖动、Pointer Capture、Popup、悬停计时、动画进度或活 `EditorWindow` 引用。
- 窗口动作按 System / Global / Window / Page 四层建模。标准 ES 基类负责创建并注册动作宿主，派生窗口只通过受控入口追加或按可重写契约隐藏；自定义标题栏必须显式提供 `ESWindowActionHosts`，禁止基础层猜测任意 Toolbar 或在未知窗口右上角覆盖式注入。
- 当前 `AttachSemiSleepControls` 找不到显式 System 宿主时会直接返回，活跃源码不再创建 `ESWindowSystemActionsFallback`；商业测试也覆盖“无宿主不注入、有宿主才挂载”。这只证明注入边界的源码与测试合同已经收口，不能替代生产窗口覆盖率、窄屏折叠或 Unity 实机手感验收。
- 资产包分离窗口已经接入 `ESMenuTreeWindow<ESAssetPackageBakeWindow>`，状态为“已实现，待实机验收”。本轮只更新 AIWarnings、AICommand 与会话历程，没有取得新的 Unity Compile、Domain Reload、窗口交互、Profiler 或多显示器证据。

## 2026-08-16 ES Composite Shader 与材质检查器

- 当前工作树存在四条职责分离的 URP Shader：`ES2DCompositeURP.shader`、`ES3DLitCompositeURP.shader`、`ES3DVFXCompositeURP.shader` 与 `ESUICompositeURP.shader`；共享 Editor 入口为 `ESCompositeShaderGUI`、`ESCompositeShaderGUI.Productivity` 与 `ESCompositeCodingHelper`。这不是 Built-in/HDRP 通用实现，也不得把其中一类 Shader 的能力自动外推到其他三类。
- 材质检查器源码已包含分组绘制、标准/进阶/高级显隐、属性 C# 示例、预设差异与选择性应用、Undo/多选处理、VFX 顶点流与 URP 深度诊断等入口。模式切换只允许改变显示范围，不能隐式修改材质；父功能关闭时，其子参数不得继续影响最终 Shader 结果。
- Renderer 级实例参数应通过 `MaterialPropertyBlock` 写入；会改变 Keyword、Render Queue、Pass、Blend、Cull、ZWrite 等渲染状态的对象必须使用独立 Material。Unity UI `Graphic` 没有 Renderer PropertyBlock 路径，只能使用受生命周期管理的缓存材质实例，禁止每帧克隆材质。
- 当前准确状态为 `Implemented-Unverified`。本轮只复核源码与规则并修正 AIWarnings，没有重新执行 Unity Shader 导入、Console、Domain Reload、PlayMode、220px/高 DPI Inspector、视觉对比、Profiler、Player 或发布验收；不得把历史编译记录或源码存在描述为本轮新鲜的实机验证。

## 2026-08-13 API 命名治理

- 高频 API 的稳定动词语义、`Submit` 允许边界和动态审查流程已经写入命名 P0；动态候选不再保存在 P0 正文。
- 既有名称按 A/B/C/D 分级：A 为高频可见入口优先整改，B 为公共协议按完整调用链迁移，C 为低频内部名称登记后顺带处理，D 为私有实现、第三方回调、生成代码和历史代码默认不动。
- 前五批低风险源码迁移已经形成：第一批将驾驶输入成对协议统一为 `TrySetDriverInput`、Entity 镜头输入改为 `TrySetCameraLook`、Item Motion 的 Pending Shot Result 入口改为 `SetPendingShotResult`；第二批将 Track 预览采样入口改为 `UpdateClipPreviewState`、Shot 命中层读取改为 `GetShotHitMask`、纹理工具入口改为 `ApplyTextureImportSettings`；第三批将状态机预检结果落地入口改为 `TryApplyStateActivation`；第四批将 Audio 覆盖值读取改为 `GetCategory` / `GetSpatialMode`，并把 Shot Inspector 的内部 `HitResolver` 文案改为“命中判定”；第五批将 MatchTarget Gizmos 帧数据入口改为 `SetFrameData`。均未保留旧名转发包装，也未修改序列化字段或资产 YAML；受 `DOCUMENT_SYNC` 管理的静态 HTML 未机械改写。
- 该批次候选、源码事实、已处理项、证据缺口和潜在迁移门禁见 `ES/Documentation/Status/API_NAMING_REVIEW_20260813.md`。前五批共处理 11 个治理问题，不等同于 11 个唯一 C# 旧符号。2026-08-13 当时，`ES_Design.csproj` 的 `dotnet-build` 已通过；`ES_Logic` 与依赖它的测试生成工程被该工作树快照中的 66 个既有 Motion Influence、VFX、Enum/String Mirror Map 与 Transform Mapping Conflict 缺失类型错误阻断，失败列表未发现该批精确旧 API 对应错误。当时也没有 Unity Editor、ReloadDomain、EditMode 或 PlayMode 证据。以上只保留为命名治理历史快照，不得描述为当前工作树状态；Mirror Map、Transform Mapping 与装备链的后续证据见本文件 2026-08-16 段落。
- UTF-8、`git diff --check`、静态编译与会话上下文验收只证明各自范围，不作为候选正确性或命名治理验收证据。

## 2026-08-13 ES Unity 菜单信息架构迁移

- 现行架构决策已定为六个可见顶部一级入口：`常用窗口`、`内容制作`、`项目配置`、`资源与发布`、`验证与诊断`、`自动化与开发`；其中“常用窗口”只是打开正式窗口的快捷投影。
- `Assets/Create/【ES】` 按资产类型使用“内容、配置、资源管线、示例”；`Add Component/【ES】` 按组件能力使用“基础设施、角色与交互、相机与表现、UI、资源、开发与验证”。三棵菜单不得共用一级分类。
- 公共路径常量、正式 C# 菜单入口、启动器/命令面板索引、现行 AIWarnings、AICommands、Agent Skill、普通操作文档和测试断言已完成主体迁移；历史复盘保留当时路径事实。
- 当前状态仍是“现行架构决策，六域菜单迁移实施中”。按项目级门禁在当前基线逐个字面量计数，正式静态技术文档 `ES/Documentation/StaticSite/ESFrameworkPublish_技术文档.html` 尚有 18 处旧菜单路径；该 HTML 受 `DOCUMENT_SYNC` 与本地更新台账约束，必须按批次整合，禁止脱离同步记录机械改写。
- 可复现只读门禁为 `ES/Tools/Validation/Test-ESMenuArchitecture.ps1`，它明确区分 ES 自有活跃源码、条件启用的 `Obsolete` 兼容源码、Attribute 字面量/符号参数和正式文档范围；活跃范围包含 ES 自有测试与示例，不计算条件编译结果，也不对 Attribute 出现次数去重。此前 `80/36/84` 只代表一次未固化筛选的字面量统计，不再作为权威全仓计数。
- 该菜单迁移批次当时取得过 `ES_Stand` 生成工程编译证据；“较高层生成工程被 `ES_Logic` 缺失类型阻断”只属于旧工作树快照，已被本文件后续 2026-08-16 的局部静态编译、Unity Tundra 与 Domain Reload 证据覆盖，禁止继续写成当前全项目阻断。菜单、分隔线、快捷键与 ReloadDomain 仍没有针对该迁移本身的完整 Unity 实机验收。

## 2026-08-16 VFX、AssetPackage 与 UGC Workbench 职责复核

- VFX 正式作者与运行时骨架已经存在：`ESVfxInfo`、`ESVfxGroup`、`ESVfxKey`、`ESVfxGameCoreTable`、`ESVfxModule`、`ESVfxHandle`。当前 `ESVfxInstanceRoot` 只缓存并驱动 `ParticleSystem[]`；没有 `VisualEffect` 运行后端、VFX Graph Event、Exposed Property 合同、Graph 完成判定或 GPU 预算证据。状态只能是 `Implemented-Unverified`，不能从类型存在推导为“新版 VFX 完整支持”。
- AssetPackage 当前工作副本已具备 Package ID/Schema/许可证、EditorOnly 分析 SO、ParticleSystem/VFX Graph 候选分析、每类固定导出路径、源变更增量更新、配置变更重导出、导出前链路修正、路径安全预检、配置指纹、Resolution Snapshot、暂存/备份/事务回滚以及 GUID/Hash 防误删等源码。相关文件仍有未提交修改，本轮没有 Unity 实机、自动化导出、Profiler、Player 或发布验收；商业状态保持 `Implemented-Unverified`。
- 公共编辑器预览底层的当前权威路径是 `Assets/Scripts/ESLogic/Runtime/EditorPreview`。AssetPackage 仍保留 `ESAssetPackagePreviewSceneContext`、独立 Camera/Light/PreviewScene、局部 `PreviewRenderUtility` 和 `Library/ES/AssetPackagePreviewFrames/AssetPackageBake` 缓存路线；这是待收口的专用实现，不能宣称已完全统一到 `ESEditorPreviewRenderContext`，也不得新增第三套预览底层。
- UGC Workbench 与 World 作者工具源码当前位于未跟踪目录 `Assets/Scripts/ESLogic/Editor/Workbench`、`Assets/Scripts/ESLogic/Editor/World`。源码可见 UI Toolkit 外壳、资源/层级、中心视口、工具轨、Inspector、底部问题区、稳定选择、拖放、锁定、Undo 目标、World Draft、外部漂移阻断和事务提交；但未跟踪源码不是正式 Git 基线，本轮也没有 Unity 窗口、窄屏、高 DPI、Domain Reload、Undo 恢复、场景交互或性能证据。
- `ESWorkbenchWindowBase.OnWorkbenchUndoRedo()` 当前只更新 `SerializedObject` 并刷新界面；World Draft 虽有 `SessionState` 恢复快照，但公共 Undo/Redo 回调没有显式通知领域重新持久化草稿 Hash、ChangeSet 与恢复快照。此处按进度回退风险处理，取得定向测试和 Unity Domain Reload/Undo 实机证据前不得宣称草稿恢复闭环已经商业验收。
- 工作台基础层已有 `ESWorkbenchModuleKind` 默认模块列表、`ESWorkbench_AdjustModules` 调整钩子，以及按稳定贡献 ID、Owner、Revision、Dependencies、IsEnabled、Inject 和释放句柄组织的轻量贡献注册表。页面、资源槽位、视口、对象/层级源、作者适配器、Inspector、工具、命令和问题源已有统一注入入口。
- 当前基础合同仍未收口：贡献描述没有模块身份，注册表不接收最终模块列表，也不按模块 List 排序。World 仅通过页面贡献自己的 `IsEnabled` 委托完成局部模块过滤；综合测试窗口仍直接注册页面。不能把“World 当前可过滤”写成“所有专业工作台都由基础层自动裁剪和排序”。
- World 宿主启用链在基础类已装配贡献后再次调用 `ESWorkbench_LoadContributions()`；基础释放方法清理大多数注入集合但不清理页面列表。固定模块下页面 ID 替换可掩盖问题，但动态移除模块、调整顺序或重复装配时仍可能保留旧页面语义。
- 地图作者态、Heightfield 与 PreviewScene 预览已经存在；Unity Terrain 后端内部也有创建/更新 `TerrainData` 和 Scene 的实现片段。但公开 `ESWorldMapTerrainEditorFacade.TryBakePersistent` 仍明确封锁正式输出，原因是未完成场景未保存检查、覆盖备份、原子提交和失败回滚。内部代码存在不能作为正式 Terrain 资产可保存的证据。
- 2026-08-16 本轮重新执行 `dotnet build ES_Logic.Editor.csproj --no-restore` 与 `dotnet build ES_Logic.Editor.World.Tests.csproj --no-restore`，均为 `0 warning / 0 error`。这只证明当前生成工程静态编译；本轮没有 Unity Editor 导入、ReloadDomain、Test Runner、深浅主题截图、Undo、PreviewScene 清理或正式 Terrain 写后重读证据。
- 当前准确状态是“工作台基础骨架和 World 局部接入已形成，仍在集成与验证”，不是商业级 UGC 工作台完成、完整 Terrain 作者链完成或所有专业工作台可直接复用。现行专项约束见 `40_编辑器与工具（EditorTooling）/专业工作台（Workbench）/专业工作台与World作者工具_贡献注册与正式资产边界_AI协作警告.md`。

## 2026-08-16 Stable Graph V2 与 AISkill 执行链复核

- 历史实验 `ESGraphView / NodeRunner` 源码、菜单、运行接口、link.xml 保留项和资产指南条目已删除，不再提供兼容入口。
- 正式图基础统一为 `ESGraphAssetBase + 具体 Graph 资产类型 -> ESBakedGraphSnapshot（验证快照 / 编译输入）-> 消费者专属不可变产物`；不得预设所有 Graph 共用 Domain Plan 或 Program。`Program` 后缀当前且唯一保留给尚未实施的 `ESBehaviorTreeProgram`。`ESGraphEdgeRecord.order` 已进入迁移、Snapshot、内容签名和消费者 Spec，作为唯一业务顺序；`EdgeId` 只负责身份。
- Story 现有 `ESStoryDefinitionDataInfo -> ESStoryDefinitionSnapshot -> Catalog -> Instance/QuestRecord -> MODULE_ESStoryModule` 运行骨架，但 `ESStoryGraphAsset` 尚未接入。当前工作树源码已把选项作者顺序写入内容签名，`MODULE_ESStoryModule.TryStart` 也只解析已注入 Catalog，不再自行 Inject/Bake；这两项目前只有源码证据。唯一权威迁移、签名迁移与回归、初始化期 Catalog 注入、原子发布、存档版本迁移和 Unity/Player/Profiler 证据完成前，Story Graph 仍为 `Verifying`。
- Agent Authoring 明确分为两种互斥模式：产物生成图 Bake `ESAgentArtifactGenerationSpec / ESAgentSkillBundleContract`，进入候选隔离、Diff Review、人工批准和哈希绑定；AISkill 执行图 Bake `ESAISkillExecutionSpec`，由 `ESAISkillExecutionCoordinator` 和 Automation Task 形成持久化 `ESAISkillWorkflowRun`。执行 Branch 的 matched/default 为独立 `Single` 出口，只有 FanOut 分发出口为 `Multi`，Join 使用 `Multi` 输入。
- Graph AI 的候选生成和单次使用已注册为 `es.agent.generate@1`、`es.agent.use@1`，并由 `ESAutomationFacade` 强制 TaskContract 后生成 RunId、输入 Hash、RunRecord 和发送回执。批准后的独立实现窗口另由不可变 Launch Envelope 与接收回执证明，不得冒充 Automation RunRecord。AISkill 执行工作流源码还具备稳定 InvocationId、超时与重试门禁、条件分支、串行 ForEach、人工批准、父子 AISkill 调用、八层深度/递归阻断、父子取消、结构化输出、持久化 RunRecord 及受 Asset GUID、GraphId、内容签名和状态 Hash 约束的恢复。
- 当前 Graph 可靠证据最高为 S1（现行源码与测试源码可检查）；本轮没有重新取得静态编译、Unity 导入、Test Runner、真实窗口执行、真实端到端 Run、失败恢复或 Profiler 证据。模块成熟度保持 `Verifying`，不得标记为 `Stable` 或商业级完成。

## 2026-08-10 玩家控制器场景刷新

- `ESPlayerControllerTest.unity` 已按官方场景构建器刷新为“ES 玩家控制器 · 24 区综合验收场”，玩家出生点为 `(-24, 0.02, -2)`；旧 5 阶段布局不再作为当前场景基线。
- 已移除玩家 `ModelOffset` 上非构建器生成的 `AreaEffector2D` 场景覆盖。MCP 静态场景诊断结果为 `totalIssues: 0`。
- 重建前基线已归档到 `ES/Bak/Reviewed/20260810_PlayerControllerRefresh/`，机器本地回滚副本位于被忽略的 `ES/Bak/Local/20260810_PlayerControllerRefresh/`；项目外 `C:\Users\asus` 不再作为正式备份位置。
- 以上只证明场景生成与静态门禁；玩家移动、跳跃、翻越、攀爬、骑乘、载具驾驶和镜头链路仍需 PlayMode/Profiler 证据，状态保持 `Verifying`。
- 已补充场景/控制器 AI 高频误操作预防表、PlayMode 生命周期安全门禁与交付前检查表，覆盖构建器权威、Prefab override、KCC/VehicleController 写入边界、输入链路、运行证据分层、UTF-8、备份、dirty changes 和“未退出 PlayMode 不得进行高危写入”规则。

## 装备推进基线

- 2026-08-16 已完成开发期硬切：`EntityEquipmentDomain` 成为 Entity 正式第五 Domain，并聚合 Inventory / Slot / Attachment / Effect；`EntityBasicCombatModule` 不再序列化持有 WeaponSlot，只通过 Equipment Slot 消费当前武器能力。当前成熟度仍为 `Verifying`，不等于装备玩法已经可玩或可发布。
- Weapon 定义链保持 `ItemDataInfo -> ItemWeaponDataBlock -> ESWeaponGameCoreTable -> ESWeaponRuntimeData`。运行时物品实例进入固定容量 `ESItemInstanceTable`；Handle 校验 TableToken、TableEpoch、Slot 与 SlotGeneration，Buff/Shot 也已迁入正式实例表，旧 `ESRuntimeInstanceIndex<T>` 已删除。
- 角色挂点已硬切为作者化 String Key：`MainHandSocket`、`OffHandSocket`、`PrimaryBackSocket`、`SecondaryBackSocket`、`HipSocket`、`TemporaryHandSocket`。`DefaultTransformKey.Weapon`、`WeaponSocket`、武器 Prefab 内的 `HoldSocket/BackSocket` 和 Combat `switchAssist*` FinalIK 写入均已移除；武器本地只提供 Grip、OffHandGrip、Muzzle、AimReference 和 PresentationRoot 等参考点。
- `EntityTransformMap` 当前是 `internal sealed` 的领域序列化实现，`EntityTransformMapping.TransformMappings` 只公开无分配的 `readonly struct EntityTransformMapView`；序列化写入只对同程序集开放，公共运行时写入口仅保留 `SetDynamic` 与 `ClearDynamic`。固定枚举键按索引密度自适应选择连续数组或字典，String Key 走字典，正常查询保持 O(1)；动态键会在写入字典前拒绝 null、空串和未规范化文本，且不得与序列化键同名。结构别名存在性由 `ContainsAlias` 判断，Unity Object 值是否仍有效由 `TryGetValue` 判断。
- `EntityTransformMapping` 已从旧 Odin `serializationData` 硬切到 Unity `[SerializeField] EntityTransformMap`。该数据属于框架开发阶段可丢弃数据，按长期容器 P0 的“框架开发阶段的数据重置政策”不恢复、不兼容旧 Odin 载荷，也不要求迁移器或逐次授权；仍只含旧载荷的对象必须重新作者化。不得描述为迁移成功、兼容完成或旧资产等价。当前权威数据只来自新 Unity 序列化字段。
- 最新 Builder 已重建“大长条”、两个角色模板和正式大黑塔；大黑塔使用独立 `Assets/ESNormalAssets/CharacterPresentation/大黑塔_表现.prefab`，正式 Variant 不再读取旧 Preview Prefab。两个角色模板的五域、Mapping 与层级自检通过，正式大黑塔保存后门禁通过。
- 当前验证证据：Unity 2022.3.45f1 完成脚本导入与 Domain Reload，Console 无编译错误；实例表、Item、Attachment、Equipment 四组定向 EditMode Job `9247373e6bb04b41a49bdc9532dfbad2` 为 27/27，通过跨泛型表 Token 隔离、实例 Handle、swap-remove、持久 ID 跳过、Item 转移、基础/专项投影、失败回滚、装备/卸下、动画绑定等待、挂点代际和回池清理；统一内容注册 Job `e81aeb064b8a4b70ac1fc889b98c04e5` 为 19/19。尚无 PlayMode、Profiler、Player 或发布证据。
- 2026-08-16 本轮重新执行 `dotnet build ES_Stand.ValueChange.Tests.csproj --no-restore` 与 `dotnet build ES_Logic.csproj --no-restore`，均为 0 warning / 0 error。Entity Mirror Map 的测试源码覆盖动态/序列化同名冲突、失败恢复、基类 `Clear()`、非法动态键、只读 View 边界、Unity Prefab 往返声明和 destroyed Unity Object；本轮没有执行 Unity Test Runner，因此不能把测试源码存在或生成工程编译写成这些测试已在 Unity 通过。
- 旧 `Assets/ESNormalAssets/EditorTools/大黑塔.prefab` 已退出正式 Builder/Variant 链路，但 `Example_SimpleTools/New Scene 1.unity` 仍有显式 Prefab 引用，因此不能在未重建该示例场景时直接删除；它不得作为正式内容、Pool 预热或发布依赖。
- 既有测试资产 `新建物品数据组1566.asset/信息数据键2` 的无显式 Shot Key 问题已补为唯一 `shot.test.data_key_2`。官方 Item GameCore 全量重建已取得 Unity 回执：扫描 3、有效 3，实际注入 Item 3 / Shot 2 / Weapon 1；“大长条”的基础 Item 与 Weapon 专项投影均进入正式强类型表。不得删除该测试项或复用已占用的 `数据键2`。
- Equip/Holster/Attack、动画事件驱动 IK、近战命中与伤害仍未进行 PlayMode 验收。大长条必须继续走现有输入、EntityAIDomain、Equipment、Combat 与 Action 链路，禁止另建输入、物理、Animator 或 FinalIK 写入后端。
- 详细推进顺序、缺口与门禁见 `20_架构现状（Architecture）/Entity与世界（EntityWorld）/装备定义与装配推进路线_AI协作说明.md`。

## 已确认基线

- AIWarnings 采用按任务分层加载：`README -> CurrentStatus -> RuleIndex -> 命中的 P0 -> 当前领域专项 -> 必要的交接/复盘或提案`。普通任务禁止递归读取全目录；P0、现行状态和任务专项必须读取原文，分批摘要只能导航，不能替代规则权威。
- 模块成熟度治理已建立统一状态、半成品隔离规则和状态跃迁证据门禁；`$es-module-lifecycle` 与模块状态审计 AICommand 现支持三个直接触发词：“审计”默认只读并在结束后最多询问一次，“审计并记录”更新固定入口 `ES/Documentation/Status/MODULE_AUDIT_STATE.md` 的目标模块块，“继续审计”从该入口恢复并先复核事实。检查点记录当前状态、Git 基线、证据缺口、下一动作和失效条件，但不代表全项目已分类，也不能向下次窗口授予实现权限。

- 2026-08-10 的生成工程静态构建：`ES_Stand.csproj`、`ES_Design.csproj`、`ES_Logic.csproj` 均为 `0 warning, 0 error`；`ES_Design.ConfigKey.Tests.csproj` 为 `2` 个既有 `CS0649` 警告、`0 error`。这只是 `dotnet-build`，不替代 Unity、Test Runner 或 Player 证据。
- Camera Core 的 P0 源码骨架已补：`ESCameraModule` 持有 Director，`ESGameManager.Camera` 只暴露模块门面；当前版本只有 `LocalControl` 当前 Entity 能提交请求与 Look，根本没有外部 Owner 注册 API。回放/观战/剧情需等模块私有受信 Bridge 落地后才可申请正式 View。普通 AI/NPC、AI 驾驶载具及 AI 技能请求会在模块边界拒绝；技能相机以技能使用者而非主目标作为 Owner。`ESCameraLease` 已有 Dispose / Look / Target 语义 API。当前 `ES_Logic.csproj` 已收录 `ESCameraModule`，并在上述 `dotnet-build` 中通过；这不等于 Unity 已导入、域重载或运行验收通过。
- `ESGenericLife` 的 Pool 分部已完成代码接线：唯一 Root、按类型唯一 Extension、新建/预热 Despawn 基线、回调异常收口与 Spawn 内延迟归还均已实现；`ES_Logic.csproj` 已取得静态编译证据，但 Unity Editor、Unity Test Runner 和运行行为仍未验收。
- Entity 模板、挂点与武器挂点链已具备静态闭环；`EntityCharacterIdentity` 是唯一的 Prefab 身份/DataInfo 入口，正式 Variant 自动绑定，通用池模板由租出方直接 `Entity.BindDefinition(...)`。仍需 Unity PlayMode 验证和发布门禁证据，不可仅凭编译签收。
- GameTag 的 `ESTagStableReference` 已统一使用 `ESSearchDropdown` Picker；`ItemDataInfo` 的旧 `ValueDropdown/GetTagOptions` 残留已移除。Tag 测试代码已按当前 NUnit / `IPoolable` 契约修正，但 Unity Test Runner 尚未实跑。
- 输入、对象池、物理查询、Item/Shot 与 Buff 都有运行时实现，是当前较成熟的底座。
- ResourcePlan、Consumer/Library 增量激活、Raw `TextAsset` 入口和资源 Scope 生命周期均已有源码。Scope Registry 已强化为：显式 Domain/合法前缀 StringKey 首次加载自动创建、`CreateScope` 只负责提前登记和父子绑定、默认 `GameSession` 路由、`Presentation` 大型展示域、父子级联释放、内部 Creating/Active/Closing 与 Generation、Resident/Temporary 分名及 Provider Transition 清理；Closing 占位会保持到旧 Scope Dispose 完成，已捕获旧 Scope/TemporaryScope 与 Scene 新请求也统一受 Transition 门禁。真实 `ESAssetScope` 不通过 Registry API 暴露，Runtime Monitor 可观察 Registry/隐式创建/Closing 数量。完整 Unity 编译、PlayMode、父子释放、Provider 切换、Domain Reload、P6/P7/P9 与 IL2CPP Player 证据仍缺失。
- 四种资源模式的源码主控制流和后端分流已形成：EditorDirect 使用 AssetDatabase；EditorSimulateBuild 校验正式发布元数据与 RuntimeMap 后使用 AssetDatabase，不下载 Bundle；LocalBuild 使用本地正式 Bundle；HotUpdate/Net 使用远端清单、缓存、下载与回退。该结论只表示源码链路，不能替代四模式 Unity 运行验收。
- 默认 `ESAssets.LoadAsync(refer)` 已从隐式 Resident 改为自动创建/复用 `GameSession` Registry Scope；`PreloadAsync()` 明确进入 Resident。Owner Scope、ResourcePlan 私有 Scope 与 Temporary Lease 仍保持独立所有权语义。
- `ESAssetDomain` 已在资源运行时 P0 中建立唯一权威语义：`GameInternal` 为框架内部、`ApplicationSession` 为跨 GameSession 的产品会话、`GameSession` 为默认游戏流程、`Presentation` 为短时大内存展示，`Scene/UI/Feature` 仅代表单一共享域，多实例必须使用带前缀 StringKey。当前 Registry 已实现统一机制，但 `GameInternal` 权限限制和各 Domain 到流程管理器的自动释放接线尚未由源码强制，属于 P1 实施缺口，不能写成运行边界已经全部验收。
- Scope Registry 已增加默认自动创建/释放、父子级联、Closing 回调重入和旧 Scope Transition 门禁的 NUnit 测试源码。此前“21 个旧 V1 `CS2001` 路径阻断”的生成工程结论已失效；当前 `ES_Design.ConfigKey.Tests.csproj` 可静态编译，但 Unity Test Runner 尚未执行，不能把“测试已编写”或“程序集可编译”写成“测试已通过”。
- 资源窗口已增加独立的“5. 发布到远端”入口：先读取第四步上传计划并执行只读预检；手动计划 Provider 不再伪报成功，真实 OSS/S3/HTTP Provider 安装前会明确阻断。Root Manifest 仍必须最后切换。
- 第五步窗口提供“初步验证远端隔离区”：真实 Provider 必须只在 `validationPrefix`（默认 `.es-validation`）执行探针上传、HEAD 校验与清理，不得用正式版本目录测试权限。
- 阿里云 OSS Provider 已接入原生签名、流式文件上传、`x-oss-meta-es-sha256` HEAD 校验与隔离探针协议；凭据仅从环境变量读取，Unity 资产不保存 Secret。仍需使用真实测试 Bucket 完成一次网络实跑。
- AI 协作历程已改为用户授权制：只有用户明确要求时才能创建、更新或恢复，普通任务不自动写入；连续约 10 轮后 AI 可以询问一次，但确认前不得修改或催促。已具备本地 Codex session 兜底工具：`Find-CodexSession.ps1` 从 `history.jsonl` 按 ID、主题、日期和 CWD 输出候选绝对路径，`Recover-CodexSessionHistory.ps1` 从已确认的 `rollout-*.jsonl` 重建逐任务时间线，`Test-ESCodexTimelineCoverage.ps1` 对消息数、正式节点、阶段、编号与字段完整性执行机械门禁。恢复器已禁止重复运行时嵌套旧完整时间线或把旧摘要 `Txxx` 误计为正式节点。候选分数仅辅助定位，档案归属仍需人工核对；详见对应 P0。
- `ES/AI协作历程（Codex）/Tools/Complete-ESCodexHandoff.ps1` 已把覆盖校验、Bootstrap Validate、私有 handoff snapshot、新窗口启动、`ContextAccepted` 接收门禁、不可变 handoff receipt 与可选源窗口关闭编排为单一入口。默认不启动窗口；`-OpenNew` 才启动，`-CloseSource` 只有接收成功后才执行。当前仍需真实新窗口接收与关闭流程冒烟，不能把脚本存在当作端到端交接已验收。
- `$es-codex-session-bootstrap` 的当前源码入口固定为可见 `cmd.exe -> codex.cmd`，支持中文任务交接、稳定 `TaskKey`/任务指纹、同任务活跃窗口去重和显式 `-ForceNew`；启动成功可返回进程 ID并尝试从本地 history 登记 session ID。现有冒烟只证明启动、去重和登记路径，不证明新窗口任务内容或 Unity 工程验收通过。

## 角色控制与手感当前状态

- AI Domain 已收口为输入意图解析与写入，KCC 是地面速度和朝向响应的唯一身体执行入口；旧的二次 `moveSmooth`/`lookSmooth` 路径和 `EntityBasicDomain.groundedDefaults` 转发层不应恢复。
- 大黑塔作者基线已统一为 `GroundMovementSharpness=20`、`OrientationSharpness=18`，并同步正式 Prefab KCC、角色模板生成器和 ActorData 资产。
- ActorData 的 `motionShared` 已在 `Entity.BindDefinition(...)` 接入 KCC 作者默认值，包含地面/空中速度、地面响应、朝向响应和跳跃参数，并在 `ClearDefinition`/回池时恢复 Prefab 基线；旧缺省 `speedMultiplier=0` 会回退为 `1`。Character Attribute/ValueChange 仍是更高优先级运行时覆盖层，因此最终值必须由运行时诊断确认。能力开关和输入许可字段尚不能仅凭 DataInfo 字段宣称已接入运行时。
- 手感成熟度保持 `Verifying`。缺少 Unity PlayMode 的起步 T90、松手停止距离、180°反向完成时间，以及 30/60/120 FPS 和 Profiler 证据前，不得称为 Stable 或“3A 手感已完成”。

## Camera 证据缺口

- 2026-08-10 的 `ES_Logic.csproj` 生成工程静态构建为 `0 warning / 0 error`，`ESCameraModule` 已被收录。不得手改生成工程；该结果也不能证明 Unity 的 `ES_Logic.asmdef` 已由 Editor 实际导入。
- 本地观测没有可调用的外部提权入口：模块只接受当前本地 Entity；未来 Replay/Spectator/Cutscene Bridge 必须作为模块私有生命周期实现。`ESCameraModuleAuthorizationTests` 已写入未登记 Owner、非本地 Entity、当前本地 Entity 三个测试源码，并随 `ES_Design.ConfigKey.Tests.csproj` 静态编译；该项目当前有 `2` 个既有 `CS0649` 警告、`0 error`。尚未取得 Unity Test Runner 执行证据。
- 因此 Camera 当前状态严格为“源码与生成工程静态编译已取得证据，Unity 导入、域重载与运行验收待完成”，不是“P0 已收口”或“稳定可用”。

## 2026-08-10 Camera、资源与基础生成工程证据矩阵

本表是 2026-08-10 对 Camera、资源链和当时基础生成工程的范围化快照，不是全项目实时总表，也不得覆盖上方 2026-08-16 装备与挂点证据。源码存在不代表 Unity 已编译，`.csproj` 收录也不代表 Player 已验证；后续 AI 报告状态时必须按领域和日期分层，不得把其中任一步替代另一步。

| 证据 | 当前状态 | 说明 |
| --- | --- | --- |
| 相关源码存在 | 部分已确认 | Raw、资源扩展、资源生命周期、发布 Provider，以及 Camera 首切片源码均存在。 |
| IDE 生成的 `.csproj` 收录 | 部分当前静态证据 | 2026-08-10：`ES_Stand`、`ES_Design`、`ES_Logic` 的生成工程均可静态编译；其中 `ES_Logic` 已收录 `ESCameraModule`。该表不等同于 Unity asmdef 收录状态。 |
| `dotnet build ES_Stand.csproj` | 已通过 | 2026-08-10：`0 warning / 0 error`。禁止手改生成工程；该结果不能替代 Unity 编译。 |
| `dotnet build ES_Design.csproj` 与 `ES_Logic.csproj` | 已通过 | 2026-08-10：均为 `0 warning / 0 error`，仅证明对应生成工程静态编译。 |
| `dotnet build ES_Design.ConfigKey.Tests.csproj` | 已通过但有警告 | 2026-08-10：`2` 个既有 `CS0649` 警告、`0 error`；不是 Unity Test Runner 结果。 |
| Unity Editor 编译 / 域重载 | 该快照未验收 | `.csproj` 编译不能替代 Unity Editor 实际导入与域重载；其他领域后续证据见其更新段落。 |
| Unity Test Runner / PlayMode | 该快照未验收 | `dotnet test` 没有产生 Unity Test Runner 执行结果；其他领域后续证据见其更新段落。 |
| IL2CPP Player | 未验收 | 尚未构建和运行。 |
| 真实 OSS 网络 | 未验收 | Aliyun OSS 实现已存在，仍缺真实测试 Bucket。 |

2026-08-10 该轮已取得当时生成工程的静态构建证据，旧的 `CS2001`、`ESCameraModule` 缺失和 `ESAudioCueRuntimeTests.cs:217` 阻断结论在该快照中已失效。Unity 是否真正收录/编译仍必须看目标 Editor 的域重载与 Console；仍不得用任何 `.csproj` 编译替代 Unity Editor、Unity Test Runner、PlayMode、Profiler 或 Player 验收。不得恢复旧 Raw 类型或手工长期维护 Unity 生成的 `.csproj`。

## 当前优先级

1. 在目标 Unity Editor 实例中触发并观察实际导入、域重载与 Console，确认 `ESCameraModule`、Track/Timeline/Preview 的真实编译状态；随后执行 `ESCameraDirectorTests` 与角色/相机场景的 PlayMode 验收。
2. 验证角色模板、挂点和武器绑定的 Unity 行为，并为基础模板/预览模型补齐发布门禁证据。
3. 在 `Entity + EntityAIDomain + ESGameManager.WorldDomain` 中收口稳定身份、控制源仲裁和世界注册。
4. 执行 ResourcePlan 的 P6/P7/P9 PlayMode 验收。
5. 完成 IL2CPP Player 发布验收。

## 状态解释

- `现行约束`：必须遵守，除非用户明确改变项目规则。
- `已实现事实`：当前源码中存在，仍需按任务验证。
- `联调中`：已有实现，但缺少完整运行或发布证据。
- `待验收提案`：仅为方向，不得宣称已落地。
- `历史复盘`：用于理解决策背景，若与源码冲突则源码优先。

此文件只记录高层状态。具体源码入口、验收标准和 P0 规则请从 `规则索引（RuleIndex）.md` 进入。
