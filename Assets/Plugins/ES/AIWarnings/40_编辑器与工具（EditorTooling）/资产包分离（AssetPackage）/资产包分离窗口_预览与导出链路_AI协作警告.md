# 资产包分离窗口_预览与导出链路_AI协作警告

记录日期：2026-07-21；最后源码复核：2026-08-16。

负责范围：`ES资产包分离窗口`、资产预览、小格子动画帧缓存、资源分类排序、导出链路与回退。

## 模块定位

- 这是编辑器资源治理工具，不是运行时资源加载系统。
- 核心链路是：资源包烘焙 -> 分类/搜索/排序 -> 预览 -> 标记使用 -> 导出前依赖通报 -> 分类目录复制 -> 链路记录 -> 回退。
- 运行时系统如 `ESInput`、`ESCommand`、`GameManager`、`RuntimeMode`、`Interaction` 不应直接依赖本窗口。
- 与 `State` 的关系仅限编辑器动画预览：可读取 `StateMachineConfig.previewModel / previewAvatar / previewIdleClip` 作为预览配置，不代表运行时状态机架构。

## 入口文件

```text
Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESAssetPackageBakeData.cs
Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackageBakeWindow.cs
Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewCore.cs
Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewUtility.cs
Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewResourceScope.cs
```

## P0 实施门禁：ES 标准界面与预览底层必须统一

本节是实施 AI 的阻断规则，不是视觉建议。凡修改 AssetPackage 窗口、分类页、记录预览窗、动态预览、工具栏或状态面板，必须先读取本文件、`编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` 和编辑器交付体验 P0；未读取不得声称“符合 ES 标准”。

### 界面标准

- `ESMenuTreeWindow` 只代表窗口外壳接入，不代表内容界面合规；主页面、分类页、预览页、弹窗和动态播放区都必须使用 `ESEditorPresentation` 的共享 Surface、Toolbar、Header、Meta、状态色和间距令牌。
- 新增或修改的可见控件不得直接引入一套自定义颜色、背景、按钮皮肤、标题层级或品牌字体。确需使用原生 `EditorStyles` 时，必须说明兼容原因，并通过 AssetPackage 的 ES Presentation 适配层统一出口；不得在业务页面散落硬编码 RGB。
- 首屏必须按“标题/当前状态 -> 关键结论 -> 主操作 -> 证据/详情”排列；状态、失败、待确认、空数据和加载中必须有不同文案与就近下一步，不能只显示空白、红色日志或无上下文 HelpBox。
- 窄窗口、高 DPI、长中文和浅色/深色皮肤下，关键动作不能被横向滚动、固定宽度或按钮挤压遮挡。固定宽度按钮不得裁切中文，长路径、GUID、Hash 和会话 ID 必须支持复制或展开查看。
- AssetPackage 页面应保持单一权威滚动容器；不得因为新增预览、Odin 宿主或 IMGUIContainer 叠加第二层滚动，导致横向条、黑色条带、内容越界或目标丢失。

### 预览底层标准

- 所有大预览、动态预览和临时实例必须从 `ESAssetPackagePreviewWorkflow` 及其专职下游进入，复用 ES 预览上下文、相机、灯光、隔离层、缓存和清理生命周期。
- 页面类不得新增自己的 ReloadDomain 回调、全局清理、长期 `EditorPrefs` 规则、缓存字典或 `StateMachineConfig` 解析顺序；新增能力应扩展工作流或播放器抽象。
- 普通 `Dispose` 只能释放本窗口/本播放器持有的实例、上下文和句柄；不得误调用全局清理去破坏其他预览窗口。
- 小格子动画继续使用缓存帧路线；大预览继续使用实时渲染。禁止把所有资源改成每帧 `PreviewRenderUtility` 或在 `Assets/` 中写入预览缓存。
- 动态特效预览必须使用临时实例，不得写回源 Prefab、源材质、VFX Graph 或正式资产；运行时行为组件必须按预览策略禁用，事件与 Exposed Property 只能通过明确的编辑器预览入口驱动。

### 验收与声明门禁

以下任一项未完成，只能报告“源码已实现/静态验证通过”，不得报告“ES 标准界面完成”或“商业级预览验收完成”：

1. Unity Editor 实机打开主窗口、分类页和完整预览窗；
2. 深色/浅色皮肤、窄窗口、高 DPI、长中文下无横向滚动、重叠或裁切；
3. 点击按钮、拖动时间轴、滚轮、对象选择和文本输入不会关闭窗口、丢失目标或改变其他页面状态；
4. 连续预览播放、切换资源、切页、刷新、窗口关闭、Domain Reload 后实例、回调、纹理和 RenderTexture 均正确清理；
5. Undo/Redo、窗口重开和重复执行后，当前资源、播放状态和导出链路仍能恢复；
6. Profiler/Console 未出现每帧创建 GUIStyle、Texture、PreviewScene、AssetDatabase 扫描或无界 Repaint；
7. 失败、部分成功和待输入状态均能从当前界面继续处理、取消、重试或定位目标。

`.csproj` 编译、UTF-8 Guard、`git diff --check` 或一次截图均不能单独替代上述门禁。

## 当前有效结论

- 小格子动画预览使用“生成帧 + 磁盘缓存”路线。
- 大预览窗口使用实时渲染，保留旋转视角、调试信息和高质量观察能力。
- 预览工作流入口已集中到 `ESAssetPackagePreviewWorkflow`：生命周期注册、ReloadDomain 清理、刷新静态预览缓存、清小格子内存帧、读取保护状态、解析预览模型/Avatar/兜底材质都应从这里走。
- 小格子队列必须当前页优先；切页、切视角、筛选、排序、分类变化后，新页面任务应排到队列最前。
- 小格子缓存不放在 `Assets/`，当前设计放在项目根目录外部缓存文件夹，避免 AssetDatabase 导入污染。
- 导出复制目标使用 `ES选用_` 前缀，按类型自动分目录。
- `ES选用_` 是默认值，不是硬编码死规则；每个 `ESAssetPackageBakeData` 可单独配置导出文件名前缀，窗口配置区也应暴露这个字段。
- 重复导出默认不覆盖、不生成 `_1`，依赖源 GUID -> 目标路径链路判断，避免目标目录资源污染。
- 导出后记录 `exportLinks` 列表和 `exportChainBySourceGuid` 字典；字典依赖 Odin/ESSO 序列化。
- 导出后，如果源资源有有效链路且目标仍存在，应显示为已选中/已导出；链路目标被删除则不再视为有效导出。

## 过时理解，禁止继续传播

- [过时] “小格子每个动画都实时 PreviewRenderUtility 播放”
  - 实测不稳定且性能差，已改为缓存帧。

- [过时] “AnimatorController 临时生成用于预览”
  - 该方案引发 `UnityEditor.Graphs.Edge.WakeUp` 等异常，已废弃。

- [过时] “重复导出自动生成唯一文件名就行”
  - 当前协议改为默认不重复导出，依赖链路判断，避免资源污染。

- [过时] “导出后只看目标文件是否存在”
  - 现在必须看源 GUID -> 目标路径链路，且目标存在，才算有效导出。

- [过时] “导出窗口只需要复制文件”
  - 商业级资源治理必须包含导出前依赖通报、冲突/重复处理、导出会话、回退、链路维护。

## 2026-08-16 当前源码能力

以下均为当前工作副本中的源码事实，整体状态为 `Implemented-Unverified`；目标文件仍有未提交修改，本轮未取得 Unity 实机或发布证据。

- 独立窗口已接入 `ESMenuTreeWindow<ESAssetPackageBakeWindow>`；配置资产为 EditorOnly `ESAssetPackageBakeData : ESSO`。
- 已有基础资源分类、动作细分、名称/路径/类型/大小/使用状态/动作分类/动作时长排序，以及小格子磁盘帧缓存和大预览实时渲染。
- 资产包身份已包含 Package ID、Schema 版本、内容版本/Hash、所有者和许可证元数据；烘焙会把 AI 分析标记为过期。
- AI 资产可用性分析使用独立 EditorOnly SO 快照，记录包 Hash、资产 Hash、ParticleSystem 数、VFX Graph 候选、可池化候选、脚本/材质风险和人工复核状态；它是检索与治理索引，不替代原资产、Prefab、Graph 或运行时验证。
- 每个分类可以配置相对文件夹名，或绑定一个固定 `Assets/` 路径；解析阶段会检查不可写、源/目标重叠和分类间冲突。
- 导出支持源资源变更增量更新、配置变化重导出、导出前自动修复链路、依赖导出、内部 GUID 重映射、可选覆盖和自定义文件名前缀。
- 导出前会生成带 Package ID、配置指纹、源 GUID/依赖 Hash/文件 Hash、目标预期身份、操作类型和原因码的 `ESAssetPackageResolutionSnapshot`；确认后源或目标漂移会阻断隐式执行。
- 写入路径已有 staging、backup、事务状态、失败恢复、部分回退、目标 GUID/Hash 校验和“目标被外部修改则跳过删除”保护；链路以 Source GUID 为主键，并保留导出会话与配置指纹。
- 窗口已提供分析摘要、事务摘要、分类路径配置、重新分析和定位分析快照等入口；不能再沿用“配置只能去 SO Inspector 改”这一旧结论。

## 仍未完成/风险点

- [未完成] 依赖通报仍缺可搜索、可展开、能显示根因与冲突决策的依赖树/冲突树产品界面。
- [未完成] 当前 Resolution Snapshot 是导出内部的确认边界，尚未形成用户可独立运行、保存、比较和导出的完整 dry-run 产品体验。
- [部分实现] 已有链路修正、定位与失效治理入口，但仍缺清晰的批量解除链路、批量修复预览、冲突选择和误操作恢复体验。
- [未完成] 缺少覆盖新增、更新、配置漂移、源漂移、目标漂移、写入失败、部分回退、重复执行和超大依赖图的自动化测试矩阵。
- [风险] `ESAssetPackageBakeData.cs` 与 `ESAssetPackageBakeWindow.cs` 已成为巨型单文件，事务、分析、路径、预览和 UI 职责继续增长会提高回归与多人并行冲突风险；拆分必须保持唯一导出/预览权威链，禁止复制第二套实现。
- [风险] 动画分类仍依赖名称近似匹配；不规范命名需要明确的手动覆盖或规则表，不能静默误分。
- [风险] Humanoid 采样仍存在 Avatar 不匹配、Clip 仅 Root 曲线、材质或渲染管线异常等 Unity 边界；禁止恢复临时 AnimatorController 资产。
- [风险] AssetPackage 仍保留专用 `ESAssetPackagePreviewSceneContext` 和局部 `PreviewRenderUtility`，尚未完整统一到公共 `ESEditorPreviewRenderContext`；迁移前不得新增第三套预览底层。
- [风险] 小格子缓存不得进入 `Assets/`。ReloadDomain 后禁止立即批量 `Texture2D.LoadImage`；`ESAssetPackageGridAnimationFrameCache` 的 `MaxEntries=48`、默认同时 24 和重载后暂停加载属于当前保护，不得恢复旧的 512 Entry/512 同时播放设计。
- [协作约束] 新增预览能力优先扩展 `ESAssetPackagePreviewWorkflow` 或其下游专职类；普通 Dispose 只释放本窗口资源，不得把全局 Cleanup 当作局部清理。

## 商业级判断

当前可以确认“商业所需的身份、分析、路径解析、增量计划、事务与回退源码骨架已经形成”，不能确认“商业级功能完成”。达到 `Accepted` 至少还需要：

1. Unity Editor 中完成主窗口、分类页、分析、路径配置、预览、导出、失败恢复和回退的真实交互矩阵；
2. 依赖树/冲突树、独立 dry-run、批量链路治理和可定位审计报告形成完整产品体验；
3. Domain Reload、Undo/Redo、重复执行、源/目标外部漂移和失败注入自动化通过；
4. Profiler 证明扫描、预览、纹理解码、Repaint 和大依赖图没有不可接受的尖峰或泄露；
5. Player/发布链按实际导出用途单独验收，不能由 Editor 复制成功替代。
