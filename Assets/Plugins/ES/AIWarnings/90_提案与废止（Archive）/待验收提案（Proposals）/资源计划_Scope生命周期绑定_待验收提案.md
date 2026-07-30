# ESResourcePlan × Scope 生命周期绑定：待验收提案

## 目标

验收 ResourcePlan 的应用持有能够归属到一个生命周期 Scope：

```text
生命周期 Scope  ──持有──> Plan retain ──持有──> Plan 内部资源 Scope ──持有──> RuntimeBackend 资产 / Bundle 引用
```

Scope 结束时只归还自身的 Plan retain；Plan 只有在全部 retain 都归还后才进入既有的延迟释放和安全点卸载流程。业务层不使用 Handle。

## 不改变的边界

- 资产与 Bundle 的唯一实际引用计数仍属于 RuntimeBackend；Plan / Scope 只管理生命周期持有。
- 资源不会在 `Dispose` 的当帧强制卸载。Plan 先按 `releaseDelaySeconds` 收尾，AB 在既有安全点统一卸载。
- `releaseOnExit = false` 的 Plan 仍是“离开目标后保持温热、显式释放”的语义；Binder 不会擅自把它改成随对象禁用释放。
- 配置寻址仍是当前 AssetTable 的 `ConfigKey -> AssetIdentity`；本提案不恢复 GUID 回退加载。

## 验收范围

| 编号 | 场景 | 操作 | 通过条件 |
| --- | --- | --- | --- |
| P1 | 单 Scope 正常结束 | 用内部生命周期 Scope 应用一个 `releaseOnExit=true` Plan，待 Ready 后 Dispose Scope | `RetainCount` 由 1 归零；Plan 最终为 `Released`；其 Plan 内部 Scope 释放；之后安全点可卸载零引用 AB。 |
| P2 | Plan 单独释放 | 同一 Scope 应用 Plan 后调用 `ReleaseAsync(plan, scope)`，Scope 保持存活 | 该 Scope 的 retain 归还并解除监听；稍后 Dispose Scope 不得再次扣减或报错。 |
| P3 | 同 Scope 重复应用 | 同一个 Scope 对同一 Plan 连续 Apply 两次 | Plan 只建一个内部资源 Scope；`RetainCount=2`；第一次单独 Release 后仍可用，第二次才进入释放。 |
| P4 | 多 Scope 重叠 | Scope A、B 分别 Apply 同一 Plan；先 Dispose A，再 Dispose B | A 结束后 Plan 必须仍为 Ready/持有；B 结束后才释放。不得因 A 误释放 B 的资源。 |
| P5 | Binder 零代码流程 | 给 GameObject 配置 `ESResourcePlanBinder` 和 `releaseOnExit=true` Plan，启用后禁用对象 | 启用自动准备；禁用自动归还；开发者不需调用 Release 或接触 Scope/Handle。 |
| P6 | 外部取消：直接 Apply | 使用可取消 Token 调用 `ApplyAsync`，在必需资源尚未完成时取消 | 调用方立即收到取消；本次 newly-acquired retain 被自动归还；无残留 Context/Plan 常驻。底层共享下载可自然收尾。 |
| P7 | 外部取消：直接准备 | 业务调用 `PrepareAsync` 时在必需资源完成前取消 | 本次 newly-acquired retain 自动归还；不影响同一 Plan 的其他使用者。 |
| P8 | 延迟释放、快速折返与安全点 | 将 `releaseDelaySeconds` 设为可观察值，结束最后一个 Scope，再在冷却内重新 Apply | 计数立即归零；冷却内重进复用原 Context、已加载资产和对象池预热，不重复预热；冷却真正结束后才归还对象池并可在安全点卸载 AB。 |
| P9 | Provider 重初始化 | ActiveLink Plan 与启用中的 Binder 均处于活跃状态时触发 Provider 重建 | 旧 Scope/Plan 先停止和收尾；无旧 Provider 回调写入新表；重建后恢复 ActiveLink 与 Binder 持有。 |
| P10 | 手动常驻计划 | Binder 配置 `releaseOnExit=false`，启用再禁用 | 禁用后 Plan 仍保持；仅显式 `ReleaseAsync(plan)` 才归还。这验证没有改变原有产品语义。 |

## 必须记录的证据

- 每个场景记录 Plan 名称、每步 `State`、`RetainCount`、`RequiredFailureCount`。
- P1-P4、P6-P7 记录 Provider 的资产/Bundle 引用计数，确认不会出现负数或残留。
- P6-P9 记录 Unity Console，必须没有 `ObjectDisposedException`、重复释放、"仍有资源请求进行中" 或未观察任务异常。
- P8-P9 使用 Profiler 记录安全点前后耗时和 GC；不得把卸载操作带入 Combat 等非安全阶段。

## 当前可接受的结果

- 可选资源下载在取消后继续由共享底层请求收尾是允许的；验收点是 Plan retain 已归还、Provider 重建/安全点会等待在途请求结束。
- Plan 进入 Required 资源失败时，报告为 `Failed` 是正确结果；验收应检查没有遗留持有，而不是要求失败被伪装为成功。

## 本轮不宣称已验收

- Unity Player（特别是 IL2CPP 真机）中的 P6-P9 压测尚需实际运行。
- Level / Map / GameMode / Region 直接引用 ResourcePlan，或由统一管理器通过 ActiveLinkList 管理；不再维护 TargetKind + InfoKey 索引。
- GameCore 展开快照在编辑器改动后、未 Bake 直接 Play 的过期阻断属于后续验收项。

## 最终准入标准

P1-P10 全部通过，且至少在 Editor PlayMode 与一个 IL2CPP Player 中各执行 P6、P7、P9 一次。任一场景出现 retain 残留、重复扣减、旧 Provider 回写或安全点并发异常，则本功能不得标记为正式验收通过。

## 生命周期商业级收口范围

本阶段只把 ResourcePlan / Scope 生命周期收口为框架标准，不再新增另一套加载器、业务 Handle 或弱类型资源入口。

### 1. 生命周期契约（必须稳定）

- 一个生命周期 Owner 最多对同一 Plan 持有一份 Binder retain；重复启用、重复 `ApplyAsync` 必须幂等。
- 一个 Scope 可对同一 Plan 持有多份显式 retain；每一份都有一次且只有一次归还。
- 最后一个 retain 归零后进入 `ReleasePending`；冷却内重新进入复用已完成 Context，真正释放后才允许新 Context。
- 外部取消只回滚本次刚获得的 retain；不得影响共享 Plan 的既有 Owner。
- Provider 重建只恢复 ActiveLinkList 与 Binder 的有效持有，绝不复用旧 Provider 的 Scope 或资产身份。
- `releaseOnExit` 只描述自动退出策略；`releaseDelaySeconds` 是所有最后持有归零后的冷却策略。

### 2. 标准生命周期入口（必须完成）

- Level、Map、GameMode、Region、Encounter 直接持有 ResourcePlan；普通代码使用 `PrepareAsync / ReleaseAsync`，统一切换使用 ActiveLinkList。
- `ESResourcePlanBinder` 是 GameObject 生命周期的零代码入口。
- 直接 `ApplyAsync / ReleaseAsync` 保留给少量高级流程，但必须保持取消回滚、幂等与诊断可见。
- 下一步接入各领域的真实进入/退出事件；不得用 `SceneManager.sceneLoaded` 猜测关卡身份。

### 3. 运行时恢复与失败语义（必须完成）

- Provider 重建后恢复 ActiveLinkList 和启用中的 Binder；恢复失败必须清理 Binder 本地持有状态并允许后续重试。
- 全量安全点是显式清空语义，不自动伪装为原生命周期恢复；调用方必须明确决定是否重新进入阶段。
- Optional 资源失败只记录报告；Required 失败必须显式报告且不得遗留 retain。

### 4. 可观测性与防回归（必须完成）

- 运行时监视器必须显示 State、RetainCount、Scope retain 总数、Scope Owner 数、Unowned、Releasing、内部 Scope 状态和 Provider 在途请求。
- P8 拆为：P8-A 已 Ready 冷却内重进复用；P8-B 加载中离开再进入安全收尾。
- P6/P7 使用真实慢加载资源执行取消；P9 使用真实 Provider 重建；禁止用 Mock 结果代替。
- Editor PlayMode、IL2CPP Player 各留存一份诊断报告和 Profiler 截图/采样。

### 明确不属于本收口

- 新资源类型、统一弱类型资源条目、第二套对象池或第二套引用计数。
- 自动扫描所有普通 Scene 并推断 Level/Map/Mode。
- 以 GUID、KeyName 或 RuntimeKey 作为 Plan 的运行时加载后门。
