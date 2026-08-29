# 公共协议与元数据分层：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.protocol-metadata-layering.v1`  
`Authority`: `AIWarnings` 原文与当前程序集/目录边界  
`RouteKeys`: `aiwarnings`, `p0`, `public-protocol`, `metadata`, `stand-boundary`, `assembly-dependency`  
`HashSchema`: `v2`  
`ContentHash`: `e2f332beae997875299d1240016a527adedc8c7b18239d451909968269c8034f`  
`SourceSetHash`: `e2f332beae997875299d1240016a527adedc8c7b18239d451909968269c8034f`  
`EntryBodyHash`: `96d6003d52dee61e6fb20b40503e4a8cb52b7e39709dfa1799d3e76e93be2983`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 目录归属、程序集依赖、协议定义、Attribute 边界或任一 SourceRef 哈希变化。

## 迁移说明

Warning 本体保留分层原则、P0 禁止事项、GUID/Meta 迁移和验收边界；本条目承载详细归属判断、准入条件、迁移步骤、反向依赖检查和原文语义快照。Knowledge 不授予代码移动或编译权限。

## 归属与准入

| 类型 | 权威目录 | 条件 |
|---|---|---|
| 跨系统/跨领域稳定协议 | `Assets/Plugins/ES/0_Stand/BaseDefine_Law` | 两个以上领域/程序集、稳定通用契约、无具体业务依赖或 Runtime/Editor 共同权威之一 |
| Attribute、绑定枚举、纯声明元数据 | `Assets/Plugins/ES/0_Stand/Attributes` | 无实例状态、无运行分派职责 |
| 单领域运行时接口 | `Runtime/<Domain>` | 仅该领域依赖 |
| Editor 内部扩展点 | `Editor/<Domain>` | 不被 Runtime/其他领域依赖 |

公共协议原则上独立为 `INTER_<InterfaceName>.cs`。BaseDefine_Law 禁止 UnityEditor、Odin/Sirenix、EditorPrefs、SessionState、窗口状态和具体业务服务，也不能成为无法分类的接口收纳箱。Attributes 不能因 Drawer 使用而定义跨系统 Runtime 协议。

## 判断、迁移与验收

新增类型依次判断协议/元数据/实现、长期权威、Runtime/Editor 共同依赖、去掉 Drawer/业务后是否仍成立，最后决定文件位置。移动协议须连同 `.meta` 保留 GUID；混合脚本提取时原 `.meta` 保留，新脚本独立 `.meta`；旧位置不得保留兼容别名、转发接口或重复定义。用 `rg` 证明全项目唯一权威定义，并检查 Stand、Runtime、Editor 依赖方向。验收覆盖唯一协议、无 Attribute 越权、Runtime 不引用 Editor、Stand 不引用业务/绘制、严格 UTF-8/U+FFFD、Meta、差异检查和定向编译；Unity 工程未刷新或有无关错误时必须降级结论。

## 原文快照

迁移前完整 Warning（83 行、3251 字节）由以下 SourceRef 保留，原始 SHA-256 为 `670f5640ac43edcbd35e135e655b67221f62b481fadb1919f67dae2ba9acaeb8`。

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_公共协议与元数据声明分层_AI协作警告.md`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_公共协议与元数据声明分层_AI协作警告.md` (`02eeab93dcac836e2ef0aca604f2f561f8e96976dc3be3c216c45cab88dc3c1d`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`fc3c0f284187dd35b2021cca0e5acf2a75fd2bbccecd78d6f0cceb22c5e20e0c`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-protocol-metadata-layering.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
