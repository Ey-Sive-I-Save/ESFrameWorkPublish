# P0：PrimeTween / DOTween 迁移边界

`StableId`: `es.aiwarning.p0.primetween-dotween-migration.v1`
`Status`: `current`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `primetween`, `dotween`, `tween-lifecycle`, `runtime-performance`
`Applicability`: 所有生产 Tween、UI、镜头、表现过渡、对象池与依赖清理。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-primetween-dotween-migration.md`; 相关静态扫描、测试与 IL2CPP 回执。
`StaleWhen`: PrimeTween/DOTween 版本、调用约束、对象池生命周期、依赖配置或本条目 SourceRefs 变化。

## 长期 P0 约束

- 新增生产代码默认使用 PrimeTween `1.4.11`；不得新增 `DG.Tweening`、DOTween 扩展或兼容适配器。
- Tween 只服务表现/UI/镜头/非权威过渡，禁止驱动 KCC 根位移、网络状态、战斗判定等权威事实。
- 对象池回收、Owner 销毁、Disable、状态切换必须停止或失效 Tween，禁止旧回调写入下一租用者。
- 禁止每帧创建捕获闭包的 Tween/Sequence/`OnUpdate`；不得把冷路径 async/Coroutine/调试快照宣称为 0 GC 热路径。
- API、回调顺序、Ease、循环、时间缩放、UpdateType 和不可复用语义必须逐项确认，不能机械替换 using。
- 只有生产引用清零并完成 ES_Logic/ES_Editor、相关测试和目标 IL2CPP 构建后，才能清理 DOTween 依赖；旧包和 Obsolete 内容不得提前删除。

详细迁移矩阵、生命周期/性能规则、原文快照和伪完成禁令见 Knowledge：`es.aiwarning.p0.primetween-dotween-migration.v1`。
