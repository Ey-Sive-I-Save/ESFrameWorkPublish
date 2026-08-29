# ESDialog 跨宿主合同：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.esdialog-cross-host-contract.v1`  
`Authority`: `AIWarnings` 原文与当前 ESDialog/Presenter 合同  
`RouteKeys`: `aiwarnings`, `p0`, `esdialog`, `cross-host`, `presenter`, `host-routing`  
`HashSchema`: `v2`  
`ContentHash`: `d181dde185416d2d51fbcf7d84e00bc6a1f7070f42c175735dd37f94edc970f8`  
`SourceSetHash`: `d181dde185416d2d51fbcf7d84e00bc6a1f7070f42c175735dd37f94edc970f8`  
`EntryBodyHash`: `b815e941ffded8c7daf5d7fcf52b3f06cca61eff2ab7a4b608260bb4ef7e47b5`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: Warning、ESDialog 合同、Presenter 注册/Lease、Host 路由或任一 SourceRef 哈希变化。

## 迁移说明

Warning 本体保留唯一权威、注册隔离、Host 错误、快照、权限和未完成证据边界；本条目承载详细对象职责、能力语义、当前实施事实、验收矩阵和原文快照。Knowledge 不授予 UI、Runtime、Player 或发布权限。

## 详细规则与验收

- `ES_Stand` 唯一拥有 `ESDialog`、Request/Result/Values、Host 和 Presenter 接口；不得出现第二门面、第二队列总线或兼容别名，Stand 不得引用 UnityEditor、具体 Runtime UI、场景 Root 或业务模块。
- `ESAdvancedDialogRequest`、ObjectField、文件选择、VisualElement 插槽、Owner EditorWindow 和同步 Modal 是 Editor 高级能力，不得伪装成跨宿主合同。
- Editor/Runtime Presenter 按 `ESDialogHost.Editor/Runtime` 隔离；Editor 走轻量 AssemblyStream，Runtime 只由 UI Root、GameCore 或明确 Bootstrap 显式注册。禁止 Auto、Runtime AssemblyStream、程序集扫描、反射发现和普通 InitializeOnLoad。
- 注册必须返回单调 Generation 的 Lease；重复 Host 明确拒绝，旧 Lease 不影响新一代；注销确定性终止活动请求、等待队列、静态窗口引用和取消令牌。
- 显式 Host 缺失返回 `HostUnavailable`；Auto 仅在恰好一个 Presenter 时可用，双 Host 返回 `AmbiguousHost`，不得用播放状态、焦点或调用程序集猜测。
- 请求提交形成深快照；Presenter 声明 Text/Choice/MultiChoice/Recommendation/AsyncValidation 等能力，缺失返回 `CapabilityUnavailable`。StableId、FieldId、OptionId 是协议身份，显示文本不是稳定键。
- 确认只代表用户选择，不授予删除、发布、Git、写资产、保存场景或释放资源权限；业务方仍负责 Undo、Dirty、Prefab Override、发布门禁、权限和取消检查。
- 当前 Runtime Presenter、UI Root、输入焦点、暂停、场景切换和 Player 证据尚未完成；静态合同不等于跨宿主 UI 交付。

验收至少覆盖：全项目只有一个 `ES.ESDialog`；Stand 无 UnityEditor/具体 UI 引用；重复注册失败；旧 Lease 与新 Generation 正确隔离；双 Host Auto 歧义；Presenter 停止后任务全部终止；Stand、目标 Editor 和合同测试定向编译。Unity 导入、ReloadDomain、EditMode、PlayMode、Player 按实际证据分层报告。

## 原文快照

迁移前完整 Warning（53 行、3929 字节）由以下不可变 SourceRef 保留，原始 SHA-256 为 `585ebdee9a2a91f24a84f88dcbbc5795fafbd1a373933c68c00490c01ccc40b1`。本条目逐项承接其唯一权威、Presenter 注册、Host 路由、请求结果、实施事实与验收语义。

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ESDialog跨宿主唯一合同与Presenter注册边界_AI协作警告.md`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ESDialog跨宿主唯一合同与Presenter注册边界_AI协作警告.md` (`7af8d226be1e85ae8d00f557ff146883e0f228d2051560064e21c103648c844b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`298b45ff7f94c8fd1a97f7da6141b46b37785ef10ced870fb8dd5bc30ec46ad5`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-esdialog-cross-host-contract.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
