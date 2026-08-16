# ES Framework Publish 本地更新整合台账

## 作用

本文件记录已经在本地完成、但尚未统一写入 `ESFrameworkPublish_技术文档.html` 的源码与工程更新总结。机器可验证的权威清单是同目录 `DOCUMENT_LOCAL_UPDATE_LEDGER.json`；本文件是每项变更的可读说明和回归整合面。

Git 提交、暂存区和工作区差异仍是源码事实的权威；本台账补足 Git 不表达的内容：改动到底改变了什么、应影响哪一章、用了哪些证据、回归覆盖了什么、哪些风险被明确保留。它不能用摘要替代源码阅读或测试。

任何 AI 或开发者开始文档更新前，必须同时读取 `DOCUMENT_SYNC.md`、`DOCUMENT_SYNC.json`、本文件与 JSON 台账。不得把“本地看起来完成”直接写进 HTML；先进入本台账并通过批次整合。

## 已推送提交缓存

本批次的 HTML 审阅基线仍是 `775cfdb`。截至 `2026-08-03T06:20:00+08:00`，本地远端跟踪引用 `origin/main` 为 `fc09d0a`，本地 `main` 已前进到 `33a2862`；因此下表的 8 个提交是已推送缓存，`33a2862` 是独立的本地已提交、待推送记录。远端跟踪引用只证明本机已知的远端状态，不替代下一次 `git fetch`，更不表示静态 HTML 已吸收任何提交。

以下 8 个已推送提交已写入 JSON 的 `batch.pushedCommitCache`，并精确映射到已完成的本地总结条目。它们全部处于 `cached-not-integrated`：可以参加后续回归整合，当前不得直接改写 HTML。

| 提交 | 推送缓存内容 | 对应台账条目 |
| --- | --- | --- |
| `8d73914` | Agent Skills、AIWarnings/AICommands 协作、模块成熟度治理与 staged-only 门禁 | `LOCAL-20260802-003`、`-004`、`LOCAL-20260803-005`、`-006`、`-007`、`-009`、`-011`、`-012` |
| `24b3605` | MCP for Unity 10.1.0 锁定与接入 | `LOCAL-20260802-002` |
| `fb6b55c` | AICommands 规则引用与 Cmd Agent 校验修复 | `LOCAL-20260802-003` |
| `bb7d471` | UnityMCP 工程验收代理路线图预备案 | `LOCAL-20260803-010` |
| `66a0758` | ES 可控目录迁移与路径合同 | `LOCAL-20260803-008`、`LOCAL-20260803-013` |
| `1274082` | ES 资源运行时与核心玩法工程能力整合 | `LOCAL-20260803-009`、`LOCAL-20260803-013` |
| `17b6013` | Codex 协作历程与恢复工具 | `LOCAL-20260803-014` |
| `fc09d0a` | 静态站点阅读与同步规则 | `LOCAL-20260803-015` |

`33a2862`（`feat(ai-audit): 强化模块审计续接状态闭环`）已记录在 JSON 的 `batch.localCommittedCache`，并映射至 `LOCAL-20260803-016`。它尚未 push，不属于上述 8 个已推送提交，也不得提前进入 HTML。当前未提交的 Project Asset Guide 展开状态与未跟踪的 `ES/Documentation/Output/index.html` 由 `INTAKE-20260802-001` 单独承接，继续保持 `needs-triage`。

## 批次状态

| 状态 | 含义 | 是否可写 HTML |
| --- | --- | --- |
| `collecting` | 正在收集已完成的本地更新；每项已具备摘要和影响范围，但回归可以尚未完成。 | 否 |
| `ready-for-regression` | 当前源码快照冻结，条目分析完成，等待或执行回归。 | 否 |
| `ready-for-html` | 每项均有回归结果或明确接受的缺口，章节目标已确定，可一次性更新 HTML。 | 是，且必须整个批次一起写入 |
| `integrated` | HTML、同步记录、源码基线和台账最终状态已一起推进。 | 已完成，不能继续往此批次追加 |

任何新增源码变更都会使当前批次快照失效。先把变更总结加入 JSON 和本文件，再运行 `Update-DocumentLocalLedgerSnapshot.ps1 -RefreshSnapshot`；该脚本以 HTML 审阅基线而非当前 HEAD 生成 tracked/staged 指纹，所以已推送提交与后续本地修改会处于同一个、可复现的比较口径。不能把旧快照继续当作当前批次事实。

## 统一整合流程

1. 为每个本地完成项建立唯一 ID、摘要、源码路径、证据路径、HTML 目标和风险。摘要必须说明行为改变，不能只写类名或“已修复”。
2. 在 `collecting` 状态继续汇集相邻更新；每次收集后刷新源码快照。同步脚本只会接受已完整登记且快照精确匹配的延迟批次。
3. 冻结为 `ready-for-regression`，运行并记录 EditMode、PlayMode、Provider、Profiler、Player/IL2CPP 或其他适用验收。未覆盖项必须写入 `knownGaps`，不能静默省略。
4. 全部条目进入 `ready-for-html` 后，按 HTML 目标统一撰写解释、流程、传统方案对照和验收边界；一次性更新 HTML、`DOCUMENT_SYNC.json`、本文件和阅读器标准。
5. 只有当 HTML 通过结构/视觉校验、源码基线已真实复核且所有条目列入 `acceptedEntryIds` 时，才标记 `integrated`。历史批次只追加记录，不回写篡改。

## 分批提交门禁

pre-commit 只检查本次暂存区，不再要求一次清空整个工作区。未暂存和未跟踪文件可以保留给后续批次，但本次暂存源码必须由已完成台账条目覆盖。

```powershell
git add <本批源码与资产>

powershell -NoProfile -ExecutionPolicy Bypass `
  -File ES/Documentation/StaticSite/Prepare-DocumentStagedBatch.ps1 `
  -EntryId LOCAL-YYYYMMDD-NNN

git add ES/Documentation/StaticSite/DOCUMENT_LOCAL_UPDATE_LEDGER.json `
        ES/Documentation/StaticSite/DOCUMENT_SYNC.json

powershell -NoProfile -ExecutionPolicy Bypass `
  -File ES/Documentation/StaticSite/Verify-DocumentStagedBatch.ps1

git commit -m "本批语义说明"
```

- `Prepare-DocumentStagedBatch.ps1` 只写台账指纹，不会自动暂存、提交或推送。
- 新增源码、删除、重命名和二进制差异都进入 staged patch 指纹；准备后再改变暂存区会被阻断。
- `INTAKE-20260802-001` 可以继续表示其他未暂存工作的待分类状态，不再阻断已由正式条目覆盖的当前提交。
- `Verify-DocumentSync.ps1` 仍负责全工作区与 HTML 整合门禁，不被 staged-only 提交验证替代。

## 当前开放批次

- 批次：`LOCAL-2026-08-02-OPEN`
- 状态：`collecting`。已推送提交范围缓存为 `775cfdb..fc09d0a`；本地已提交待推送的 `33a2862` 已独立登记；当前未提交的 Project Asset Guide 展开状态和 Output 产物仍为 `needs-triage`。
- 快照：以 HTML 审阅基线比较当前 `HEAD`、未暂存 diff、暂存 diff 与未跟踪源码清单；详见 JSON。
- HTML 整合：禁止。已推送条目等待统一回归；推送后本地改动尚未拆分为可独立复核的完成项。

| ID | 当前状态 | 本地完成更新总结 | 回归 | HTML 目标 |
| --- | --- | --- | --- | --- |
| `INTAKE-20260802-001` | `needs-triage` | 历史总入口现承接未提交的 Project Asset Guide 展开状态和未跟踪 Output 产物；已推送范围与本地已提交待推送范围均已分项登记。 | 未开始 | `#editor-overview`、`#editor-verification`、`#deep-warning-16`；必须先拆分。 |
| `LOCAL-20260802-002` | `documented` | 固定 Git tag 安装 MCP for Unity 10.1.0；UPM 锁定、PackageCache 注册、HTTP 服务、Unity 插件握手和实际工具调用均已确认。 | 8080 已监听，Unity 注册 35 个工具，MCP 只读与编辑器调用成功；当前 Codex 窗口仍需重启才能原生加载新增工具。 | `#editor-overview`、`#editor-verification`；批次未到 `ready-for-html`，禁止写入。 |
| `LOCAL-20260802-003` | `documented` | 修复 AICommands 全库失效引用，并为 Cmd Agent 增加模板自检、无效阻断、风险识别和直接使用说明。 | 52 个模板在 Unity 进程内批量解析为 0 个无效；ES_Stand、ES_Editor 和 Unity Console 均无编译错误。 | `#editor-overview`、`#deep-warning-16`；批次未到 `ready-for-html`，禁止写入。 |
| `LOCAL-20260802-004` | `documented` | 在项目根 `.agents/skills` 建立五个官方格式 ES Skills，并提供 AICommand、编译、UTF-8 和工作树确定性脚本。 | 五个 Skill 全部通过官方验证器；四个脚本完成 AST 与实际运行验证。 | `#editor-overview`、`#editor-verification`；批次未到 `ready-for-html`，禁止写入。 |
| `LOCAL-20260803-005` | `documented` | AIWarnings 与 AICommands 正式登记五个 Agent Skills，并新增三层协作边界、任务映射与能力展望。 | 五份文档严格 UTF-8；Skill 路径全部存在；AICommands 52/0；Unity 本轮 Console 待复核。 | `#editor-overview`、`#editor-verification`、`#deep-warning-16`；批次未到 `ready-for-html`，禁止写入。 |
| `LOCAL-20260803-006` | `documented` | 新增 GameCore、资源、Tag/Config、Entity、输入、ESCommand、编辑器工具和发布验收八个 ES 领域 Skill。 | 13 个 Skill 官方验证全部通过；29 个文件 UTF-8 通过；项目路径有效；AICommands 52/0。 | `#editor-overview`、`#editor-verification`、`#deep-warning-16`；批次未到 `ready-for-html`，禁止写入。 |
| `LOCAL-20260803-007` | `documented` | 在 `.agents/README.md` 建立统一项目级 AI 文件归属、Skill 结构规范和 13 个 Skill 简介。 | 三个入口文档 UTF-8 通过；13 个直接子目录结构完整；六个权威路径存在；AICommands 52/0。 | `#editor-overview`、`#editor-verification`；批次未到 `ready-for-html`，禁止写入。 |
| `LOCAL-20260803-008` | `documented` | 将明确可控的项目根 ES 配置、文档、工具、测试、输出、发布与资源管线目录统一迁入 `ES/`，并同步代码和序列化路径。 | 现行旧路径扫描为 0；Unity Tundra 编译和程序集重载成功；资源 Manifest、Git hook 与文档门禁已同步。 | `ES_DOCUMENT_SYNC`、`#editor-overview`、`#editor-verification`；仅机械同步路径，功能扩写仍延期。 |
| `LOCAL-20260803-009` | `documented` | AIWarnings 改为按任务分层加载，禁止普通任务递归读取全目录，并固定 P0 原文、跨系统分批摘要及历史/提案边界。 | 四个入口/边界文件已同步；外部路径有效；重复内部前缀已修复；UTF-8 与 diff 检查通过。 | `#editor-overview`、`#deep-warning-16`；批次未到 `ready-for-html`，功能说明延期。 |
| `LOCAL-20260803-010` | `documented` | 新增 UnityMCP 与 AI 工程验收代理路线图预备案，登记候选能力、阶段、边界和未来验收要求。 | 文件位于待验收提案区，明确未实现；Markdown、meta、UTF-8 与 diff 检查通过。 | `#editor-overview`、`#editor-verification`；仅备案，不写入现行能力说明。 |
| `LOCAL-20260803-011` | `documented` | 建立模块成熟度与未完成实现治理，新增统一状态、半成品隔离门禁、只读审计命令和 `$es-module-lifecycle`。 | AICommands 53/0；15 个文本文件 UTF-8 通过；Skill 等价结构与 meta GUID 已验证；同步验证器正确保持既有门禁。 | `#editor-overview`、`#deep-warning-16`；批次未到 `ready-for-html`，禁止写入。 |
| `LOCAL-20260803-012` | `documented` | pre-commit 改为 staged-only 语义批次门禁，未暂存和未跟踪工作不再阻断本次提交。 | 暂存补丁指纹、条目覆盖、台账同批暂存和 HTML 禁写规则已建立；不自动改暂存区或提交。 | `#editor-overview`、`#editor-verification`；功能说明延期整合。 |
| `LOCAL-20260803-013` | `documented` | 以工程级原子批次整合资源运行时、GameCore、角色、技能、载具、编辑器工作台与相关测试/规范。 | 已记录编译、导入与领域验收边界；完整运行回归仍待 Unity 稳定后执行。 | `#runtime-overview`、`#editor-overview`、`#editor-verification`；禁止拆散提前写入。 |
| `LOCAL-20260803-014` | `documented` | 恢复 Codex 协作历程、定位与会话恢复工具，保持历史档案和当前实现分离。 | 文件、工具和独立档案存在；其他旧格式记录未逐一复核。 | 不适用。 |
| `LOCAL-20260803-015` | `documented` | 补齐静态站点阅读和同步治理规则，明确 Git、台账、HTML 与验收证据的边界。 | UTF-8、staged-only 门禁与范围化空白检查通过。 | 文档治理章节；当前 HTML 仍不提前改写。 |
| `LOCAL-20260803-016` | `documented`，本地已提交待推送 | 模块审计增加受控续接检查点：默认只读、精确区域授权、事实漂移失效与恢复前重新核对。 | AICommands 53/0、文本编码与结构检查通过；未运行 Unity 或官方 Python 验证器。 | `#editor-overview`、`#deep-warning-16`；先 push 与统一回归，后续才可整合。 |
| `LOCAL-20260803-017` | `documented` | 固定模块审计状态路径，并建立“审计”“审计并记录”“继续审计”三个直接触发协议。 | 固定路径和触发词已同步至 Skill、AICommand、AIWarning 与入口索引；不扩大源码、Git、Unity 或发布权限。 | `#editor-overview`、`#deep-warning-16`；当前延期。 |
| `LOCAL-20260815-001` | `documented` | 新增 URP Composite Shader 完整切片，覆盖 2D、3D Lit、3D VFX、UI、PropertyBlock 强类型参数、案例材质与卡片化 Inspector。 | C# 静态构建通过；Unity 本次日志无 Shader/C# 编译错误；Inspector 视觉和运行表现仍待实机验收。 | `#runtime-overview`、`#editor-overview`、`#editor-verification`；当前延期。 |

### LOCAL-20260802-002：UnityMCP 安装与 Codex 接入

- **源码路径**：`Packages/manifest.json`、`Packages/packages-lock.json`。
- **规范与证据**：当前项目为 Unity `2022.3.45f1`；官方 `v10.1.0` 最低要求 Unity `2021.3`。网络复测确认 GitHub 443 与 `git ls-remote` 可用；Unity Package Manager 已锁定并注册 `com.coplaydev.unity-mcp`，`Library/ScriptAssemblies` 已生成 Runtime 与 Editor DLL。
- **完成分析**：项目使用固定 `#v10.1.0` Git URL，避免跟随 `main` 漂移；`uv 0.11.32` 已安装，UnityMCP 自动把 Codex 配置为 `http://127.0.0.1:8080/mcp`。本项只增加编辑器开发桥接，不修改 ES 运行时代码。
- **回归状态**：UPM 安装、锁文件和 Unity 程序集编译证据成立；`127.0.0.1:8080` 已监听，Unity 项目通过 WebSocket 注册 35 个工具，并已实际执行 Console 读取、资产重新导入和 Unity 内 C# 验证调用。
- **已知缺口**：当前 Codex 窗口不会热加载新增 MCP 工具，仍需重启后验证原生工具面；尚未运行 Unity Test Runner、PlayMode 或 Player 验证。
- **HTML 目标**：批次达到 `ready-for-html` 后，评估在 `#editor-overview` 与 `#editor-verification` 记录外部 AI 编辑器桥接及证据等级；当前禁止提前修改 HTML。

### LOCAL-20260802-003：AICommands 可用闭环

- **源码路径**：`Assets/Plugins/ES/AICommands`、`Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentWindow.cs`。
- **规范与证据**：读取 AIWarnings 入口、编辑器生命周期约束、GameCoreEditorGlobalData 与 AICommands 边界，以及 ESCmdAgent 失败复盘；所有模板只在用户打开选择器或准备发送时按需读取，不进入域重载自动扫盘。
- **完成分析**：52 个模板原有 115 条规则引用都重复了 `Assets/Plugins/ES` 前缀，复制后无法读取。现已全部指向真实文件；Cmd Agent 会校验命令类型、默认改文件、风险等级和项目内引用路径，无效命令标记为“无效”并禁止发送。可写权限改为只接受明确的“是”或“允许”前缀，避免模糊字符串判断。
- **回归状态**：`ES_Stand.csproj` 与 `ES_Editor.csproj` 均为 0 warning / 0 error；Unity 实际重新导入后 Console 为 0 error / 0 warning；Unity 内调用真实解析器得到 `files=52; invalid=0`，Prompt 路径、需求和边界组合验证通过。
- **已知缺口**：尚未人工逐个点击 52 个下拉项，也未新增 Unity Test Runner 自动化；批量模板解析和 Prompt 生成已在当前 Unity 进程内执行。
- **HTML 目标**：后续批次达到 `ready-for-html` 后，在 `#editor-overview` 和 `#deep-warning-16` 说明 AICommands 的发现、校验、授权和发送闭环；当前禁止提前修改 HTML。

### LOCAL-20260802-004：ES Agent Skills

- **源码路径**：`.agents/skills/es-use-ai-command`、`es-unity-compile`、`es-fix-compile-error`、`es-utf8-guard`、`es-worktree-audit`。
- **规范与证据**：使用 OpenAI `skill-creator` 官方初始化器生成目录和 `agents/openai.yaml`；项目级发现路径采用 `.agents/skills`，不进入 Unity `Assets`。每个 Skill 均包含必需的 `SKILL.md`，界面元数据使用 `openai.yaml`，确定性流程放在 `scripts`。
- **完成分析**：五个 Skill 分别负责选择并执行一个 AICommand、分层验证 Unity 编译证据、最小修复一个编译错误、守卫 UTF-8 与补丁完整性、审计脏工作树。它们读取实时 AIWarnings/AICommands，不复制 52 份命令内容，也不把 `.csproj` 编译冒充 Unity 验收。
- **回归状态**：在 `PYTHONUTF8=1` 下运行官方 `quick_validate.py`，五个 Skill 全部有效；四个 PowerShell 脚本均通过 AST 解析并实际执行。AICommand 校验为 52/0，Skill 文件 UTF-8 检查为 14/14，ES_Stand 构建包装器成功且保留证据边界。
- **已知缺口**：当前 Codex 窗口不会热加载新 Skill，需要从项目根启动新窗口或重启验证选择器；尚未用独立新会话完成五个真实任务的前向测试。
- **HTML 目标**：批次达到 `ready-for-html` 后，在 `#editor-overview` 和 `#editor-verification` 说明项目级 Skills、AICommands、AIWarnings 与 UnityMCP 的协作关系；当前禁止提前修改 HTML。

### LOCAL-20260803-005：Agent Skills 接入 AIWarnings / AICommands

- **源码路径**：AIWarnings 开始阅读入口、规则索引、新增 `AgentSkills与AICommands协作边界_AI协作警告.md`，以及 AICommands README 与命令合集索引。
- **规范与证据**：项目根五个 `.agents/skills/*/SKILL.md` 均真实存在；AICommand 自检脚本实跑为 `52 commands / 0 invalid`；五份文档严格 UTF-8 解码通过且不含 U+FFFD。
- **完成分析**：现在三层职责被正式固定：AIWarnings 管长期事实和禁止事项，AICommands 管本次任务授权，Agent Skills 管可复用执行工作流；UnityMCP 和脚本只执行或采证，不自行扩大权限。文档同时给出五个当前 Skill 的触发映射，并说明 Skills 不进入 Unity `Assets`。
- **回归状态**：路径、名称、UTF-8、AICommand 全库有效性与 scoped `git diff --check` 已验证；新增 Markdown 已补齐 Unity `.meta`。
- **已知缺口**：导入期间 Unity 编辑器退出，UnityMCP 报告插件会话断开，因此本轮没有可引用的 Unity Console 结果。本条登记时领域 Skill 尚未实现；后续第一版由 `LOCAL-20260803-006` 补齐，自动验收脚本、上下文采集和 Plugin 分发仍未实现。
- **HTML 目标**：批次达到 `ready-for-html` 后，再在 `#editor-overview`、`#editor-verification` 和 `#deep-warning-16` 统一解释三层协作模型；当前禁止提前修改 HTML。

### LOCAL-20260803-006：八个 ES 领域专用 Agent Skills

- **源码路径**：`.agents/skills` 下新增 `es-gamecore-integration`、`es-resource-pipeline`、`es-tag-config`、`es-entity-authoring`、`es-input-action`、`es-command-authoring`、`es-editor-tooling`、`es-release-acceptance`，并同步 AIWarnings/AICommands 五份协作文档。
- **规范与证据**：八个 Skill 均由官方 `init_skill.py` 初始化，包含必需的 `SKILL.md`、`agents/openai.yaml` 和单层 `references` 导航；项目根现有 13 个 Skill 全部通过官方 `quick_validate.py`。
- **完成分析**：领域 Skill 固化 ES 独有的源码入口、AIWarning 路由、AICommand 映射、所有权和生命周期判断、最小修改工作流及证据交付。它们不会复制框架运行时代码，不进入 Unity `Assets`，也不会因为掌握领域流程而自动扩大本次写权限。
- **回归状态**：八个新 Skill 内所有字面项目路径均真实存在；本轮 29 个文件严格 UTF-8、无 U+FFFD 和疑似乱码，scoped `git diff --check` 通过；AICommands 实跑为 `52 commands / 0 invalid`。`es-release-acceptance` 已声明 `unityMCP` 依赖。
- **已知缺口**：当前窗口不会热加载新 Skill，需要从项目根新开窗口或重启验证选择器；尚未在独立新会话中逐个完成八项真实任务前向测试；本轮没有新增自动 Unity、Profiler、Player、IL2CPP 或发布执行脚本。
- **HTML 目标**：批次达到 `ready-for-html` 后，再在 `#editor-overview`、`#editor-verification` 与 `#deep-warning-16` 统一说明基础 Skill 与领域 Skill 的协作层次；当前禁止提前修改 HTML。

### LOCAL-20260803-007：项目级 Agent 文件夹组织规范

- **源码路径**：新增 `.agents/README.md`，并在 AIWarnings README 与 AICommands README 登记该统一入口。
- **规范与证据**：项目内 AI 文件采用“一个索引、多个唯一权威路径”；`.agents/skills`、AIWarnings、AICommands、Documentation、AI 协作历程与 `ES/Documentation/StaticSite` 均保持真实固定位置，不复制正文。
- **完成分析**：Skill 必须直接归属 `.agents/skills/<skill-name>`，内部只使用 `SKILL.md`、`agents/openai.yaml` 和按需创建的 `references/scripts/assets`。基础层与领域层只在目录索引中分类，不增加可能影响发现的中间文件夹。
- **回归状态**：三个入口文件严格 UTF-8 和 scoped `git diff --check` 通过；13 个 Skill 均存在必需文件；六个目录入口真实存在；AICommands 为 `52 commands / 0 invalid`。
- **已知缺口**：本轮只整理项目级 AI 文件归属，没有擅自移动项目根历史压缩包、临时测试文件或生成目录。Codex 与 Unity 的固定发现路径不能物理合并；当前窗口仍需重启验证新 Skill 的选择器展示。
- **HTML 目标**：批次达到 `ready-for-html` 后，在 `#editor-overview` 和 `#editor-verification` 统一说明项目级 Agent 能力入口；当前禁止提前修改 HTML。

### LOCAL-20260803-008：ES 可控目录完整迁移

- **源码路径**：`ES/Config`、`ES/Documentation`、`ES/Tools`、`ES/Tests`、`ES/Output`、`ES/Releases`、`ES/ResourcePipeline`，以及资源管线、UnityPackage、Luban、SoTable 对应的路径代码和编辑器序列化配置。
- **规范与证据**：只迁移明确属于 ES 且项目可控的路径；Unity 固定目录、第三方包、`.agents/skills`、AIWarnings、AICommands 和历史协作档案不迁移。物理目录、C# 路径合同、序列化默认值、PowerShell/Bat 脚本、资源 Manifest、Git hook、忽略规则和现行文档引用保持一致。
- **完成分析**：资源四阶段输出统一归入 `ES/ResourcePipeline`；Luban 与 SoTable 归入 `ES/Config`；静态站点、同步规则和台账归入 `ES/Documentation/StaticSite`；项目级工具、测试夹具、输出和发布包分别进入对应 ES 分区。迁移后的代码不会继续向旧项目根目录生成同名文件夹。
- **回归状态**：排除 `ES/AI协作历程（Codex）` 历史事实后，代码、序列化资产、脚本、Manifest 与现行文档中的旧目录引用扫描为 0。Unity `Editor.log` 显示 `Tundra build success`、程序集重载成功，并实际加载新的编辑器配置序列化资产；八个 InitialTarget Manifest 的绝对依赖路径已同步。
- **已知缺口**：独立 `.csproj` 构建被工作区既有删除文件仍残留在 Unity 生成项目中的引用阻断，不构成本次迁移编译失败；未运行 PlayMode、Profiler、IL2CPP Player 或真实 CDN 发布。项目根 `NormalResources/Sprites` 为空，但删除操作被当前命令策略拦截。
- **HTML 目标**：本轮只同步静态站点自身路径指针和内嵌旧路径，不扩写功能章节；行为内容仍按开放批次规则延期整合。

### LOCAL-20260803-009：AIWarnings 按任务分层加载

- **源码路径**：AIWarnings 的 README、CurrentStatus、RuleIndex、Agent Skills 与 AICommands 协作边界，以及一条 GameCore P0 内部引用。
- **规范与证据**：固定 `README -> CurrentStatus -> RuleIndex -> 命中的 P0 -> 当前领域专项 -> 直接关联交接/复盘 -> 必要时历史与提案`。普通任务不得递归读取全部 AIWarnings；P0、现行状态和任务专项必须读取原文。
- **完成分析**：普通任务约 1～2 万字符、复杂跨系统任务约 2～5 万字符只作为上下文预算建议。跨系统任务按领域分批读取，摘要必须保留规则路径、状态、结论、禁止事项和证据入口，不能冒充全部原文已复核。
- **回归状态**：四个入口/边界文件已登记相同加载协议；AIWarnings 内 Assets 外路径均存在，旧迁移路径命中为 0；重复 `Assets/Plugins/ES` 前缀已修复；相关文件严格 UTF-8 与 scoped `git diff --check` 通过。
- **已知缺口**：token 消耗仍取决于模型分词、中文、代码和长路径比例；本轮没有对全部历史规则重新逐条审阅。
- **HTML 目标**：批次达到 `ready-for-html` 后，再在编辑器协作与警告治理章节说明分层加载机制；当前不提前扩写 HTML。

### LOCAL-20260803-010：UnityMCP 与 AI 工程验收代理预备案

- **源码路径**：`90_提案与废止（Archive）/待验收提案（Proposals）/UnityMCP_AI工程验收代理与自动化能力路线图_预备案提案.md` 及 Unity `.meta`。
- **规范与证据**：提案登记 Unity 一键验收、序列化健康审计、任务上下文、Prefab 契约、资源发布、性能预算、ReloadDomain、语义 Diff、安全回滚和证据追踪，并按只读采证、受控验收、高风险自动化分阶段。
- **完成分析**：文件只防止方向丢失，不是开发计划、授权合同、现行架构事实或已交付能力。任何能力开工前仍需重新读取当前规则、源码和 AICommand，并取得用户明确授权。
- **回归状态**：文件位于待验收提案目录；状态、前置条件、禁止事项和未来验收要求完整；Markdown 与 meta 通过严格 UTF-8、GUID 唯一性和 scoped `git diff --check`。
- **已知缺口**：本轮没有实现任何候选能力，没有新增 Skill、AICommand、Unity 自动化或发布操作。
- **HTML 目标**：仅在未来能力真实落地并取得证据后，才允许进入现行技术文档；当前保持延期。

### LOCAL-20260803-011：模块成熟度与未完成实现治理

- **源码路径**：新增模块成熟度 AIWarning、`检查_模块成熟度与半成品影响_AI命令.md`、`.agents/skills/es-module-lifecycle`，同步 AIWarnings、AICommands 和 `.agents` 三组入口索引，并修复台账快照与同步验证脚本对 Git 空输出的兼容。
- **规范与证据**：统一使用 `Proposed`、`Scaffolded`、`Experimental`、`Implementing`、`Integrating`、`Verifying`、`Stable`、`Deprecated`、`Archived`；`Blocked` 只作为附加结论。状态必须回到模块边界、默认激活、依赖方向和分层证据，不能由目录、接口、TODO 或完成百分比决定。
- **完成分析**：未开始模块只保留提案，不创建伪实现；开发中模块必须可编译、显式隔离、明确失败并可退出；稳定模块不得静默依赖半成品。只读 AICommand 负责单次审计权限，Skill 负责复用检查流程，二者都不能扩大用户授权。
- **回归状态**：AICommands 全库实跑为 `53 commands / 0 invalid`；本轮 15 个 Markdown、YAML、JSON 与 PowerShell 文件通过严格 UTF-8 和 scoped diff 检查；新增 Skill 的 frontmatter、名称、默认 Prompt、简介长度与行数完成等价结构检查；两个 Unity meta GUID 唯一；台账快照与同步验证脚本不再对空集合调用 `string.Join`，并在脚本入口显式设置 UTF-8，避免 Git GUI/Windows PowerShell 5.1 损坏中文 Git 路径。
- **已知缺口**：当前终端找不到可运行的 Python/uv，新增 Skill 尚未取得官方 `quick_validate.py` 实跑证据；当前窗口不会热加载该 Skill；本轮没有对现有全部模块逐个分类，也没有运行 Unity、Test Runner、PlayMode、Profiler、Player 或发布验收；`DOCUMENT_SYNC` 仍被当前源码漂移和既有 `INTAKE-20260802-001 = needs-triage` 阻断。
- **HTML 目标**：批次达到 `ready-for-html` 后，再在 `#editor-overview` 与 `#deep-warning-16` 解释模块状态和半成品治理；当前保持延期。

### LOCAL-20260803-012：staged-only 分批提交门禁

- **源码路径**：新增 `Prepare-DocumentStagedBatch.ps1` 与 `Verify-DocumentStagedBatch.ps1`，切换 `.githooks/pre-commit`，同步 DOCUMENT_SYNC 和本地台账说明。
- **规范与证据**：pre-commit 只读取 `git diff --cached HEAD`。暂存源码必须由一个或多个 `documented` 条目覆盖，并与 HEAD、补丁 SHA-256、路径清单和文件数完全匹配；台账 JSON 与同步 JSON 必须同批暂存且不能落后于工作副本。
- **完成分析**：其他未暂存或未跟踪文件不再阻断当前提交；`INTAKE-20260802-001` 可继续描述尚未分批的剩余工作。完整 HTML 整合仍使用原全工作区验证器，stage-only 不会放宽 HTML、回归或发布证据。
- **回归状态**：两个新脚本使用显式 UTF-8，保持中文路径安全；真实 index 始终保持原有 1 个暂存文件。替代 index 已验证三条路径：1 个由 `LOCAL-20260803-006` 覆盖的源码文件与同批台账通过；准备后追加未覆盖源码被补丁、清单、数量和覆盖检查拒绝；只暂存门禁文档且没有源码时通过。
- **已知缺口**：当前暂存区由用户继续控制；目录迁移、API 与调用方、Prefab/Scene 与依赖资产必须按可编译、可导入的语义批次组合。
- **HTML 目标**：批次达到 `ready-for-html` 后，再写入 `#editor-overview` 与 `#editor-verification`；当前保持延期。

### LOCAL-20260803-013：ES 工程功能整合批次

- **源码路径**：`Assets`、`Documentation` 与解决方案入口，覆盖资源运行时和发布、GameCore 数据、角色与交互、Buff/属性、技能与 Operation、载具、音频、剧情、相机、编辑器工作台、Prefab/Scene 和测试。
- **规范与证据**：旧资源 V1 删除与 `Obsolete/ResourceV1` 归档、新资源 Scope/Provider/发布链、Group/Info 契约、运行时模块、编辑器生成器、领域 AIWarnings、规范和测试资产保持同批。
- **完成分析**：这些改动存在类型、程序集、Unity meta 和序列化引用依赖，拆成只删旧实现、只加新实现或只提交场景资产会制造不可恢复的中间状态，因此作为一个工程级原子批次提交。
- **回归状态**：提交前执行 staged-only 路径覆盖、补丁指纹和 `git diff --check`；批次携带 EditMode 合同测试和测试场景，但本次提交不冒充 Unity Test Runner、PlayMode、Profiler、IL2CPP Player 或真实发布通过。
- **已知缺口**：完整运行回归需在 Unity 完成导入并稳定编译后按领域执行。
- **HTML 目标**：后续统一整理 `#runtime-overview`、`#editor-overview` 与 `#editor-verification`，当前保持延期。

### LOCAL-20260803-014：Codex 协作历程恢复档案

- **源码路径**：`ES/AI协作历程（Codex）` 下 README、八份独立窗口历程和两个本地 session 恢复工具。
- **规范与证据**：历史文件保留原档案 ID；定位工具只负责候选排序，核对路径、时间、CWD、首尾提示和档案尾部后才能恢复。
- **完成分析**：这些文件记录外部窗口事实，不归属本窗口实现；新窗口必须建立自己的档案，不能继续追加已收尾记录。
- **回归状态**：README、定位工具、恢复工具与独立历程文件均存在，并与功能源码分批提交。
- **已知缺口**：其他旧格式历程不代表已经全部逐条恢复。
- **HTML 目标**：不适用。

### LOCAL-20260803-015：静态文档阅读与同步规则

- **源码路径**：`DOCUMENT_READER_STANDARD.md` 与 `DOCUMENT_SYNC.md`。
- **规范与证据**：明确源码事实、Git、本地台账、HTML 产物和 Unity 分层验收证据的职责边界。
- **完成分析**：两份 Markdown 是治理规则，不是生成后的 HTML，可以独立提交，但不能据此宣称 HTML 已吸收全部源码批次。
- **回归状态**：严格 UTF-8、staged-only 门禁和范围化空白检查通过。
- **已知缺口**：HTML 仍需在批次达到 `ready-for-html` 后统一生成和验收。
- **HTML 目标**：未来文档治理章节，当前延期。

### LOCAL-20260803-016：模块审计续接状态闭环

- **源码路径**：模块成熟度 AIWarning、只读审计 AICommand、`$es-module-lifecycle`、续接状态契约，以及 AIWarnings、AICommands、Skills 三组入口索引。
- **规范与证据**：新增 `audit-only`、可选 `audit+checkpoint` 与 `resume`。默认不写；用户确认精确文件和区域后，只更新稳定标记块，并记录 Git 基线、证据、下一动作和失效条件。
- **完成分析**：检查点只降低下次定位成本，不替代源码、Unity 证据或最新规则，也不向未来窗口授予实现、Git、Unity 或发布权限。事实漂移后必须先报告 `stale` 字段。
- **回归状态**：12 个目标文本严格 UTF-8 通过；AICommands 为 `53 commands / 0 invalid`；Skill frontmatter、引用、行数、默认 Prompt 与简介长度完成等价结构检查；范围化 `git diff --check` 通过。
- **已知缺口**：当前终端没有可运行的 Python/uv/uvx，官方 `quick_validate.py` 未实跑；本轮没有创建实际模块状态检查点，也没有运行 Unity 或发布验收。
- **HTML 目标**：未来在 `#editor-overview` 与 `#deep-warning-16` 解释受控续接机制，当前延期。

### LOCAL-20260803-017：模块审计固定入口与短语触发

- **源码路径**：`ES/Documentation/Status/MODULE_AUDIT_STATE.md`、`$es-module-lifecycle`、模块成熟度 AICommand/AIWarning，以及 AIWarnings、AICommands、Skills 三组入口索引。
- **规范与证据**：唯一续接状态路径固定为 `ES/Documentation/Status/MODULE_AUDIT_STATE.md`；“审计”默认只读，“审计并记录”更新目标模块块，“继续审计”读取固定文件并重新核对事实。
- **完成分析**：用户不再提供文件路径和区域。稳定模块键隔离各模块记录；普通审计不会自动写文件，记录权限也不扩大为源码、Git、Unity 或发布授权。
- **回归状态**：固定路径和三个触发词已在 Skill、AICommand、AIWarning 与入口索引中统一；执行严格 UTF-8、AICommand 全库和范围化 diff 检查。
- **已知缺口**：当前环境没有可运行的 Python/uv/uvx，官方 `quick_validate.py` 仍无法实跑；本轮未运行 Unity 或发布验收。
- **HTML 目标**：未来在 `#editor-overview` 与 `#deep-warning-16` 解释固定续接入口，当前延期。

### LOCAL-20260808-001：AI 协作治理与会话恢复

- **源码路径**：`.agents`、`.codex`、`AGENTS.md`、AICommands、AIWarnings、Codex 历程目录及其受管压缩副本。
- **规范与证据**：项目 Skill 定义会话启动、上下文验证、审计路由和权限边界；AICommands 与 AIWarnings 提供用户可见的规则入口；历程目录保留独立会话事实与恢复工具。
- **完成分析**：这组文件共同约束 AI 的发现、执行和恢复流程，拆开会使规则入口、会话协议或历史档案处于不完整状态，因此作为治理批次提交。
- **回归状态**：提交前执行 staged-only 范围覆盖、路径清单和补丁指纹校验；本批不宣称 Unity、Test Runner、PlayMode、Player 或发布验收已完成。
- **已知缺口**：Skill 和规则不扩大后续源码、Git、Unity 或发布权限。
- **HTML 目标**：后续在 `#editor-overview` 与 `#deep-warning-16` 汇总，当前延期。

### LOCAL-20260808-002：受管自动化与编辑器输入

- **源码路径**：Automation Editor 入口、CmdAgent、通用 ESAdvancedDialog、Automation 合约/Worker/受管 Python 运行时、验证资产和场景扫描记录。
- **规范与证据**：AI Bridge 通过 JSON 契约和 Inbox 进入 Unity 主线程；受管运行时锁定解释器；通用对话框只收集输入，不执行命令或业务副作用。
- **完成分析**：请求、运行时、Worker、报告和 Editor 门面相互引用，必须同批保持以避免缺少协议、实现或证据。
- **回归状态**：批次携带 Unity 编译与 ReloadDomain 记录、测试入口与受管运行时锁；提交前执行 staged-only 指纹门禁。
- **已知缺口**：生产环境、端到端 Inbox 与 PlayMode 仍需按实际环境复核；运行记录只代表已发生的证据快照。
- **HTML 目标**：后续在 `#editor-overview` 与 `#editor-verification` 汇总，当前延期。

### LOCAL-20260808-003：资源发布与受管 IO

- **源码路径**：资源运行时、受管 IO、Bundle 发布/安装器、资源计划、旧资源归档、相关资产和定向测试。
- **规范与证据**：受管路径、唯一暂存、哈希复核、冲突拒绝和最佳努力恢复统一约束发布与安装链。
- **完成分析**：资源加载、文件写入、发布和安装存在同一数据边界，必须与调用方和验证资产一并提交，避免保留绕过保护的中间状态。
- **回归状态**：批次包含受管 IO 与信任边界 EditMode 测试入口；提交前执行 staged-only 指纹门禁。
- **已知缺口**：生产公钥、真实签名包和供应链轮换仍需环境验收；源码提交不替代 Unity 故障注入。
- **HTML 目标**：后续在 `#runtime-overview` 与 `#editor-verification` 汇总，当前延期。

### LOCAL-20260808-004：Graph V2 与 Agent Authoring

- **源码路径**：Graph V2 数据与编辑器、Agent Authoring 资产、旧 NodeRunner 隔离代码和定向测试。
- **规范与证据**：图资产的稳定身份、保存/回滚保护和 legacy scope 共同约束创作入口。
- **完成分析**：数据模型、编辑器和资产引用必须同批，避免中间提交产生不可打开资产或失效的回滚语义。
- **回归状态**：批次包含 Graph 合同和 Agent Authoring 测试入口；提交前执行 staged-only 指纹门禁。
- **已知缺口**：真实 Undo/Redo、域重载、外部修改故障注入及旧 NodeRunner 的完整迁移仍待 Unity Editor 验收。
- **HTML 目标**：后续在 `#editor-overview` 与 `#editor-verification` 汇总，当前延期。

### LOCAL-20260808-005：AITest Package 与业务能力接入

- **源码路径**：嵌入式 AITest Package、ESLogic Editor/Runtime 能力提供者和 Package 锁。
- **规范与证据**：协议、运行时、UGUI 观察执行、能力注册、输入及场景验证通过 asmdef 与 Package 依赖协同。
- **完成分析**：Package 和 ESLogic 桥接必须随锁文件同批，避免 Unity 无法解析嵌入式依赖或能力发现缺失。
- **回归状态**：提交前执行 staged-only 指纹门禁；程序集、协议和提供者保持同一可追溯范围。
- **已知缺口**：独立 Player 的完整 AI 闭环、Test Runner、IL2CPP 和网络环境仍需真实验收。
- **HTML 目标**：后续在 `#runtime-overview` 与 `#editor-verification` 汇总，当前延期。

### LOCAL-20260808-006：相机、Profile 与动态图集

- **源码路径**：相机定义资产、迁移工具、运行时导演、轨道预览、通用 Profile、动态图集、GameManager 接入和测试。
- **规范与证据**：新定义资产取代旧 Profile 资产；动态图集通过受管资源临时 Lease 上传，并由模块与 Graphic 生命周期管理。
- **完成分析**：资产迁移、模块接入与运行时/Editor 代码不可拆分，否则会留下已删除类型引用或缺失图集模块。
- **回归状态**：批次携带相机、Profile、动态图集 EditMode/PlayMode 测试入口；提交前执行 staged-only 指纹门禁。
- **已知缺口**：真实场景、GPU、窄屏 UI、Player/IL2CPP 和目标项目旧资产迁移仍需 Unity 验收。
- **HTML 目标**：后续在 `#runtime-overview` 与 `#editor-overview` 汇总，当前延期。

### LOCAL-20260808-007：角色与玩法运行时

- **源码路径**：角色、载具、音频、状态、标签与 GameCore 的运行时代码、模板构建入口、测试场景和序列化资产。
- **规范与证据**：实体领域和运行时模块由角色/载具资产、状态配置与测试场景共同驱动。
- **完成分析**：模型代码、生成入口和预制体/资产引用不能独立提交，否则容易出现缺失类型或绑定中断。
- **回归状态**：批次携带领域测试、运行时契约和 Unity 测试场景；提交前执行 staged-only 指纹门禁。
- **已知缺口**：完整角色、载具、音频和存档流程及所有序列化资产仍需 PlayMode/Player 实测。
- **HTML 目标**：后续在 `#runtime-overview` 与 `#editor-verification` 汇总，当前延期。

### LOCAL-20260808-008：Editor 交付体验与项目收口

- **源码路径**：Editor 绘制器、窗口、工具栏、主题和项目导航资产、Unity meta、程序集/项目设置、文档台账及归档文件。
- **规范与证据**：用户可见的状态、下一步、定位入口和排版规则与 Editor 工具共同维护；Unity meta 与对应目录同批保留 GUID 身份。
- **完成分析**：Editor UI、项目设置和文档台账在交付侧共同约束可发现性和可追溯性，单独提交会让入口或元数据缺失。
- **回归状态**：提交前执行 staged-only 指纹门禁；规则与工具已纳入同一可追溯范围。
- **已知缺口**：真实窄窗口、高 DPI、视觉和交互仍需 Unity 实机检查。未跟踪静态 HTML 仍被开放批次的 `ready-for-html` 门禁阻断，未绕过。
- **HTML 目标**：后续统一整合，当前延期。

### LOCAL-20260815-001：URP Composite Shader 与卡片化材质面板

- **源码路径**：`0_Stand/BaseDefine_RunTime/ShaderSystem`、`0_Stand/InternalAssets/Shaders`、`0_Stand/InternalAssets/ShaderExamples` 与 `Editor/ESShader`，包含对应 Unity `.meta`。
- **规范与证据**：只支持 URP，按 2D、3D Lit、3D VFX、UI 分离 Shader 职责；运行时通过强类型属性 ID 和 `MaterialPropertyBlock` 写入，编辑器通过分类卡片、功能卡片、中文标签、搜索导航、状态/成本提示及逐属性 C# 示例组织材质参数。
- **完成分析**：Shader、共享 HLSL、属性合同、案例材质与 CustomEditor 必须同批，否则会产生材质 Shader 丢失、属性名漂移或自定义 Inspector 无法加载。排版借鉴成熟的分类/功能折叠策略，但没有复制第三方 Shader 或 Editor 源码。
- **回归状态**：`ES_Stand.csproj` 为 0 警告、0 错误；`ES_Editor.csproj` 为 0 错误，仅有两个与 Shader 无关的既有警告；Unity 2022.3.45f1 启动当前工程后，本次 `Editor.log` 区段没有 Shader/C# 编译错误；目标文本通过严格 UTF-8 与范围化空白检查。
- **已知缺口**：Inspector 的窄窗口、高 DPI、多选混合值与完整点击交互仍需 Unity 实机人工验收；案例材质的 PlayMode、Profiler、Player、IL2CPP 和各平台 Shader Variant 尚未验收。
- **HTML 目标**：后续在 `#runtime-overview`、`#editor-overview` 与 `#editor-verification` 统一整合，当前不修改正式 HTML。

### LOCAL-20260816-001：HybridCLR 与 Luban 嵌入包治理

- **源码路径**：`Packages/com.code-philosophy.hybridclr`、`Packages/com.code-philosophy.luban`、`Packages/packages-lock.json`、`Assets/Plugins/ES/Editor/Installer/ESExternalPackageMenuSuppressor.cs` 及其 `.meta`、`ES/Tools/Validation/Test-ESEmbeddedPackages.ps1`。
- **规范与证据**：HybridCLR 8.12.0 与 Luban 1.2.0 以项目内嵌包存在，Package 锁记录使用 `embedded`；第三方原生顶栏菜单在嵌入源码中隔离，ES 保留轻量兼容入口和可复现检查脚本。
- **完成分析**：嵌入目录、锁文件、菜单隔离和验证门禁必须同批提交，否则会形成包来源、Unity 解析状态或入口治理不一致的中间状态。第三方源码与 Unity `.meta` 的上游空白格式保持原样，不做难以追溯的机械清洗。
- **回归状态**：`Test-ESEmbeddedPackages.ps1` 返回 HardFailures 0、Warnings 0；两个 `package.json` 的名称和版本匹配；staged 高置信凭据扫描无命中，最大文件为 1.121 MiB。
- **已知缺口**：本轮尚未重新观察 Unity Package Manager 的 Embedded 状态、脚本导入、Domain Reload、Player、IL2CPP 或热更新真实运行；上游 vendored-source 空白格式会使范围化 `git diff --check` 报告尾随空白。
- **HTML 目标**：后续在 `#editor-overview` 与 `#editor-verification` 统一整合，当前不修改正式 HTML。

### LOCAL-20260816-002：StableGraph 与 Agent Authoring 创作闭环

- **源码路径**：`1_Design/Graph`、Graph 定向测试、`Editor/ESGraphViewV2`、AICommand 目录与 Graph/Automation/Agent Skills 专项 AIWarnings。
- **规范与证据**：稳定身份、快照/变更语义、编辑服务、payload 迁移、候选生成和导入记录共同约束 AISkill 执行与 Agent Artifact 生命周期；Graph 当前保持 `Verifying`，`Program` 仅保留给尚未实现的 `ESBehaviorTreeProgram`。
- **完成分析**：数据合同、编辑器交互、生成计划、导入记录和 AICommand 目录必须同批，避免序列化版本错位、候选缺少追溯信息或编辑器使用旧合同。
- **回归状态**：`ES_Design.csproj` 与 `ES_Editor.csproj` 均为 0 警告、0 错误；`ES_Design.ConfigKey.Tests.csproj` 为 0 错误，仅保留两个既有 `CS0649` 警告；34 个目标文本通过严格 UTF-8 检查。
- **已知缺口**：本轮没有 UnityMCP，未取得 Unity Editor 编译、ReloadDomain、Test Runner 或真实 GraphView 交互证据；少数 Unity `.meta` 空字段保留 Unity 序列化尾空格格式。
- **HTML 目标**：后续在 `#editor-overview` 与 `#editor-verification` 统一整合，当前不修改正式 HTML。

### LOCAL-20260816-003：UI、本地化与剧情呈现链

- **源码路径**：十语言稳定身份和本地化测试、字体制作工具、Story 文本引用、运行时 UI Window、Dialog 模态合同及对应 Unity `.meta`。
- **规范与证据**：本地化目录、运行时字体目录和字体生成工具以同一语言身份协作；Story 与 UI 使用稳定文本/窗口身份；Dialog 只在宿主实现原生模态 Presenter 时提供同步调用。
- **完成分析**：语言、字体、剧情文本和 UI 生命周期属于同一呈现数据边界，必须保持序列化身份和运行时注册顺序一致，避免字体目录已更新但 Story/UI 仍使用旧身份。
- **回归状态**：`ES_Stand.csproj`、`ES_Design.csproj`、`ES_Logic.csproj`、`ES_Logic.UI.Tests.csproj`、`ES_Logic.Story.Tests.csproj` 与 `ES_Design.ConfigKey.Tests.csproj` 静态构建均为 0 警告、0 错误。
- **已知缺口**：没有 Unity Editor 编译、ReloadDomain 或 Test Runner 证据；字体资产生成、TMP Fallback、多语言字形和 UI Window PlayMode 生命周期仍需 Unity 实机验收。
- **HTML 目标**：后续在 `#runtime-overview`、`#editor-overview` 与 `#editor-verification` 统一整合，当前不修改正式 HTML。

### LOCAL-20260816-004：核心身份与通用实例表

- **源码路径**：稳定镜像映射、`ESInstanceTable`、`ESConfigKeyTable`、本地化运行时目录类型和对应定向测试。
- **规范与证据**：实例 token、持久身份、定义键和所有者键由通用表统一；同一 ConfigKey 数据实例不能接受第二个不同 StringKey；本地化目录提供运行时解析和校验合同。
- **完成分析**：镜像身份、配置身份与运行时实例索引属于共享底层合同，同批提交可避免上层装备、Buff、Shot 或本地化目录继续依赖已移除的旧实例索引语义。
- **回归状态**：`ES_Stand.csproj`、`ES_Design.csproj`、`ES_Logic.csproj` 与 `ES_Design.ConfigKey.Tests.csproj` 在当前完整工作树上静态构建均为 0 警告、0 错误；批次携带三组定向 NUnit 测试源码。
- **已知缺口**：静态构建包含仍未提交的工作树依赖，不能证明单个 Commit 独立可构建；未运行 Unity Test Runner、ReloadDomain、PlayMode、Player 或 IL2CPP。
- **HTML 目标**：后续在 `#runtime-overview` 与 `#editor-verification` 统一整合，当前不修改正式 HTML。

### LOCAL-20260816-005：实体装备、物品实例与角色表现

- **源码路径**：Item/Weapon ConfigKey、物品实例表、Equipment Domain、装备挂接与武器绑定、角色/武器构建器、Prefab、测试场景和领域测试。
- **规范与证据**：定义身份与实例身份分离；装备事务由独立 Domain 管理库存、槽位、挂接和效果；角色内挂点通过稳定映射解析；移动脚本保留原 Unity GUID。
- **完成分析**：定义、实例、装备事务、动画事件、表现挂点和正式 Prefab 必须同批，否则会留下旧 Basic Domain 类型引用、失效挂点或无法解析的 Weapon/Item Key。
- **回归状态**：`ES_Logic.Editor.Generation.Tests.csproj` 为 0 警告、0 错误；`ES_Logic.Editor.csproj` 为 0 错误并保留 17 个非本批阻断警告；39 个目标文本通过严格 UTF-8 检查。
- **已知缺口**：未运行 Unity Test Runner、ReloadDomain、PlayMode、Player 或 IL2CPP；装备动画事件、视图转移、存档恢复及多人并发仍需真实运行验收；Unity YAML 空字段尾空格按序列化格式保留。
- **HTML 目标**：后续在 `#runtime-overview`、`#editor-overview` 与 `#editor-verification` 统一整合，当前不修改正式 HTML。

## 条目模板

每新增一个条目，必须同时更新 JSON 与本表。JSON 字段是门禁输入；本表是人类评审入口。

| ID | 当前状态 | 本地完成更新总结 | 证据与影响范围 | 回归与已知缺口 | HTML 目标 |
| --- | --- | --- | --- | --- |
| `LOCAL-YYYYMMDD-001` | `documented` | 行为、边界、受益者和失败模式。 | 源码 / AIWarnings / 测试 / 资产路径。 | 已跑证据，或明确未覆盖的环境。 | 锚点、章节、表格或流程。 |

禁止使用“代码已改”“待 AI 处理”“同上”“看 diff”作为摘要。条目需要独立成立，才能在批次整合时被合并、延期或拒绝。
