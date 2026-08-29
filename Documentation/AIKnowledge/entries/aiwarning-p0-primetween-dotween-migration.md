# P0 PrimeTween / DOTween 迁移：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.primetween-dotween-migration.v1`  
`Authority`: `AIWarnings` 原文与当前依赖/运行时迁移规则  
`RouteKeys`: `aiwarnings`, `p0`, `primetween`, `dotween`, `tween-lifecycle`, `runtime-performance`  
`HashSchema`: `v2`  
`ContentHash`: `4270bdfb55ddf117d380a13c18d43d7d9d91290a2d1f497637ef4a0919211f5f`  
`SourceSetHash`: `4270bdfb55ddf117d380a13c18d43d7d9d91290a2d1f497637ef4a0919211f5f`  
`EntryBodyHash`: `78ebaab310e611f3dacb3f26b4af271f64421a955fe56595f73ab7dbafc95645`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 原 Warning、PrimeTween/DOTween 版本、依赖配置、测试回执或任一 SourceRef 哈希变化。

## 迁移说明

Warning 本体只保留长期 P0 约束、权限和证据边界；本条目保存详细迁移矩阵及原文快照。Knowledge 不授予依赖删除、Runtime、发布或其他权限。

## 详细迁移规则

- PrimeTween `1.4.11` 为新生产 Tween 默认；新增代码不得引用 DOTween 或启用 `PRIME_TWEEN_DOTWEEN_ADAPTER`。
- Tween 仅用于表现/UI/镜头/非权威过渡；对象池回收、销毁、Disable、状态切换必须停止或失效当前 Tween。
- 生产热路径避免闭包、捕获回调和每帧创建；冷路径分配不得宣称 0 GC。
- `DOMove/DOFade/DOScale`、`Append/Join`、`Kill/Complete` 等映射必须逐语义确认；完成后不可复用的 Tween 不得机械翻译。
- 生产引用为零并完成代码、测试、对象池和 IL2CPP 证据后，才可清理 asmdef/link.xml/安装器配置；旧包未确认无依赖前不得删除。

## 原文保真快照（迁移前）

```markdown
# PrimeTween / DOTween 迁移 P0 协作警告

## 当前标准

- PrimeTween `1.4.11` 是项目新的默认 Tween 实现。
- 新增生产代码不得继续引用 `DG.Tweening`、`DOTween` 或 DOTween 扩展方法。
- 不建立运行时兼容壳，不启用 PrimeTween 的 `PRIME_TWEEN_DOTWEEN_ADAPTER`。
- 迁移期间允许暂存旧 DOTween，但每完成一个批次都必须清零该批次的 DOTween 引用。

## 生命周期边界

- Tween 只用于表现、UI、镜头和非权威过渡。
- 禁止用 Tween 驱动 KCC 根位移、网络同步状态、战斗判定窗口或其他权威事实。
- 对象池回收、Owner 销毁、Disable 和状态切换时，必须停止或失效当前 Tween。
- 不得让完成回调在对象已经回池或绑定到下一租用者后继续写入。

## 性能边界

- PrimeTween 的无分配能力只在热身容量、非捕获回调和正确生命周期管理下成立。
- 禁止在每帧创建带闭包的 Tween、Sequence 或 `OnUpdate` 委托。
- 需要回调时优先使用 target 参数重载，避免捕获 `this`。
- `async/await`、Coroutine 和调试快照属于可能分配的冷路径，不得宣称为 0 GC 热路径。

## API 迁移规则

- `DOMove/DOFade/DOScale` 等调用改为对应的 `PrimeTween.Tween` 静态 API。
- `Sequence.Append/Join` 改为 `Sequence.Chain/Group`。
- `Kill` 改为 `Stop`，`Complete` 语义必须逐处确认。
- DOTween 的可复用 Tween、`SetAutoKill(false)`、`PlayForward/PlayBackwards` 不得机械翻译；PrimeTween Tween 完成后不可复用，应重新创建。
- `Ease`、循环、时间缩放、UpdateType 和回调顺序必须逐项回归，不能只替换 using。

## 依赖清理门禁

- 生产代码扫描为零后，才能移除 `DOTween.Modules` asmdef 引用。
- 随后同步清理 HybridCLR AOT 引用、link.xml、安装器配置和旧文档。
- `Assets/Plugins/Demigiant` 与 `Obsolete` 内容在确认无程序集依赖前不得直接删除。
- 每批迁移必须通过 ES_Logic、ES_Editor、相关 EditMode/PlayMode 测试和目标 IL2CPP 构建。

## 禁止的伪完成

- 只修改包依赖、不迁移任何实际调用，不得宣称 PrimeTween 已接入。
- 只把 `using DG.Tweening` 改成 `using PrimeTween`，但保留不可复用 Tween 语义，不得宣称迁移完成。
- 只在 Editor 或 Demo 中验证，不得宣称运行时、对象池和 IL2CPP 已验收。
```

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/PrimeTween_DOTween_迁移_P0_AI协作警告.md` (`ae47d72f6cbd36fb956b1e1a1fbcf610732b1675179c5c52671c7f748ddcda4a`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`c444db776d2ceb642b7c85b29f269c1e4335242055995227de0e5a4119018061`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-primetween-dotween-migration.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
