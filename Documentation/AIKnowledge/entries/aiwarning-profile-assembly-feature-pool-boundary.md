# P0 Profile 装配、Feature 目录与池化边界

`KnowledgeId`: `es.aiwarning.p0.profile-assembly-feature-pool-boundary.v1`  
`Authority`: `AIWarnings + current Profile/Life source`  
`RouteKeys`: `aiwarnings`, `p0`, `profile`, `assembly`, `feature`, `pool`, `extension`, `runtime-context`, `editor`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `ddaa4c2929fda27a6348d54ab1357cbc6bc7826467af83a2e245ed3b226700aa`  
`SourceSetHash`: `ddaa4c2929fda27a6348d54ab1357cbc6bc7826467af83a2e245ed3b226700aa`  
`EntryBodyHash`: `e7ba0663502156f6250e489bfc6dc0f3c26e69fcb09aeb2fde0724686262b978`  
`StaleWhen`: `Profile Header/Settings/Extension/RuntimeContext、ESGenericLife、Feature 目录或 SourceRef 哈希变化。`

## 迁移说明

原 Warning 327 行、17,870 UTF-8 字节；现 Warning 保留 Profile 装配权威、命名所有权、Editor/Player 分层、池化生命周期和资源边界。本条目承接统一 Profile 结构、扩展生命周期、迁移事务、目录语义和验收清单。

## Profile 语义

- Profile 是 Prefab/场景对象的能力装配和默认策略权威，不是全局 Definition/Catalog、资源 Owner、运行服务、动态状态或第二个 Pool Root。标准结构为 `ESProfileHeader + XxxProfileSettings + XxxProfileExtension[] + 非序列化 XxxProfileRuntimeContext`。
- `Profile` 术语只能用于具备稳定 ProfileKey、SchemaVersion、Settings、Extension 列表、RuntimeContext 和完整生命周期边界的对象；普通 Config/Definition/Preset/Policy/Identity/Plan 不得借名。
- Settings 只放静态默认配置；RuntimeContext 只记录当前实例、Pool Generation、生命周期状态、Handle 和临时表现引用，不能成为业务事实第二权威。Extension 为单层、强类型、可校验列表，不嵌套 Domain/Module 树。
- Extension 必须具备 TypeId、版本、依赖/互斥规则，并在低频 Awake/Enable/Disable/Pool Spawn/Despawn/Destroy 边缘正序开始、逆序结束；失败只回滚已进入阶段，不能在 Update/热路径遍历、反射或按类型名动态分派。

## Editor/Player 与迁移

- Header/Profile 迁移只能由 Editor 显式事务执行：检查版本→预检全部目标→单一 Undo→逐步迁移→完整校验→统一提交；失败必须整体恢复并复核序列化内容、Managed Reference、Unity 引用、Prefab Override 和 Dirty 状态。Drawer/OnGUI/OnValidate/Awake 不得静默迁移，Player 不发现或执行迁移器。
- Editor Registry、菜单、Drawer、Workbench 和迁移代码留在 Editor；Player 只执行已验证生命周期转发。运行时不得依赖 Registry、反射、CLR 类型名、TypeId 或 JSON 动态选择。
- Profile 位于 `Runtime/Profile`，Feature/Audio、Feature/Camera 放使用侧组件和桥接，全局 Module 负责服务/仲裁/资源收口；Profile 不保存 Raw Unity 资源引用或内嵌 Key 绕过内容库。

## Pool 与资源

- `ESGenericLife` 是对象所有权、Pool Generation 和 Spawn/Despawn 顺序唯一权威。Entity 同根时 Profile 只能是 Extension；独立 Prefab 无其他合法 Root 时才可作为 Root。Spawn 先 Root 基础绑定，再 Profile 应用；Despawn 先撤销 Extension 意图，再回收 Root。
- 预热对象必须先经历 Despawn 基线；新代不得继承旧 Handle、注册或异步续体。Profile 不创建 Scope、不拥有 Cue/Clip/Prefab 等资源，资源由 ResourcePlan 或 Owner Scope 管理；禁止 Spawn/Despawn 扫描子树。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/Profile/Generic/ESGenericProfile.cs`
- `Assets/Scripts/ESLogic/Runtime/Profile/Generic/ESGenericProfileRuntimeContext.cs`
- `Assets/Scripts/ESLogic/Runtime/Life/ESGenericLife.cs`
- `Assets/Plugins/ES/1_Design/Tests/ESGenericLifePoolTests.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_Profile装配权威_Feature目录与池化边界_AI协作警告.md` (`965fd0a20b81b7f06bcfb4c0cebca6454197d27aceae8246848eebf9df561ce8`)
- `Assets/Scripts/ESLogic/Runtime/Profile/Generic/ESGenericProfile.cs` (`433e835c2c36539c3948caafd1612947400c72c86b4d96fa216a34713f7bd52a`)
- `Assets/Scripts/ESLogic/Runtime/Profile/Generic/ESGenericProfileRuntimeContext.cs` (`7917c5da58c4c0c8db37c55df1dce32dc215ac0dd6d91a966057aaecaf106585`)
- `Assets/Scripts/ESLogic/Runtime/Life/ESGenericLife.cs` (`519aad2dfef5778a906962d6ebce516ecfad983a2b5c526d76b162eb0c599425`)
- `Assets/Plugins/ES/1_Design/Tests/ESGenericLifePoolTests.cs` (`57f1260c75da436d7f8e9c9cc0befc3332c8c4107f52e7ef60cbc4d8878c47cc`)
