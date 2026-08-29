# P0 长期序列化与成熟泛型容器边界

`KnowledgeId`: `es.aiwarning.p0.generic-container-serialization-boundary.v1`  
`Authority`: `AIWarnings + current generic-container source`  
`RouteKeys`: `aiwarnings`, `p0`, `identity`, `serialization`, `generic`, `container`, `unity`, `il2cpp`, `migration`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `c8bd90155fbc6c85edcdc62da455528fbf717e6c0f67f81e9acef09ac77ac7e9`  
`SourceSetHash`: `c8bd90155fbc6c85edcdc62da455528fbf717e6c0f67f81e9acef09ac77ac7e9`  
`EntryBodyHash`: `58746c4546d5f37cde5ef469f9ae02cc65df2e3b61e7eef11036fcdc407f020e`  
`StaleWhen`: `Unity/Odin 序列化、ESEnumStringMirrorMap、领域具体类型、兼容阶段或 SourceRef 哈希变化。`

## 迁移说明

原 Warning 181 行、14,167 UTF-8 字节；现 Warning 保留“保留通用底座、长期合同使用领域具体类型、兼容门禁和开发期数据政策”等 P0 边界。本条目承接判定范围、继承/组合矩阵、迁移步骤、证据分层和 AI 禁令。

## 长期合同规则

- `ESEnumStringMirrorMap<TEnum,TValue>` 等通用底座必须保留、复用并独立测试；本规则不禁止泛型，也不授权删除、禁用、复制底座。
- 进入 Unity/Odin/Prefab/Scene/ScriptableObject/存档、GameCore/RuntimeData 权威表、跨模块公共 API 或 IL2CPP/AOT 稳定边界的闭合泛型，应由真实领域职责的 `sealed` 具体类型承载。
- 具体类型至少承担真实可验证职责之一：固定序列化/AOT/迁移身份、维护不变量/冲突策略、版本迁移、作者态与运行时隔离、领域 API/诊断/Inspector/发布门禁；仅固定参数、改名或空壳 `sealed` 不足。
- 只因不希望普通用户调用继承 API，不得改成组合或只读 View；ES 自有真实内部入口使用 `Internal_` 前缀。组合只在独立序列化布局、版本、安全或程序集边界成立时采用。
- 短生命周期 Pool/Lease/Handle/Scheduler/Builder、局部算法、普通 BCL 集合和底座自身默认不命中，不能因扫描结果机械包装。

## 兼容迁移门禁

正式发布内容、玩家存档、UGC、外部版本协议、不可丢失生产数据或用户明确要求保留的数据变更结构前，必须识别受影响资产/版本/API，保留旧载荷读取或桥接，完成全量预检、备份与哈希记录，受控写入，保存后卸载/重载等价比较；任一失败必须停止并逐文件恢复、复核哈希，不能宣称回滚成功。`FormerlySerializedAs` 不能证明容器换型、字典拆并或 Odin/Unity 后端迁移安全。

ESFramework 仍处于开发期时，框架自身旧 Prefab、Scene、SO、测试/示例资产和开发存档默认允许破坏性切换；这只免除旧格式兼容责任，不证明新格式、Unity 往返、PlayMode、Player、IL2CPP 或发布正确。交付必须列出会重置的数据类别，并验证新格式能保存、卸载、重载和被真实消费者读取。

## 证据与自动化边界

源码检查只证明类型关系和入口存在；静态编译、内存回调、Unity Serializer 往返、真实资产迁移、Test Runner/PlayMode/Player/IL2CPP 各自独立，不能互相冒充。禁止扫描命中后自动删除/替换泛型、批量生成空壳、复制算法、无迁移器改变兼容数据，或在无用户授权时写 Git、历史、审计状态、发布状态和旧资产。

## EvidenceRefs

- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/Container/DictionatyPro/ESEnumStringMirrorMap.cs`
- `Assets/Plugins/ES/0_Stand/Tests/ValueChange/ESEnumStringMirrorMapTests.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_长期序列化与成熟核心泛型容器具体类型边界_AI协作警告.md` (`4be8a11146dcaf71a308f98e5ac946e9f068d8898ab586b42dbf9e4bec35732d`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/Container/DictionatyPro/ESEnumStringMirrorMap.cs` (`068bbb052c3020368d1c6b72684dcb18d8f1d1d914931ac2b29e015de2d2a3b2`)
- `Assets/Plugins/ES/0_Stand/Tests/ValueChange/ESEnumStringMirrorMapTests.cs` (`8afce3740ce5639b5f8283e3e34f3b51a4d317afbffc9d49e606e44330fe0149`)
