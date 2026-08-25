# ESCommand、Player、RunnerTick 与服务边界

`KnowledgeId`: `es.project.command-runner-lifecycle.v1`  
`Authority`: `Current source > AIWarnings > existing AIKnowledge projection`  
`RouteKeys`: `command`, `player`, `runner`, `runner-tick`, `playable`, `cancel`, `virtual-input`, `runtime-mode`, `lifecycle`  
`ContentHash`: `2ad31dd47927c205a1631e4c67ed6853c54f0d7440abb7d8d5ec09d5e5bd8e41`

## 当前执行链

```text
ESCommandPlayer.Play(event)
  -> ESCommandPlayerRunner.Register(player)
  -> ESCommandModule.Update()
  -> ESCommandPlayerRunner.TickAll(Time.time, Time.deltaTime)
  -> ESCommandPlayer.Tick(frame, time, deltaTime)
  -> ESCommand.InvokeCommand() 或 IESCommandPlayable
```

`ESCommandModule` 是 `TickAll` 的当前唯一驱动者，并在销毁时清空 Runner。Runner 使用 `List + Dictionary` 注册活跃 Player，以 swap-back 移除；Player 用 `lastTickFrame` 拒绝同帧重复推进。

普通 `ESCommand` 是同步、可序列化定义：禁用返回 `Skipped`，`Invoke()` 正常返回后固定报告 `Succeeded`。跨帧命令实现 `IESCommandPlayable`，生命周期为 `OnPlayStart -> TickPlay -> OnPlayCancel`。`tickImmediatelyOnPlay` 会以 `deltaTime=0` 在 Play 当帧推进到第一个等待点。

`Cancel()` 只登记请求，下一次 Tick 才取消；`Stop()` 立即调用当前 Playable 的 `OnPlayCancel`、注销 Runner 并进入 `Canceled`。当前 `Play()` 在已有 Playable 运行时会直接清空 `currentPlayable`，不会先补偿旧 Playable；调用方重播前必须先 `Stop()`。

## 服务与命令效果边界

`ESCommandServices` 只注入 `ESInputModule` 和 `ESRuntimeModeService`，不是通用 Service Locator。Input 命令只写 `UIPulseButton/UISet*/UIClear*` 等 VirtualSource API，不直接执行 Entity、Buff、Skill 或 Interaction 业务。

当前源码存在两类必须保留的失败语义：

1. Input 命令在 `ESCommandServices.InputModule == null` 时静默不写入，但基类仍返回 `Succeeded`。
2. 所有 RuntimeMode ESCommand 已冻结；Push/Remove/Pop/AddTag/RemoveTag/Clear 都只记录拒绝警告，不修改模式栈，但基类仍返回 `Succeeded`。

因此 `Succeeded` 当前只证明同步 `Invoke()` 没有抛出，不证明业务效果发生。业务验收必须检查目标服务、可观察状态和清理结果。

## 权威差异与 stale 投影

AIWarning 与共享 `Documentation/AIKnowledge/entries/entity-input-command-runtime.md` 仍描述 RuntimeMode 命令执行 Push/Pop/Remove。当前 `COMMAND_ESCommandRuntimeMode.cs` 与该描述冲突；按项目权威顺序，当前源码优先。共享条目的 SourceRefs 没有包含该命令文件，因此即使它列出的哈希均未漂移，也无法检测这项语义变化。

本条目只记录差异，不修改共享 AIWarning 或既有摘要事实；本轮仅把本条目登记到共享 KnowledgeIndex。

## 已知风险

- Runner 没有逐命令异常隔离；任一 `InvokeCommand`、`OnPlayStart`、`TickPlay` 或 `OnPlayCancel` 抛出都可能中断当帧 `TickAll`。
- 持续虚拟按钮/轴/向量必须有对称 Stop/Cancel 清理；普通同步命令本身没有所有权实例。
- Runner 和静态 Services 的场景/Subsystem 清退需要真实生命周期验证，源码存在不能替代运行证据。

## 非声明

- 未运行 Unity、PlayMode、命令重播、异常注入、场景切换或 Runner 清退测试。
- 未声明 Runner 低分配、命令业务成功或 RuntimeMode 命令可用。
- 不把同步 `Succeeded` 解释为已产生业务结果。

## EvidenceRefs

- `Git`: `main@a31d58c740210f79eb346415168d7ba425037564`
- `StaticReview`: 当前命令源码、AIWarning 与旧 AIKnowledge 投影已交叉读取。
- `StaticTest`: `ESStorySliceATests.RuntimeMode_LegacyCommandCannotDeleteStoryLease` 直接验证 RuntimeMode 旧命令被拒绝且不得删除 Lease；仅为静态绑定，未执行 Unity Test Runner。
- `Runtime`: `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md` (`05d19860d7ab966b84b98e5c065404b8a6d62f8ebf05719ac18d8be450b53d18`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_Command/ABSTRACT_ESCommand.cs` (`3b5190f5bbcdc4ede2e0e44d48772a20490008c9d944df2a2d8fec4417765fb3`)
- `Assets/Scripts/ESLogic/Runtime/Command/Docs/ESCommand_STANDARD.md` (`8935ceee7b310583bcee6f34e047aa99566d8f0b78ca9c531d542773c1d90c37`)
- `Assets/Scripts/ESLogic/Runtime/Command/Components/ESCommandPlayer.cs` (`f1f4aa07b76a96160b157958bd5febeb8bfe6cc8e9c77fb779ce602e86c1b1db`)
- `Assets/Scripts/ESLogic/Runtime/Command/Runtime/SERVICE_ESCommandPlayerRunner.cs` (`63ac41ab45a06028b8977a232dba5cf642adcd49e8fd233ee864248e67bf0364`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESCommandModule.cs` (`6323d8626c1f0db00a7662b7e3a9737679c3b175c2807bfcebf3afd8b249b3f1`)
- `Assets/Scripts/ESLogic/Runtime/Command/Runtime/SERVICE_ESCommandServices.cs` (`c350315ef3757c7664405592cc9a0323d7fc17dbab23593d3c875ec6c832f6c8`)
- `Assets/Scripts/ESLogic/Runtime/Command/Runtime/INTER_IESCommandPlayable.cs` (`c5e4c8c9ad1a864a76189651b3b12b56c0d4994c5ae9649284c9695953c3e137`)
- `Assets/Scripts/ESLogic/Runtime/Command/Runtime/STRUCT_ESCommandPlayFrame.cs` (`32edc60504b297a402b456c080d0d5dc9ba6d486eb2562f58553f88d98368e99`)
- `Assets/Scripts/ESLogic/Runtime/Command/Commands/COMMAND_ESCommandInput.cs` (`d5729a2c04094ff28ffb3d0ae514f89054025d030e7bd91a9757c23a27f381fc`)
- `Assets/Scripts/ESLogic/Runtime/Command/Commands/COMMAND_ESCommandRuntimeMode.cs` (`c45f55eeb47ee9419069741d3cf32845141e2ed168509fb087d8b53889ffbc39`)
- `Documentation/AIKnowledge/entries/entity-input-command-runtime.md` (`6e79078709f154be1c81f7ba304343beb959194d9fe7a76313776d317705bf29`)
- `Assets/Scripts/ESLogic/Tests/Story/EditMode/ESStorySliceATests.cs` (`94eda09935ba3bb094a606326447c69e986fea88a4e0f31f1221cee72d579202`)

`EvidenceLevel`: `S1`（源码与规则静态核对；runtime-not-run）  
`StaleWhen`: ESCommand 返回合同、Player Play/Cancel/Stop、RunnerTick 驱动、异常隔离、Services 注入、Input/RuntimeMode 命令效果、共享投影或任一 SourceRef 哈希变化。
