# Internal_ 前缀与 new 成员隐藏：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.internal-prefix-new-member-boundary.v1`  
`Authority`: `AIWarnings` 与当前 C#/API/程序集合同  
`RouteKeys`: `aiwarnings`, `p0`, `architecture`, `csharp`, `internal-prefix`, `member-hiding`, `api-boundary`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `99e46775ff4ebac7b9fb3d444dbd82701ee36cc467f2d713b62f37be5bb7712b`  
`SourceSetHash`: `99e46775ff4ebac7b9fb3d444dbd82701ee36cc467f2d713b62f37be5bb7712b`  
`EntryBodyHash`: `9ae9a0d140e13ba75f95e346fecdeb2917e7c9df83f86e0345040a7d453179f0`  
`StaleWhen`: 继承/API 合同、程序集依赖、序列化/AOT、调用链或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留禁止 `new` 伪收口、`Internal_` 语义边界和结构改造授权门槛；本条目承载判定范围、标准写法、合法例外、真实结构理由、审查问题及验证清单。Knowledge 不授予代码或程序集修改权限。

## 判定与标准写法

成员声明上的 `public new void Inject(...)` / `public new bool TryRemove(...)` 只是隐藏，不移除基类 API；调用方改变静态类型后仍可访问。对象创建、`where T:new()`、数组/集合实例化、正式 `override` 和必须维持外部合同的 Unity/第三方窄例外不属于该禁令；窄例外必须说明静态类型、隐藏后语义与测试证据。

ES 自有、兼容暴露但不面向普通业务的真实底层入口使用 `Internal_` 命名，例如 `Internal_Clear`、`Internal_Inject`、`Internal_Register`。该前缀是协作语义警示，不是 C# 访问控制、安全隔离或编译器禁止调用；普通业务使用 `InjectWithDefaults` 等领域入口，底层实现、扩展和测试按职责调用。

不为命名边界增加一次性转发包装。ES 自有入口应重命名真实实现并迁移当前触达调用链；Unity/BCL/第三方入口不可改名时保留外部合同，并在 ES 领域入口声明使用边界。存量 `new` 隐藏按触达迁移，不得无授权全仓机械改名。

## 允许结构调整的证据

只有在不可信调用方越权、Runtime/Editor 或程序集依赖方向、Unity/Odin 序列化身份与版本迁移、IL2CPP/AOT、独立资源/事务/并发/生命周期所有权，或正式只读发布合同确实要求时，才可采用组合、接口、访问级别或程序集边界。“普通用户不该用”“API 列表不好看”“想让智能提示少几个方法”不是充分理由。无法证明时登记为触达迁移债务，不做结构重构。

## 审查与验证

修改前逐项回答：`new` 是创建还是成员隐藏？是否仅制造访问受限错觉？`Internal_` 是否命名真实入口？是否新增无职责的 View/接口/程序集/转发层？若需真正隔离，安全/依赖/序列化/生命周期证据是什么？是否只覆盖当前调用链？验证搜索 `new` 成员和新增 `Internal_` 名称，回看派生类型/基类视图/接口调用方，并执行严格 UTF-8、U+FFFD/乱码与 `git diff --check` 检查。

## 原文快照与 SourceRefs

迁移前台账快照：93 行、5951 字节，原始 SHA-256 `f3b5f5e1b9a6362188cc8be95392f7d7b85819cec4a3df920f23a7cdb9a3ec21`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_代码结构规范_Internal前缀与new成员隐藏边界_AI协作警告.md` (`bbfdf40d223c489ecd39235f65496bcac71b69e89969fc3a4c9ffcaf1b26fb48`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`b997ebedef048ce72af2a929b7ab5b0bba99091ed9fc49afa0449cfbb6cae0e3`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-internal-prefix-new-member-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_代码结构规范_Internal前缀与new成员隐藏边界_AI协作警告.md`
