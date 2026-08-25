# InputAction、RuntimeMode 与实体意图路由

`KnowledgeId`: `es.project.input-runtime-mode-routing.v1`  
`Authority`: `Current source + AIWarnings + Unity 2022.3.45f1/Input System 1.11.2 package documentation`  
`RouteKeys`: `input`, `input-action`, `binding`, `profile`, `virtual-input`, `runtime-mode`, `policy`, `control`  
`ContentHash`: `c6e0b7511ea0c53b053cbfc30c4560cf5c4e6c2f95e2347fb5888280abb01326`

## 已验证版本基线

- Unity Editor：`2022.3.45f1`。
- Input System：`1.11.2`。
- Unity 官方包文档说明：Action 启用时解析 Binding；启用期间不能修改部分 Binding 配置；Interaction 具有 Waiting/Started/Performed/Canceled 等阶段且顺序会影响结果；Processor 保持输入输出值类型一致。

这些 Unity 事实只约束底层 Input System。ES 的 Profile 合并、RuntimeMode Policy、虚拟输入和帧缓存语义仍以 ES 当前源码为准。

## 编译与运行主链

```text
IESInputRuntimeConfigSource / ESInputConfig
  -> ESInputRuntimeBuilder
  -> ESInputRuntimeCache + ESInputCompiledBinding[]
  -> ESInputModule
  -> InputSystemSource / VirtualSource / AITestSource
  -> ESInputService.BeginFrame / EndFrame
  -> EntityPlayerInputWriteModule
  -> EntityInputState
  -> EntityAIDomain executor
```

`ESInputRuntimeBuilder` 先用最大稳定 ActionId 建立数组容量，再编译 bindingId、scheme、原始/有效 path、虚拟 control、interaction、processor 与 composite 标记。只有 Action 的 `allowRebind=true` 时，Profile override 才能改变有效 Binding。

`ESInputModule` 长期持有一个 `ESInputService`，重建只替换缓存和各输入源的运行时表。正常帧同时轮询 Input System 与 VirtualSource；AITest 成功取得 `owner + token + generation` 租期后，帧更新改为只读取 AITestSource，硬件和普通虚拟源都不参与该帧。释放、Disable 或 Dispose 会清空输入并推进 AITest generation。

## RuntimeMode 只做粗粒度读取策略

`ESRuntimeModeService` 维护 Mode/Tag Active Set，并把 Player、Move、Camera、Combat、Interaction、UI 等字段提交为 `CurrentPolicy`。LeaseOwned 请求只能由匹配的 host、generation、owner 和 handle 释放；`Clear()` 推进 generation，使旧 Lease 失效。

`ESInputService` 在 Policy 改变时重置被阻断输入。按钮若在阻断发生时仍处于按住状态，会保留 `policyBlockedUntilRelease`；即使策略重新允许，也必须先观察到真实释放，避免把旧按键误判为新 Press。`BeginFrame` 清帧态并再次应用 Policy，`EndFrame` 只处理 active index，提交按钮状态并 swap-back 移除无运行态项。

RuntimeMode 回答“这一类输入是否允许被读取”；Entity 的 LocalControl、Tag、Permit、State、Buff、Skill 回答“哪个实体写入、以及行为能否执行”。不得把怪物 AI、网络、硬直或单实体能力限制塞回全局 Input Policy。

## 失败定位顺序

1. 检查 ActionId、metadata、bindingId、active scheme 和 `allowRebind`。
2. 检查当前来源：AITest 独占、Input System 或 VirtualSource。
3. 检查 RuntimeMode `CurrentPolicy` 与 `policyBlockedUntilRelease`。
4. 检查 LocalControl 和 Entity writer。
5. 最后检查 Entity controlPermit、Tag Gate 和具体行为执行模块。

## 非声明

- 未运行 Input System 设备、改键持久化、Scheme 切换、AITest、RuntimeMode 或 PlayMode 测试。
- 未验证 Unity InputAction 回调时序与 ES 轮询时序在所有 UpdateMode 下的组合行为。
- 不声明低 GC、设备兼容或玩家操作闭环已经验收。

## EvidenceRefs

- `Git`: `main@a31d58c740210f79eb346415168d7ba425037564`
- `UnityPackageDocs`: 本机 `com.unity.inputsystem@1.11.2/Documentation~`。
- `StaticReview`: 当前源码、项目版本与官方包文档已读取。
- `Runtime`: `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）/输入与交互入口_AI协作警告.md` (`aee8ffd9518528479d662f1a27d1c3f47704417228e19589dc97e8d9c13f9da8`)
- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Library/PackageCache/com.unity.inputsystem@1.11.2/Documentation~/Actions.md` (`876208fa0d63e7eac4be10092be51d53a4769311e9a8ecc2a8cbd4ebedffa7f2`)
- `Library/PackageCache/com.unity.inputsystem@1.11.2/Documentation~/Interactions.md` (`172b938ee3116acb5c461d840cb72dedfb4370db193e0155334448e48786839a`)
- `Library/PackageCache/com.unity.inputsystem@1.11.2/Documentation~/Processors.md` (`716ddb9296c8e0e7447a8466ea082a5da0c1acfe7eeee698e30972a00e2f3ab8`)
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputRuntimeBuilder.cs` (`274c150db81f02b6fbd045677cb5427a85267132b413647bc85695cd42a18a72`)
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputService.cs` (`139a12d9501c86343da2cc3caf75cbd5455e281895fa910d558d9b0e61eaaf7c`)
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputSystemSource.cs` (`4df47f86eafc21d63ecf32b5e424ecddd23666d7c1152869142c9f6338541bbe`)
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputVirtualSource.cs` (`c554409c275c04eec753473229c0b439d6cb4ddae29d74466e7a4c0e6f3766a0`)
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputProfileBaker.cs` (`0eaadc09592fa9059a0914eb58cf4b8e9f593548891d8113c4a72b9c0d52a6a3`)
- `Assets/Plugins/ES/1_Design/Input/STRUCT_ESInputBindingProfile.cs` (`bab77adcf01e76602c2c1165b6ae49fba2cc18ce6e47a523da6fefd59b106b47`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESInputModule.cs` (`b6937aff65d7dfa57839a35bccfbedb51b5cfd605eaaa6d3b988ef418e3820c5`)
- `Assets/Plugins/ES/1_Design/RuntimeMode/SERVICE_ESRuntimeModeService.cs` (`bcbd39aa9c24e56e6ce77fad65354bee59b60ad1df346250363724cfda549dd7`)
- `Assets/Plugins/ES/1_Design/Input/STATIC_ESInputActionBindingSelfTest.cs` (`759157e84278ee0f46d6297c0f90e5e2ef2dd0893f5d07969bf3082870d2b8a9`)
- `Assets/Plugins/ES/1_Design/Input/STATIC_ESInputRuntimeModeSelfTest.cs` (`47af7fbd7f7c8242e66d86d209679d35d69246767dc6047d997c7dbd848cd400`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs` (`1d2a4bd6f45cfc7841b6a0c226798370d85684fd92fc1303df70334b409a76f1`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs` (`28578ef54995dbcc085e7856e237bffb0292914d7b3bcae34b8152b470a99b05`)

`EvidenceLevel`: `S1`（源码、配置与本机官方包文档静态核对；runtime-not-run）  
`StaleWhen`: Unity/Input System 版本、Action/Binding/Profile schema、来源选择、AITest 租期、RuntimeMode Policy/Lease、帧缓存或任一 SourceRef 哈希变化。
