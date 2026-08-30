# 游戏核心循环验证矩阵

| 层级 | 必查对象 | 通过证据 | 缺失结论 |
|---|---|---|---|
| structure | 权威入口、状态/命令、场景接线、清理契约 | 源码/配置/合同哈希 | `unverifiable`（仅结构可见） |
| implementation | 编译、域重载、EditMode、状态转移、失败恢复 | 当前 Unity/Test Runner 回执 | `runtime-not-run` 或 `blocked` |
| presentation | 输入响应、相机/移动、动画、反馈、重置 | 当前 PlayMode 观察回执 | 不得升级为可玩 |
| performance | CPU、GC、内存、延迟、并发、加载、Player | Profiler/Player/IL2CPP 回执 | 仅预算设计，不得宣称达标 |

每行字段：`taskId, layer, object, owner, entryPoint, precondition, expected, observed, status, evidenceRef, sourceHash, environmentHash, runtimeStatus, claimsNotProven`。

ABCD 证据必须保留 `runId`、阶段顺序、分支父子关系、权重历史、拒绝原因、预算使用和 `finalDecision`；ABCC 交换必须保留 A/B 字段映射、lossPolicy、normalizedResult、evidenceSetRef、receiptRef。 
