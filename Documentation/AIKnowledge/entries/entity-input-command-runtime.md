# Entity、输入与 ESCommand 运行链完整机制

`KnowledgeId`: `es.project.entity-input-command-runtime.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `entity`, `prefab`, `domain`, `input`, `runtime-mode`, `control`, `command`, `runner`, `interaction`  
`ContentHash`: `8bb569844f1254a1592731f8491deb28cce28013b4f184727e7d043c5692685b`

## Entity 不是松散组件集合

`Entity` 直接继承 `Core` 并接入 KinematicCharacterMotor，KCC 属于超高频核心而不是普通 Module。可扩展行为被分到 Basic、AI、Buff、Equipment、State 五个序列化 Domain；每个 Domain 再拥有自己的 Module 集合。Entity 还持有生命周期代数、Tag Host、OpSupport、稳定挂点映射和 Profile 定义入口。

初始化顺序先修复结构、捕获作者运动基线、准备 OpSupport/Tags/TransformMapping，再从同根 `EntityCharacterIdentity` 应用 Prefab Profile 定义，最后初始化 KCC。稳定挂点首次绑定后重建缓存，业务热路径不应反复 `Find` 层级。

## Prefab 与定义边界

- 角色 Variant 从同根 Profile 取得唯一 DataInfo。
- 通用池模板只提供能力骨架，由租户显式 BindDefinition。
- 基础模板可以无定义；不能把模板上的临时字段伪装成正式 DataInfo。
- Pool spawn/despawn 通过 Domain 转发给 Health、Combat、Interaction 等生命周期模块；生命周期代数用于拒绝旧 Token/Lease 回调污染新一轮实例。

## 输入编译链

```text
ESInputConfig / IESInputRuntimeConfigSource
  -> ESInputRuntimeBuilder
  -> Action metadata + compiled bindings
  -> profile layers and per-binding override
  -> ESInputSystemSource / VirtualSource / AITest source
  -> ESInputService frame cache
  -> Entity writer module updates EntityInputState
  -> EntityAIDomain resolves and dispatches effects
```

RuntimeBuilder 根据 ActionId 计算缓存容量，编译 bindingId、scheme、原始/有效路径、虚拟控制、interaction、processor 和 composite 标记。只有 `allowRebind` 的 Action 才应用 Profile override。`ESInputModule` 管理硬件、虚拟和 AITest 三类源、SchemeResolver、RuntimeModeService 与有效 Profile；AITest 控制有 owner/token/generation，不能无身份长期占用。

`ESInputService.BeginFrame` 清理帧态并应用 RuntimeMode Policy；`EndFrame` 只处理活跃索引，提交 Button 状态并交换删除无运行态条目。Policy 阻断时保留“直到释放”的边界，防止模式切换后把旧按键误解释为新 Press。

## 控制权与执行阻断不是一回事

EntityAIDomain 的 `inputState` 是已解析意图，Player/AI/Network 等 writer 决定谁写入；`controlPermit` 只决定这些意图是否可以作用于 Entity。`AcquireControlBlock(source, ownerId, ...)` 返回代数安全 Token，不能用零 owner 制造永久孤儿阻断。切换 writer、抢占 LocalControl 和添加 hard block 必须分别建模，不能用一个 bool 混合。

## ESCommand 执行链

`ESCommand` 是可序列化的同步命令基类；禁用命令返回 Skipped。`ESCommandEvent` 顺序执行列表，Failed/Canceled 立即短路，至少一个成功则最终 Succeeded。需要跨帧行为的命令实现 `IESCommandPlayable`，由 `ESCommandPlayerRunner` 管理帧推进，而不是让普通 `Invoke` 隐式启动无主协程。

`ESCommandPlayer` 是场景/Prefab 上的命令宿主；GameManager 的 CommandModule 提供系统服务。输入类命令写 VirtualSource，RuntimeMode 命令修改模式栈；它们不应绕过 InputModule 或直接改 Entity 字段。

## 失败模式与检查顺序

1. Entity 不动：先查输入源和 RuntimeMode Policy，再查 writer，最后查 AIDomain control block 与 Tag dispatch 条件。
2. 池化后旧行为复活：检查 generation、Lease/Token 归还和 Domain despawn 回调。
3. Command 卡住：检查是否实现 Playable、Runner 是否注册、Stop/Cancel 是否有所有者。
4. Profile 改动无效：检查 bindingId、allowRebind、有效 Scheme 和 Layer 合并，而不是直接改缓存。
5. Prefab 数据冲突：检查同根 Profile/DataInfo 与显式 BindDefinition，不从层级名推断身份。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色Prefab职责与DataInfo入口_AI协作警告.md` (`3ffbcc8b1030f7c47e82eff496f0a22cc892d65d04e02e97ff1116a6aba31d83`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）/输入与交互入口_AI协作警告.md` (`aee8ffd9518528479d662f1a27d1c3f47704417228e19589dc97e8d9c13f9da8`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md` (`05d19860d7ab966b84b98e5c065404b8a6d62f8ebf05719ac18d8be450b53d18`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs` (`28578ef54995dbcc085e7856e237bffb0292914d7b3bcae34b8152b470a99b05`)
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputService.cs` (`139a12d9501c86343da2cc3caf75cbd5455e281895fa910d558d9b0e61eaaf7c`)
- `Assets/Plugins/ES/1_Design/Input/SERVICE_ESInputRuntimeBuilder.cs` (`274c150db81f02b6fbd045677cb5427a85267132b413647bc85695cd42a18a72`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESInputModule.cs` (`b6937aff65d7dfa57839a35bccfbedb51b5cfd605eaaa6d3b988ef418e3820c5`)
- `Assets/Scripts/ESLogic/Runtime/Command/Components/ESCommandPlayer.cs` (`f1f4aa07b76a96160b157958bd5febeb8bfe6cc8e9c77fb779ce602e86c1b1db`)
- `Assets/Scripts/ESLogic/Runtime/Command/Runtime/SERVICE_ESCommandPlayerRunner.cs` (`63ac41ab45a06028b8977a232dba5cf642adcd49e8fd233ee864248e67bf0364`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_Command/ABSTRACT_ESCommand.cs` (`3b5190f5bbcdc4ede2e0e44d48772a20490008c9d944df2a2d8fec4417765fb3`)

`EvidenceLevel`: `S1`（源码机制已检查，未运行角色/输入 PlayMode 验收）  
`StaleWhen`: Entity 生命周期、Domain/Module、输入编译、RuntimeMode Policy、控制仲裁或 Command Runner 变化。
