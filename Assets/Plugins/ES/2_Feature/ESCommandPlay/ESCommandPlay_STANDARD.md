# ESCommandPlay 标准

## 定位

`ESCommandPlay` 是 `ESCommand` 的可选增强层。

基础命令仍然只负责：

```text
ESCommand.Invoke()
ESCommandEvent.Invoke()
```

播放层负责：

```text
顺序执行
延时
取消
未来扩展并行、循环、等待条件
```

## 默认原则

1. 简单 UI 按钮不需要 `ESCommandPlayer`。
2. 只有需要跨帧播放时才挂 `ESCommandPlayer`。
3. 普通命令不实现播放接口，会在播放序列里立即执行。
4. 需要跨帧的命令实现 `IESCommandPlayable`。
5. 播放帧数据命名为 `ESCommandPlayFrame`，不要叫 Context。

## 当前能力

```text
ESCommandPlayer.Play(event)
ESCommandPlayer.Cancel()
ESCommandPlayer.Stop()
ESCommand_Delay
```

## 扩展分类

后续命令建议放在：

```text
Assets/Plugins/ES/2_Feature/ESCommandPlay/
```

命名：

```text
ESCommand_Delay
ESCommand_Parallel
ESCommand_Loop
ESCommand_WaitUntil
```

## 性能约束

运行时播放路径：

```text
Update -> Tick -> while 顺序推进 -> virtual/interface call
```

不要在 `TickPlay` 内使用：

```text
LINQ
反射
new 临时对象
字符串查找
```
