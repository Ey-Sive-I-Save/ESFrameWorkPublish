# AI 协作历程与模块审计：商业可行性验收 Knowledge

`KnowledgeId`: `es.aiwarning.validation.ai-collaboration-commercial-acceptance.v1`  
`Authority`: `AIWarnings` + current governance contracts  
`RouteKeys`: `aiwarnings`, `validation`, `collaboration`, `audit`, `commercial`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `fd1cecfdd45f0abf797ba9c9c55cfafbdb333e4f5f151d0d2f1f0b35d7e17147`  
`SourceSetHash`: `fd1cecfdd45f0abf797ba9c9c55cfafbdb333e4f5f151d0d2f1f0b35d7e17147`  
`EntryBodyHash`: `351cb33eb5f2a57cfb0044a2353fea61cd7116203bb03b0155f45e894bdb5ca3`  
`StaleWhen`: 历程/审计合同、恢复格式、权限边界、验收用例或交接协议变化。

## 迁移范围

Warning 从 76 行、4295 字节压缩为长期验收边界；本条目保存 C0-C3 准入、W1-W10 用例、商业维度、交接条件和失败面。Knowledge 不写入审计历史，不授予 Git、Runtime、网络、发布或交接权限。

## 准入等级

- C0：规则和工具源码存在，只能内部开发；C1：单窗口完整闭环；C2：长窗口恢复、失败注入和跨窗口接手；C3：多人/多 AI、权限、性能、回归和长期运行。未取得 C2/C3 证据只能称“工作流已实现，待商业验收”。

## 必验用例 W1-W10

- W1 需覆盖 50 条以上消息且每条独立语义都有节点；W2 同一 turn 的多条补充/纠正独立编号。
- W3 按顺序保留失败、撤回和外部交付；W4 从确认 JSONL 恢复并报告截止点与计数。
- W5 错误 session/档案候选只读阻断；W6 HEAD/工作树漂移标记 stale 并重新采证。
- W7 历程与审计并行时完整过程留在历程，状态只留恢复导航；W8 覆盖校验非零即禁止完成表述。
- W9 用户不需要交接时只询问一次；W10 用户需要交接时提示必须包含范围、权威、证据、缺口和权限边界。

## 商业可行性维度与签收

完整性、可恢复性、权限安全、证据真实性、并发安全、性能成本、可运维性和用户体验必须分别有证据。只有 W1-W10 全部可复现，并完成 50 条以上真实 session 恢复、错误候选阻断、stale 检查点复核和跨 AI 接手演练，才可达到 C2；C3 还要求长期回归、多人并行、性能成本和权限事故演练。交接文案只在完整工作流交付后、用户明确要求时生成一次。

## 原 Warning 保真快照（HEAD）

以下保留迁移前 Warning 的完整文本；其 HEAD SHA-256 为 `f1a8b0037b0941c6a2837da94d20dacef03f50393f7866cdea9bc51e1ce6b2d0`。

~~~~markdown
# AI 协作历程与模块审计：商业可行性验收标准

状态：现行验收标准。

本标准验证“生命历程 + 模块审计 + 跨窗口交接”能否在长周期、多 AI、脏工作树和证据分层条件下稳定使用。规则文件存在或单次演示成功，不等于商业可行。

## 准入等级

| 等级 | 含义 | 允许范围 |
|---|---|---|
| C0 | 规则与工具源码存在 | 只能内部开发，不得称工作流可用 |
| C1 | 单窗口完整闭环通过 | 可用于普通内部协作 |
| C2 | 长窗口恢复、失败注入和跨窗口接手通过 | 可用于项目级持续协作 |
| C3 | 多人/多 AI、权限、性能、回归和长期运行通过 | 可称商业项目治理基座 |

当前未取得 C2/C3 证据时，准确表述只能是“工作流已实现，待商业验收”。

## 完整工作流

```text
用户授权历程或模块审计
  -> 确认窗口/session/模块唯一归属
  -> 读取最小权威规则集与当前 Git/源码事实
  -> 阶段容器下逐条建立 T 节点，或形成模块证据矩阵
  -> 保留失败、纠正、反复、外部交付和未完成项
  -> 运行覆盖/编号/UTF-8/工作树机械门禁
  -> 评估商业可行性与缺失证据
  -> 仅把精简恢复导航写入固定审计状态
  -> 最终醒目询问一次是否生成跨 AI 交接文案
  -> 用户同意后才生成可直接复制的新 AI 提示
```

## 必验用例

| 编号 | 场景 | 必须观察 | Blocker |
|---|---|---|---|
| W1 | 50 条以上用户消息 | 每条独立语义都有 T 节点；阶段不抵扣节点 | 只保留最近或重要节点 |
| W2 | 同一 turn 多条补充/纠正 | 共享执行轮但各自独立编号 | 合并成一次任务 |
| W3 | 失败、撤回、外部交付 | 按发生顺序保留，权责不冒领 | 只留下最终漂亮结论 |
| W4 | 上下文压缩或失联恢复 | 从确认 JSONL 恢复并报告截止点和计数 | 凭摘要宣称完整恢复 |
| W5 | 错误 session 或档案候选 | 只读阻断，不污染已有档案 | 模糊分数自动授权写入 |
| W6 | 工作树或 HEAD 漂移 | 检查点标记 stale 并重新采证 | 旧状态直接授予继续实现 |
| W7 | 审计与历程并行 | 历程保存完整过程；状态文件只保存恢复导航 | 把完整时间线塞进状态文件 |
| W8 | 覆盖校验失败 | 返回非零状态并禁止完成表述 | 口头保证替代机械结果 |
| W9 | 用户不需要交接 | 只询问一次，拒绝后正常结束 | 反复催促或自动写交接 |
| W10 | 用户需要交接 | 输出可直接复制提示，含规则、范围、证据、缺口和权限边界 | 把历史结论当当前事实或授予新权限 |

## 商业可行性维度

- 完整性：用户消息、任务节点、排除项、完成/中止/未闭合数量可对账。
- 可恢复性：新 AI 能从固定入口定位，而不需要猜文件或全量阅读 AIWarnings。
- 权限安全：历程、审计、实现、Git、Unity、发布权限相互独立。
- 证据真实性：源码、Unity、测试、Profiler、Player、网络和发布不越级。
- 并发安全：脏工作树、其他 AI 修改和旧检查点不会被覆盖。
- 性能与成本：长窗口允许分阶段读取；状态文档保持精简，完整档案按需加载。
- 可运维性：脚本失败有退出码、错误说明和可重复运行方式。
- 用户体验：完成后明确提供交接选项，但不默认生成、不阻断交付。

## 最终交接询问格式

只有历程或完整审计工作流已经交付后，最终答复末尾才追加一次：

---

## 是否需要生成 AI 对话交接文案？

如果需要，我会生成一份可直接复制给新 AI 的交接提示，包含当前范围、权威入口、已完成事项、证据等级、未完成项、恢复步骤和禁止越界事项。

回复“生成交接文案”即可。

普通问答、未完成工作流、用户已拒绝或本轮已经生成交接文案时，不再追加该询问。

## 当前签收条件

只有 W1-W10 均有可复现证据，且至少完成一次 50 条以上真实 session 的恢复、一次错误候选阻断、一次 stale 检查点复核和一次跨 AI 接手演练，才能达到 C2。C3 还要求长期回归、多人并行、性能成本和权限事故演练。
~~~~

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/AI协作治理验收（AICollaborationAcceptance）/AI协作历程与模块审计_商业可行性验收标准.md` (`cac7f1746d29499373d6d715689ffbcc484b9313685ba2de61370c8f3d970558`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-validation-ai-collaboration-commercial-acceptance.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
