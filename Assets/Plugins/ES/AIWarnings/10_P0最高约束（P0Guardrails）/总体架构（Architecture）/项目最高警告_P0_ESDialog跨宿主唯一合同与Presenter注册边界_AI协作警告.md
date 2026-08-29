# P0：ESDialog 跨宿主唯一合同与 Presenter 注册边界

`Status`: `current`
`StableId`: `es.aiwarning.p0.esdialog-cross-host-contract.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `esdialog`, `cross-host`, `presenter`, `host-routing`
`Applicability`: ESDialog Request/Result/Values、ESDialogHost、IESDialogPresenter、Editor/Runtime Presenter 及其生命周期。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-esdialog-cross-host-contract.md`
`StaleWhen`: ESDialog 合同、Presenter 注册/Lease、Host 路由、能力门禁或 SourceRefs 变化。

## 长期 P0 约束

- `ES_Stand` 是 `ESDialog`、Request/Result/Values、Host 和 Presenter 接口唯一权威；禁止 ES_Editor、Runtime UI 或业务程序集定义同名门面、第二队列总线或兼容别名，Stand 不得引用 UnityEditor、具体 UI 或业务模块。
- Editor 与 Runtime Presenter 必须按 `ESDialogHost` 隔离并显式注册；Editor 使用 AssemblyStream，Runtime 只由 UI Root/GameCore/产品 Bootstrap 显式注册，禁止 Auto、运行时扫描、反射发现和普通 InitializeOnLoad。
- 注册返回单调 Generation 的 Lease；重复 Host 必须拒绝，旧 Lease 不得注销新一代；注销必须确定性结束活动请求和等待队列。
- 显式 Host 缺失返回 `HostUnavailable`；Auto 仅在唯一 Presenter 时可用，双 Host 返回 `AmbiguousHost`，不得猜测。请求提交必须深快照，能力缺失返回 `CapabilityUnavailable`。
- StableId/FieldId/OptionId 是协议身份；确认只代表用户选择，不授予删除、发布、Git、写资产或其他业务权限。业务方仍负责 Undo、Dirty、Prefab Override、权限与取消检查。
- 当前 Runtime Presenter、UI Root、输入焦点、暂停/场景切换和 Player 证据未完成；静态合同或 Editor 实现不得宣称跨宿主 UI 全部交付。

详细对象边界、当前实施事实、验收矩阵和原文快照见 Knowledge：`es.aiwarning.p0.esdialog-cross-host-contract.v1`。
