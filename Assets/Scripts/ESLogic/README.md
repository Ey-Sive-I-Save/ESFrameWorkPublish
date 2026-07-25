# ESLogic 目录放置规则

`ESLogic` 是当前项目的运行实现层。新增代码前先按职责选择位置，不要因为目录名相近而重复造系统。

| 新内容 | 放置位置 |
| --- | --- |
| 通用底层协议 | `Assets/Plugins/ES/0_Stand` |
| 可跨项目复用的设计能力 | `Assets/Plugins/ES/1_Design` |
| 当前项目的运行玩法与服务 | `Assets/Scripts/ESLogic/Runtime` |
| 开发者常用薄封装、组件与模板 | `Assets/Scripts/ESLogic/Runtime/Developer` |
| 可运行示例 | `Assets/Plugins/ES/3_Examples` 或 `Assets/Scripts/ESLogic/Samples` |
| 测试或历史验证材料 | `Assets/Scripts/ESLogic/Tests~` |
| 已废弃实现 | `Assets/Plugins/ES/Obsolete`，默认不参与编译 |

## Runtime 根目录

- `Entity`：生命体与角色身体运行主链。
- `Item`：非生命体世界逻辑体，例如飞行物、掉落物、机关。
- `GameManager`：游戏级系统、流程和世界模块。
- `State`：状态机、动画、IK 与状态数据。
- `Skill`、`Operation`、`Command`：技能时序、可编排行为和命令入口。
- `Developer`：面向开发者的轻封装、示例、文档和场景模板；模板不等于完整运行时系统。

## 放置检查

1. 若类型要成为所有项目共享的低层协议，先考虑 `0_Stand`。
2. 若类型是可复用设计能力但不拥有当前项目业务，考虑 `1_Design`。
3. 若类型直接服务当前游戏玩法，放入对应 `Runtime` 子系统；不要新建空壳顶层目录。
4. 未稳定、仅用于演示或验证的内容，不得伪装成正式 Runtime 能力。

