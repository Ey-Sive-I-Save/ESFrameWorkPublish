# ESCommand 标准

## 定位

`ESCommand` 是轻量可序列化命令，用来取代一部分 UnityEvent 场景。

适合：

- UI 按钮触发框架动作
- 简单对象行为调用
- 配置化同步命令列表
- 后续接入 Link 或 Feature 播放器

不负责：

- 任意方法反射调用
- 任意参数列表
- 延时、等待、并行、循环
- 高频消息总线

## 核心规则

1. 命令本身可以多态序列化。
2. 参数必须是命令类自己的强类型成员字段。
3. 不使用 `object[]`、`object context`、多态参数树。
4. 简单命令只实现 `Invoke()`。
5. 运行时默认路径是 `for` 循环加虚调用，不做反射。
6. 延时、顺序、并行播放以后放到 `Feature/ESCommandPlay`，不污染基础命令。

## 文件分类

底层协议：

```text
Assets/Plugins/ES/0_Stand/BaseDefine_Law/
    ENUM_ESRunState.cs
```

基础命令：

```text
Assets/Plugins/ES/0_Stand/BaseDefine_Command/
    ABSTRACT_ESCommand.cs
    STATIC_ESCommandCategory.cs
    ESCommand_STANDARD.md
```

具体命令：

```text
Assets/Plugins/ES/1_Design/<模块名>/Command/
Assets/Plugins/ES/2_Feature/<功能名>/Command/
```

## 分类常量

命令菜单路径必须优先使用 `ESCommandCategory` 和 `ESCommandTypeName`。

常见分类：

```text
命令/通用
命令/输入
命令/UI
命令/运行模式
命令/场景
命令/对象
命令/动画
命令/音频
命令/Link
命令/调试
命令/播放
```

示例：

```csharp
[TypeRegistryItem(ESCommandTypeName.InputSetVirtualButton)]
public sealed class ESCommand_Input_SetVirtualButton : ESCommand
{
}
```

## 命名格式

基础类型：

```text
ESCommand
ESCommandEvent
```

具体命令：

```text
ESCommand_<模块>_<动作>
```

示例：

```text
ESCommand_Input_SetVirtualButton
ESCommand_RuntimeMode_PushMode
ESCommand_UI_OpenPanel
```

## 字段格式

字段使用中文 `LabelText`。

推荐：

```csharp
[Serializable]
public sealed class ESCommand_Input_SetVirtualButton : ESCommand
{
    [LabelText("输入动作")]
    public ESInputActionId actionId;

    [LabelText("是否按住")]
    public bool held;

    public override void Invoke()
    {
        // Direct typed call.
    }
}
```

不推荐：

```csharp
public object[] args;
public string methodName;
public object owner;
```

## 运行结果

`ESRunState` 是底层通用运行协议，不属于 Command 专用协议。

当前简单命令默认：

- 禁用命令返回 `Skipped`
- 正常执行返回 `Succeeded`
- 具体命令如果需要失败语义，后续可以扩展专门执行入口

## 扩展路线

第一阶段：

```text
ESCommand.Invoke()
ESCommandEvent.Invoke()
```

第二阶段：

```text
Feature/ESCommandPlay
    ESCommandPlayer
    ESCommandSequence
    ESCommandDelay
```

播放层的帧数据命名使用 `ESCommandPlayFrame`，不要使用 `Context`。
