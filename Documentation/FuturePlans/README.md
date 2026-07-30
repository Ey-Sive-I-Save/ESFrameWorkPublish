# ES Future Plans

本目录统一保存尚未立项、尚未进入当前运行时契约的未来架构方案。

## 使用规则

- 本目录内容不是当前功能说明，也不是 AI 可直接执行的任务。
- 每份计划必须写明现状、触发条件、非目标、风险、迁移边界和验收门禁。
- 未满足触发条件、未经项目作者批准，不得依据本目录修改生产代码。
- 当前已经冻结且必须遵守的规则仍以 `Assets/Plugins/ES/AIWarnings` 为准。
- 当前可以直接执行的扩展流程仍以 `Assets/Plugins/ES/AICommands` 为准。
- 已废弃实现和历史源码进入根目录 `Archive`，不得与未来方案混放。
- Unity 自动生成的 `Library/Artifacts`、`Library/TempArtifacts` 不是文档目录，禁止写入人工资料。

## 状态定义

| 状态 | 含义 |
| --- | --- |
| `Idea` | 仅记录问题和方向，尚未完成边界分析 |
| `Reserved` | 边界已明确，等待产品触发条件 |
| `Proposed` | 已形成可评审方案，等待批准 |
| `Approved` | 已批准，可拆分实施任务 |
| `Implemented` | 已完成并应迁移到正式文档/AIWarnings |
| `Rejected` | 已否决，仅保留决策原因 |

## 当前计划

| 计划 | 状态 | 当前结论 |
| --- | --- | --- |
| [ES Asset Runtime Generation](ASSET_RUNTIME_GENERATION.md) | `Reserved` | 当前继续使用预检保护重建；只有玩法中无停顿热插拔且失败必须续玩时才立项 |
| [ESFramework 职责收口](ESFRAMEWORK_RESPONSIBILITY_CONSOLIDATION.md) | `Reserved` | 先冻结权威边界，再按风险逐步收口；禁止为了目录整齐进行大规模搬迁 |

