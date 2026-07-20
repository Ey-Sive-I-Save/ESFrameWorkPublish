# ES 输入与交互入口 AI 协作警告

本文给后续协作 AI 使用，职责范围只覆盖：输入运行时、改键覆盖、虚拟输入、RuntimeMode 对输入的过滤、玩家交互入口衔接。不要把本文当成 Entity、KCC、StateMachine、IK、Save、SO 表工具的总说明。

## 当前实际位置

- 输入配置 SO：`Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESInputConfig.cs`
- 输入设计层：`Assets/Plugins/ES/1_Design/Input/`
- RuntimeMode：`Assets/Plugins/ES/1_Design/RuntimeMode/`
- GameManager 输入模块：`Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESInputModule.cs`
- GameManager 静态入口：`Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`

## 已确定主链路

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

## 职责边界

- `ESInputConfig`：项目默认输入定义，不存玩家改键。
- `ESInputBindingProfile`：玩家覆盖档案，只存覆盖，不复制默认配置。
- `ESInputModule`：GameManager 模块入口，负责初始化、Profile 读写、烘焙、重建、启停、UI 虚拟输入 API。
- `ESInputRuntime`：稳定持有 `ESInputService / ESInputSystemSource / ESInputVirtualSource / ESInputSchemeResolver`。
- `ESInputService`：高频状态计算核心，不负责 SO、文件、Profile、GameManager 生命周期。

不要把 `ESInputService` 合并进 `ESInputModule`。业务可以缓存 Service；运行时改键或重建配置时只能替换内部表和输入源，不应替换 Service 对象本身。

## 配置与绑定规则

- 合法 binding path 必须来自 Unity Input System：控件目录、监听 Rebind、临时 `InputAction` 辅助导入。
- 不要恢复手写幻想 path 表，例如 `STATIC_ESInputControlPreset`。
- 不要要求策划直接记忆 `<Keyboard>/j` 这类字符串。
- `ESInputControlCatalog` 当前通过 `InputSystem.AddDevice(layout).allControls` 生成键盘、鼠标、手柄选项；中文名只是显示层翻译，path 仍来自 InputSystem。
- `ESInputBindingDefineDrawer` 提供三类配置入口：选择、监听、导入临时 `InputAction`。

## UI 虚拟输入规则

UI 按钮、摇杆、触摸区只写输入状态，不直接执行业务：

```csharp
ESGameManager.InputModule.UIPressButton(ESInputActionId.Interact);
ESGameManager.InputModule.UIReleaseButton(ESInputActionId.Interact);
ESGameManager.InputModule.UISetVector2(ESInputActionId.Move, value);
ESGameManager.InputModule.UIClearVector2(ESInputActionId.Move);
```

业务仍统一读取：

```csharp
ESGameManager.InputModule.ConsumeClick(ESInputActionId.Interact);
ESGameManager.InputModule.ConsumeLongPress(ESInputActionId.Interact);
ESGameManager.InputModule.ConsumeDoublePressed(ESInputActionId.Interact);
```

## 触发方式

点击、松开、按住、长按、双击由 `ESInputService` 统一计算。只有动作配置启用了对应 `ESInputTriggerFeature` 时才承担相关判断成本。组合绑定的子项按按钮处理；动作本身按 `ESInputValueType` 过滤可选控件。

## RuntimeMode 与权限

`ESRuntimeModeService` 是全局高速服务，输入模块读取它的 `CurrentPolicy` 来过滤输入。输入静默、剧情接管、UI 屏蔽、交互锁定这类问题不要散落成临时 bool；严肃场景优先接 `ESPermitSet / ESPermitLaw`。

常见策略字段：

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

## 后续优先事项

- 把 `Interact` 输入接到现有 `BasicInteraction -> ESInteractable` 主链路。
- 继续增强绑定 UI：搜索、按设备分组、显示当前 path 的中文解释。
- 后续做动态 Action 或代码生成时，不要破坏当前 enum 快速路径。
- 如果继续修改输入底层，至少验证：

```text
dotnet build ES_Design.csproj --no-restore
dotnet build ES_Logic.csproj --no-restore
```
