# AIKnowledge 维护事务与并发一致性

`KnowledgeId`: `es.knowledge.maintenance-transaction.v1`
`Authority`: `Current AIKnowledge command, validator contract, and maintenance scripts`
`RouteKeys`: `knowledge`, `maintenance-transaction`, `refresh-plan`, `stable-refresh`, `source-ref`, `content-hash`, `cas`, `concurrent-update`, `atomic-projection`, `recovery`, `stale`
`ContentHash`: `2d43c9c5d24990cda2f1b052949e21fda3f9823da7903ca95b54eee20bc3e78b`
`EvidenceLevel`: `S2`
`ExternalReviewDate`: `2026-08-24`
`StaleWhen`: 受管 AIKnowledge 更新命令、Knowledge Creator/Validator 合同、refresh-plan 或 stable-refresh 脚本的输入、哈希、并发拒绝、写入或恢复语义变化；采用互斥、持久化或 generation 方案前还必须重新核对对应官方资料和目标平台

## Scope

本条目是 AIKnowledge **维护事务**的 canonical owner，负责从 SourceRef 漂移发现到 Entry/Index 重新闭合的可执行顺序、并发拒绝和失败恢复。

本条目不负责：

- 判断知识内容是否高质量、是否重复或路由是否准确；这些属于 `es.knowledge.routing-quality.v1`。
- 提供通用文件读取缓存、Parser 或 ProjectionPacket；这些属于 `es.engineering.task-read-snapshot.v1`。
- 授予 KnowledgeIndex、AIBRAIN_ENTRY、Skill、源码、Git、Unity、发布或删除权限。
- 把当前协作式跨进程锁、目标文件 CAS 和可捕获异常回滚外推为任意写者互斥、读者快照一致或进程崩溃级原子提交能力。

## Trigger and routing

自然语言触发包括：“刷新 SourceRef 哈希”“批量修复 stale Knowledge”“防止多个窗口互相覆盖”“KnowledgeIndex 与条目不一致”“刷新计划过期”“维护事务失败后怎么恢复”。

优先路由：

1. 维护、刷新、并发覆盖或失败恢复：本条目。
2. 内容质量、canonical 去重、路由探针：`es.knowledge.routing-quality.v1`。
3. 多工具重复读取、Parser 投影或通用缓存失效：`es.engineering.task-read-snapshot.v1`。

只出现 `transaction`、`snapshot`、`cache` 或 `hash`，但没有 AIKnowledge、Entry、Index、SourceRef、刷新或维护语义时，不得命中本条目。

## Current mechanism

### Canonical SourceRef hashing and dependency convergence

SourceRef hashes use `es-source-ref-hash-v2`: UTF-8 text sources normalize BOM-free decoding and all `CRLF`/`CR` line endings to `LF` before hashing; binary sources retain raw-byte SHA-256. Entry/Index CAS hashes remain raw-byte hashes. The validator, refresh-plan exporter, and stable refresher must use the same source-hash function. A refresh may update an upstream Knowledge entry that is itself referenced by another entry, so maintenance runs a bounded fixed-point loop: regenerate the plan, apply only a ready stable plan, and repeat until `SOURCE_HASH_DRIFT=0`; exceeding the configured iteration limit is `blocked`, never silently accepted.

`Invoke-ESKnowledgeFixedPointRefresh.ps1` packages this loop with a maximum of eight iterations and refuses to continue after a blocked plan or non-zero `staleAtApplyCount`.

以下事实由当前合同和脚本静态证明：

1. `Export-ESKnowledgeRefreshPlan.ps1` 严格解析每个带 KnowledgeId 或 SourceRefs 标记的条目，要求恰好一个 SourceRefs 节、至少一个格式规范的 bullet，以及项目根内、非 reparse、存在且不重复的来源路径；结构或路径不合法时写入 `reject-*` blocker、令 `planStatus=blocked`，并以 exit 2 结束。
2. Exporter 对每个去重后的 SourceRef 连续采样两次；两次哈希不同则输出 `wait-for-source-stability`，稳定但与声明不一致则输出 `review-and-refresh-source-ref`。
3. schema v3 刷新计划的 `entrySnapshots[]` 为每个识别条目记录当前 Entry、SourceRef、Index binding 状态，以及刷新后预期的 `expectedContentHash`、`expectedSourceSetHash` 和 `expectedEntryBodyHash`。每个 Index binding 也保存当前与预期投影；`planHash` 绑定 `refreshAlgorithmVersion`、全局 `indexHash`、全部当前/预期投影、完整来源集合和 findings。Invoker 在读取写入目标前拒绝旧 schema，并重新推导预期结果，不能只信任调用方改写后重新计算的 `planHash`。
4. `Invoke-ESKnowledgeStableRefresh.ps1` 默认 preview；只有显式 `-Apply` 才写 Knowledge。Preview 与 Apply 都先校验 schema、计数、路径、`sourceSetHash`、预期投影和 `planHash`，再比较目标 Entry、Index、完整 SourceRef 声明集合以及每个来源文件的当前哈希；新增、删除、改名、改声明哈希或改变任一来源内容都会使整批进入 `staleAtApply`，不会顺带接受计划后变化。
5. Apply 使用项目固定的 `ES/Output/KnowledgeValidation/stable-refresh.lock`，以 `FileShare.None` fail-fast 获取协作式跨进程写锁。锁内创建同目录 staging/backup 文件、重复检查完整 Entry/SourceRef/Index 状态，并在提交每个目标前再次比较其 `originalHash`；任一漂移会拒绝提交或触发已提交目标的回滚。
6. 每个目标通过 `File.Replace` 单文件替换；脚本立即验证 backup 等于原哈希、目标等于预期哈希。捕获到提交异常时按真实提交顺序逆序恢复，并验证恢复哈希；backup 缺失或损坏会明确报错、保留可用恢复现场，不会静默跳过。清理错误与主提交错误聚合报告，不能覆盖主错误。
7. 固定锁能串行化遵守同一 StableRefresh 协议的写者，回归测试要求第二个并发 Apply fail-fast。它不能阻止 Unity、编辑器、任意脚本或人工直接写目标；非协作写者仍可能在目标哈希比较与单文件替换之间制造竞争。顺序替换也不能阻止未参与读锁/快照协议的读者在中间窗口观察到 Entry/Index 混代。
8. 只有实际取得固定锁并进入 Apply 事务的回执才使用 `transactionExecuted=true`、`atomicBatch=true` 和 `transactionMode=locked-exception-rollback`，它们只描述受控进程内、可捕获异常下的批回滚。Preview、WhatIf 和无变更 Apply 必须声明未执行事务，不能借用能力标签冒充本次证据；所有路径均为 `crashSafe=false`。当前实现没有持久 journal、启动恢复或明确的 `Flush(true)` 持久化门禁；进程强杀、系统崩溃和断电不在证明范围内。
9. `Invoke-ESKnowledgeValidation.ps1` 的 `Entry`/`Index`/`All` 结果是最终静态闭包证据；refresh receipt 不能替代验证器。

## High-risk failure modes

以下剩余场景属于高危。AI 不得因为当前脚本已有双采样、协作锁、final CAS、备份或 `atomicBatch=true` 就降低严重度。

### Critical: non-cooperating writer lost update

时间线：StableRefresh A 已取得固定锁并完成目标哈希比较；不遵守该锁的 B 随后直接修改同一 Entry/Index；A 在单文件替换窗口中覆盖 B。根因是 lockfile 只能协调采用同一协议的写者，普通路径哈希比较与替换也不是对任意写者生效的文件系统 CAS。

停止条件：已知 Unity、编辑器、其他脚本或人工窗口正在直接写目标 Entry/Index 时，不得执行 Apply；先等待该写者结束并生成新计划。锁获取失败必须 fail-fast，不能等待后继续使用旧计划。

### Critical: crash-visible mixed generation

时间线：Entry 已替换，Index 尚未替换，进程被强制终止或机器断电。`catch/finally` 不会获得执行机会，读者可能看到新 Entry 配旧 Index，临时文件和备份也可能残留。

停止条件：没有启动恢复协议、持久化事务记录或单一 generation 提交点时，不得声称 crash-safe、durable 或真正原子。

### High: reader observes half commit

即使只有一个写者且最终回滚成功，未参与写锁的读者仍可在两次替换之间读取混代内容。写者互斥只解决 writer/writer 竞争，不自动解决 reader/writer 一致性。

### High: durability confused with atomicity

`File.Replace`、rename、备份、`Flush(true)` 和 write-through 分别处理不同问题。单文件替换或落盘完成不等于多个文件共同原子提交；有备份也不等于自动恢复已经得到验证。

## Network-informed hardening levels

### Level 1: current cooperative writer serialization

当前 helper 使用固定 lockfile 串行化遵守协议的 Apply，在锁内重复 final CAS，并以单文件替换、backup 哈希验证和异常回滚闭合受控失败。它仍不能承诺非协作写者互斥、读者快照一致、进程强杀恢复或崩溃安全。

### Level 2: ecosystem-wide read/write coordination

1. 所有 Knowledge 写入者都必须遵守同一固定锁；禁止旁路脚本直接修改 Entry/Index 后仍声称由该锁保护。
2. 需要一致读取时，读者必须取得兼容读锁或绑定同一不可变快照；仅串行化写者不能解决读者混代。
3. 需要可诊断 owner 时，再增加有界 owner/transaction metadata；不得按时间盲删锁或把持久 lockfile 的存在误判为锁仍被持有。
4. 获取锁失败返回明确并发写者错误，不得无限等待、删除 lockfile 或静默覆盖。

### Level 3: recoverable multi-file commit

1. 提交前写持久化 intent/journal，记录 transaction id、目标、before/after hash、备份路径和阶段：`prepared -> committing -> committed`。
2. 临时文件必须位于目标同卷，写完后执行明确的 flush/durability 策略，再开始替换。
3. 单文件替换优先使用具有明确备份合同的 API；每次替换后推进 journal。启动时发现未完成 journal 必须进入恢复，不得直接开始新事务。
4. 恢复必须幂等：重复启动要么完成同一代，要么恢复全部旧代；无法裁决时保留现场并 `blocked`。
5. 该等级是可恢复协议，仍不得把两个顺序文件替换称为不可观察的多文件原子提交。

### Level 4: single commit point for readers

当要求任何读者都不能看到混代状态时，把 Entry/Index 输出到不可变 generation 目录，并让读者先读取一次小型 manifest/pointer，再只读取该 generation。提交只原子替换这一个指针；旧 generation 延迟回收。

这一方案把跨文件一致性转化为单一提交点，但必须同时约束读者：读者不能在一次任务中重新读取 pointer 后混用两个 generation。删除旧 generation 属于独立清理操作，不由本条目授权。

### Rejected shortcut: Transactional NTFS

不得为了多文件原子性新依赖 TxF。Microsoft 已建议开发者采用替代方案，且 TxF 可能不在未来 Windows 版本持续可用。

## Decision rules

### 可以继续

- 目标条目、Index 投影和全部 SourceRef 均位于项目根内且可严格 UTF-8 读取。
- 当前用户明确请求覆盖实际条目和索引范围；若选择受管通道，其 AICommand、AIBrain 计划与 TaskContract 也必须彼此闭合。
- refresh-plan 为 schema v3，`refreshAlgorithmVersion` 与当前 Invoker 相同，且 `planStatus=ready`、`blockerCount=0`、`unstableFindingCount=0`；目标 `entrySnapshots[]` 的完整来源均为 `snapshotStable=true`，当前与预期 Entry/Index 投影完整，Apply 前来源哈希仍等于计划的 `currentHash`。
- 目标 Entry 和匹配的 Index block 自本次人工复核后没有被非协作写者修改；Apply 能取得项目固定 StableRefresh 锁。

### 必须停止或重新规划

- 任一来源双采样不稳定、缺失、越界、为 reparse point，或 Apply 前哈希变化。
- 目标 Entry/Index 已被并发修改、固定锁不可取得、已知非协作写者仍在运行，或无法证明当前 Index block 唯一匹配目标 KnowledgeId/file。
- `staleAtApplyCount > 0`、目标索引块未更新、ContentHash 不闭合或验证器返回 `blocked`。
- 只有旧 receipt、旧 planHash、旧聊天摘要或缓存，无法回到当前文件。
- 需要写 KnowledgeIndex/AIBRAIN_ENTRY，但当前用户请求未覆盖该目标；路径的控制面分类本身不构成拒绝理由。

## Maintenance transaction

1. **Discover**：从 AIBRAIN_ENTRY 和 KnowledgeIndex 选择最多 1～3 个相关条目，记录 KnowledgeId、Entry、Index block、SourceRefs 和预期写入范围。
2. **Snapshot**：读取目标 Entry/Index 当前内容并计算基线哈希；工作树有重叠时停止，不合并来源不明的覆盖。
3. **Plan**：运行 refresh-plan；只接受 schema v3、当前 `refreshAlgorithmVersion`、`planStatus=ready`、`blockerCount=0`、`unstableFindingCount=0` 的新计划，人工核对 `entrySnapshots[]` 的完整 SourceRef 集合与当前/预期 Entry/Index 投影，并审查每个漂移是否来自真实权威变化。
4. **Authorize**：确认当前用户请求覆盖条目和索引投影。只有选用受管通道时再校验 `knowledge.entry.update`、计划与 TaskContract；通道缺口不得伪造成用户未授权或内容错误。
5. **Compare**：Apply 前重新检查 SourceRef、目标 Entry 和目标 Index block。任一变化都丢弃旧计划并重新开始。
6. **Serialize**：Apply 取得项目固定 StableRefresh lockfile 的独占句柄，并在持锁期间重复 Compare、提交、回滚与清理；获取失败立即停止。该锁只约束遵守同一协议的写者，不能把它外推为对任意编辑器、脚本或人工写入的强制互斥。
7. **Apply**：一次只处理声明的有界条目集合；不得顺带刷新全部 stale 条目。当前 helper 的 `-Apply` 仅用于已审查的 stable-only 计划。
8. **Close**：立即运行 Entry 验证，再运行 Index 验证；目标 Entry、Index block、SourceRefs 或权限元数据变化后，旧结果失效。
9. **Recover**：若 Entry 已更新而 Index 未更新，停止后续批次；依据 journal/备份和同一份已接受 SourceRef 快照补齐或恢复目标投影，然后重新运行 Entry/Index 验证。不得删除冲突条目或回滚其他窗口改动。

## AI failure-prevention matrix

| 错误行为 | 可观察症状 | 预防检查 | 恢复动作 |
|---|---|---|---|
| 把双采样稳定等同于全事务 CAS | SourceRef 未变，但 Entry/Index 覆盖了新编辑 | 单独比较 Entry 与 Index block 基线哈希 | 丢弃旧计划，重读目标并重新规划 |
| 先更新 Entry，索引失败后仍报告完成 | Entry ContentHash 与 Index 不同 | Apply 后立即跑 Entry + Index 验证 | 停止批次，按已接受来源补齐单一目标投影后重验 |
| 用旧 planHash/receipt 继续 | receipt 时间早于目标文件变化 | 每次 Apply 前重算当前输入 | 旧回执标 stale，重新导出计划 |
| 自动接受所有稳定漂移 | 权威源误改也被写成新事实 | 人工判断每个 SourceRef 漂移的语义 | 拒绝该 finding，回到权威源处理 |
| 为通过 All 验证顺带刷新无关条目 | 批次扩大且覆盖其他窗口 | 固定 KnowledgeId、文件数和 change budget | 只保留目标批次，其他 stale 单独排队 |
| 把 refresh receipt 当验收 | 有 receipt 但 Index/SourceRef 仍 blocked | 以 Validator 结果作为静态闭包证据 | 运行 Entry/Index 验证并报告真实 blocker |
| 第二个协作写者与当前 Apply 并发 | 两个进程都尝试提交同一计划 | 固定 lockfile 必须让第二个 Apply fail-fast | 等首个事务结束，重读文件并生成新计划 |
| 非协作写者绕过 lockfile | 目标在哈希比较与替换窗口被直接改写 | Apply 前停止其他直接写 Entry/Index 的工具并复核目标哈希 | 保留现场，丢弃旧计划，按权威内容人工裁决后重验 |
| 强杀发生在 Entry 与 Index 替换之间 | 混代文件、残留 `.tmp`/`.bak`、无终态 receipt | 持久化 journal 和启动恢复探针 | 保留现场，按 before/after hash 幂等完成或回滚 |
| 读者在半提交窗口读取 | 单次任务得到 Entry/Index 不同代 | generation manifest 或读锁/任务快照 | 丢弃该读取结果，从同一 generation 重新加载 |
| 把 flush/rename 当成多文件原子性 | 单文件落盘成功但整体仍混代 | 分别声明 durability、single-file atomicity、multi-file consistency | 降级声明并补齐事务/读者协议 |

## Execution checklist

### 开始前

- 读取 AIBRAIN_ENTRY、KnowledgeIndex、目标条目及本条目 `RequiredReads`。
- 检查分支、HEAD、staged/unstaged/untracked 和目标路径重叠。
- 固定目标 KnowledgeId、允许写入文件、最大条目数、停止条件与恢复路径。

### 完成后

- `entrySnapshots[]`、`sourceSetHash`、SourceRef SHA-256 与 ContentHash 重算一致，且未漂移来源也属于 Apply 前后 CAS 检查范围。
- KnowledgeIndex 中 KnowledgeId、file、routeKeys、requiredReads、relatedSkills、Authority、EvidenceLevel、ContentHash 唯一且一致。
- Entry 与 Index 验证均为 `passed`；相同输入重复运行结果确定。
- 严格 UTF-8、U+FFFD/乱码扫描与 `git diff --check` 通过。
- 明确记录 `runtime-not-run`；静态维护不证明 Unity、Player、发布或外部系统状态。

### 并发与崩溃专项

- 双写者屏障：让 A 持有固定锁并停在 final CAS 后，再启动 B；B 必须 fail-fast 且不能修改 Knowledge，A 释放锁后仍按原计划闭合。
- 非协作写者：明确记录固定锁不约束直接文件写入；不得用双 StableRefresh 竞争测试证明任意工具都不会覆盖目标。
- 首文件后强杀：在第一个目标替换后终止进程；重启恢复必须得到全旧代或全新代，禁止混代。
- 读者并发：提交期间持续读取；每次读取必须绑定同一 generation，或明确拒绝读取。
- 失败注入：覆盖锁超时、磁盘空间不足、flush 失败、替换失败、回滚失败和残留锁恢复。
- 重复恢复：对同一 journal 连续运行两次，第二次不得再次改变已闭合状态。

## RequiredReads

- `Assets/Plugins/ES/AICommands/受管AIKnowledge更新_AI命令.md`
- `.agents/skills/es-knowledge-creator/SKILL.md`
- `.agents/skills/es-knowledge-validator/SKILL.md`
- `.agents/skills/es-knowledge-validator/references/knowledge-validation-contract.md`
- `.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1`

## SourceRefs

- `Assets/Plugins/ES/AICommands/受管AIKnowledge更新_AI命令.md` (`9abb93f4bedd67ea1d2560655efddc4a2eb16ad6110398b24d98fe008320e1d7`)
- `.agents/skills/es-knowledge-creator/SKILL.md` (`bb2d2869573f9468db36afa74b8d86ee928987ae0e297dc46b858f71f8876ad7`)
- `.agents/skills/es-knowledge-validator/SKILL.md` (`6183ac59608a55c03a46bd0a3575e699116fb6e7910ac4f1ad23431da5f6a61e`)
- `.agents/skills/es-knowledge-validator/references/knowledge-validation-contract.md` (`c779de68adbe398d46a3ed672f9f07b1c131132a7de5568937a5b2217cc83906`)
- `.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1` (`691150a3b51bf2f500ab99fa058ea70bb6a643ce5feca606acc625a2a80ec98c`)
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1` (`6ad678e350af07aa2e0d50cad6ae9a6f532b3f6ab2e71a7943f1bd84e00f29c5`)

## ExternalReferences

这些 URL 是设计约束的在线参考，不参与本地 `ContentHash`，不抬高本条目的 Authority，也不替代当前源码。实现相关能力前必须重新访问并绑定目标 .NET/Windows/Git 版本。

- Git lockfile API：独占创建 lockfile 提供写者互斥，再用 rename 提交单文件。https://git-scm.com/docs/api-lockfile
- .NET `Mutex`：可用于跨进程同步。https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex
- .NET `File.Replace`：替换单个文件并可创建备份。https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace
- Win32 `ReplaceFileW`：替换单个文件并保留指定文件属性。https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew
- Win32 `MoveFileExW`：`MOVEFILE_WRITE_THROUGH` 描述特定移动/复制删除路径的落盘保证。https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw
- .NET `FileStream.Flush(Boolean)`：`flushToDisk=true` 还会清除中间文件缓冲。https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush
- Microsoft TxF 替代建议：不建议新采用可能在未来 Windows 不可用的 TxF。https://learn.microsoft.com/en-us/windows/win32/fileio/deprecation-of-txf

## Evidence boundary

当前静态回归可以证明 schema v3 计划投影绑定、重签计划后的语义重算拒绝、协作式双 Apply 锁竞争、final CAS、单文件替换、backup 哈希检查、恢复后哈希验证、可捕获提交/回滚/清理失败及错误聚合，并区分本次是否真正执行锁事务。它不能证明非协作写者互斥、读者 generation 一致性、进程强杀、断电、磁盘故障、启动恢复、Unity、PlayMode、Profiler、Player、IL2CPP 或发布验收；`crashSafe=false` 必须保持为显式 non-claim。
