# ES 输入与交互入口协作警告

本文给后续协作 AI 使用，职责范围只覆盖“输入系统、UI 虚拟输入、运行上下文、玩家交互入口衔接”。它不是整个 ES 框架总说明，也不替代 Entity/KCC/State/IK/Save/SO 表工具的专题文档。

本文件重点回答：

- 玩家输入如何从配置进入运行时。
- UI 触摸/按钮如何模拟输入。
- 输入如何被 RuntimeMode 限制。
- 交互按钮如何统一走输入系统，而不是 UI 直接执行业务。
- 改键、恢复默认、Profile 序列化如何保持高性能和稳定引用。

目标是避免重复推翻已确定的架构，减少幻想式 API，保持运行时高性能和编辑器可用性。

## 当前实际位置

- 输入配置 SO：`Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESInputConfig.cs`
- 输入底层设计层：`Assets/Plugins/ES/1_Design/Input/`
- RuntimeMode 设计层：`Assets/Plugins/ES/1_Design/RuntimeMode/`
- GameManager 输入模块：`Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESInputModule.cs`
- GameManager 静态入口：`Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`

注意：输入模块曾经讨论过放在 `Assets/Plugins/ES/2_Feature/ESGameCore`，但当前已经移动到 ESLogic 的 GameManager 体系内。不要按旧路径新增重复模块。

## 本文件职责边界

负责说明：

- `ESInputConfig`
- `ESInputBindingProfile`
- `ESInputModule`
- `ESInputRuntime`
- `ESInputService`
- `ESInputSystemSource`
- `ESInputVirtualSource`
- `ESRuntimeModeService` 与输入许可的关系
- UI 虚拟按钮/摇杆到业务交互的统一入口

不负责说明：

- KCC 运动细节。
- StateMachine 动画生命周期。
- FinalIK 求解细节。
- Buff 系统。
- 保存系统。
- SO 表导入导出工具。

这些内容应优先看同目录下其他专题文档。

## 已确定分层

输入主链路：

```text
ESInputConfig 默认动作/绑定
+ ESInputBindingProfile 玩家覆盖档案
-> ESInputUtility / ESInputProfileBaker 烘焙 EffectiveProfile
-> ESInputRuntimeBuilder 构建运行时表
-> ESInputRuntime 持有输入源和服务
-> ESInputSystemSource / ESInputVirtualSource 写入
-> ESInputService 计算 pressed / held / released / longPress / doublePress
-> 业务读取 ConsumeClick / ConsumeLongPress / ReadVector2
```

职责边界：

- `ESInputConfig`：项目默认输入定义，属于默认配置，不存玩家改键。
- `ESInputBindingProfile`：玩家覆盖档案，只存覆盖，不复制默认配置。
- `ESInputModule`：GameManager 模块入口，负责初始化、Profile 读写、烘焙、重建、启停、UI 虚拟输入 API。
- `ESInputRuntime`：稳定持有 `ESInputService / ESInputSystemSource / ESInputVirtualSource / ESInputSchemeResolver`。
- `ESInputService`：高频状态计算核心，不负责 SO、文件、Profile、GameManager 生命周期。

不要把 `ESInputService` 合并进 `ESInputModule`。Module 取代 Service 的对外地位，不取代 Service 的底层职责。

## 高频引用稳定性

`ESInputModule` 必须长期持有同一个 `ESInputRuntime`，`ESInputRuntime` 必须长期持有同一个 `ESInputService`。

允许高频业务缓存：

```csharp
private ESInputService input;

void OnEnable()
{
    input = ESGameManager.InputModule.Service;
}
```

运行时改键、切方案、重建 Runtime 时，只能替换内部 `cache / InputAction / VirtualSource` 数据，不应替换 `ESInputService` 对象。否则角色控制器、相机、战斗模块缓存会失效。

## RuntimeMode 是全局高速服务

`ESRuntimeModeService` 已作为 GameManager 静态直达：

```csharp
ESGameManager.RuntimeMode
```

它负责：

- Mode 栈：`PushMode / PopMode / RemoveModeWithAbove / PopTopMode`
- Tag 叠加：`AddTag / RemoveTag`
- 统一策略：`CurrentPolicy`
- 调试追踪：`CurrentTrace`
- 策略变更事件：`OnPolicyChanged`

常用策略字段：

```text
allowPlayerInput
allowMoveInput
allowCameraLook
allowCombatInput
allowInteractionInput
allowUIInput
showCursor
lockCursor
pauseWorld
showGameplayHud
```

输入系统当前已在 `ESInputService.IsActionAllowed` 读取 RuntimeMode Policy。后续性能优化方向是把 RuntimeMode 前移到采样层：Policy 变化时刷新 `ESInputSystemSource.enabledActionIds`，不要每帧轮询无效 Action。

## 不要恢复这些旧思路

不要重新引入：

- `STATIC_ESInputControlPreset` 这类手写幻想 path 表。
- 让用户手填 `<Keyboard>/j` 作为主要编辑体验。
- `InputActionAsset` 作为运行时主数据源。
- `ESInputConfig` 同时保存默认配置和玩家覆盖。
- 每次 Rebuild 都 `new ESInputRuntime()` 或 `new ESInputService()`。
- UI 按钮直接执行业务逻辑。

正确方向：

- 用 Unity Input System 自身的控件目录、监听 Rebind、临时 `InputAction` 编辑能力生成合法 path。
- `ESInputConfig` 永远是默认定义。
- `ESInputBindingProfile` 永远是覆盖档案。
- UI 只写输入状态，业务统一从输入系统读取。

## UI 虚拟输入规则

UI 入口在 `ESInputModule`：

```csharp
ESGameManager.InputModule.UIPressButton(ESInputActionId.Interact);
ESGameManager.InputModule.UIReleaseButton(ESInputActionId.Interact);
ESGameManager.InputModule.UISetVector2(ESInputActionId.Move, value);
ESGameManager.InputModule.UIClearVector2(ESInputActionId.Move);
```

重要规则：

- UI 事件只写输入状态，不执行业务。
- 业务仍读 `ConsumeClick / ConsumeLongPress / ReadVector2`。
- `ActionId` 直写不要求当前方案存在虚拟绑定，确保 UI 可随时模拟动作。
- `virtualControlId` 写入仍走配置映射，适合触摸方案和可配置虚拟控件。
- UI 关闭或禁用时必须释放按钮/摇杆状态，避免卡键。

## 点击、长按、双击

点击、长按、双击由 `ESInputService` 统一计算，不交给 UI 或业务零散实现。

常用读取：

```csharp
ESGameManager.InputModule.ConsumeClick(ESInputActionId.Interact);
ESGameManager.InputModule.ConsumeLongPress(ESInputActionId.Interact);
ESGameManager.InputModule.ConsumeDoublePressed(ESInputActionId.Interact);
```

只有配置启用了对应 `ESInputTriggerFeature` 的动作才应该承担相关计算。后续如继续优化，应做“活跃按钮列表”和“触发特性筛选”，而不是让每个动作无脑执行长按/双击逻辑。

## 运行时改键与恢复默认

运行时应用覆盖入口：

```csharp
ApplyPlayerPathOverride(...)
ApplyPlayerInputSystemOverride(...)
ApplyPlayerVirtualOverride(...)
ApplyScheme(...)
ApplyPlayerProfile(...)
ApplyPlayerProfileJson(...)
```

恢复默认入口：

```csharp
ResetPlayerBindingToDefault(bindingId, saveNow, rebuildNow)
ResetAllPlayerOverrides(saveNow, rebuildNow)
ResetPlayerProfile(rebuildNow, saveNow)
```

序列化入口：

```csharp
PlayerProfileToJson(prettyPrint)
ApplyPlayerProfileJson(json, saveNow, rebuildNow)
LoadPlayerProfile(path, rebuildNow)
SavePlayerProfile(path)
```

频繁应用更改时：

- 可 `saveNow: false` 批量修改，最后统一保存。
- `rebuildNow: true` 会重建运行时 Action，但不替换 `ESInputService`。
- 多 Profile 先烘焙为 EffectiveProfile，运行时只消费最终结果。

## 性能原则

核心目标：

```text
配置可以很多，运行时只采样当前方案、当前上下文、当前启用分类需要的 Action。
```

当前已做：

- 按 active scheme 构建 InputAction。
- 没绑定的 Action 不进启用列表。
- UI 虚拟输入不走 InputAction。
- 高频读写路径尽量减少无意义判空。
- `ESInputService` 可被业务缓存。

后续建议：

- `ESInputSystemSource` 增加按 RuntimeMode Policy 刷新 `enabledActionIds`。
- 增加 Mode/Tag owner 批量移除，例如 `RemoveAllByOwner(owner)`。
- 增加 RuntimeMode 调试面板展示 Mode 栈、Tag 列表、Policy Trace。
- 增加输入配置编辑器的合法控件选择和监听绑定，不要手写 path 表。

## 编译与验证提示

改输入底层后至少验证：

```text
dotnet build ES_Design.csproj --no-restore
dotnet build ES_Logic.csproj --no-restore
```

如果 Unity 生成的 csproj 未刷新，先确认文件实际路径，不要在旧路径新增重复文件。遇到已有 unrelated dirty worktree，不要回滚用户改动。
