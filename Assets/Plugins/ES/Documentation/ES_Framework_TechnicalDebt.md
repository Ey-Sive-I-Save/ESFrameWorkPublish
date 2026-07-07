# ES Framework Technical Debt

本文档只记录已结合当前代码核验过的问题。历史 AI 分析文档已归档到 `Assets/Plugins/ES/Obsolete/AIAnlyze`，其中未验证的性能数字、评分、口号和 AIPreview 旧引用不再作为重构依据。

## P0 - 优先处理

### 1. Link 派发链路存在高频 UnityObject 判空

位置：

- `Assets/Plugins/ES/1_Design/Link/Pool_Container/LinkReceivePool.cs`
- `Assets/Plugins/ES/1_Design/Link/Pool_Container/LinkReceiveList.cs`
- `Assets/Plugins/ES/1_Design/Link/Pool_Container/LinkReceiveChannelPool.cs`
- `Assets/Plugins/ES/1_Design/Link/Pool_Container/LinkReceiveChannelList.cs`

现状：

- `SendLink` 遍历接收者时会判断 `receiver is UnityEngine.Object`，并执行 Unity null 检查。
- 该逻辑能自动清理已销毁的 Unity 对象，行为上有价值。
- 风险主要出现在高频消息、大量接收者、每帧多次派发的场景。

建议：

- 保留自动清理能力，但不要在每次派发中承担全部清理成本。
- 增加延迟清理或分帧清理策略。
- 为 Link 派发增加轻量统计入口，用实际数据判断是否需要进一步优化。

### 2. Runtime / Editor 边界仍不够干净

位置：

- `Assets/Plugins/ES/0_Stand/_Res/Master/Shared`
- `Assets/Plugins/ES/0_Stand/_Res/Master/_Editor`

现状：

- 部分 Shared 文件内仍出现 `#if UNITY_EDITOR` 和 `UnityEditor` 条件引用。
- 这不等同于当前必然会造成 IL2CPP 构建失败，但会增加引用边界和维护成本。

建议：

- Runtime 文件只保留运行时 API。
- Editor-only 方法移动到 `_Editor` 或正式 Editor 程序集内。
- Shared 目录只放纯数据结构、路径规则、平台无关工具。

## P1 - 近期整理

### 3. SafeNormalList / SafeKeyGroup 的 ApplyBuffers 语义需要统一封装

位置：

- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/Container/ListPro/SafeList.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/Container/KeyGroupPro/SafeKeyGroup.cs`
- Link 相关容器调用点

现状：

- 容器通过缓冲队列避免遍历期间直接修改集合。
- 多处调用方需要知道何时调用 `ApplyBuffers`。
- Link 容器内部已经在派发前调用，但公共容器 API 仍容易被误用。

建议：

- 为常见遍历场景提供统一入口，例如 `ForEachApplied` 或快照式枚举。
- 明确区分“立即可见”和“缓冲后可见”的 API 命名。
- 示例代码统一展示推荐用法。

### 4. Res 加载调度需要补齐策略层

位置：

- `Assets/Plugins/ES/0_Stand/_Res/Master/ESResMaster.cs`
- `Assets/Plugins/ES/0_Stand/_Res/Master/Runtime`

现状：

- 当前加载任务不是纯串行，`ESResMaster` 已支持最多 8 个协程并发。
- 仍缺少清晰的优先级、取消、超时和日志等级控制。
- 加载流程中 `Debug.Log` 较多，运行时噪音和性能影响需要收敛。

建议：

- 增加任务优先级与取消标记。
- 对网络和磁盘加载设置超时策略。
- 将调试日志接入统一开关，默认关闭高频日志。

### 5. 对象池策略需要统一配置和统计反馈

位置：

- `Assets/Plugins/ES/0_Stand/Stand_Tools/SimpleTools/Pool/3_ESSimplePool.cs`
- Res、Link、Callback 等池化使用点

现状：

- `ESSimplePool` 支持最大容量，也支持通过 `maxCount <= 0` 关闭上限。
- 问题不在于固定容量本身，而是不同池的容量策略缺少统一依据。

建议：

- 给核心池暴露统计数据：创建次数、命中次数、丢弃次数、当前活跃数。
- 对高频池设置明确容量配置。
- 避免在没有统计数据前做大规模池化重构。

## P2 - 可持续维护

### 6. Editor 工具仍有菜单、自动注册和日志噪音

位置：

- `Assets/Plugins/ES/Editor`

现状：

- 多个 Editor 工具使用 `InitializeOnLoad`、`EditorApplication.update`、菜单项或全局 GUI 回调。
- 部分工具存在临时日志、调试输出和自动打开行为。

建议：

- 对全局自动注册工具逐个评估必要性。
- 低价值或低使用频率工具移动到 `Obsolete` 或加条件编译。
- 日志统一通过开关控制。

### 7. 历史文档与原型引用需要清理

位置：

- `Assets/Plugins/ES/Obsolete/AIAnlyze`
- `Assets/Plugins/ES/Obsolete/AIPreview`

现状：

- AIAnlyze 中大量内容引用 AIPreview 原型。
- AIPreview 功能本体已归档到 Obsolete，默认不再参与编译。

建议：

- 不再从 AIAnlyze 直接派生开发任务。
- 后续重构任务只从本文档或重新核验后的代码审查中产生。
- Obsolete 内容只作为历史参考，不作为用户入口。
