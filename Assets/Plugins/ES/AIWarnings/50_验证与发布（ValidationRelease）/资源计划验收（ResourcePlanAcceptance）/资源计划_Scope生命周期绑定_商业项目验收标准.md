# ESResourcePlan × Scope：商业项目验收标准

本标准用于判断 ResourcePlan 生命周期绑定是否可以进入真实商业项目，而不是只判断“示例场景能不能加载”。

## 一、准入结论等级

| 等级 | 含义 | 发布许可 |
| --- | --- | --- |
| L0 代码可编译 | 程序集通过编译，静态检查无错误 | 不允许对外宣称可用 |
| L1 Editor PlayMode | P1-P10 在 Editor PlayMode 可重复通过 | 允许内部联调 |
| L2 Windows/IL2CPP | 至少一个 IL2CPP Player 通过取消、切换、重建 | 允许测试服 |
| L3 商业准入 | 多平台、压力、长时间运行、升级回归均通过 | 允许生产使用 |

任何 Blocker、retain 泄漏、重复扣减、旧 Provider 回写或安全点竞态，均不得进入 L3。

资源发布还必须经过独立的第五步“发布到远端”。第四步只生成并校验本地 Release 与上传计划；第五步才允许访问 OSS、S3 或其他远端 Provider。手动上传计划不能伪报远端发布成功，Root Manifest 必须在所有版本化叶子文件通过大小、SHA-256 与缓存头校验后最后上传。

首次配置远端时，必须先使用第五步的隔离验证：Provider 在独立 `validationPrefix` 写入一次探针对象，完成远端 HEAD 校验后清理；隔离验证失败时禁止进入正式上传。

## 二、不可改变的架构边界

1. RuntimeBackend 是资产和 Bundle 的唯一实际引用计数者。
2. Plan 只持有一个内部资源 Scope；每个外部生命周期 Scope 只拥有 Plan retain。
3. Scope Dispose 只归还自己注册的 Plan retain，不得释放其他 Scope 的 retain。
4. `Dispose` 不得在当帧强制卸载 Bundle；必须经过 releaseDelay 和安全点。
5. `releaseOnExit=false` 只允许显式 Release 归还，不得被 Binder 的禁用事件改写。
6. 运行时寻址必须继续使用 AssetTable 的 ConfigKey → AssetIdentity，不得恢复 GUID 回退加载。
7. Provider 重建必须阻止旧请求进入新表，旧 Scope 必须先收尾。

## 三、功能验收矩阵

| 编号 | 验收项 | 必须观察 | Blocker 条件 |
| --- | --- | --- | --- |
| P1 | 单 Scope 结束 | Retain 1→0、Plan Released、内部 Scope 释放 | 资源仍被 Plan 持有 |
| P2 | 显式 Release 后 Scope 结束 | 不重复扣减、不报错 | 第二次释放改变计数 |
| P3 | 同 Scope 重复 Apply | Retain 1→2→1→0 | 重复创建内部 Scope 或计数不平衡 |
| P4 | 多 Scope 重叠 | A 结束后仍 Retain=1，B 结束后才释放 | A 误释放 B |
| P5 | Binder 自动流程 | Enable Apply、Disable Release | 需要业务手写 Handle/Release |
| P6 | 直接 Apply 取消 | 本次 retain 自动归还 | 取消后 Plan 常驻或误扣他人 |
| P7 | Prepare 取消 | 本次 Plan 持有自动回滚，不影响其他使用者 | 取消后残留持有或误释放共享资源 |
| P8 | 延迟与安全点 | 计数先归零，延迟后安全点卸载 | Dispose 当帧同步卸载 |
| P9 | Provider 重建 | 旧 Context/Scope 收尾，新表可重新 Apply | 旧回调写入新 Provider |
| P10 | 手动常驻 | Disable 保持，显式 Release 才归还 | Disable 擅自释放或重复累加 |

## 三-A、TemporaryScope / LoadAsyncLease 验收矩阵

ResourcePlan 的 Scope retain 与全局 `ESAssetTemporaryScope` 的逐调用持有是两套语义，必须分别验收，不能用 Plan 的 P1-P10 替代：

| 编号 | 场景 | 必须观察 | Blocker 条件 |
| --- | --- | --- | --- |
| T1 | 两个 `LoadAsyncLease` 交错完成、交错 Dispose | 两个独立 Token 各归还一次，任一释放不影响另一租期 | 以 AssetIdentity 幂等扣减或提前释放 |
| T2 | Lease 值复制后重复 Dispose | 复制品共享同一 Token，底层只扣一次 | 重复 Dispose 继续减少 `LeaseCount` |
| T3 | 等待取消后迟到完成并释放 | 取消只结束等待者；成功取得的 Lease/引用仍按自身语义归还 | 迟到回调写入新 Scope 或永久悬挂 |
| T4 | TemporaryScope 安全点后释放旧 Lease | generation 推进，旧 Token 释放失败且不能影响新一代 | 旧 Lease 释放新一代资产 |
| T5 | Provider 切换后释放旧 Lease | 旧 Scope/Token 只对旧代生效，Provider 重建后可重新加载 | 旧 Lease 触碰新 Provider |
| T6 | 普通 `LoadAsync(scope)` 与严格 Lease 混用 | `ReferenceCount`、`LeaseCount` 分别归零后才释放底层资源 | 一种入口误扣另一种计数 |
| T7 | 加载失败后重试 | 失败移除临时状态，下一次同身份加载可重新开始 | 失败状态卡死或残留计数 |

每个用例都必须记录：Scope 代际、资产身份、`ReferenceCount`、`LeaseCount`、Token 是否仍有效、Provider 代际和最终底层释放次数。未完成这些证据前，不得宣称 Temporary Lease 生命周期已商业验收。

## 四、每个用例必须记录的证据

每个步骤至少记录：

```text
Timestamp
ProviderId / ProviderVersion
PlanName
ScopeId（仅诊断，不暴露给业务）
State
RetainCount
ScopeOwners
UnownedRetain
InternalScopeDisposed
RequiredFailureCount
OptionalPendingCount
```

当前监视器的诊断入口：

```text
【ES】/运行时诊断/资源系统/资源运行时监视器
```

## 五、并发和取消标准

必须覆盖：

- Apply 尚未 Ready 时取消。
- 多个 Owner 同时 Apply 同一 Plan。
- 一个 Owner 取消，其他 Owner 继续使用。
- 多个 Plan 同时 Release，其中一个等待被取消。
- Release 与 Scope Dispose 同帧交错。
- Provider 重建与 Apply/Release 同时发生。
- 同一 Plan 旧 Provider 释放期间，新 Provider 再次 Apply。

判定要求：

- 取消调用方可以及时返回。
- 底层共享下载可以自然收尾。
- 所有权计数必须最终归零或回到取消前值。
- 任何异步回调不得修改新 Provider 的表或资源状态。

## 六、性能标准

### Editor PlayMode

- 100 个 Plan 连续进入/离开 100 次，无 retain 漏洞。
- 1000 个资源条目的 Plan 切换无主线程长卡顿。
- 取消操作从发起到调用方返回不超过一帧级别的可接受延迟。
- releaseDelay 不创建持续增长的 UniTask、Timer 或 Context。

### Player / IL2CPP

- Windows IL2CPP 至少一轮 P6、P7、P9。
- 目标平台至少再选一个移动或主机平台验证。
- Development 和 Release 两种构建均验证。
- 开启代码裁剪后，ResourcePlan、Scope、Provider 类型均未被错误裁剪。

## 七、长时间和压力标准

至少执行：

```text
连续关卡切换       ≥ 500 次
同 Plan 重叠 Scope  ≥ 32 个
取消/重进循环       ≥ 1000 次
Provider 重建       ≥ 20 次
运行时间            ≥ 2 小时
```

结束时必须满足：

- Active Plan = 0（常驻计划除外）。
- Releasing Plan = 0。
- Live Scope 回到基线。
- Provider Pending = 0。
- AssetTable 没有旧 Provider 身份。
- Bundle 只剩真实缓存或常驻引用。

## 八、兼容性标准

- 旧的 `plan.ApplyAsync()` / `plan.ReleaseAsync()` API 继续可用。
- 业务代码不需要接触 `ESAssetScope`、Handle 或内部计数。
- Binder 的 Inspector 配置不需要迁移脚本即可运行。
- `releaseOnExit=false` 的历史 Plan 语义保持不变。
- Provider 重建后旧版本 Catalog 不得混入新版本 AssetTable。

## 九、当前实现评估

当前代码状态：

- L0：通过。
- P1-P5、P8、P10：已有监视器自动验收入口。
- P6/P7/P9：代码路径已具备回滚和 Provider 边界，但仍需慢加载、真实取消和 IL2CPP 实跑证据。
- L1：待在目标 PlayMode 场景执行并保存报告。
- L2/L3：尚未宣称通过。

因此当前结论是：

> 可进入内部联调和验收阶段，不可宣称已经达到商业生产准入。

## 十、生产准入签字条件

只有以下条件全部满足，才能标记为“商业可用”：

1. P1-P10 全部有可复现日志。
2. Editor PlayMode 和至少一个 IL2CPP Player 均通过 P6、P7、P9。
3. 连续切换和压力测试无 retain、Scope、Provider 泄漏。
4. 至少两个目标平台完成安全点和取消测试。
5. 失败报告可定位到 Plan、Owner、Provider 和步骤。
6. 版本升级后旧 Plan、Binder 和 Catalog 回归通过。
