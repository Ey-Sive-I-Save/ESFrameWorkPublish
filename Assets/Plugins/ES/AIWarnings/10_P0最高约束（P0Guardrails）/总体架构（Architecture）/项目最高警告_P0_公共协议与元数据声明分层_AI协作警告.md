# P0：公共协议、领域接口与 Attribute 元数据必须分层

> 级别：P0。适用于新增、迁移或评审 public interface、跨系统协议、Attribute、Drawer 共用契约及其程序集归属。

## 最高结论

新增接口前必须先判断其权威层级。不得因为调用方位于 Editor、Drawer 或某个 Attribute 文件附近，就把跨系统协议就近定义在那里。

## 固定归属

| 类型 | 权威目录 |
|---|---|
| 跨系统、跨领域稳定协议 | `Assets/Plugins/ES/0_Stand/BaseDefine_Law` |
| 字段 Attribute、绘制模式、纯声明元数据 | `Assets/Plugins/ES/0_Stand/Attributes` |
| 单一领域运行时接口 | 对应 `Runtime/<Domain>` |
| 仅编辑器内部使用的扩展点 | 对应 `Editor/<Domain>` |

## `0_Stand/BaseDefine_Law` 准入条件

协议至少满足以下一项：

1. 被两个以上领域或程序集共同使用；
2. 表达稳定、通用的数据或行为契约；
3. 不依赖具体 Profile、Drawer、窗口或业务模块；
4. 需要作为 Runtime 与 Editor 共同理解的唯一权威。

每个公共协议原则上使用独立脚本：

```text
INTER_<InterfaceName>.cs
```

禁止把公共协议随手追加到 Attribute、Drawer、工具类或某个业务类型文件中。

## `BaseDefine_Law` 禁止事项

- 禁止引用 `UnityEditor`。
- 禁止依赖 Odin/Sirenix 绘制实现。
- 禁止包含 `EditorPrefs`、`SessionState` 或窗口状态。
- 禁止包含具体业务运行服务。
- 禁止把该目录变成无法分类接口的收纳箱。

## Attribute 边界

`Attributes` 目录只允许：

- Attribute 类型；
- 与 Attribute 直接绑定的枚举；
- 无实例状态、无运行分派职责的声明元数据。

禁止在 Attributes 文件中定义跨系统运行时协议，仅仅因为某个 Drawer 当前使用了该协议。

## 判断顺序

新增类型时必须依次判断：

1. 它是协议、元数据还是实现？
2. 谁是长期权威？
3. Runtime 与 Editor 是否共同依赖？
4. 去掉当前 Drawer 或业务模块后，该协议是否仍然成立？
5. 最终才决定文件位置。

禁止先选择“修改文件最少”的位置，再倒推架构理由。

## 迁移规则

- 整个协议脚本移动时必须连同原 `.meta` 一起移动并保留 GUID。
- 从混合职责脚本提取协议时，原脚本保留原 `.meta`，新协议脚本创建独立 `.meta`；禁止把原文件 GUID 偷换给新文件而破坏原脚本资产身份。
- 旧位置不得保留兼容别名、转发接口或重复定义。
- 使用 `rg` 确认全项目只有一个权威定义。
- 验证 Stand、Runtime 调用方与 Editor 调用方的程序集依赖方向。

## 验收门禁

- 公共协议只有一个定义。
- Attribute 文件中没有越权公共协议。
- Runtime 不引用 Editor。
- `BaseDefine_Law` 不引用具体业务或绘制实现。
- `.meta`、严格 UTF-8、U+FFFD 和差异检查通过。
- Stand、相关 Runtime、Editor 定向编译通过；若 Unity 生成工程未刷新或被无关错误阻断，必须按实际证据降级报告。

违反本规则会让协议权威依赖当前 UI 或业务实现，形成程序集倒置、重复定义和长期迁移成本，按 P0 架构问题处理。
