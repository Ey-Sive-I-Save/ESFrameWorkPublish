# ESCommand project map

## Authorization and context

- `Assets/Plugins/ES/AICommands/执行_新增ESCommand运行时命令_强约束_AI命令.md`
- `Assets/Plugins/ES/AICommands/ESCommand新增运行时命令_AI命令.md`
- `Assets/Plugins/ES/AICommands/信息_ESCommand上下文_AI命令.md`

## Mandatory rules

- `Assets/Plugins/ES/0_Stand/BaseDefine_Command/ESCommand_STANDARD.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md`

## Source and editor integration

- `Assets/Plugins/ES/0_Stand/BaseDefine_Command/ABSTRACT_ESCommand.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_Command/STATIC_ESCommandCategory.cs`
- Search current non-obsolete source for `ESCommandPlayer`, Runner implementations, and similar command subclasses.
- `Assets/Plugins/ES/Editor/ESCommand/ESCommandEventDrawer.cs`
- `Assets/Plugins/ES/Editor/ESCommand/ESCommandPlayerEditor.cs`
