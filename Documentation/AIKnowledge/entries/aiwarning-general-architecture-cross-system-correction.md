# 通用架构跨系统纠偏

`KnowledgeId`: `es.aiwarning.general-architecture-cross-system-correction.v1`  
`Authority`: `AIWarnings + current cross-system source`  
`RouteKeys`: `aiwarnings`, `architecture`, `cross-system`, `domain`, `module`, `facade`, `runtime-state`, `hot-path`, `buff`, `input`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `98a41d60faa73e4a27e7917250917b59ba7830f25a86c51b5339dc3ffbf2ab00`  
`SourceSetHash`: `98a41d60faa73e4a27e7917250917b59ba7830f25a86c51b5339dc3ffbf2ab00`  
`EntryBodyHash`: `e115471ccc250bdbea7111066e1885a6303c212bb81e6ae8cc678c40980bffaa`  
`StaleWhen`: `Core/Domain/Module、Entity、Input、Buff、State、Item、GameManager 或跨系统边界变化。`

## 迁移说明

原 Warning 243 行、8,805 UTF-8 字节；现 Warning 保留“边界清晰、运行态可释放、热路径可缓存、表现可桥接、编辑器不改运行语义”的长期原则。本条目承接跨系统纠偏、职责矩阵、Buff/AITalk 判断和自检清单。

## 稳定职责原则

- 底层协议管规则，Domain 管域级协调，Module 管具体能力，Facade 管结构和入口，Binding 管跨层翻译，Runtime State 管运行变量，DataInfo 管配置声明，Editor 管制作体验。
- Entity/Identity 是角色运行根与定义入口；AI 域管理控制来源/请求，Basic 域执行身体能力，State 域管理状态/动画/IK，Equipment 与 Buff 保持各自实例和效果边界。不得用同义外壳或第二套根系统接管全部控制。
- State 表现层不拥有属性合成、Buff 叠层、驱散、权限、伤害、背包或装备规则；表现桥接不能反向夺取逻辑主权。
- ValueChange 管持续值/权限合成/缓存，Expression 在刷新时求值为 primitive，Op 管一次动作或 Start/Stop，Buff 管生命周期/来源/叠层/驱散/事件；热结构不得持有 Unity Object、字符串 Key 或目标 Entity。
- Item/Shot 高频 Tick 只做运动、命中候选、到达/过期/停止和 NonAlloc 检测；事件发生时才进入 Op/Expression/Support。禁止每帧反射、LINQ、字符串查找、动态 GetComponent/模块查找和临时分配。
- Domain 是大边界，能力点优先放 Module；不要为“完整”拆出大量 Motion/Collision/Lifetime/Presentation Domain。允许业务缓存的服务对象应稳定，重建换内部数据；必须替换时提供 Rebind。
- 可复用 Player/Calculator/Data 只存配置和结构，实体本次运行变量放 State.UserData/Instance/RuntimeData，防止多实体复用时串号。Entity Inspector 只做总览，Domain 自己负责完整绘制。

## Buff 与协作协议

Buff 扩展复用 ValueChange、Permit、BuffInstance、Expression Binding、Op Trigger 和 VisualBridge 链路；必须可释放 token、可清理 owner/source、可解释叠层/优先级，并隔离表现与数值。AITalk 若启用，必须使用绝对路径和 Session、读取协议/参与者/历史、持续轮询、声明修改权限、区分公开/私密信息并由主持者分配权威身份；最终结论主动回报用户。

## 跨系统自检

改运行时架构前确认对象是在管配置、运行实例、服务、桥接还是表现；运行变量是否进入共享对象；是否为了统一增加 Domain/脚本；热路径是否有反射/字符串/临时集合；token/owner/source/version 能否释放；缓存引用重建是否保留；Expression 是否离开热路径；编辑器排版是否改变运行语义；是否读了当前源码而非旧讨论。

## EvidenceRefs

- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Core.cs`
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Domain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_Law/INTER_IESMotionInfluenceReceiver.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/通用架构理解_跨系统纠偏_AI协作警告.md` (`cd4c04a5bb3cd6e6852f3f0d706fce06bed579e6203ce9cd78d39d1cf79e860d`)
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Core.cs` (`4e3c4d6cab28401a466a4d0ff80061c2fdc81299dd4660cedca50f86769d8cd4`)
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Domain.cs` (`4adb66b6792a6198b6d002f93ed91556d471884c150574a7287aecfd8626ab77`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_Law/INTER_IESMotionInfluenceReceiver.cs` (`52e6b7eb20a456207a21dda9ab385704e98032b3fdd2e7c0bffa1df2021807f6`)
