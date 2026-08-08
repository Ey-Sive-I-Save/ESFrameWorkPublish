# ES Framework Publish 文档同步记录

## 作用

本文件是 `ESFrameworkPublish_技术文档.html` 的固定同步记录。机器可验证的全局状态在同目录 `DOCUMENT_SYNC.json`；本地待整合更新的机器台账在 `DOCUMENT_LOCAL_UPDATE_LEDGER.json`，可读摘要在 `DOCUMENT_LOCAL_UPDATE_LEDGER.md`；提交前检查脚本是 `Verify-DocumentSync.ps1`。

后续任何 AI 或开发者更新该 HTML 前，必须先读取本文件、`DOCUMENT_SYNC.json`、两份本地更新台账与 `DOCUMENT_READER_STANDARD.md`；更新完成后，必须同时推进 JSON 基线、本文件的“已审阅源码基线”和“最近同步记录”。不要只修改 HTML 而不推进这些记录。

## 当前文档

- 文档文件：`ES/Documentation/StaticSite/ESFrameworkPublish_技术文档.html`
- 文档形式：单文件离线 HTML；本记录只服务于仓库内的后续维护，不影响转发该 HTML。
- 阅读器标准：`ES/Documentation/StaticSite/DOCUMENT_READER_STANDARD.md`；约束信息架构、视觉层级、移动端和呈现验收。
- 本地更新台账：`ES/Documentation/StaticSite/DOCUMENT_LOCAL_UPDATE_LEDGER.json` 是批次、快照、证据和状态的机器权威；`DOCUMENT_LOCAL_UPDATE_LEDGER.md` 是必须同步维护的可读变更总结。
- 已推送提交缓存：台账 `batch.pushedCommitCache` 记录从 HTML 审阅基线到当前本地远端跟踪引用的完整提交范围、SHA、时间、标题与本地总结条目映射。它是回归整合输入，不是 HTML 已更新的证明。
- 最近同步日期：`2026-08-02`
- 已审阅 Git HEAD：`775cfdb57dd49ac96075fb6f4c49039ee11996be`
- 已审阅源码状态：该文档基于上述 HEAD 与当时存在的未提交/未跟踪工作区改动分析，不代表一个干净提交。
- 基线快照：由 `DOCUMENT_SYNC.json` 记录 tracked diff、staged diff、未跟踪源码清单的 SHA-256 指纹及文件数量。
- 提交门禁：仓库本地启用 `.githooks/pre-commit` 后，提交会执行 `Verify-DocumentSync.ps1`；源码或文档状态与 JSON 不一致时，提交失败。
- 当前门禁状态：`待分诊`。`775cfdb..fc09d0a` 的 8 个已推送提交已缓存并映射至正式条目；本地已提交、待推送的 `33a2862` 也已独立登记。HTML 仍停留在原审阅基线，当前未提交的 Project Asset Guide 展开状态和未跟踪的 `ES/Documentation/Output/index.html` 尚未分诊。不能通过重写指纹、更新台账哈希或提前改 HTML 绕过。
- 最近文档改动：`2026-08-02` 完成阅读器排版重构、关键语义高亮、开发者收益矩阵重组、锚点定位修正与诊断列占位符清除，仅改变 HTML 的呈现与导航，不推进任何源码快照，也不解除上述待同步状态。

## 本地更新批次与统一 HTML 整合

Git 负责回答“当前源码事实是什么”；本地台账负责回答“哪些完成项已经理解、为什么会影响文档、回归到什么程度、何时可以一起写入 HTML”。两者缺一不可。

已推送提交也必须先进入本地缓存：以 `DOCUMENT_SYNC.json.baseline.head` 为起点，核对本地 `HEAD` 与本地远端跟踪引用，按 SHA 建立“提交 -> LOCAL 条目”映射，再进入回归整合。不要把“已经 push”误写成“HTML 已同步”；也不要把尚未 `git fetch` 的远端状态称为已核实。

1. 每个本地完成项先在两份台账中登记唯一 ID、行为总结、源码/规范/测试证据、影响的 HTML 锚点、回归结果和已知缺口。仅有文件名、commit 标题或“已修复”不构成完成项。
2. `collecting` 只允许归集和完善摘要；`ready-for-regression` 冻结源码快照并做回归；全部条目具备通过结果或明确接受的缺口后才能设为 `ready-for-html`。
3. `ready-for-html` 是唯一允许修改 HTML 以表达这批源码行为的状态。统一更新解释正文、流程、对比、验收边界、HTML 哈希、同步记录和台账；不得逐条提前塞入 HTML。
4. 所有条目均被 `acceptedEntryIds` 接纳、HTML 校验完成且源码基线真实推进后，批次才可标为 `integrated`。已整合批次不可继续追加；新改动必须开新批次。
5. 每次本地变更加入或移出批次后，运行 `powershell -ExecutionPolicy Bypass -File ES/Documentation/StaticSite/Update-DocumentLocalLedgerSnapshot.ps1 -RefreshSnapshot`。它只刷新台账快照与其 JSON 指纹，绝不推进已审阅源码基线或改写 HTML。

## 更新流程

1. 确认仓库根目录为 `F:/aaProject/ESFrameWorkPublish`，读取本文件、`DOCUMENT_SYNC.json`、两份台账和 HTML 中的 `ES_DOCUMENT_SYNC` 标记。
2. 先运行 `powershell -ExecutionPolicy Bypass -File ES/Documentation/StaticSite/Verify-DocumentSync.ps1`。若台账条目、快照或状态不完整，先完成本地总结和回归整合；不能直接改 JSON 绕过。
3. 先以“已审阅 Git HEAD”为起点检查 `git log <baseline>..HEAD`，将已推送提交写入 `pushedCommitCache` 并映射至完成项；再检查 `git diff HEAD`、`git diff --cached` 与未跟踪源码文件。不要只依据提交信息，也不要把未完成的暂存内容当作已发布行为。
4. 将已理解的局部更新登记为台账条目，阅读关联 AIWarnings、测试、编辑器工具或发布契约；源码摘录只能作证据，不能充当正文增量。
5. 在批次达到 `ready-for-html` 前完成回归整合；更新 HTML 时将行为变化写成解释性正文、流程、对比和验收项，而不是罗列变更日志。
6. 统一更新 HTML 后，重新生成 JSON 中的文档 SHA-256、源码快照指纹、文件数、验收结果和未覆盖项；同时更新台账状态、本文件的日期/记录和 HTML 标记，使这些记录一致。
7. 默认只暂存 HTML、本文件、阅读器标准、两份台账、同步 JSON、验证/辅助脚本与 hook。除非用户明确要求，绝不把已有源码改动、资源改动、AIWarnings 或测试文件一并加入暂存区。
8. 最后检查 HTML 的 UTF-8 编码、内部锚点、离线依赖和文件大小；记录尚未完成的 PlayMode、IL2CPP Player 或真实 Provider 验收，不能以 Editor 成功替代。

## 最近同步记录

| 日期 | 源码基线 | 范围 | 结果 |
| --- | --- | --- | --- |
| 2026-08-02 | `775cfdb57dd49ac96075fb6f4c49039ee11996be` + 脏工作区 | ResourcePlan、SODataWindow/SO Table、角色控制、技能/Operation、容器/池/Link、编辑器生产链、AIWarnings | 建立单文件离线文档、分组深度正文、核心链回放与自定义机制索引；创建本同步记录。 |
| 2026-08-02 | 沿用既有基线，不推进源码快照 | 单文件阅读器排版、桌面/移动导航、深浅主题入口 | 默认深色阅读器，收窄正文、弱化证据栏；复核内部锚点与离线依赖。此项仅为视觉更新，源码漂移仍待语义分析。 |
| 2026-08-02 | 沿用既有基线，不推进源码快照 | 单主题阅读布局、阅读器标准、同步门禁 | 移除常驻右侧证据栏，桌面收敛为左目录与单一正文列；新增 `DOCUMENT_READER_STANDARD.md`，验证脚本强制检查该标准及 HTML 指针。 |
| 2026-08-02 | 沿用既有基线，不推进源码快照 | 宽屏工作区、来源树、章节筛选 | 对照 zread 的宽屏阅读布局后，扩展正文工作区以容纳工程矩阵；右栏仅在超宽屏显示实际源码入口树，新增可用的章节筛选。 |
| 2026-08-02 | 沿用既有基线，不推进源码快照 | 关键结论、警告约束与代码标识的视觉语义 | 正文结论改为字重、轻底色与青绿色下划线的组合；提示块中的高风险约束升级为琥珀色强调；代码保持为等宽边框标识。已在宽屏与 `390px` 深色阅读器中复核，规范新增强制语义和验收要求。 |
| 2026-08-02 | 沿用既有基线，不推进源码快照 | 开发者收益矩阵、章节定位 | 将“角色 / 工作杠杆 / 维护责任”改为可扫描的不同列语义，长表表头保持可见；移除全局与点击导航的平滑滚动，避免带 `#锚点` 或章节跳转出现空白/半程画面。规范新增矩阵与定位验收。 |
| 2026-08-02 | 沿用既有基线，不推进源码快照 | 核心功能块的诊断入口、占位符门禁 | 将 15 个 `undefined` 全部替换为命名的系统/状态/证据排查链；增加面向读者文本的占位符扫描规则，保留可折叠源码证据中的真实语言字面量。 |
| 2026-08-02 | 沿用既有基线，不推进源码快照 | 本地更新台账、回归整合、延迟 HTML 批次 | 新增机器台账和可读摘要，记录本地完成项、精确源码快照、证据、回归、已知缺口与 HTML 目标；验证脚本仅接受内容完整且快照匹配的延迟批次，批次达到 `ready-for-html` 后才可统一更新 HTML。当前开放批次仅完成快照捕获，仍待分诊。 |
| 2026-08-03 | 审阅基线仍为 `775cfdb`；已推送缓存至 `fc09d0a`，本地 HEAD 为 `33a2862` | 已推送提交缓存、待推送提交隔离、条目映射与基线一致的快照口径 | 缓存并核对 8 个 `origin/main` 已知提交，逐条映射到 `LOCAL-...-002` 至 `-015`；本地已提交待推送的模块审计续接闭环登记为 `LOCAL-...-016`。刷新脚本改为按 HTML 审阅基线计算指纹。HTML 未修改，未提交的 Project Asset Guide 展开状态与未跟踪 Output 产物继续保持待分诊。 |
