# State 目录说明

`Runtime/State` 是状态系统主目录，负责状态机、状态基类、层运行时，以及由状态驱动的动画、IK、参数和状态数据。

## 当前分类

```text
Core        状态机核心、状态基类、层运行时、基础结果类型
Animation   动画混合、Playable 计算、动画配置与运行时
IK          状态驱动 IK、MatchTarget、FinalIK 适配
Parameter   状态参数定义、默认值和参数链接
Data        状态配置、枚举、合并消耗、共享数据和变量数据
Examples    示例
Docs        状态系统说明
```

## 放置规则

- 不再使用 `ValyeTypeSupport` 这种历史阶段目录。
- 不再把动画计算器放到 `AnimationCalculators` 顶层目录。
- 不再把状态参数放到 `ParameterSystem` 顶层目录。
- `Animation`、`IK`、`Parameter` 仍属于 State 内部能力，不拆成 `Runtime/Animation` 或 `Runtime/IK` 顶层系统。
- `Skill` 只负责技能序列和技能时间线，不承接状态机核心。
- `Buff` 先留在 Entity Domain 和 Operation 入口，等 Buff 系统完整后再独立。
