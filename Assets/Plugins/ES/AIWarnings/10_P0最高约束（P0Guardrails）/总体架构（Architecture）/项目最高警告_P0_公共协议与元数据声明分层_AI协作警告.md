# P0：公共协议、领域接口与 Attribute 元数据必须分层

`Status`: `current`
`StableId`: `es.aiwarning.p0.protocol-metadata-layering.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `public-protocol`, `metadata`, `stand-boundary`, `assembly-dependency`
`Applicability`: public interface、跨系统协议、Attribute、Drawer 共用契约及程序集归属。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-protocol-metadata-layering.md`
`StaleWhen`: 目录归属、程序集依赖、协议定义、Attribute 边界或 SourceRefs 变化。

## 长期 P0 约束

- 新增类型先判断协议/元数据/实现、长期权威、Runtime/Editor 共同依赖和去掉当前 Drawer/业务后的成立性，再决定位置；禁止为少改文件而倒推架构理由。
- 跨系统稳定协议归 `Assets/Plugins/ES/0_Stand/BaseDefine_Law`，纯 Attribute/枚举/无状态声明元数据归 `0_Stand/Attributes`，单领域接口归对应 Runtime/<Domain>，Editor 扩展点归 Editor/<Domain>。
- BaseDefine_Law 准入至少满足跨两个领域/程序集、稳定通用契约、无 Profile/Drawer/业务依赖或 Runtime/Editor 共同权威之一；原则上独立命名 `INTER_<InterfaceName>.cs`。
- BaseDefine_Law 禁止 UnityEditor、Odin/Sirenix、EditorPrefs、SessionState、窗口状态和具体业务服务；不得成为无法分类接口的收纳箱。Attributes 禁止定义跨系统 Runtime 协议。
- 协议移动必须连同 `.meta` 保留 GUID；从混合脚本提取时原 `.meta` 保留，新脚本独立 `.meta`；旧位置不得保留别名、转发接口或重复定义。
- 必须用 `rg` 确认唯一权威定义，并验证 Stand、Runtime、Editor 依赖方向；公共协议、Attribute 越权、Runtime→Editor、Stand→业务/绘制均为 P0 违规。
- 验收必须覆盖唯一协议定义、严格 UTF-8/U+FFFD、`.meta` 与差异检查及定向编译；Unity 工程未刷新或有无关错误时按证据降级，不得宣称通过。

详细归属表、迁移规则、验收门禁和原文快照见 Knowledge：`es.aiwarning.p0.protocol-metadata-layering.v1`。
