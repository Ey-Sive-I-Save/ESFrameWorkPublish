# Profile 基础设施与 GenericProfile 实装交接

## 已授权目标

在 ESFramework 中开始实装统一 Profile 基础设施，并交付首个 `ESGenericProfile`。用户已明确授权在本项目内修改源码、Editor 工具与必要的定向测试；不得迁移、清理或覆盖无关工作树改动。

`ESGenericProfile` 的首批低频示例能力：

1. 仅在非 Editor 的 Player 初始化阶段销毁 `ESGenericProfile` 组件自身，不销毁 GameObject。
2. 在启用和禁用边缘按配置输出 Debug 日志，支持事件掩码、Log/Warning/Error、消息与 Development-only 门禁；不得使用 Update 轮询。
3. 将指定 Prefab 实例化到显式指定子节点；每个 Profile 实例只创建一次，池化 Disable/Despawn 不重复创建，OnDestroy 清理；父节点必须为 Profile 根或其后代。

## 已冻结职责边界

```text
DataInfo / Cue = 全局内容定义权威
Profile        = Prefab / 场景对象能力装配与默认策略权威
Runtime State  = 当前动态事实
Domain         = 全局职责边界
Module         = 全局运行服务与仲裁
Feature        = 可复用能力使用侧与执行组件
```

Profile 不持有资源、不创建 Scope、不替代 `ESGenericLife`。运行时上下文只能保存本实例的池代际、注册状态、临时 Handle 或异步状态；不得成为 Voice、HP、Buff、Cooldown、目标或网络状态的权威。

Profile Extension 只允许单层，禁止 Module/Domain 嵌套树。Player Runtime 不得依赖 Editor Registry、反射扫描、CLR 类型名、JSON 或未烘焙的 `SerializeReference`。Editor Registry 仅负责 Inspector、校验、预览和迁移；运行时只读取强类型 Snapshot。

`ESAudioEmitter` 等实际执行组件属于 `Feature/Audio`，而不是 Profile。

## 推荐目录与类型

```text
Assets/Scripts/ESLogic/Runtime/Profile/
├─ Shared/
│  ├─ ESProfileHeader.cs
│  └─ ESProfileRuntimeContextBase.cs
└─ Generic/
   ├─ ESGenericProfile.cs
   ├─ ESGenericProfileSettings.cs
   ├─ ESGenericProfileRuntimeSnapshot.cs
   └─ ESGenericProfileRuntimeContext.cs

Assets/Plugins/ES/Editor/ESProfileWorkbench/
├─ Core/
└─ Generic/
```

类型的固定形态：

```text
XxxProfile
├─ ESProfileHeader
├─ XxxProfileSettings
├─ XxxProfileExtension[]
└─ XxxProfileRuntimeContext（非序列化）
```

首版可使用 Editor-only Authoring 数据，但必须 Bake 为固定强类型的 `ESGenericProfileRuntimeSnapshot`；Player 不解析 Authoring Extension。

## 池化与 Inspector 契约

- `ESGenericProfile` 应为 `MonoBehaviour, IESGameObjectPoolLifecycle`，并标记 `DisallowMultipleComponent`。
- 不自动抢占或创建 `ESGenericLife` Root。仅在同根已有合法 Root，且对象 inactive 的合法时机注册为 Extension。
- 参考 `Assets/Scripts/ESLogic/Runtime/Life/ESGenericLife.cs`：Spawn 顺序为 Root 到 Extension；Despawn 为 Extension 逆序到 Root；禁止池化边缘子树扫描。
- 普通 Inspector 必须能完成 Header、基础 Settings、三个扩展配置、Bake 与 Validate；不能把独立窗口作为前置。
- 编辑器写入使用 `Undo.RecordObject` 和 `EditorUtility.SetDirty`；`OnValidate` 不做全项目扫描或自动 Bake。

## 需要补的定向验证

1. Pool Spawn/Despawn 会清理 Profile RuntimeContext。
2. Debug 仅在对应启用事件配置时输出。
3. 自毁仅 Player 条件下生效。
4. 子 Prefab 只创建一次，回池不重复创建。
5. 非后代 Parent 被 Validate 拒绝。
6. Runtime Snapshot 不依赖 Editor Authoring 类型。

## 必读规则与证据边界

1. 初始化仅读取 AIWarnings 的 README、CurrentStatus、RuleIndex，再读取下列 Profile P0：
   `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_Profile装配权威_Feature目录与池化边界_AI协作警告.md`
2. 当前工作树很脏；禁止 reset、checkout、clean 或删除无关文件。
3. 所有文本使用 UTF-8；PowerShell 读取明确使用 `-Encoding utf8`；文件写入只使用 `apply_patch`。
4. 禁止手改 Unity 生成的 `.csproj`。
5. 当前 `dotnet build` 已知可能被陈旧工程路径的 CS2001 阻断；这不等价于 Unity 编译阻断。必须明确区分源码、dotnet、Unity ReloadAssembly、Test Runner、PlayMode、Profiler 与 IL2CPP 的证据层级。
6. 已新增 Profile P0 文档及现有音频文件可能仍为未跟踪状态；不得把未跟踪源码误报为版本化基线。

## 完成汇报要求

报告改动文件、静态/实际验证结果、剩余未验收项和下一最小动作。不得将“源码存在”表述成 Unity Test Runner、PlayMode、Profiler 或 IL2CPP 已通过。
