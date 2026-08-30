# Web UI 制作闭环运行手册（方案校准）

## 结论

当前 WebPageStudio 已具备 Intent、Design、Static 和 Backend Contract 的静态主链。Network、Preview、Visual、Release 的“执行证据”仍必须在受控环境中逐层产生；本手册定义最小运行顺序、输入、回执和拒绝条件。未得到对应动作授权或运行条件时，结论保持 `runtime-not-run`。

## 四层闭环

### Network：安全请求与恢复

1. 使用 `New-ESWebPageStudioBackendContract.ps1` 生成只读合同，固定 HTTPS `apiBase`、host allowlist、GET/HEAD、超时、最大响应、重试上限和取消语义。
2. 使用 `Invoke-ESWebPageStudioBackend.ps1`，默认只返回 `not-run`；只有显式 `-ExecuteNetwork` 才能请求，目标主机不在 allowlist 时硬失败。
3. 回执只保留 host、方法、尝试次数、状态码和脱敏错误；不写入响应正文、凭据或敏感头。必须覆盖成功、超时、DNS/连接失败、非 2xx、取消和重复调用。

MDN `fetch()` 说明网络错误与 `AbortController` 的 `AbortError` 是不同失败面，生成器应据此区分可重试、可取消和不可恢复错误：https://developer.mozilla.org/en-US/docs/Web/API/Window/fetch

### Preview：固定浏览器与结构检查

1. 固定浏览器可执行路径、版本、视口 profile、语言、时区、字体和无扩展干净用户目录。
2. 使用 `Invoke-ESWebPageStudioPreview.ps1` 生成 screenshot、DOM 快照、节点统计和运行回执；禁止安装依赖、外部资源和隐式网络。
3. 回执必须记录浏览器版本、合同/HTML 哈希、视口、截图哈希、DOM 哈希、interactiveCount 和 claimsNotProven。
4. 预览失败时保留源工件，按 `runtime-geometry`、`runtime-token` 或 `runtime-structure` 生成可回滚 RevisionPatch；不能把截图存在当作视觉通过。

Playwright 的实践要求固定截图环境，因为操作系统、浏览器版本、字体和硬件会改变像素结果：https://playwright.dev/docs/next/test-snapshots

### Visual：像素、语义和人工复核

1. 每个页面至少建立 desktop/mobile 两个基线，并覆盖 light/dark、forced-colors、reduced-motion、空态和错误态。
2. 像素差异使用固定阈值和 `maxDiffPixels`；动态时间、随机数、广告位和视频帧必须通过 screenshot stylesheet 固定或隐藏。
3. 同时保存 DOM/ARIA 快照；视觉通过不能替代可访问性树、键盘路径或真实交互。
4. 六类 VisualCheck 分开记录：geometry、token、asset、pixel、motion、human-review；人工项不得自动改成 passed。
5. 传入 `ui-validation-matrix.yaml` 时，`passed` 必须覆盖全部 `requiredPairs`；每个 pair 都要有 profile/theme/state 对应的截图、DOM、ARIA 路径与哈希，并有同 baseline 的通过 comparison。

Playwright 官方建议将截图与 ARIA snapshot 组合，以同时验证视觉布局和可访问结构：https://playwright.dev/docs/aria-snapshots

### Release：性能、部署和回滚

1. 先在 staging 产生固定产物，再运行 Lighthouse CI；设置性能、Accessibility、SEO、PWA 和资源大小预算，超预算以非零退出码阻止交付。
2. 记录目标平台、浏览器、网络/CPU 模拟、LCP/INP/CLS、JS/字体/图片大小、缓存命中、Service Worker 更新和回滚路径。Release 回执的 `cacheUpdate` 必须声明策略、是否请求清理、是否观察到更新以及版本键；`accepted` 不能缺少 `purgeObserved=true`。
3. 发布结论必须绑定产物哈希、部署地址、测试时间和回滚点；本地静态扫描不能代替 staging/生产证据。

Lighthouse CI 支持把预算和审计阈值接入 CI，并在不满足时失败：https://web.dev/articles/lighthouse-ci

## 阻断与恢复矩阵

| 代码 | 触发 | 结论 | 最小恢复 |
|---|---|---|---|
| WEB-NET-001 | 目标主机不在 allowlist | blocked | 更新合同并重新验证，不放宽通配符 |
| WEB-NET-002 | 超时/取消未区分 | review | 记录 AbortError、重试次数和最终状态 |
| WEB-PREVIEW-001 | 浏览器版本/字体漂移 | stale | 固定执行镜像并重建基线 |
| WEB-VISUAL-001 | 像素差异超阈值 | review | 输出 diff、DOM/ARIA 快照和人工复核项 |
| WEB-RELEASE-001 | Lighthouse/资源预算超标 | blocked | 压缩资源、减少动效层或调整已批准预算 |

## 当前项目可执行入口

- Network：`ES/Automation/WebPageStudio/Invoke-ESWebPageStudioBackend.ps1`
- Preview：`ES/Automation/WebPageStudio/Invoke-ESWebPageStudioPreview.ps1`
- Static/Quality/Accessibility/Contract：`ES/Automation/WebPageStudio/Test-ESWebPageStudio*.ps1`
- Release 方案：Lighthouse CI 或等价受控流水线；当前项目未启动该运行时。

## 授权后的固定执行顺序

以下命令是证据采集顺序；每一层都必须使用同一 `taskId`、同一产物哈希和同一环境锁。命令本身不会替代授权，也不会把 `runtime-not-run` 提升为通过。

```powershell
# 0. 先完成离线静态信号
.\ES\Automation\WebPageStudio\Test-ESWebPageStudioStaticSignals.ps1 `
  -HtmlPath <artifact>\index.html -ContractPath <artifact>\web-page-contract.json

# 1. Network：仅在显式允许请求时执行
.\ES\Automation\WebPageStudio\Test-ESWebNetworkRuntimeReceipt.ps1 `
  -ReceiptPath <run>\network-receipt.json

# 2. Preview：固定 browser-environment.lock.json 后执行 DOM/ARIA 检查
.\ES\Automation\WebPageStudio\Test-ESWebPreviewRuntimeReceipt.ps1 `
  -ReceiptPath <run>\preview-receipt.json -EnvironmentLockPath .\ES\Automation\WebPageStudio\browser-environment.lock.json

# 3. Visual：按 ui-validation-matrix.yaml 逐项提交截图与像素差异
.\ES\Automation\WebPageStudio\Test-ESWebVisualRegressionReceipt.ps1 `
  -ReceiptPath <run>\visual-receipt.json -MatrixPath .\ES\Automation\WebPageStudio\ui-validation-matrix.yaml

# 4. Release：绑定 staging URL、deploymentId、产物哈希、预算与回滚点
.\ES\Automation\WebPageStudio\Test-ESWebReleaseAcceptanceReceipt.ps1 `
  -ReceiptPath <run>\release-receipt.json -BudgetPath .\ES\Automation\WebPageStudio\performance-budget.yaml

# 5. 聚合并投影到 ABCD/Focus（只接受上面四层的真实回执）
.\ES\Automation\WebPageStudio\Invoke-ESWebUiEvidenceAggregate.ps1 `
  -TaskId <task-id> `
  -NetworkReceiptPath <run>\network-receipt.json `
  -PreviewReceiptPath <run>\preview-receipt.json `
  -VisualReceiptPath <run>\visual-receipt.json `
  -ReleaseReceiptPath <run>\release-receipt.json
```

执行顺序中的任一层失败、过期或身份漂移，都必须保留原始回执并进入 `partial`/`blocked`/`stale` 分层；不得以 synthetic fixture、截图文件存在或静态回放结果代替该层真实证据。

## 当前边界

本手册和官方资料只完成方案与证据契约校准。由于当前任务明确禁止网络请求、浏览器启动和常驻进程，本轮不执行 Network/Preview/Visual/Release；四层保持 `runtime-not-run`，不声明闭环已验收。

## 静态回放回执（2026-08-29）

最近一次 `Test-ESWebUiClosureStaticReplay.ps1` 返回 `passed`：23/23 个静态检查通过，并已把 Web 知识库静态门禁、子 Agent 执行计划、admission 门禁、波次调度计划、Scheduler 内核回放和持久 Lease/CAS 存储合同和 Worker RunRecord 和 WorkerHandle 合同纳入同一回放。报告同时记录验证矩阵、浏览器锁、Release 预算、四类回执 Schema、Schedule Schema、知识库索引和外部来源计划的 SHA-256 `sourceHashes`，并已由报告验证器复核 `hashVerified=true`。环境锁、视觉矩阵哈希、Release 预算哈希、staging 身份、并行执行计划、候选准入、波次依赖和 Scheduler 内核检查均通过。回执 `runtimeStatus=runtime-not-run`，并明确声明不证明浏览器、网络、Unity、Worker 调度或 Release 行为。该结果可作为生成器/验证器变更的回归基线，不能替代四层运行证据。

### 子 Agent 加速编排（静态计划）

`Invoke-ESWebUiSubAgentProjection.ps1` 输出可重放的 `executionPlan`：`static-preparation` 串行完成工件准备后，将 Network、Preview、Visual、Release 四个 layer-evidence 子任务放入同一 `ConcurrencyBudget`（默认 4）并行窗口；每个子任务使用 Lease/CAS 取消语义，全部结果进入 `layer-validation`，再回到 `evidence-aggregation` 串行阶段，最后投影到 ABCD/Focus。`Test-ESWebUiSubAgentExecutionPlan.ps1` 验证阶段、依赖、子任务数量、预算范围、取消语义和 not-run 保留；`Test-ESWebUiSubAgentAdmission.ps1` 验证候选预算、验证哈希绑定、聚合依赖和禁止将 not-run 准入。

`Invoke-ESWebUiSubAgentSchedule.ps1` 将该执行计划展开为可供上层 Worker 消费的波次：证据层按预算分波，验证层并行，聚合层单线程；`Test-ESWebUiSubAgentSchedule.ps1` 检查波次依赖、最大并发和外部调度边界。它只生成调度输入，不启动 Worker。

该脚本是调度输入投影，不启动 Worker 或外部 Agent；实际加速需要上层受管 Worker 调度器消费该计划，并回传四层真实回执。静态投影通过不能证明跨进程并行、运行时性能或最终交付。

知识库门禁 `Documentation/AIKnowledge/tools/Test-ESWebKnowledgeStaticGate.ps1 -ProjectRoot <project-root>` 同样返回 `passed`：覆盖矩阵 11 个领域、15 个 Web 知识条目及外部来源计划均通过，索引/注册表/矩阵 UTF-8 检查通过；其 `runtimeStatus` 仍为 `runtime-not-run`。

## 开源方案映射（用于实现校准）

| 开源实践 | 本项目落点 | 必须保留的边界 |
|---|---|---|
| 浏览器自动化框架的固定执行环境与截图基线 | `browser-environment.lock.json`、Preview/Visual 回执 | 环境锁漂移只能标记 `stale`，不能自动重写基线 |
| ARIA/DOM 快照与视觉快照并行 | Preview DOM/ARIA 字段、Visual `pixel`/`human-review` 检查项 | 像素通过不能推导可访问性或交互通过 |
| Fetch + AbortController 的取消/错误分类 | Network 回执的 timeout/cancel/retry 状态 | 取消不得伪装成网络失败；凭据和响应正文不得进入回执 |
| Lighthouse CI 的预算门禁与产物绑定 | `performance-budget.yaml`、Release 回执 | 本地静态预算检查不等于 staging 性能通过 |

这些映射只说明“采用了可复用的开源工程模式”，不构成对外部项目版本、浏览器运行或生产部署的事实声明；真实闭环仍需在授权的受控环境中产生可重放回执。
