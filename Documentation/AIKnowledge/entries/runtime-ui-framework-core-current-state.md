# Runtime UI Framework Core 当前状态

`KnowledgeId`: `es.project.runtime-ui-framework-core-current-state.v1`  
`Authority`: `Current runtime source + EditMode contract tests + AIWarnings RuleIndex`  
`RouteKeys`: `runtime-ui-window`, `ui-root`, `ui-navigation`, `ui-focus`, `ui-state-binding`, `ui-overlay`, `ui-transition`, `ui-lifecycle`, `ui-content-presenter`, `lease`  
`HashSchema`: `v2`  
`ContentHash`: `eca6c62781b9e70d362db7bfe16015a42a2a926b582dfcea8a99df7c4f0d2421`  
`SourceSetHash`: `eca6c62781b9e70d362db7bfe16015a42a2a926b582dfcea8a99df7c4f0d2421`  
`EntryBodyHash`: `1ee447dbbd6d54a3470a35f127c7b480cf23801dffcbe84f2e81db2e42f234c2`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`

## 定位与已具备能力

本条目只覆盖 UI 框架本体的运行时合同，不覆盖静态 UI 生成、业务 Presenter 或具体页面内容。当前源码已提供：

- `ESUIPageNavigator`：Page Stack、Push/Pop、Replace、Back、Clear，并在 Pop 后恢复上一页。
- `ESUIFocusCoordinator`：统一焦点 owner、Claim/Clear/Restore、EventSystem 绑定和可达性判断；实际移动仍依赖 Unity `Selectable` 图。
- `ESUIStateBinding<T>`：只读 getter、订阅、增量 Publish 和 Dispose；尚未接入业务状态仓库。
- `ESUIOverlayArbiter`：按优先级仲裁 Overlay，支持去重、排队、超时和取消；尚未绑定具体 Modal/Popup/Toast 视图。
- `ESUITransitionCoordinator`：Enter/Exit 异步协调、超时和异常传播；尚未有 Unity 动画实现或 PlayMode 证据。
- `ESUIContentPresenter<T>`：加载、空态、失败、重试、释放状态；Provider 生命周期由调用方提供。
- `ESUIWindowLifecycleEvents` 与 `ESUIRootCoordinator`：Open/Shown/Focus/Blur/Pause/Resume/Close/Rebind 事件入口，窗口 Lease 暴露 Context。

## 失败面与边界

| 面 | 已定义 | 仍未证实/缺口 |
|---|---|---|
| 导航 | 栈语义、返回和页面恢复 | 多 Root、异步打开并发、非法 Pop 的运行时行为 |
| 焦点 | owner 与恢复接口 | 手柄/键盘设备、Modal 抢焦点、关闭后真实恢复 |
| 状态 | 只读订阅与增量通知 | 业务状态源、线程/主线程调度、解绑泄漏回归 |
| Overlay | 优先级、队列、去重、取消 | 视图层级、输入阻塞、Toast 去重策略的产品配置 |
| 转场 | 超时、取消等待、异常传播 | 动画打断、并发窗口竞态、超时后的资源回收 |
| 内容 | Loading/Failed/Empty/Retry/Release 状态 | 真实 Provider、缓存、占位视图和网络错误分类 |

## 证据与非声明

EditMode 测试仅覆盖状态绑定、导航历史和焦点 Claim/Clear；未执行 Unity 编译、PlayMode、设备输入、动画、资源加载、性能或发布验收。当前 Knowledge 只能证明静态 API 与测试意图，不能宣称商业运行时可用。RuleIndex 对 `runtime-ui-window` 的治理状态仍需与实际实现和运行证据单独核对，不能由本条目自动升级。

## SourceRefs

- `Assets/Scripts/ESLogic/Runtime/UI/ESUIRuntimeFramework.cs` (`88577d2cbcfcbdc7c3e6788a2cfca98eb61974ab74eeb4e3c947129f0f526966`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIRootCoordinator.cs` (`d1bd5674c78b8d9890f5a45e9d3aa74f37589c8c407e57323f6d1c93a66bb15d`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowLease.cs` (`592a6ea9011b555249c0e71bcd087f216517ae117ad6f087936102f98885a296`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowTypes.cs` (`c13ab66caf735352299f899026f2f51557d3a467cfb937fc40d76fe03de6c170`)
- `Assets/Scripts/ESLogic/Tests/UI/EditMode/ESUIRuntimeFrameworkTests.cs` (`293655416334ec15bbb3e2577f1c194f32f64a9b58564d6b8ff5779dc44de3a7`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`2aa56abe81352fd79ad59b1364ffa7381d70b26674a1676b8439173a515d9b6c`)

`StaleWhen`: 任一 SourceRef 哈希、UI Window 生命周期、Unity EventSystem/Input System、转场或 Overlay 合同变化。
