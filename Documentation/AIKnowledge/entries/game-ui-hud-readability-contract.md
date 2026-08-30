# R4 HUD 可读性与状态观察契约

`KnowledgeId`: `es.project.game-ui-hud-readability-contract.v1`  
`Authority`: `Current project UI source + governed UI contracts + bounded official calibration snapshot`  
`RouteKeys`: `hud-ui`, `ui-visual-design`, `ui-layout`, `ui-behavior-spec`, `ui-focus`, `visual-qa`, `evidence`  
`HashSchema`: `v2`  
`ContentHash`: `6084c51335d6893bdd9dc0334907ff8bf684977f9f0085b48d778b79734be38f`  
`SourceSetHash`: `6084c51335d6893bdd9dc0334907ff8bf684977f9f0085b48d778b79734be38f`  
`EntryBodyHash`: `124c806bc99faff17ea95885b270c4d6f911f060ab188701926b17c5e5488832`  
`EvidenceLevel`: `S1`  
`StaleWhen`: `ESUIRootCoordinator`、Window Lease、ScreenSpec/Materializer 合同、UI 视觉证据合同、AIWarnings RuleIndex 或官方校准快照变化。

## 适用范围

本条目只覆盖 R4 的 HUD 观察、信息层级、遮挡预算、焦点/失焦和状态呈现。它不创建新的 HUD 管理器，不拥有 Combat/HP/Stats 数据，也不把 UI 源码存在等同于 Unity 或 PlayMode 验收。

## 已验证项目事实

- `ESUIRootCoordinator` 维护一个 UI World，并按 HUD、Page、Modal、Popup、Toast、System 分层；Root 注册带 generation，层内请求串行化。
- `ESUIWindowLease` 以调用者 token 保护窗口操作；共享 singleton 只有最后一个 Lease 才能提出强制关闭，旧 generation 不能操作新 Root。
- `ESUIWindowDefinition` 拥有 Layer、Prefab 引用和关闭策略；`PoolOnClose` 需要有效 Prefab 与共享 pool scope。
- `ESUIScreenSpecAdapter` 将 ScreenSpec v3 规范化；`ESUIGameScreenMaterializer` 只接受 v3 并执行字段、状态几何和输入可达性快照检查。

## R4 最小设计规则

1. HUD 只观察已存在的业务状态投影；业务状态源仍由 Combat/Entity/Config 权威提供，UI 不直接写入。
2. 信息优先级固定为：战斗关键数值/危险反馈 → 当前目标与主动作 → 次要上下文 → 装饰。Modal/System 可遮挡 HUD，但必须有可恢复的焦点路径。
3. 颜色不是唯一状态信号；selected、focused、disabled、loading、empty、error 必须至少再有文本、图标、轮廓、结构或动画中的一种可区分信号。
4. 遮挡预算按 profile 记录可见矩形与被遮挡面积；任何“看不见但仍可点击”的元素都视为失败，需结合 `visibility` 与 `inputReachability` 快照复核。
5. Screen Space/World Space 选择必须来自屏幕目标：稳定 HUD 优先使用屏幕空间；世界空间信息必须额外验证相机距离、缩放和事件相机，不得凭 Canvas 存在宣称可读。
6. LayoutPlan 负责几何；状态变体不得偷偷改变父级布局。长文本、窄屏、缺失素材、加载和错误状态保留骨架并局部替换内容区。

## 失败面与恢复

| 失败面 | 触发/症状 | 预防检查 | 恢复动作 |
|---|---|---|---|
| 层级遮挡 | Modal/Popup 覆盖关键 HUD 或 sibling 顺序错误 | 记录六层 host、Canvas 顺序和可见矩形 | 通过 Root/Layer 合同调整，不在组件内抢写排序 |
| 视觉可见但不可交互 | CanvasGroup、Mask 或同级 Graphic 阻断射线 | 检查 `inputReachability.reachable` 与 blocker | 修复 owner 的交互/遮罩配置，再重建快照 |
| 状态误读 | 仅以红绿或亮度区分状态 | 校验文本/图标/结构冗余信号 | 回到 Token/StateSpec，禁止装饰性 Glow 掩盖问题 |
| 重载/池化残留 | 旧窗口或旧 Lease 影响新 Root | generation/token、Pool scope 和关闭路径检查 | 丢弃旧 Lease，按当前 Root 重新 Open |
| 长文本/窄屏溢出 | 文本截断、按钮被推出安全区 | 长内容 fixture、safe-area、最小目标尺寸检查 | 保留骨架，使用局部滚动/换行策略 |

## 外部校准（非项目事实）

Unity 2022.3 文档说明 Canvas 有 Overlay、Camera、World Space 三种渲染模式；`Canvas.renderOrder` 文档提示不同模式的排序行为存在差异。WCAG 2.2 的对比度和目标尺寸可作为可读性校准，但 CSS px 不自动映射为 Unity 像素或 ScreenSpec 尺寸。上述内容仅用于设计校准，不证明目标项目运行时通过。

## 验收矩阵与非声明

- Static：SourceRef/ContentHash、ScreenSpec v3 字段、六层 Root、Lease generation/token、LayoutPlan 几何与快照字段。
- Unity/Runtime：需在目标场景验证焦点、层级、遮挡、窄屏/DPI、重载、池化和实际文本可读性；本条目当前没有这些证据。
- Performance/Release：Profiler、Player、IL2CPP 和发布证据均未包含。

## SourceRefs

- `ES/AISpace/Local/CodexSessionTasks/20260830/R4-ui-hud-readability/TASK.md` (`9b0abb2238ef53fd04b66f391201d71a9384da3936c5acdfd5c28b06eb173ce4`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIRootCoordinator.cs` (`d1bd5674c78b8d9890f5a45e9d3aa74f37589c8c407e57323f6d1c93a66bb15d`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowLease.cs` (`592a6ea9011b555249c0e71bcd087f216517ae117ad6f087936102f98885a296`)
- `Assets/Scripts/ESLogic/Runtime/UI/Window/ESUIWindowDefinition.cs` (`384906164f510db5a2bf7d0d7db2dfd965914409f2daba6075fd346806eb8703`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`dad8470537b6236ad3cda2d9e78ac862eeaf513e63f4b799c2cc79fb23ca4a07`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`34d01ce42b7d811729397f40265e72f31f3f8a05ff880f8da9d810d650f471cc`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`2aa56abe81352fd79ad59b1364ffa7381d70b26674a1676b8439173a515d9b6c`)
- `Documentation/AIKnowledge/ExternalSources/ui-hud-readability-official-snapshot.v1.json` (`a7c899ed29d3f3b040eafaa2f051fb877a100e8585048afef89a6b470bed63f1`)
