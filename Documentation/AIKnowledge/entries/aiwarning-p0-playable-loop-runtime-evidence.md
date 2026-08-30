# 实际可玩闭环与运行证据：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.playable-loop-runtime-evidence.v1`  
`Authority`: `AIWarnings` 原文与当前运行证据/成熟度合同  
`RouteKeys`: `aiwarnings`, `p0`, `playable-loop`, `runtime-evidence`, `playmode`, `profiler`, `game-core-loop`, `structure-validation`, `implementation-validation`, `presentation-validation`, `performance-validation`, `abcd`, `ai-abc`  
`HashSchema`: `v2`  
`ContentHash`: `d5a5946085442dcaed922a572fbe074b6a2a0690f9921b8f167b993add2eabdf`  
`SourceSetHash`: `d5a5946085442dcaed922a572fbe074b6a2a0690f9921b8f167b993add2eabdf`  
`EntryBodyHash`: `42b53dc2de8d7c1f1ea28ac05022749b802d2881b960e37deabbde5d53ea5816`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 运行闭环、测试场景、证据等级、成熟度或任一 SourceRef 哈希变化。

## 迁移说明

Warning 保留完整闭环、玩家主链、失败清理、表现/性能和证据分层边界；本条目承载场景模板、AICommand 生命周期、风险报告和原文语义。Knowledge 不授予 Unity/Runtime/发布权限。

## 闭环与场景合同

运行时功能必须可追踪：玩家/系统输入意图 → 权威 Request/State/Command → 唯一执行入口 → 业务执行 → 运动与世界结果 → 动画/IK/VFX/音频/UI 反馈 → 可观察终态 → 成功/失败/取消/打断/超时/禁用/回池清理。只有类型、接口、Profile、Adapter、Registry、事件或日志而没有真实消费者和可观察结果，不得称完成。

玩家主链需证明角色控制权、相机旋转/跟随/避障、相机映射移动、转身意图，以及重复/抖动/同时输入、设备/焦点切换和成功/失败/受阻/取消/打断/超时/拒绝/结束的反馈与重置。测试场景至少包含角色、相机、输入、起点、目标、障碍、失败区、重置和成功/失败/取消/重入/回池观察点；无正式/专用场景只能报告未验收。

生命体和 AICommand 覆盖发现/接受、准备/进入、持续执行、成功/失败/取消/打断、表现收尾、控制权释放、资源/Lease/临时目标清理、回池安全重置与再次进入。验收须写目标对象、前置条件、控制权、执行入口、唯一消费者、进行中状态、失败码、可观察结果、清理和重试/取消路径；日志、动画或事件不是业务成功。

视觉效果必须与业务状态一致。按风险实测输入到结果延迟、相机/移动响应、动画过渡、状态切换及 Profiler GC/CPU/内存；不适用指标要说明理由，不能凭源码形态声称无性能风险。报告分开实现事实、静态、Unity 编译/域重载、真实操作/PlayMode、表现、Profiler、发布/IL2CPP 证据。缺运行证据时成熟度最高为 `Verifying` 或更低。

## 原文快照

迁移前完整 Warning（72 行、6729 字节）由以下 SourceRef 保留，原始 SHA-256 为 `ef80427c19ab315e9d69ec810caaabb0164a7a2b93f6406d7ee4c5cdd8b7d740`。

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md` (`92894b62cf1af0cc26e7ee7d2de31bfd88ad88377b64499f759f721f27621d85`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`c1fc2f3dd03713d0bedf4c12c4e95190613033af55cc28eb79b075976501c31b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`2aa56abe81352fd79ad59b1364ffa7381d70b26674a1676b8439173a515d9b6c`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`67794270442817648d4894f45766bf83d44aabc25e06f944f96717eda2462ddc`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-playable-loop-runtime-evidence.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
