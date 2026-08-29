# P0 高频命名与无职责包装边界

`KnowledgeId`: `es.aiwarning.p0.naming-and-wrapper-boundary.v1`  
`Authority`: `AIWarnings + current naming/lifecycle source`  
`RouteKeys`: `aiwarnings`, `p0`, `identity`, `naming`, `api`, `wrapper`, `scheduler`, `program`, `compiler`, `runtime`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `c0f454ef1779ee93a0bb8fd0d2ab477c57585da904ef0acd904349982334b07f`  
`SourceSetHash`: `c0f454ef1779ee93a0bb8fd0d2ab477c57585da904ef0acd904349982334b07f`  
`EntryBodyHash`: `6fa424a79d31e57c7f048e2585d39dbf88085e58dccab763ddb2c37631bd8596`  
`StaleWhen`: `公共 API、Inspector/菜单命名合同、ESWorkScheduler、Tag Lease、生命周期协议或 SourceRef 哈希变化。`

## 迁移说明

原 Warning 161 行、17,059 UTF-8 字节；现 Warning 只保留 P0 高频入口可理解性、架构词职责、P1 包装层判定和权限边界。本条目承接命名矩阵、已确认案例、分级流程、Tag/生命周期示例与迁移前语义集合。

## 高频入口规则

- GameCore 字段、Inspector 文案、Picker、菜单、AICommand 以及业务层公共 API 必须让目标使用者直接理解对象、时机和效果；优先 `Add/Set/Apply/Clear/Remove/Get/TryGet/Select` 等项目常用词。
- `Submit` 只在完整输入跨越独立权威、存在身份/版本/会话校验且可能拒绝并推进独立流程时成立；普通字段赋值、字典写入或一次转发不得包装为 Submit。
- `Try` 只表达真实、可预期的业务拒绝分支；没有拒绝语义不得机械添加。
- `Acquire` 必须建立需要释放、失效保护或代际保护的所有权；`Commit` 必须有预检、事务/版本边界和失败不落地保证；`Dispatch` 必须确有既定接收者集合；`Resolve` 只用于跨来源消歧。

## 架构词唯一职责

- `Scheduler` 必须拥有任务注册/注销、稳定顺序、遍历期增删保护和明确宿主执行；单个条件、switch 或转发不得称为 Scheduler，优先复用 `ESWorkScheduler<TTask>`。
- `Program` 只属于已校验、解析、链接、编译后的不可变 BehaviorTree 执行产物；`Compiler` 负责产物生成和诊断，不执行；`Runner` 只推进已验证运行单元，不拥有作者数据。
- `Snapshot` 必须来自明确时点/版本/签名并与后续源修改脱离；`Dispatcher` 只送达已确定消息；`Router` 只选目的地；`Selector` 只返回选择；`Policy` 只判断规则；`Definition`/`Template`/`Binding`/`Table`/`Registry`/`Catalog` 不得越权承担运行状态或副作用。
- 不得用 `WeaponScheduler`、`AttackDispatcher`、`TickScheduler` 等重名词掩盖选择、策略或一次转发；Shot Tick 用 Policy，攻击选择用 Selector，执行仍由明确的 Combat 入口负责。

## P1 无职责包装

- 禁止只包一个字段的 Config/Data/Info/Runtime 类型、只转发的 Manager/Bridge、为未来字段预留的嵌套和外内双权威。
- 新类型至少要维护多个字段共同不变量、独立生命周期/资源释放、版本迁移边界或调用方无法安全重复完成的验证之一；否则字段直接放入真正拥有者。
- 高频配置/API、双权威、生命周期掩盖或让配置者看不见真实数据时，P1 自动升级 P0。

## 既有边界案例

- `ESTagGrantConfig` 已删除，不得恢复；Tag 配置由真实拥有者直接持有，只有确实需要释放时才使用 `ESTagCollection.Acquire` 的公开 Lease。统一 Drawer/Picker 不得复制缓存或分叉规则。
- `ESGenericLife` 组织根对象生命周期与显式扩展注入，不是 Pool/Entity/Item 包装；Pool 使用 `IESGameObjectPoolLifecycle` 与明确时机回调，废止的全子树广播协议不得换名复刻。
- `ESMotion.AddVelocity`、Story/Automation 的完整 Submission、Equipment 的真实事务 Commit 可以保留；是否合理必须同时审查声明、实现、调用点、生命周期、拒绝语义和兼容影响。

## 审查与证据边界

动态候选、扫描日期、具体改名建议和迁移阶段放入审查报告，不把候选写成永久禁词。批量改名、兼容别名、序列化迁移和源码修改需要单独授权；UTF-8、静态回放和 diff 检查不能替代命名语义或 Unity 迁移验收。

## EvidenceRefs

- `Assets/Plugins/ES/1_Design/Work/ESWorkScheduler.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_Law/INTER_IESMotionInfluenceReceiver.cs`
- `Assets/Plugins/ES/1_Design/Tests/EntityPrimaryAttackSelectorTests.cs`
- `Assets/Plugins/ES/1_Design/Tests/ESGenericLifePoolTests.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` (`40d6e8f476a7a9246af75b35f48573c2769d8ad5b4a699305f605b3abf93905a`)
- `Assets/Plugins/ES/1_Design/Work/ESWorkScheduler.cs` (`39c4ba867fb2fad0d9e64bf15fe99154605b9eab3d092e82827b60417032306a`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_Law/INTER_IESMotionInfluenceReceiver.cs` (`52e6b7eb20a456207a21dda9ab385704e98032b3fdd2e7c0bffa1df2021807f6`)
- `Assets/Plugins/ES/1_Design/Tests/EntityPrimaryAttackSelectorTests.cs` (`7a1fc3d2b768859df07bd3fe58d3dcd12bc7f324b91af7e49ae527162ad0398f`)
- `Assets/Plugins/ES/1_Design/Tests/ESGenericLifePoolTests.cs` (`57f1260c75da436d7f8e9c9cc0befc3332c8c4107f52e7ef60cbc4d8878c47cc`)
