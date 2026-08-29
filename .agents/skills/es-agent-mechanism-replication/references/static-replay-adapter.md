参照 `.agents/skills/es-static-deep-replay/references/static-replay-contract.md` 和本 Skill 的 `static-replay.manifest.json`。StaticDeepReplay 必须先于 Runtime 升级。

通用回放案例：

- `normal-input`：六类机制、RouteStage、Knowledge、Probe、别名和闭环谓词存在。
- `invalid-input`：未知机制、缺少 GoalRevision、过期 SourceRef 或 malformed Evidence 被拒绝。
- `denied-expansion`：未授权写入、网络、Unity、宿主进程和 alternate handoff 被拒绝。
- `repeat-idempotency`：同一快照重复执行不会重复注册或改变稳定排序。
- `hash-change-cache-invalidation`：源/正文/路由/读取哈希变化使旧计划、缓存和 Receipt 失效。
- `interruption-recovery`：从已接受 Transcript/Context 和私有快照恢复，不替换 handoff 来源。
- `deterministic-output`：相同有界输入产生相同映射、finding 和决定。

Responsibility profile: governance

专用检查：`authority-routing`、`permission-boundary`、`deterministic-replay`、`evidence-contract`、`knowledge-boundary`、`lifecycle-boundary`、`change-boundary`、`external-data-boundary`、`operation-allowlist`。
