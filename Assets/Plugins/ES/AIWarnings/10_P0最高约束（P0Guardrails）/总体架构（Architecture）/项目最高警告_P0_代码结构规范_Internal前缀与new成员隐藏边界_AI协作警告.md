# P0：代码结构规范、Internal_ 前缀与 new 成员隐藏边界

Status: current
StableId: es.aiwarning.p0.internal-prefix-new-member-boundary.v1
Authority: AIWarnings（长期 P0 约束）；详细判定与审查门禁见 Knowledge
RouteKeys: aiwarnings, p0, architecture, csharp, internal-prefix, member-hiding, api-boundary
Applicability: ES 自有 C# 类型继承、成员可见性、内部入口命名、API 收口与结构重构
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-internal-prefix-new-member-boundary.md
StaleWhen: 继承/API 合同、程序集依赖、序列化/AOT、调用链或任一 SourceRef 哈希变化。

## P0 长期约束

- 禁止用 C# 成员 `new` 隐藏基类 API；改变静态类型后仍可访问，不能形成可靠访问控制。对象创建、`where T:new()`、正常 `override` 和有明确外部合同的窄例外不属于此禁令。
- 仅因“不希望普通用户调用”不得改用组合、只读 View、internal 外壳、重复接口、转发包装或拆分程序集；结构必须由真实职责、依赖方向、安全、序列化/AOT、事务/生命周期或发布合同证明。
- ES 自有且兼容暴露但不面向普通业务的真实入口使用 `Internal_` 前缀（如 `Internal_Clear`、`Internal_Inject`）；它是语义协作警示，不是权限控制、编译器隔离或安全边界。普通业务使用领域语义入口，不调用底层入口。
- 不为前缀再造一次性转发层；重命名真实入口并迁移当前触达调用链。Unity/BCL/第三方不可改名时保留外部合同，在 ES 领域入口声明边界。存量 `new` 冲突按触达迁移，不得无授权全仓机械改名。
- 真正隔离必须给出具体调用方、依赖、序列化、生命周期或安全证据；不得把“API 列表不好看”“智能提示少几个方法”写成结构改造理由，也不得宣称 `new` 已封闭 API。

## Knowledge 导航

完整判定范围、标准写法、允许的真实结构理由、审查问题、与泛型容器/协议 P0 的关系及验证清单见 `es.aiwarning.p0.internal-prefix-new-member-boundary.v1`。本 Warning 不授予代码修改、程序集重构或权限变更授权。
