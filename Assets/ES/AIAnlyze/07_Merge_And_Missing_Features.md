# 可合并的相似功能与缺失的使用功能分析

## 1. 功能相近、可考虑合并/抽象的模块

- **多种 Link 容器实现**  
  - LinkReceiveList / LinkFlagReceiveList / LinkReceiveChannelList / LinkReceiveChannelPool / LinkReceivePool：
    - 共同点：都基于 SafeNormalList 或 SafeKeyGroup 管理接收者列表，并提供 Add/Remove/Send 三类操作；
    - 差异：是否有 Flag（old/new）、是否有 Channel、是否跨 Type。  
  - 合并方向：
    - 可以抽象出一个通用的 `LinkContainerBase<TReceiver>`，内部封装 ApplyBuffers + 遍历 + UnityEngine.Object 判空；
    - 具体容器只负责决定 Key（Type/Channel/Flag）与 OnLink 的参数结构。




## 2. 当前缺少的“使用功能”（对开发者友好的部分）

- **统一的诊断与可视化面板**  
  - 对 Res / Link / Hosting / Module 等核心系统，缺少“总览式”的 Editor 面板：
    - 当前有哪些 Host / Module 在运行；
    - 当前有哪些 Link 类型及活跃监听者；
    - 当前加载了哪些 AB / 资源，占用多少内存大致估算。


## 3. 总结

- 合并方向：
  - 不建议一次性重构所有容器和生命周期逻辑，而是：
    - 在 AIPreview 中先实现一套“统一容器/生命周期辅助类”的原型；
    - 等验证成熟后，再考虑逐步迁移现有模块。
- 缺失的功能更多集中在“开发体验与可视化”层面：
  - 这些内容对最终玩家不可见，却极大影响团队效率；
  - 非常适合通过后续的 Editor 工具和 Preview 原型逐步补全。
