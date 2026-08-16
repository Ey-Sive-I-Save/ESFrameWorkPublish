# AIWarnings 当前状态

> 状态：现行导航 / 活跃索引。
> 最后核对：2026-08-16。
> 权威边界：本文件只导航当前规则、模块状态卡和证据入口；它不替代源码、工作树、Unity、测试、Player 或发布验证。

## 使用方式

1. 先读本文件，再按 `规则索引（RuleIndex）.md` 命中当前任务的 P0 与领域规则。
2. 只在索引或任务明确指向时读取 `80_交接与复盘（Handover）` 的历史快照。
3. 每次实现、审查或验收前仍必须检查当前 branch、HEAD、工作树和源码；本索引不提供持续有效的运行许可。

## 状态卡格式

活跃模块或跨系统工作应有唯一状态卡，至少包含：

| 字段 | 要求 |
|---|---|
| `ModuleMaturity` | `Proposed` 至 `Archived` 的模块演进状态。 |
| `EvidenceLevel` | S0-S6 证据层级，并写清平台、入口和范围。 |
| `DeliveryVerdict` | `Designed / Implemented-Unverified / Accepted / Released`。 |
| `Blocked` / `Failed` | 可选附加结论，必须指向可重读证据。 |
| `lastVerifiedHead` | 对应 Git 基线；HEAD 或相关范围变化后即视为 stale。 |
| `evidenceRef` | 日志、XML、Job receipt、场景、构建产物或源码入口。 |

## 当前路由状态

| Route / 范围 | 状态 | 入口 |
|---|---|---|
| `ui-icon-atlas` | `current`：现行 SpriteAtlas 与运行时动态图集分流规则。 | `30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md` |
| `runtime-ui-window` | `reserved`：只预留 Runtime UI Window 的规则读取边界，不代表实现、API、AICommand 或默认启用。 | `规则索引（RuleIndex）.md` 的“预留路由”与 `AIWarningsRouteCatalog.json` |

## 证据写入边界

- 不把编译失败详情、Console 文本、错误码或 Warning 数量写入本文件。
- 只记录证据等级和可重读证据入口；需要诊断时读取本次构建日志、Test Runner XML、Profiler 数据或用户交付报告。
- 历史快照不覆盖当前事实。当前状态需要重新确认时，优先检查源码、HEAD、工作树和目标验证产物。
