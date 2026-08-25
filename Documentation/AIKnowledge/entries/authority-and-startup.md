# 启动入口与权威分层知识条目

状态：目标架构 / 当前启动链仍在使用。

`KnowledgeId`: `es.aibrain.authority-startup.v1`
`EvidenceLevel`: `S1`
`Authority`: `AIWarnings`
`RouteKeys`: `startup`, `authority`, `aiwarnings`, `context`
`ContentHash`: `a73113da4bd34dce0126e74f659fcc6c4d61f9cbfd6dfab25d69970ab575dabe`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`04af5af87127d069f4a5d2914ee12ce885043b804bd4d6050a3ec342721ca66b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)

`EvidenceRefs`: 当前 Codex launch-envelope/AIWarnings start chain 的只读初始化事实；AIBrain 启动器尚无运行回执。

`StaleWhen`: AIWarnings start chain、会话启动合同、AIBrain 启动实现或任一 SourceRef 哈希变化。

## 启动原则

目标上由 AIBrain 作为协作编排入口，启动时建立 BrainContext 并按任务查询最小知识集合。当前真实入口仍是现有 Codex/AIWarnings start chain；在 AIBrain 启动器取得运行证据前，不得宣称默认入口已经切换。

## 事实与约束权威

`FactAuthority`: `CurrentSourceAndEvidence > AIWarningsP0 > CurrentDomainRules > KnowledgeProjection > ExternalCache`

```text
当前源码/配置/真实证据 > AIWarnings P0 与现行领域规则 > AIBrain 索引 > Knowledge 摘要 > 外部缓存
```

这条顺序只裁决“项目现在是什么”和“实现/证据必须满足什么”，不裁决用户是否已经授权本轮动作。
“不作为启动来源”不等于“失去权威”。AIBrain 必须在执行前按 routeKeys 加载命中的 P0 与领域规则。
AICommand、TaskContract 和 AIBrain 计划是执行协议，不是项目事实权威。

## 动作授权与受管通道

- `ActionAuthority`: `CurrentExplicitUserInstruction`
- `ManagedProtocolRequiredWhen`: `ManagedAIBrain/Worker`

- 当前用户明确指令是本轮项目动作授权来源。授权覆盖其有界目标所必需的项目内修改，但不得由 AI 自主引申
  到未请求的删除、Git、Unity/Runtime、外部进程、网络、发布或凭据动作。
- AIWarnings、Knowledge、Skill、Catalog 和缓存可以限制实现方式与完成声明，不能扩大或缩小当前用户请求。
- 只有选用 ManagedAIBrain/Worker 通道时，才要求匹配的 AICommand、TaskContract、`planTask/runTask` 和回执；
  它们约束该通道的传输、幂等与证据，不构成第二次用户批准。直接用户通道不要求这些受管工件。

## 启动输出

每次启动应产生可追踪的 `BrainContext`：

- 当前项目根、分支、HEAD 和工作树摘要。
- 任务目标、授权范围、责任标识和会话上下文。
- 命中的 KnowledgeId、RequiredReads、RelatedSkills。
- AIWarnings 规则哈希和加载结果。
- 未验证证据与阻断原因。

## 禁止

- 通过删除 AIWarnings 来“简化启动”。
- 让 AIBrain 自己定义新的 P0。
- 以历史交接、Feishu 消息或 zread 页面覆盖当前源码事实。
- 把 AICommand、TaskContract、Skill 状态或 SourceRef 漂移解释为用户授权失效；漂移只使相关知识与计划 stale，
  必须回读事实并重规划。
