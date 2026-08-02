# ES input project map

## Authorization commands

- `Assets/Plugins/ES/AICommands/执行_新增输入动作_强约束_AI命令.md`
- `Assets/Plugins/ES/AICommands/新增输入动作_AI命令.md`
- `Assets/Plugins/ES/AICommands/检查_输入动作绑定缺失_AI命令.md`
- `Assets/Plugins/ES/AICommands/RuntimeMode输入过滤_检查_AI命令.md`
- `Assets/Plugins/ES/AICommands/检查_RuntimeMode阻断规则_AI命令.md`
- `Assets/Plugins/ES/AICommands/信息_输入运行模式上下文_AI命令.md`
- `Assets/Plugins/ES/AICommands/方案_玩家控制请求架构_AI命令.md`

## Mandatory rule areas

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）`
- Read the P0 active-request arbitration warning for player control ownership.

## Source and self-tests

- `Assets/Plugins/ES/1_Design/Input/ENUM_ESInputActionId.cs`
- `Assets/Plugins/ES/1_Design/Input/STRUCT_ESInputDefine.cs`
- `Assets/Plugins/ES/1_Design/Input/STRUCT_ESInputBindingProfile.cs`
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputService.cs`
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputRuntimeBuilder.cs`
- `Assets/Plugins/ES/1_Design/Input/STATIC_ESInputFullSelfTest.cs`
- `Assets/Plugins/ES/1_Design/Input/STATIC_ESInputRuntimeModeSelfTest.cs`
- Editor integration: `Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputSelfTestMenu.cs`
