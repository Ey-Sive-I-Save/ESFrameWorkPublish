# 完备运行时 UI 框架对比与 ES 可迁移合同

`KnowledgeId`: `es.project.runtime-ui-framework-comparative-patterns.v1`  
`Authority`: `Bounded official framework snapshots + current ES runtime UI source`  
`RouteKeys`: `runtime-ui-window`, `ui-navigation`, `ui-focus`, `ui-input-routing`, `ui-overlay`, `ui-lifecycle`, `ui-state-binding`, `ui-testability`  
`HashSchema`: `v2`  
`ContentHash`: `5378e38faf2c59142b6488872e1a9f8f5843284d867ce89749518fa72f10a191`  
`SourceSetHash`: `5378e38faf2c59142b6488872e1a9f8f5843284d867ce89749518fa72f10a191`  
`EntryBodyHash`: `98be961d8a58d84092aeb871913d7b9e52612a4b45deede0b4897a1bbc1e1b7e`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`

## 对比结论

| 框架 | 完备性来源 | 对 ES 的可迁移原则 | 不应照搬 |
|---|---|---|---|
| Unity UI Toolkit | Panel sort order、EventSystem、跨 uGUI/Toolkit 焦点协调 | 明确输入路由 owner、面板层级与焦点上下文 | 不能把 Toolkit 的面板行为当作当前 UGUI 运行证据 |
| Unreal CommonUI | Activatable Widget 树、最高绘制层路由、输入配置、默认焦点目标 | Overlay 必须有 active/inactive 状态、路由树、Back 语义和 desired-focus 合同 | 不复制 Slate/Viewport 类型；只提炼行为合同 |
| Flutter | 持久 Focus Tree、FocusScope、显式通知与 dispose | Focus 节点必须有 owner、生命周期和释放检查；状态订阅需可观察 | 不把 Widget rebuild 模型等同于 Unity GameObject 生命周期 |

## ES 框架应持续强化的最小合同

1. `InputRouter`：输入先经过当前最高层 active UI，再决定是否阻塞游戏输入；每次路由可记录 owner 与 handled 状态。
2. `NavigationGraph`：Page Stack 处理页面历史，Selectable/显式目标处理局部移动，二者不能互相冒充。
3. `FocusScope`：每个 Page/Modal 保存 last focus 与 desired initial focus；关闭时按 scope 恢复，恢复失败必须可诊断。
4. `OverlayPolicy`：Modal、Popup、Toast 使用统一优先级/去重/取消策略，但输入阻塞与视觉层级是独立字段。
5. `StateSubscription`：View 只读订阅状态，更新必须可增量、可解绑、可在主线程边界验证；不得由 UI 直接拥有业务状态。
6. `LifecycleContract`：Open/Shown/Focus/Blur/Pause/Resume/Close/Rebind 的顺序、异常和取消结果必须可测试。
7. `EvidenceAdapter`：每个合同提供 EditMode 单测和 PlayMode 输入/焦点/转场证据入口；静态通过不得升级为运行时通过。

## 当前 ES 差距

ES 已有 PageNavigator、FocusCoordinator、OverlayArbiter、TransitionCoordinator、ContentPresenter 与生命周期事件入口，但尚缺统一 InputRouter、FocusScope 的持久恢复记录、Overlay 输入阻塞字段、业务状态主线程约束和 PlayMode 证据。以上是框架本体缺口，不要求新增大量脚本；优先通过现有类型补合同与测试矩阵。

## 非声明

本条目是跨框架模式校准，不证明任何第三方框架已集成 ES，也不证明 ES 已完成 Unity 编译、设备输入、动画、性能、网络或发布验收。

## SourceRefs

- `Documentation/AIKnowledge/ExternalSources/ui-runtime-framework-comparison-official-20260830.json` (`5a5004849f2d28c6cd2467fb2d3a3fcd0432d475a45755939aba5ab3229e4930`)
- `Assets/Scripts/ESLogic/Runtime/UI/ESUIRuntimeFramework.cs` (`88577d2cbcfcbdc7c3e6788a2cfca98eb61974ab74eeb4e3c947129f0f526966`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIRootCoordinator.cs` (`d1bd5674c78b8d9890f5a45e9d3aa74f37589c8c407e57323f6d1c93a66bb15d`)

`StaleWhen`: 官方快照、ES UI 源码、输入系统或生命周期合同变化。
