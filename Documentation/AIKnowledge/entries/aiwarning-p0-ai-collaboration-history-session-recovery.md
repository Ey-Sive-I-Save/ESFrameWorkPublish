# P0 AI 协作历程与本地 Session 兜底恢复

`KnowledgeId`: `es.aiwarning.p0.ai-collaboration-history-session-recovery.v1`  
`Authority`: `AIWarnings + es-codex-session-bootstrap tools`  
`RouteKeys`: `aiwarnings`, `p0`, `codex`, `session`, `history`, `handoff`, `recovery`, `timeline`, `privacy`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `c5958a60d2cd715f140c1c410de694bd014e409d9674d5e02653c4b29f47a25f`  
`SourceSetHash`: `c5958a60d2cd715f140c1c410de694bd014e409d9674d5e02653c4b29f47a25f`  
`EntryBodyHash`: `b7d35664d710786951f66605c24030c0bf605c7dd5d00c6df23ee1a599feadcd`  
`StaleWhen`: `session bootstrap、handoff envelope、timeline coverage 工具、档案格式或 SourceRef 哈希变化。`

## 迁移说明

原 Warning 161 行、12,638 UTF-8 字节；现 Warning 只保留历程不是成果宣传、真实窗口唯一归属、用户授权和恢复完整性等 P0 门槛。本条目承接术语、交接路由、恢复流水线、隐私脱敏和完整性规则。

## 权威术语与状态分离

- 一个真实对话窗口对应一个唯一窗口档案；档案 ID 在重命名后保持不变。候选定位、归属确认、逐轮恢复和技术验收是四个不同状态，不能混写。
- `history.jsonl` 只用于候选搜索；`rollout-*.jsonl` 才是逐轮恢复证据。必须人工核对 session ID、开始时间、CWD、首尾任务和档案尾部连续性。
- `task_complete` 只表示当时答复，不等于源码修改、构建通过或 Unity 验收；失败、中止、用户否决、撤回和未闭合任务必须保留。

## 写入授权与交接路由

- 普通问答、代码修改、读取、构建和测试不得自动创建或更新 AI 协作历程。只有用户明确要求创建、更新、补全、恢复、整理或交接时，才获得对应范围写权限。
- “生成交接文案”只生成受证据约束的文案；“准备交接/先校验交接”使用默认校验路径；“直接交接/交给新窗口”才允许 OpenNew；关闭源窗口必须等目标返回 `ContextAccepted=true`。
- 交接必须先解析当前 Session、唯一档案和稳定 TaskKey，完成覆盖校验与 Bootstrap Validate；不能用聊天摘要或临时 Markdown 冒充正式交接。新窗口仍需 immutable launch envelope 和私有 handoff snapshot。
- 交接授权不扩大到源码、Git、Unity、发布、删除或外部发送；这些仍需独立用户指令。

## 恢复流水线

`用户提示/主题 → Find-CodexSession → 人工核对候选 → Recover-CodexSessionHistory → 覆盖审计 → UTF-8/ID/链接/脱敏检查`。

候选分数只帮助排序：ExactSessionId 可核对后确认；HighCandidate/ManualReview/LowCandidate 仍不得自动写入或合并。没有可靠已有档案时，才在授权范围内创建独立恢复档案。

## 完整性与隐私门禁

- 每条独立用户消息、纠正、失败、重试和外部交付复核都要有独立连续 `Txxx` 节点；阶段不能抵扣节点。编号、ID、README 尾号、链接和必备字段必须连续唯一。
- 必须排除 system/developer/world state/reasoning 原文和无关工具输出；API Key、Authorization、Cookie、密码、环境凭据和个人隐私必须脱敏。
- 活跃 session 只能声明截至某时的快照，并记录读取前后大小和最后事件时间。禁止手改 session JSONL、自动合并候选、用阶段总结替代完整时间线或在覆盖脚本失败后声称恢复完成。
- 生成/恢复后必须运行 `Tools/Test-ESCodexTimelineCoverage.ps1`；非零时结论只能是未完成/待修复。原阶段总结可保留，但旧 `Txxx` 标题必须降级，不能被覆盖校验器误计。

## EvidenceRefs

- `.agents/skills/es-codex-session-bootstrap/SKILL.md`
- `ES/AI协作历程（Codex）/Tools/Find-CodexSession.ps1`
- `ES/AI协作历程（Codex）/Tools/Recover-CodexSessionHistory.ps1`
- `ES/AI协作历程（Codex）/Tools/Test-ESCodexTimelineCoverage.ps1`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_AI协作历程与本地Session兜底恢复_AI协作警告.md` (`d6fd3966b7a74d7683509b3a9519278253941913c5ad3665d77ff1e25575f46d`)
- `.agents/skills/es-codex-session-bootstrap/SKILL.md` (`c1e82db86536e3a3786773eb5a13bfff5a1acac141d05955f613355c88f24071`)
- `ES/AI协作历程（Codex）/Tools/Find-CodexSession.ps1` (`662beb19706a8ada0aa3433350488fa757bb8fad2ce60630216435076ea17d65`)
- `ES/AI协作历程（Codex）/Tools/Recover-CodexSessionHistory.ps1` (`c946696fc4d6daf7c84bd19037fe314b07fd1f8c4bfcc5928234ac8e4080b634`)
- `ES/AI协作历程（Codex）/Tools/Test-ESCodexTimelineCoverage.ps1` (`848846aab10a57ce10e45b422e2715efbe2ed6384cbd844a0de0e54a56e56784`)
