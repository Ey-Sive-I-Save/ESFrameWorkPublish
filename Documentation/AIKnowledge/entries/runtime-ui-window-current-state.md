# Runtime UI Window 当前源码状态与权威差异

`KnowledgeId`: `es.project.runtime-ui-window-current-state.v1`  
`Authority`: `Source + AIWarnings reconciliation`  
`RouteKeys`: `runtime-ui-window`, `ui-root`, `window`, `lease`, `layer`, `focus`, `pool-on-close`, `resource-scope`  
`ContentHash`: `aea241636d1583fd89fd8223636a45692f37aa1a63936c177c55b8ba737f1609`

## 重要差异

RuleIndex 仍把 `runtime-ui-window` 标为 `reserved`，并明确“不宣称实现或 AICommand 已存在”；但当前源码已经存在 Definition、Catalog、RootCoordinator、Lease、Runtime instance 和 GameManager Module。正确结论是：**实现源码已出现，但治理路由仍未将其提升为 current，且没有由此证明 Unity 运行验收、正式 AICommand 或发布可用。**

## 当前实现结构

- `ESUIWindowDefinition`：窗口稳定身份、Layer、Prefab 资源引用、单例/保留/关闭策略。
- `ESUIWindowCatalog`：按内建 ID 或 StringKey 解析 Definition。
- `ESUIWindowModule`：注册多个 Root，拥有全局池化窗口共享资源 Scope，路由公开 Open 请求。
- `ESUIRootCoordinator`：一个 UI World；显式 HUD/Page/Modal/Popup/Toast/System host，每个 Layer 有独立串行 lane。
- `ESUIWindowLease`：调用者持有窗口的 generation/token-safe 租约。
- Runtime instance：记录状态、leaseTokens、定义、View 和资源/池所有权。

RootKey 重复时强失败；Root registration 带 generation，旧 Lease/registration 不能操作新 Root。Root 的资源 Scope 为 `ui:<root>`，PoolOnClose 还使用 Module 共享 pool scope；不同 Scope 的所有权不能混淆。

## Open/Close 语义

Open 先验证 Root 注册和 Catalog，再在对应 Layer lane 的 Semaphore 下串行处理。Singleton 可能复用活动或保留实例；每个调用获得独立 Lease token。Close 时若仍有其他 Lease，只释放当前 token；强制关闭策略只有最后一个 Lease 可以提出，防止一个调用者越权销毁他人窗口。

关闭效果由 Definition 与请求共同解析为 KeepInactive、PoolOnClose 或 DestroyOnClose。PoolOnClose 要求有效 Prefab 资源引用和共享 pool scope；模块停用、Provider 变化或 Root shutdown 会终止 lanes、清理窗口和资源所有权。

## 尚未被证实的部分

- RuleIndex 路由仍为 reserved，治理状态与源码成熟度未对齐。
- 未检查到本次 Unity PlayMode、焦点仲裁、层级排序、动画取消、Provider 切换和 Pool 回收端到端证据。
- 不能因为 Module 存在就创建或选择一个不存在的 AICommand。

下一步治理动作应是运行目标测试/场景验收，明确 owner 与 release evidence，再决定把 route 从 reserved 升为 current 或维持 implementing。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`c5359cb022ebc2902c4400ad44429da36d1a2dcfa44803586f8f91aaca0d704f`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowDefinition.cs` (`384906164f510db5a2bf7d0d7db2dfd965914409f2daba6075fd346806eb8703`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowCatalog.cs` (`b279d9f901e0329acd873aad5ed86d5d962f0015574d856840e97aacfe0ee86e`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIRootCoordinator.cs` (`ef6541e2d52bbc402b30792775c8af6d77678050e3e6f219540fbafa79d581c9`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowLease.cs` (`e7aafe905e0090decaa635e91974af2eb4f7b4b0a9d77a9da60b029566382af3`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowRuntime.cs` (`59d1e4d540cc4fe06eb71e376b05cbec165de08ac016dd301c92dcb73360e691`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/MODULE_ESUIWindowModule.cs` (`6b15e40d1db6230e87f54be13ffdcce64430e4b19b2ea4780b63bd4e5526650a`)

`EvidenceLevel`: `S1`; `StaleWhen`: RuleIndex 状态、UI Window 源码、AICommand、PlayMode 或发布证据变化。
