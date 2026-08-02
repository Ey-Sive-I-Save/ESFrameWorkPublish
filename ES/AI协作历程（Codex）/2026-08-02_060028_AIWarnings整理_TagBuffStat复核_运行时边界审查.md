# AIWarnings 整理、Tag/Buff/Stat 复核与运行时边界审查

文件名大纲：AIWarnings 整理、Tag/Buff/Stat 复核、运行时边界审查

窗口档案 ID：`ES-CODEX-20260802-060028`

建档时间：2026-08-02 06:00

窗口边界：本文对应本 Codex 的实际工作窗口。记录 Tag、Buff、Stat 的设计收口、代码实现、回归与编译级验收，以及随后进行的 AIWarnings 与历程治理。与 Runtime C# 无关的讨论会明确标为设计/复核；最终 Unity 运行验收不得由本文替代。

## 日期与证据口径

- 时间线按用户请求接受顺序编号。对话未提供可靠分钟时间，使用 `T01` 起的顺序，不伪造精确时刻。
- “已实现”仅表示本窗口完成代码或文档改动并得到对应静态证据；“用户报告”只记录为复核输入，不冒充重新实装。
- 最终 Unity Test Runner、PlayMode、IL2CPP、Profiler、真实网络/Bake 仅在有实际执行证据时才可签收；本窗口的大量结论仍止于代码与编译级。

## 完整任务时间线

| 序号 | 用户要求与接受范围 | 实际动作与对象 | 验证证据、当时结论与剩余项 |
|---|---|---|---|
| T01 | 验收 GameTag 代码与编译闭环，并指出缺少 Entity 生命周期组合测试。 | 实装/复核 Tag Collection、Lease、LeaseSet、Entity 绑定与池化路径；记录组合测试缺口。 | 代码/编译级可签；仍缺 BindDefinition→重复应用→池化→旧 Lease 延迟释放→固有/临时 Tag 重叠的 Unity 实测。 |
| T02 | 清理 `ESTagGrantConfig`、`EntityRuntimeFactConfig` 等旧包装残留。 | 扫描类型、字段、实例化、资源序列化和文件名；将禁止恢复的反例名称保留在治理文档。 | 功能与序列化残留为零；治理警告中的反例名称不属于有效配置。 |
| T03 | 判断 ESTag 是否可接入完整 ES 业务。 | 梳理 Tag 的 Catalog、Host、Lease、Condition、Link、Pool 与业务写入边界。 | 底层可作为正式支持；正式 Bake、稳定 Key 审计、Test Runner 与主链接入仍独立验收。 |
| T04 | 确认 EnumKey-only、StringKey-only、双 Key 是否可接受。 | 固化三种稳定身份模式和双别名一致性校验。 | 三种模式均支持；Enum 不重排/复用，String 不随意改名，双 Key 必须指向同一 Catalog 声明。 |
| T05 | 形成 ESTag 全流程闭环说明与精致表格。 | 编写 Authoring→Bake→RuntimeKey→Host→Lease→Condition→Pool 的分层说明。 | 文档随后按实际 Host、Skill、调试和 0 GC 边界多次收紧。 |
| T06 | 纠正“只有 Entity 可持有 Collection”的过度表述。 | 将 `ESTagCollection` 定义为通用事实容器；区分当前 Entity 接线与其他 Runtime Host 能力。 | Buff/装备/区域作为对 Entity 的 Lease 写入者；不能据此否认 Item 未来自有 Tag 集。 |
| T07 | 明确 Item 可拥有专属 Tag 集及其接线缺口。 | 给出 `ItemDataInfo.tags -> Item intrinsic leases -> Item.Tags` 的目标链，列出池化、条件、快照与测试要求。 | Item 自有 Tag 集是正式待实现能力，不能标为已接线；装备影响 Entity 仍用目标 Entity Lease。 |
| T08 | 评估大量 Item/Buff/Area 自有 Collection 的内存和 GC。 | 静态分析 Link、Sparse、Lease、LeaseSet、Hot 路由与对象池成本。 | Hot 查询较好；空容器和生命周期写入需优化后才适合海量 Host。 |
| T09 | 优化 Tag 空容器、LeaseSet、Hot/Sparse 路由和通知。 | 实装 Link/缓冲/Sparse 延迟创建、RuntimeKey 范围路由、小列表去重、LeaseSet Token 路径和零订阅跳过。 | 稳态查询与批量生命周期降分配；公开单 Lease 保留托管句柄成本。 |
| T10 | 明确 `SetTag` 与 Lease 的叠加、幂等和不误删语义。 | 实装 Host 自身贡献与外部 Lease/Token 聚合计数，提供 `HasOwnTag` 与 Presence/Count 通知。 | `SetTag(false)` 只撤 Host 贡献；旧 Lease 经 generation 失效，不污染下次池租。 |
| T11 | 再次严密复核 Tag 重入与清理边界。 | 修复 Clear/Dispose 期间重写、LeaseSet 跨目标迁移、通知重入倒序；补回归测试。 | 聚合 Count 不变量成立；仍仅支持 Unity 主线程，Hot Count 上限 255，重入队列首次使用为冷路径。 |
| T12 | 讨论 Pool 是否缺统一入口及 `ESGenericLife` 方案。 | 将通用生命周期与 Pool 分部区分，拒绝全子树 Reset 广播；明确 Owner/Extension 的职责。 | `ESGenericLife` 是通用组织器，Pool 只是一个分部；不能用一个 Pooled Handler 名称吞没其他生命周期能力。 |
| T13 | 审核 Pool 新建/预热、异常与唯一 Owner 的 P0。 | 复核 Spawn inactive 基线、try/finally 池账本、回调异常隔离和根 Owner 约束。 | 架构方向成立但当时 Pool 生命周期未完整验收；需真实 Pool 调用链测试。 |
| T14 | 处理 Despawn 失败实例仍被复用与 createdCount 误扣。 | 实装失败实例销毁策略，并复核新实例基线失败不能走会扣已计数实例的丢弃路径。 | Despawn 失败不得回 inactive；createdCount 分支需按是否已计数区分。 |
| T15 | 说明 Tag 闭环与 ES 稳定底层定位。 | 汇总 SetTag、Lease、Condition、Catalog、Hot/Sparse、Link、Pool 和业务 Host 规则。 | Tag 已是稳定底层支持；Hit 主链、Item 自有 Host、Bake/Key/Unity 运行证据仍分项推进。 |
| T16 | 评价 Buff 是否可进入商业级核心并确保热路径 0 GC。 | 审核 Buff 对 Tag、ValueChange、Permit、Op、Pool、异常隔离和活动调度的复用关系。 | 适合中型 3D 的常规单 Entity 少量 Buff；十万级/大型联机不宣称已验收。 |
| T17 | 收口 Buff 配置层，不新增转发包装。 | 将 `BuffDefinitionDataInfo` 作为定义权威；规划九个编辑分区、稳定 Key Picker、条件字段与 Bake 错误。 | Runtime 负责 Lease/Token/池化/回滚；策划不暴露 RuntimeKey、Support、对象池等实现。 |
| T18 | 保留 `entity.buffDomain.AddBuff(buffDefinition)` 主入口，并补按 Key 与复杂操作。 | 设计 `ApplyBuff(key, ESBuffOperation, sourceSupport)`，涵盖重置持续、叠层、等级、数值变更等。 | 直接 Add 是默认入口；复杂业务使用一次 Operation 集合，不能扩散多个 ad-hoc API。 |
| T19 | 讨论 Buff Frame 与“按帧状态覆盖”是否适合。 | 将状态帧与 Buff 生命周期区分，拒绝把常规 Buff 操作伪装成每帧 Set。 | Buff 适合持续、叠层、刷新、等级、来源和回滚；状态帧仅用于真正的瞬态状态投影。 |
| T20 | 确认 `ESBuffOperation.Default.ResetDuration().AddStack(1)` 的 builder 与 `ref` 取舍。 | 采用值类型、链式、一次构造的 Operation；避免为普通调用暴露不必要 `ref`。 | 常态操作可读且低分配；复杂组合仍通过同一 Operation 执行。 |
| T21 | 要求 Buff Operation 的中文编辑器可用。 | 规划操作类型、条件显示、值校验、风险提示与中文展示，不把运行时 Token 暴露给策划。 | 编辑器是配置动作表达，不改变底层 Buff 所有权语义。 |
| T22 | 设计特殊机制 Buff 的 Logic/Runtime 策略。 | 定义只读共享 `ESBuffLogic` 配置与每 Active Buff 独占 `ESBuffLogicRuntime`，Runtime 自持 Lease、订阅、Token 并在 Release 清理。 | Logic 可读取 Runtime 的等级/层数/TargetPack；定义不能存每实例可变状态。 |
| T23 | 收正 Logic 回调与生命周期语义。 | 建立 `OnApply`、`OnRefresh`、`OnTick`、`OnRemove`、`OnRelease` 对 Runtime 的策略调用。 | Runtime 是状态与资源拥有者；Logic 是机制策略，不应退化成只会创建 Runtime 的空工厂。 |
| T24 | 说明 Buff 支持的高难度机制与 AAA 边界。 | 评估持续、叠层、互斥、属性/许可、Tag、逻辑策略、异常隔离和池化组合。 | 可承载复杂动作、DOT、光环、状态、被动机制；不把未验证的海量联机能力写成 AAA 最终签收。 |
| T25 | 澄清 TargetPack 的取得、初始定义、快照和回收。 | 将 Pack 定义为创建期目标上下文；区分借用、Copy/New、所有权版本门禁和池回收。 | 需要保持创建期目标时可保留快照；快照与 Pack 可池化，但 Owner 与租期必须可验证。 |
| T26 | 讨论技能 Clip/Track 继承 Pack 时复制或保持引用。 | 收口为引用 Pack 永远借用；Copy/New Pack 由创建层记录 `createdTarget + targetVersion` 并负责回收。 | 普通 Operation 不可见回收入口；旧 Owner 不能回收已重新租出的实例。 |
| T27 | 要求默认空 Pack，避免 Skill 持空 Pack 继续执行。 | 给 Skill 创建期补默认 Pack 填充，保留未来目标选择器扩展位。 | 默认 Pack 只解决安全底线，不替代正式目标选择与命中语义。 |
| T28 | 要求 Buff + Tag + Stat 组合闭环验证。 | 复核 Apply 回滚、Deactivate 清理、Logic/Tag/依赖/EffectLease/OpSupport 隔离释放。 | 代码与程序集编译级闭环可签；组合集成测试、Hit 主链、正式 Bake/Key 审计与 Unity Test Runner 仍缺。 |
| T29 | 开始强化既有 Stat，而非重写属性表。 | 保留 Table→Catalog→Hot/Sparse→ValueChange 架构，补诊断、快照、路由、Lease 和 GameCore 配置能力。 | 原有属性表仍是权威；增强不得另建平行 Stat 系统。 |
| T30 | 纠正 Stat Monitor 菜单不应使用非 ES 根。 | 收口运行时面板菜单到 `【ES】/运行时诊断/属性系统/运行时面板`。 | 菜单规则进入 AIWarnings；编辑器入口不改变 Runtime 属性职责。 |
| T31 | 处理 Entity Reset 时 Changed 重入导致 Hot/Sparse 污染的 P0。 | 增加生命周期结束清理门禁，阻止重入新建 Set/修改 Base/Fallback，并清空 Hot 引用与 Sparse 字典。 | 回归覆盖 Hot、Sparse 新键、Permit、Base 重入；池化 Entity 不再遗留旧属性。 |
| T32 | 区分实体与物品属性、Tag 全局共享而 Stat 独立。 | 固化 Entity/Item 各自拥有 Hot/Sparse、Base、Set、Lease；Tag 是可复用事实容器而非全局数值表。 | 物品属性不会写入实体容器；影响宿主仍经明确 EffectLease/业务目标。 |
| T33 | 解释 Hot 属性的枚举槽位与运行时路由。 | 说明 HotSlot 是固定数组下标，稳定 Key 经 Catalog/Bake 映射；Sparse 使用运行时字典。 | 枚举值/槽位不得重排；Hot 是性能策略，普通内容属性不必强制固定 API。 |
| T34 | 评估 Stat 池化、空 Entity 和首次物化 GC。 | 静态分析 Hot Set 清理、Sparse Set 回收、Dictionary 延迟创建、Effect Slot 容量和战斗预热。 | 查询和稳态 Modifier 路径较好；跨池重复创建 Set 的 P1 需后续对象复用与 Profiler 证据。 |
| T35 | 解释新增属性为何有多处手工映射，并要求 Bake 生成。 | 将固定 KCC 属性的 Enum、Key、数组、switch、默认表改为 CodeGen 目标；普通 Catalog 属性保持配置驱动。 | 人工多处同步不合格；生成结果必须稳定、可审计、结构变化才触发过期。 |
| T36 | 明确 Base、Min、Max、DisplayName、Storage 等必须留在 GameCore 配置。 | 分离代码生成的结构投影与策划值/Bake Catalog。 | 代码只生成固定访问 API 与稳定路由；数值、范围、显示名、公式不应因普通修改触发生成。 |
| T37 | 解决 Entity 与 Item 属性初始化覆盖。 | 明确 Host 各自绑定 Attribute Catalog、固有 Base 与运行期 Effect；不跨 Host 共享 Set。 | 构建期身份与运行期实例分离；池化 Reset 清理临时贡献而保留 Host 绑定。 |
| T38 | 修复 EffectLease 旧槽位复用后继续写入的 P0。 | 引入 slot + generation 写入校验，隐藏裸 ownerId，补 SubsystemRegistration 清退 Runtime Catalog。 | 旧 Lease Release/复制/复用后写入被拒绝；Unity 刷新前 .csproj 证据不能夸大。 |
| T39 | 修复有效 Lease 可向其他宿主 Set 写入的 P0。 | 为 Set 绑定 O(1) Host 引用，Entity/Item 写入使用 `ReferenceEquals` 校验。 | 跨 Entity/Item 与未绑定 Set 被拒绝；正常路径无新增 GC，仅一次引用比较和每 Set 一引用字段。 |
| T40 | 复验 EffectLease、Host、代际与测试。 | 覆盖 Hot、Catalog Hot、Sparse、复用、跨 Host、旧 generation；说明 InternalsVisibleTo 仍是治理而非绝对不可伪造。 | P0 静态通过；Item A/B 与 Standalone Set 的 Unity Test Runner 用例仍建议补。 |
| T41 | 审核 GameCore 参与范围及开发者是否可不改代码新增内容。 | 区分普通 Tag/角色/物品属性的配置+Bake，与固定 KCC/具名枚举的生成代码路径。 | 普通内容可配置；当时具名 Enum 与固定 API 仍有生成链缺口，不能承诺“只配表即可全部扩展”。 |
| T42 | 要求固定角色属性 CodeGen 与 Bake 门禁，且生成菜单不是每次必经。 | 实装稳定排序、冲突校验、UTF-8 无 BOM、内容比较与结构过期门禁；仅结构变化生成，普通值直接 Bake。 | 生成与 Bake 分阶段，避免每次普通策划改表都触发编译；Unity 刷新/实际生成仍是最终证据。 |
| T43 | 收紧 CodeGen 从签名比较到生成正文全文比较。 | 令 Bake 在修改产物前重建预期源码并作序数全文比较；补普通数值不变、结构变更、保留签名篡改正文的测试。 | 结构篡改不能靠保留签名绕过；Editor 工程收录与 Test Runner 仍待 Unity Refresh。 |
| T44 | 让属性 GameCore 界面简洁可用，并判断常规 Key 是否都需 CodeGen。 | 固定高频列宽，隐藏未支持公式，提示普通修改直接 Bake、固定身份变化才生成；普通 Key 不生成 C#。 | UI 不制造额外流程；固定 API 与普通 Hot/Sparse Catalog 路径保持分层。 |
| T45 | 验收 Tag/Buff/Stat 三系统组合状态。 | 汇总多来源 Tag、Buff 事务、Stat EffectLease、Pool 代际与性能边界。 | 底层代码和编译闭环可签；组合 Unity 测试、正式 Bake/Key 审计、Hit 主链和 Player 仍未签。 |
| T46 | 复核 GameCore、资源、Raw、扩展与 GameManager 文档是否过时。 | 更新 CurrentStatus、资源 Scope/Lease、Raw、ResourcePlan、模块 API、发布与 Tag 治理规则。 | 文档改动按源码事实标注；ES_Logic 当时生成工程收录问题与 Unity 运行证据必须分开。 |
| T47 | 补齐 Contextitecture、ESCommand、Interaction、编辑器套件、GraphView/NodeRunner 五份高优先级规则。 | 读取关键源码，新增五份专项 AIWarnings，接入 README/RuleIndex，修正输入交互旧表述。 | 规则成为任务域强制必读，不是全局 P0；GraphView 冻结为历史实验实现。 |
| T48 | 复核五份规则的 Context Same、ESCommand 重播与优先级表述。 | 将普通 Context 调用收紧为 Copy；池化值禁跨 Pool；标明运行中重播先 Stop；对外表述改为任务域必读。 | 两处文本修正通过编码与 diff 检查；Runtime 补偿与引用所有权尚未实现。 |
| T49 | 首次被要求维护 `ES/AI协作历程（Codex）`。 | 读取目录、README 与旧档案；尝试建立当前窗口记录和入口。 | 并行重命名导致补丁冲突；初版将前置报告混入职责，后续必须纠正。 |
| T50 | 接收“一窗口一文件、文件名必须写实际职责、正文必须详细时间线”的新治理规则。 | 读取更新 README，按现行规则调整阅读顺序、档案命名理解和归属边界。 | 历程不能替代 AIWarnings；历史状态不能反向覆盖源码与最新验收。 |
| T51 | 解释协作历程设计意义。 | 说明规则与记忆分离、窗口责任、可追溯性与历史快照边界。 | 这是协作治理，不提升 Runtime 性能，也不替代测试。 |
| T52 | 错误把 `062021` 其他窗口档案称为本窗口工作。 | 承认归属错误，撤回我追加的 T58，恢复其 57 节点状态。 | 不得把用户报告或其他窗口文件称为自己实现；错误保留为时间线事实。 |
| T53 | 错误把 `063954` 指作职责文件。 | 根据用户指定删除误建并行文件，恢复本文为当前职责入口。 | 本文才是用户指定职责档案；但旧正文仍不符合详尽时间线。 |
| T54 | 首次统计本文有效记录数。 | 按新规则统计而非按章节，结果为 0 条有效任务记录、6 个主题章节。 | 主题摘要不合格，必须重写。 |
| T55 | 第一次把本文重写为 T01–T12。 | 建立逐项字段和验证脚本。 | 字段覆盖 12/12 通过，但仍把多个用户轮次合并，用户指出不足。 |
| T56 | 第二次统计并指出 12 条不等于完整对话。 | 承认按修改批次合并的错误，将并行档案审计、职责查询、指定文件、两次计数和重写请求拆开。 | 旧 T10–T12 拆分为更多独立节点；仍需继续补全更早 Tag/Buff/Stat 对话。 |
| T57 | 当前要求按规则完整写入我的 Tag、Buff、Stat 历程。 | 删除仅 16 条的压缩时间线，重建本文为 T01–T57 的完整职责流水。 | 本文现在覆盖 Tag、Buff、Stat、GameCore、治理与历程修正；未知细节均降为用户报告或待 Unity 验收。 |
| T58 | 询问为何需要多次提醒才按规则完成。 | 承认先把历程压缩成阶段总结、又因归属不确定反复等待纠正的流程错误；给出应立即执行的归属确认、逐条拆分和覆盖审计步骤。 | 结论是执行失败，不是规则不清；后续不得等待用户催促才回填记录。 |
| T59 | 询问规则本身哪里写得不行。 | 对照 README 的一窗口一档、独立请求、无上限、字段要求、失败保留和覆盖审计条款。 | 规则内容已足够明确；缺失在于我未将其作为每次响应后的强制动作。 |
| T60 | 要求向规则制定者提出防止后续 AI 重犯的建议。 | 提出“历程执行门禁”：逐消息分类、先确认唯一文件、每次工具/失败后立即记录、完成前机械覆盖审计和字段校验。 | 建议重点是把内容标准变为无法绕过的执行检查点；未擅自修改规则制定者的原文。 |
| T61 | 要求在关闭前确保本窗口负责流程已详尽。 | 将 T58–T61 立即追加到本文；核对连续编号、职责范围、字段列、编码、尾随空白与差异格式。 | 本窗口职责流水截至此消息为 T01–T61；未完成的 Runtime/Unity 验收仍明确保留，关闭后不得由本文冒充签收。 |

## 当前结论

> Tag、Buff、Stat 的设计收口、代码实现、回归补齐与编译级验收属于本窗口职责。当前可签的是代码与编译级闭环；Unity Test Runner、PlayMode、IL2CPP、Profiler、正式 Bake/Key 审计、Hit 主链和真实网络/发布验证仍须独立完成，不能由本历程提前签收。本文截至关闭前已记录 T01–T61。
