# ExpressionSources

ExpressionSource 是“直接值 + 是否使用直接值 + 动态表达式”的统一包装。

- 默认直接读取原生字段，适合伤害、倍率、概率、对象引用等高频配置。
- 只有需要 RuntimeTarget/上下文动态计算时，才关闭直接值并启用表达式。
- Inspector 排布保持统一：左侧模式开关，右侧直接值或表达式，避免字段堆叠。
