# 与商业化框架的差距分析（UI / 网络 / 动画 / 资源 / 物理）

> 说明：以下对比基于当前工程中可见的 ES 工具集、Res 系统和部分设计模块，未发现完整的自研 UI/网络/动画/物理大框架，因此很多是“缺失项”的差距分析。

## 1. UI 系统 vs FairyGUI 等商业 UI 框架

- **当前项目现状（从 DevManagement 等工具侧观察）**
  - 主要使用 Unity 自带 UI + EditorWindow + Odin Inspector 构建工具面板；
  - 未见成体系的“运行时 UI 框架”（如界面栈管理、UI 路由、数据绑定、皮肤系统等）。

- **与 FairyGUI 等相比的主要差距**
  - 缺少：
    - 统一的 UI 资源打包与动态加载策略（当前主要依赖 Res 系统处理 AB，但 UI 层无专门抽象）；
    - 基于描述文件（XML/Json）的 UI 布局与皮肤系统，UI 大多仍是 Prefab + 手写逻辑；
    - 视图层与数据层的双向绑定机制（Form/Model <-> View）。
  - 优点/潜在优势：
    - 已有大量 Editor 工具与 Odin 面板经验，可以很容易扩展为 UI 配置面板。  

- **可提升方向**
  - 在 AIPreview 中设计一个 `ESUIRouter` + `UIView` + `UIScreen` 的轻量框架，用 Link 或事件总线驱动 UI 状态切换；
  - 将 UI Prefab 与 ResLibrary 结合，形成可替换/皮肤化的 UI 资源体系；
  - 逐步引入简单的数据绑定（属性监听 + 自动刷新）能力。

## 2. 网络模块 vs ET 等商业网络框架

- **当前项目现状**
  - 在当前仓库结构中未发现类似 ET 的“Actor/Session/Message”网络模块，更多是单机工具和 Editor 扩展代码。

- **与 ET 的差距**
  - 基础功能缺失：
    - 会话管理、断线重连、RPC/消息分发等完整网络栈；
    - 服务器端逻辑框架与客户端逻辑的契约（共享 Proto/IDL、热更等）；
    - 分布式或多进程协作模型（Actor/Entity）。

- **可提升方向**
  - 如未来需要网络：
    - 可以先从简单的 `NetworkService` + `MessageBus` 开始，结合 Link 体系进行消息分派；
    - 逐渐在 AIPreview 中构造“小型 ET 风格 Demo”，但保持与当前项目解耦。

## 3. 动画控制 vs Playable / Timeline 等系统

- **当前项目现状**
  - 从工程结构看，未见大规模使用 Unity PlayableGraph/Timeline 的自定义封装；
  - 有自研 TrackView 相关编辑器（ESTrackView），但具体实现不在本次上下文里。

- **与 Playable 的差距**
  - 系统性不足：
    - 没有统一的“动画状态机 + PlayableGraph”封装；
    - 动画/特效/音效/Timeline 协同播放的编排能力尚未显式体现。

- **可提升方向**
  - 在 AIPreview 中设计一个基于 ScriptableObject 的“动画片段资产 + 播放配置”，配合状态机原型使用；
  - 如果已有 ESTrackView，可考虑将其与通用状态机/任务系统对接，形成“动画驱动逻辑”的闭环。

## 4. 资源管理 vs Addressables 等框架

- **当前项目现状（ESRes 系统）**
  - 已经具备：
    - AB Hash / Dependence Json 生成；
    - ResKey / ResSource / ResLoader / ResMaster 的多层抽象；
    - ResLibrary / ResBook / ResPage 的 So 管理模型（资源逻辑分组）。
  - 这是当前项目最接近商业化框架的一块。

- **与 Addressables 的差距**
  - 编辑器工具链：
    - Addressables 提供完整的分组面板、标签系统、Profile（多环境路径）支持；
    - ESRes 当前在可视化配置与报表/诊断上仍然偏弱。
  - 运行时特性：
    - Addressables 内建异步 API + 自动引用计数 + 智能卸载策略；
    - ESRes 的引用计数/生命周期管理粒度较粗，需要开发者自己把握何时卸载。

- **可提升方向**
  - 强化 So 层：在 AIPreview 中构造 `ResLibraryManager`，提供：
    - 按 Library/Book/Page 的统计、筛选、批量操作；
    - 与 ResMaster 的直连加载接口（按逻辑分组加载/卸载）。
  - 增加运行时诊断：
    - 当前已加载 AB / 资源数量与内存估算；
    - 引用链可视化（哪个系统持有了哪些资源）。

## 5. 物理模拟 vs Havok 等商业物理引擎

- **当前项目现状**
  - 主要依赖 Unity 内置物理（从工程结构尚未看到自定义物理封装）。

- **与 Havok 等的差距**
  - 无独立的物理抽象层：
    - 没有对碰撞层、过滤器、物理材质、连续碰撞检测等进行统一建模；
    - 没有面向“逻辑事件”的物理回调管线（例如统一的命中结果对象、过滤规则）。
  - 无大规模场景性能调优手段：
    - 如空间划分（四叉树/八叉树）、宽相/窄相分离等手段目前更多由 Unity 引擎内部隐式处理。

- **可提升方向**
  - 在 AIPreview 中设计“物理查询服务”与“碰撞事件总线”：
    - 将常用 Raycast/Overlap 操作封装为服务接口；
    - 用 Link 或事件系统派发逻辑碰撞事件。

## 6. 总结

- 当前项目在“资源管理（ESRes）”上已经走到半商业化框架的级别，差距主要在：
  - 可视化与运维工具；
  - 更细腻的生命周期管理与诊断能力。
- UI / 网络 / 动画 / 物理等方面，目前更多是“按需使用 Unity 原生能力 + 自研工具”，离 FairyGUI/ET/Playable/Havok 等有明显差距，但**也因此拥有较大设计自由度**：
  - 可以在不受历史包袱限制的前提下，逐步在 AIPreview 中搭建自己的轻量框架，再视情况抽象为正式模块。
