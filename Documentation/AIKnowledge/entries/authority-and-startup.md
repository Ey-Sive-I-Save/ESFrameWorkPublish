# 启动入口与权威分层知识条目

状态：目标架构 / 当前启动链仍在使用。

`KnowledgeId`: `es.aibrain.authority-startup.v1`
`EvidenceLevel`: `S1`
`Authority`: `AIWarnings`
`RouteKeys`: `startup`, `authority`, `aiwarnings`, `context`
`ContentHash`: `494030d538bdfde2a0e3f182c4b1a70e7468e044ca5baba7167502de6dbc4458`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`b59233c67b4e86f2c85b96e975af76f633a1a4b0dbe6e6796ca8ef26df826863`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`c5359cb022ebc2902c4400ad44429da36d1a2dcfa44803586f8f91aaca0d704f`)

`EvidenceRefs`: 当前 Codex launch-envelope/AIWarnings start chain 的只读初始化事实；AIBrain 启动器尚无运行回执。

`StaleWhen`: AIWarnings start chain、会话启动合同、AIBrain 启动实现或任一 SourceRef 哈希变化。

## 启动原则

目标上由 AIBrain 作为协作编排入口，启动时建立 BrainContext 并按任务查询最小知识集合。当前真实入口仍是现有 Codex/AIWarnings start chain；在 AIBrain 启动器取得运行证据前，不得宣称默认入口已经切换。

## 不变的权威优先级

```text
当前源码/真实证据 > AIWarnings P0 > AICommand > AIBrain 索引 > Knowledge 摘要 > 外部缓存
```

“不作为启动来源”不等于“失去权威”。AIBrain 必须在执行前按 routeKeys 加载命中的 P0 与领域规则。

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
