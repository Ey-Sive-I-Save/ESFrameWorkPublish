# 项目最高警告：P0 高频命名清晰；P1 无意义包装禁止

Status: current
StableId: es.aiwarning.p0.naming-and-wrapper-boundary.v1
Authority: AIWarnings（长期 P0/P1 约束）；详细矩阵与案例见 Knowledge
RouteKeys: aiwarnings, p0, identity, naming, api, wrapper, scheduler, program, compiler, runtime
Applicability: GameCore 字段、Inspector/Picker/菜单、AICommand、公共 API、配置层和生命周期类型
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-naming-and-wrapper-boundary.md
StaleWhen: 公共 API、Inspector/菜单命名合同、架构词职责、生命周期协议或 SourceRef 哈希变化。

## P0 长期约束

- 高频入口名称必须让策划、业务开发和 AI 直接理解对象、时机和效果；优先项目常用词。`Submit/Resolve/Dispatch/Acquire/Commit/Try` 只有在真实的权威校验、消歧、接收者集合、所有权或事务/拒绝语义成立时使用。
- `Scheduler`、`Program`、`Compiler`、`Runner`、`Snapshot`、`Dispatcher`、`Router`、`Selector`、`Policy`、`Definition`、`Template`、`Binding`、`Table`、`Registry`、`Catalog` 必须承担项目约定的唯一职责，不得用重名词包装条件、switch、一次转发或其他领域产物。
- 命名审查必须同时检查声明、实现、调用点、生命周期、拒绝语义、序列化和兼容影响；候选、扫描日期和改名建议只进入审查报告，不成为永久禁词。

## P1 长期约束

- 禁止只包一个字段、只转发、为未来字段预留或形成内外双权威的 Config/Data/Info/Runtime/Manager/Bridge 类型。
- 新类型至少具备共同不变量、独立生命周期/释放、版本迁移边界或不可安全重复的独立验证之一；高频误导、双权威或掩盖生命周期时升级 P0。
- 已确认的 Tag、Pool、Selector、Submission、Commit 案例必须按真实职责判断，不得恢复废止协议或机械改名。

## Knowledge 导航

命名矩阵、架构词职责表、Submit/Try/Acquire/Commit 判定、Tag 与生命周期案例、分级审查流程和迁移前语义见 `es.aiwarning.p0.naming-and-wrapper-boundary.v1`。本 Warning 不授予批量改名、源码写入、序列化迁移、运行时或发布权限。
