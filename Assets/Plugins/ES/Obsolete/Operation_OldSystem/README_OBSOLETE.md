# Operation 旧系统归档

状态：历史归档，禁止作为生产实现依据。

最后核对：2026-08-01。

本目录保留 ES Framework 早期 Operation、RuntimeLogic、RuntimeTarget 和 Buffer Operation 实现，仅用于追溯历史设计。程序集 `ES_Operation_OldSystem_Obsolete` 默认不自动引用，并且只有显式定义 `ES_ENABLE_OBSOLETE_CODE` 时才参与编译。

## 当前生产替代

- 一次性行为使用 `ESOutputOp`，默认 `NeedsStop == false`。
- 持续资源由少数 `NeedsStop == true` 的 Op 持有明确运行时凭证，并通过 `ESOpSupport` 清理。
- 数值持续修改使用 ValueChange Token/Lease。
- Buff 持续效果使用 `ESActiveBuffRuntime`。
- 临时 Tag 使用 `ESTagLeaseSet`。

生产目录中的 `OutputOperationBuffer`、Buffer Float 空壳和 `ESOpSupport.storeForBuffer` 已于 2026-08-01 移除。扫描时没有发现具体生产派生类、调用者或序列化资产引用，因此不需要运行时或资产迁移。

## 禁止事项

- 禁止从本目录复制 `OutputOperationBuffer`、`IOpStoreKeyGroup` 或 Buffer Float 包装回生产代码。
- 禁止为了预留未来能力，在每个 `ESOpSupport` 中恢复未被业务消费的常驻容器。
- 如未来出现新的持续效果，先使用现有 Token、Lease、Handle 和 `NeedsStop` 所有权模型；只有得到独立性能与生命周期证据后才能提出新容器。

现行规则见 `Documentation/SKILL_OPERATION_LIFECYCLE.md` 和 `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md`。
