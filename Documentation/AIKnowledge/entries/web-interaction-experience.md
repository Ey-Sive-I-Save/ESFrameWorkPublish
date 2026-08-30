# Web 交互体验与状态反馈知识

`KnowledgeId`: `es.project.web-interaction-experience.v1`
`Authority`: WebPageStudio Page IR、动态 Backend Contract、Quality/Accessibility 合同与官方 Web 标准校准快照
`RouteKeys`: `web-interaction`, `web-microinteraction`, `web-input-feedback`, `web-navigation`, `web-form-ux`, `web-state-design`, `web-accessible-interaction`, `web-cache`, `cache-invalidation`, `dynamic-state-replay`
`ContentHash`: `ad19ce2c7b2b01c9a3ab5812e6d0174945ca599c7cd11f6519f6fd0478a6f504`
`EvidenceLevel`: `S1`
`StaleWhen`: 交互/Backend Contract、Accessibility/Quality 验证器、路由语义或 SourceRef 哈希变化。

## 交互生成方法

- 每个可操作元素先声明意图、触发器、反馈、成功/失败、取消和恢复；按钮、链接、输入、拖拽、快捷键不得只靠颜色或 hover 表达。
- 所有异步动作具备 `idle/loading/success/empty/error/retry` 状态；乐观更新必须有回滚，破坏性操作必须可撤销或二次确认。
- 导航提供当前位置、返回路径、焦点转移和深链接；View Transition 只增强连续性，不隐藏 URL、标题或语义变化。
- 表单即时校验要说明错误原因、修复方式和 aria 关联；提交期间锁定重复操作但保留取消；键盘、触摸和指针行为保持等价。
- 微交互使用短时 transform/opacity、明确焦点环和触控目标尺寸；禁止闪烁、自动播放声音和无限循环干扰。
- 动态数据遵守 Backend Contract 的 host allowlist、超时、取消、脱敏和响应大小预算；静态生成阶段不宣称数据已真实请求。

## 案例抽象

高质量产品站常见可复用模式为 `ProgressiveDisclosure`（渐进披露）、`DirectManipulation`（直接操控）、`ContextualFeedback`（上下文反馈）和 `RecoveryFirst`（错误可恢复）。创意交互必须先保证可预测性，再叠加磁吸、视差、手势或声音等增强层。

CSS Design Awards 的 UX/Innovation 分类和 Immersive Garden 的 One Year 案例可归纳出一条经验：沉浸式视觉仍需让用户知道当前位置、下一步和返回路径。对 AI 生成尤其重要的是先生成可用的静态状态图，再叠加 hover、拖拽、视差或转场；任何增强层失败，都应保留内容、焦点和恢复操作。

案例来源：https://www.cssdesignawards.com/wotd-award-winners；https://www.uicoach.io/inspirations/award-winning/one-year

## 失败恢复

`WEB-UX-001` 无反馈：补充状态机和 aria-live；`WEB-UX-002` 误操作：加入撤销、确认和恢复；`WEB-UX-003` 键盘断路：恢复 tabindex、焦点管理和 Escape 取消；`WEB-UX-004` 重复提交：加入幂等键、loading 锁和超时回退；`WEB-UX-005` 动态请求越界：拒绝未 allowlist 的主机并记录脱敏 finding。

## SourceRefs

- `ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json` (`db40aad82f8eb6647de4a69357d1022e8cef520f389d83cea590cb5da6ff49e1`)
- `ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1` (`718a9698d4aa78b833b1bd269609fbd8ec7bda3cfe0f466d5d0a3292f95a9a26`)
- `ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1` (`72fba7042e5da70008a5b30a9fa49dfcb6263b959d0120d677cb740808b78ad8`)
- `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` (`54db99b92b4ba54f42fd981f3aadf813e9077d2ab788e14ee3932a94a6a8eb49`)
- `ES/Automation/Contracts/es-web-dynamic-state-replay-v1.schema.json` (`2e94689ef36631d9022e8f58b01de2a7e7cbb595afc9f8e7e7ec90535bd27162`)
- `ES/Automation/WebPageStudio/Invoke-ESWebDynamicStateReplay.ps1` (`74cd1a3287eeb8022906b3c710a82bc1af6301fff0f598dbc067465cc7538042`)
- `ES/Automation/WebPageStudio/Test-ESWebDynamicStateReplay.ps1` (`e74cf05bf6baf58a4c900c36c421a3400922e0220f38db72cd28cc0ce30ded04`)
- https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/ARIA_Live_Regions
- https://developer.mozilla.org/en-US/docs/Web/API/View_Transition_API
