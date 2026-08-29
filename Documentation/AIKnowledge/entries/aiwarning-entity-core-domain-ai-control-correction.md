# Entity Core/Domain/AI 控制边界纠偏

`KnowledgeId`: `es.aiwarning.entity-core-domain-ai-control-correction.v1`  
`Authority`: `AIWarnings + current Entity/Core source`  
`RouteKeys`: `aiwarnings`, `architecture`, `entity`, `core`, `domain`, `module`, `ai-control`, `control-request`, `facade`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `eded16b7c4951482416af99a02d7bf738d6f21996dd1f4f043f08f95b52d875b`  
`SourceSetHash`: `eded16b7c4951482416af99a02d7bf738d6f21996dd1f4f043f08f95b52d875b`  
`EntryBodyHash`: `033f18624520cf56ab609717c6849d8d25ae9e35d4075af217a2c864f20edce8`  
`StaleWhen`: `Core/Domain/Module 生命周期、EntityAIDomain、控制请求合同或 SourceRef 哈希变化。`

## 迁移说明

原 Warning 237 行、7,227 UTF-8 字节；现 Warning 保留 Core/Domain/Module 逻辑能力、AI 域控制来源、外壳边界、最小增量和性能禁令。本条目承接纠偏背景、职责矩阵、场景模板边界、控制请求定义及迁移前结论。

## 当前职责边界

- Core 可以拥有 Awake/Update、Domain 注册和调度；Domain 可以拥有生命周期、域级缓存/规则/协调；Module 负责具体功能。不得把 Domain 误当纯容器，也不得膨胀成巨型实现类。
- 角色外壳只规范整体结构、引用、统一入口和旧 Entity/新模板桥接，不成为接管玩家、AI、剧情、网络的第二套控制系统。
- 若 EntityAIDomain 承载意识、输入、调度和控制来源，玩家输入、AI、剧情、网络、载具和状态限制应先在 AI 域收集/仲裁，输出本帧控制请求，再由 Basic/State/Skill/战斗模块执行。
- 第一阶段只需要薄外壳（可选）、一个控制来源模块和一个轻量控制请求数据；不得一开始铺设大量玩家/AI/剧情/网络/载具/眩晕脚本。只有验证后才考虑独立 EntityControlDomain。
- 控制请求只是本帧移动、朝向、跳跃、交互、技能和接管结果；移动由 Basic/KCC，技能由技能模块/状态机，表现由 State/IK，镜头由相机系统执行。
- Buff 与 Equipment 维持已有域职责；Buff 的实例/叠层/Permit/OpSupport 和 Equipment 的 Inventory/Slot/Attachment/效果来源不得回流 Combat 或按空域重建。

## 场景与性能边界

层级模板可以分离根、运行逻辑、碰撞、表现、IK/挂点、装备、特效音频、相机点和运行生成区，但模板不等于已跑通工业 Prefab；Runtime/Save/Network 快照和控制链仍需独立验证。热路径禁止每帧 Find、反射、字符串查找或动态获取模块；初始化缓存引用，控制请求优先 struct/复用对象，避免每帧分配。

## 不可升级的结论

本条目是架构纠偏与导航，不授权新增 Domain、脚本、Prefab、源码或运行时改造。源码、静态编译、Unity、PlayMode、Profiler、Player、IL2CPP 和发布证据各自独立，不能用本条目或静态检查互相替代。

## EvidenceRefs

- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Core.cs`
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Domain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/模型重构_今日修正_CoreDomain与AI域控制_AI协作警告.md` (`4d167fd7670ba3cb848fb70dcd27dc3b373538d02fd639a32714afbd246426a2`)
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Core.cs` (`4e3c4d6cab28401a466a4d0ff80061c2fdc81299dd4660cedca50f86769d8cd4`)
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Domain.cs` (`4adb66b6792a6198b6d002f93ed91556d471884c150574a7287aecfd8626ab77`)
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Module.cs` (`56e3d764de427a9550d1acc0e4678ce0ee15228e9dec675f67c25f025c142613`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs` (`28578ef54995dbcc085e7856e237bffb0292914d7b3bcae34b8152b470a99b05`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs` (`1d2a4bd6f45cfc7841b6a0c226798370d85684fd92fc1303df70334b409a76f1`)
