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
