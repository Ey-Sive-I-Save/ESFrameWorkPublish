# AIWarnings 规则索引

按任务选择必读文档；所有任务都先读 `10_P0最高约束（P0Guardrails）/编码与文本（Encoding）`。

| 任务 | 必读目录或文档 |
|---|---|
| 修改 GameCore、ConfigKey、RuntimeData | `10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）`、`配置与稳定身份（IdentityConfig）` |
| 修改资源加载、Manifest、AssetBundle、ResourcePlan | `10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）`、`50_验证与发布（ValidationRelease）` |
| 修改编辑器初始化、扫描、预览或窗口 | `10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）`、`40_编辑器与工具（EditorTooling）` |
| 修改 Entity、角色、输入、控制或世界系统 | `20_架构现状（Architecture）/Entity与世界（EntityWorld）`、`输入与交互（InputInteraction）`、`通用架构（GeneralArchitecture）`；涉及角色 Prefab、DataInfo、挂点、武器或模板时必须先读 `角色Prefab职责与DataInfo入口_AI协作警告.md` 与 `Documentation/CHARACTER_PREFAB_CONTRACT.md` |
| 修改 StateMachine、FinalIK 或 Buff 表现 | `20_架构现状（Architecture）/状态机与IK（StateIK）`、`10_P0最高约束（P0Guardrails）/总体架构（Architecture）` |
| 修改 Buff、Tag、ValueChange、Permit | `20_架构现状（Architecture）/通用架构（GeneralArchitecture）`、`10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）` |
| 修改 Pool、Item、Shot、运动或物理 | `30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md`、`Documentation/ES_GENERIC_LIFE.md`；涉及 Pool 回调命名和 Extension 注入时再读 `10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` |
| 修改 SO 表格、资产包窗口或 SimpleTools | `40_编辑器与工具（EditorTooling）` |
| 进行发布、IL2CPP、性能或资源生命周期验收 | `50_验证与发布（ValidationRelease）`、`10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）` |

`80_交接与复盘（Handover）` 用于补充背景；`90_提案与废止（Archive）` 中的文件不能作为新增实现的唯一依据。
