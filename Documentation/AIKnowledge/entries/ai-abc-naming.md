# ES AI ABC 命名权威（ABCD / ABCC / ABCP）

`KnowledgeId`: `es.architecture.ai-abc-naming.v1`
`Authority`: `ES/Automation/Contracts/es-ai-abc-mode.registry.json` 的 `namingAuthority` 与当前 ES 高频命名 P0
`RouteKeys`: `ai-abc`, `abc-naming`, `abc-core`, `abc-part`, `knowledge`, `routing`, `evidence`
`HashSchema`: `v2`
`ContentHash`: `d4963ea63a1ae4f8e9caf33dc8583304e568b64907c9e63f9e998cb9d0c6231e`
`SourceSetHash`: `d4963ea63a1ae4f8e9caf33dc8583304e568b64907c9e63f9e998cb9d0c6231e`
`EntryBodyHash`: `9e9f78a3a85efc0e9ebe7c99d17ec4f0ad0f60516898a4f19ef6f181a153ec52`
`EvidenceLevel`: `S1`
`StaleWhen`: `namingAuthority、ABC 模式注册表、高频命名 P0、ABCC/ABCP 合同、KnowledgeIndex、AIBrain 路由或任一 SourceRef 哈希变化。`

## Canonical vocabulary

`ABC` 的正式展开是 `Agent–Behavior–Collaborator`，中文为“智能体—机制—协作者”。

- **A / Agent（智能体/流程端）**：提出目标、生成意图，并消费 B 返回的归一化结果。
- **B / Behavior（机制/能力端）**：提供可协商、可验证能力的独立机制；这里的 Behavior 不是
  BehaviorTree 专属名称。
- **C / Collaborator（协作者）**：用户、AI 或真人，提供目标、授权和最终接受。

三种正式模式使用相同的 ABC 角色，不重新定义字母：

| 稳定 modeId | 正式英文名 | 中文短名 | 模式职责 |
|---|---|---|---|
| `ABCD.Dynamic` | `Agent–Behavior–Collaborator Dynamics` | `ES 动态协作体` | 面向广泛场景动态组织 A↔B↔C，不依赖固定 Part |
| `ABCC.Core` | `Agent–Behavior–Collaborator Core` | `ES ABC 核心` | 独立完成 A↔B 语义适配、能力协商和证据归一化 |
| `ABCP.Part` | `Agent–Behavior–Collaborator Part` | `ES ABC 部件` | 在特定领域绑定 ABCC，保持领域语义和边界 |

`D`、`Core` 和 `Part` 是模式后缀，不是第四个角色。`modeId` 是稳定机器身份；英文正式名和中文短名是
可本地化的显示名称，不能反向改写稳定身份。ABCP 失败时只能按显式合同回退到 ABCD，不能静默切换。

## Discovery and usage

机器路由优先使用 `modeId`，需要展示时从模式注册表的 `namingAuthority.modeNames` 读取正式名和短名。用户可
使用“ES 动态协作体”“ES ABC 核心”“ES ABC 部件”进行发现；这些名称只提供导航，不授予写入、运行时、网络
或发布权限。详细语义仍回到对应的 ABCC/ABCP 合同和当前权威源码。

## Failure-surface matrix

### `ABC-NAME-001` 稳定身份被显示名替换

- `severity`: `identity/authority`
- `erroneousBehavior`: 用中文短名替换 `ABCD.Dynamic`、`ABCC.Core` 或 `ABCP.Part` 作为机器引用。
- `triggerAndSymptom`: 合同引用断裂、旧计划无法重放或 Part 找不到 Core。
- `rootCause`: 把本地化显示名称误当稳定 `modeId`。
- `preventionCheck`: 机器字段只接受注册表中的稳定 `modeId`，显示层单独读取 `modeNames`。
- `correctAction`: 保留原 `modeId`，只修正显示映射。
- `recoveryAction`: 从当前模式注册表重建路由并使旧计划 stale；禁止猜测替代身份。
- `evidencePresent`: 模式注册表的 `modes` 与 `namingAuthority.modeNames`。
- `evidenceMissing`: 实际所有消费者对本地化名称的运行时兼容回执。

### `ABC-NAME-002` B 角色被错误收窄为 BehaviorTree

- `severity`: `recoverable`
- `erroneousBehavior`: 只把 B 路由到行为树，拒绝武器、UI 或其他独立机制。
- `triggerAndSymptom`: 领域 Part 无法发现正确能力，出现错误路由或无能力结果。
- `rootCause`: 忽略命名权威对 B 的“机制/能力端”定义。
- `preventionCheck`: 路由解释同时检查 `Behavior` 的机制/能力语义和领域 routeKey，不以单词猜测实现。
- `correctAction`: 回到 A↔B 合同，按能力声明、前置条件和证据选择 B。
- `recoveryAction`: 标记当前计划 replan，保留原始意图和失败证据，重新执行有界发现。
- `evidencePresent`: `namingAuthority.base.roles.B` 与 ABCC 接口合同。
- `evidenceMissing`: 各领域路由在真实运行时的误命中率和恢复回执。

### `ABC-NAME-003` 三套模式各自重新定义 ABC

- `severity`: `identity/authority`
- `erroneousBehavior`: ABCC 或 ABCP 在自身文档中复制并改写 A/B/C 含义。
- `triggerAndSymptom`: 同一句用户需求在 Dynamic、Core、Part 中得到不同语义，出现双权威。
- `rootCause`: 把模式差异误建成角色差异，绕过公共命名权威。
- `preventionCheck`: 校验所有模式引用同一 `namingAuthority.authorityId`，领域扩展只能增加约束，不能改写 ABC。
- `correctAction`: 以模式注册表为唯一命名源，删除重复定义或降级为链接说明。
- `recoveryAction`: 重新读取注册表和目标 Knowledge，丢弃受污染的路由缓存与计划。
- `evidencePresent`: `namingAuthority` 的 authorityId、版本和三模式映射。
- `evidenceMissing`: 未来新增 Part 对公共命名源的自动一致性验证。

### `ABC-NAME-004` 名称被误报为运行时能力

- `severity`: `advisory`
- `erroneousBehavior`: 因注册表出现正式名称，就宣称对应模式已完成 Unity、Runtime 或发布验收。
- `triggerAndSymptom`: 设计层名称被压平成“已可用”或“已验收”。
- `rootCause`: 混淆命名身份、静态合同和运行时证据。
- `preventionCheck`: 交付报告分别声明 EvidenceLevel、Runtime 状态和非声明项。
- `correctAction`: 将结论限制为 S1 静态命名与路由事实。
- `recoveryAction`: 撤回越级结论，补充真实验证回执后再升级证据等级。
- `evidencePresent`: 当前模式注册表、Knowledge 条目和 AI 交付声明 P0。
- `evidenceMissing`: Unity/Player/Runtime/Release 验收回执。

## Evidence boundary and non-claims

本条目支持三种模式的稳定命名、角色定义、显示别名和发现导航。它不证明模式已经通过 Unity 导入、Runtime、
Player、Profiler、IL2CPP、网络、视觉、性能或发布验收，也不授予任何执行权限。

## SourceRefs

- `ES/Automation/Contracts/es-ai-abc-mode.registry.json` (`5950220db01715980e2456fdea26a80f8f816c5e61cb47f99c03739a8510e95e`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` (`40d6e8f476a7a9246af75b35f48573c2769d8ad5b4a699305f605b3abf93905a`)
