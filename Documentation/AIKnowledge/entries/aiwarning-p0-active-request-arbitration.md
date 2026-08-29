# ES 活跃请求仲裁协议：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.active-request-arbitration.v1`  
`Authority`: `AIWarnings` 与当前各领域仲裁实现/测试  
`RouteKeys`: `aiwarnings`, `p0`, `arbitration`, `active-request`, `lease`, `generation`, `commit`, `executor`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `70f6e15968688196a95b307a7872c1f4f05a5fbe0d4f7795306825cae55f6085`  
`SourceSetHash`: `70f6e15968688196a95b307a7872c1f4f05a5fbe0d4f7795306825cae55f6085`  
`EntryBodyHash`: `f565667f5245b2f414cdf110e5f13fe06af5f937b17b24ea8a81b1672cdfa7d8`  
`StaleWhen`: Lease/Generation/Commit、领域仲裁、Camera 投影或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留协议闭环、Lease 安全、确定性提交、领域不可强行合并和 Camera 未验收边界；本条目承载完整术语、领域投影、验收矩阵和实施前问题。Knowledge 不授予通用仲裁器或运行时执行权限。

## 协议与术语

统一闭环是 `Request（意图） → Lease（独立租期） → Active Set（有效请求集） → Arbitration（确定性仲裁/合成） → Commit（领域唯一提交） → Executor（执行后端）`。`Request` 不等于生效结果；`Lease` 不是裸 OwnerId/索引/对象引用；`Token` 是宿主当前代际中的不透明定位值，不是稳定 Key、CancellationToken 或可持久化身份；`Generation` 是运行时代际，不等同于发布 Version；`Owner` 不单独构成租期身份；`Executor` 只执行结果，不自行发明优先级。唯一获胜主体称 `Winner`，可叠加值称 `Modifier`。

每个可独立释放 Request 必须用 `Owner + Token/Slot + Generation` 或等价不透明身份验证写入和释放。重复/过期/跨代/跨 Host/View 操作、值类型 Lease 复制品的错误释放必须失败且不影响当前租期；Owner 销毁、回池、取消、场景或 Provider 代际变更必须使旧 Lease 失效。清空 Active Set 时同步清 Token 或推进 Generation。

`Push/Update/Release` 只改 Active Set 并标脏；唯一 Commit 清理失效请求、重算结果并驱动 Executor。普通业务不得直写 Executor，`FlushNow` 只能是明确受限边界。同优先级必须有稳定可重现决胜键；不得依赖 Dictionary 枚举、对象 Hash 或未定义回调顺序。单请求/适配器异常不得污染合成半成品或跳过清理，多 View/玩家/输出域先按 ViewId 等价键隔离。诊断需解释 Owner、类型、优先级、决胜键、失效原因、Winner/Modifier 与提交结果；关闭诊断不得分配。

## 领域关系与 Camera 投影

Tag 复用 Lease/代际但按存在性/引用计数；Stat/ValueChange 按固定顺序合成 Modifier；Resource 处理 Scope/Handle/并发加载和释放；Audio 保留 Voice 预算/抢占；Vehicle 有 seat/driver 控制权；State/Input 保留状态环境与路由边界。只有至少两个领域拥有完全相同 slot/generation 状态机、异常和清理规则，才可抽取极小不透明身份组件，不得为统一外观创建大型管理器。

Camera 首切片为 `CameraRequest → CameraLease → ViewId Active Set → Base Winner + Modifier → CameraDirector.LateUpdate Commit → Cinemachine Executor`。CameraDirector 是每 View 唯一写入权威，Cinemachine 只执行；Lease 绑定 View、Slot/Token、Generation、Scene Epoch，释放只撤销请求。当前切片仍缺 Unity Test Runner、PlayMode、Profiler、Player/IL2CPP、TrackView Preview、Timeline 和载具镜头证据，不得写成已交付/冻结。

## 验收矩阵与实施问题

至少验证双 Lease/复制品乱序释放、Owner 回池旧 Lease、清空后重用、销毁/取消/场景/Provider 转换、同优先级不同顺序一致、仲裁轮次、单请求异常回滚、多 View 隔离、绕过链路关闭和预热后 GC/CPU。实施前必须明确 Active Set 所有者、Commit/Executor、Winner/Modifier 顺序、Lease 过期校验、Owner/异常/场景清理及是否确有两个同语义领域可抽取。静态与 Editor 证据不能替代 Player/IL2CPP/Profiler。

## 原文快照

迁移前台账快照：124 行、9398 字节，原始 SHA-256 `064642f794962c253c2504ae6516586d3232ce0002cdebf849433e6d0ba354ef`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md` (`16daf5464a5c30913b6ceeefd224c7b01d1d0403bf5fe662d588d287e7ae032d`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`c3c6d03e58cb446c42d4a873411302804c3a298e5d7bf5acfeeb12871bbe5481`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-active-request-arbitration.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md`
