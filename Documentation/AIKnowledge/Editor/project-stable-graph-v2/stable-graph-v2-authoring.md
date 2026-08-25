# Stable Graph V2 稳定身份、Undo、迁移与烘焙边界

`KnowledgeId`: `es.project.stable-graph-v2.v1`  
`Authority`: `Current source + AIWarnings domain rule + Unity 2022.3 official documentation`  
`RouteKeys`: `editor`, `graph`, `stable-graph-v2`, `graph-identity`, `graph-undo`, `graph-migration`, `edge-order`, `graph-snapshot`, `graph-bake`, `legacy-graph`  
`RequiredReads`: Graph P0、稳定身份与命名 P0；涉及 Story 当前权威时读取 DataInfo、Snapshot/Catalog 与 Module 源码；选择 Managed Automation/AIBrain 执行通道时再读 Stable Graph AICommand 与 Automation TaskContract
`ContentHash`: `a584a63d09c6c9a30cf8400f59dc63256a8d90d116f47a31be0ebf91c718e186`

## Scope

本条目描述 ESFramework 在 Unity `2022.3.45f1`（revision `a13dfa44d684`）下的 Stable Graph V2
作者数据边界。它只总结当前源码、现行 Graph 规则、测试源码和 Unity Undo 官方 API；不修改或替代
Graph 资产、AIWarnings、AICommand、KnowledgeIndex、消费者产物或运行时合同。

本条目不负责通用 Unity SerializedObject、Prefab Override、Dirty/Save API，也不负责 PlanHash、TaskContract、RunRecord 或 SkillCall 编排。前者由 `es.unity.editor-serialized-undo-dirty.v1` 负责，后者由 `es.project.automation-aibrain-graph.v1` 负责。

## Trigger and routing

- 自然语言触发：Stable Graph、GraphView V2、Node/Port/Edge 稳定身份、`edge.order`、Graph Undo、Schema 迁移、Graph Bake/Snapshot、Legacy Graph/NodeRunner。
- 精确路由：`graph-identity`、`graph-undo`、`graph-migration`、`edge-order`、`graph-snapshot`、`graph-bake`、`legacy-graph`。
- 相邻误路由：Task Read Snapshot 只处理文件读取一致性；Automation Graph 只处理 TaskContract/RunRecord/SkillCall；Story 条目拥有迁移前 DataInfo 作者权威。
- 若只出现泛化 Graph 或 Snapshot 且无法判断作者数据、执行编排还是文件读取，停止并请求领域信息。

## Decision rules

- SourceRef、ContentHash、Graph P0、Schema 或 Unity 版本任一漂移时标记 stale，回读当前来源后再继续。
- 修改作者资产前必须确认唯一 Owner、稳定 Graph/Node/Port/Edge 身份、Undo group、Dirty/Save 和失败回滚。
- 缺少显式迁移、端点语义不完整、跨 Domain 不兼容或 Legacy 恢复请求时必须停止。
- 只有静态源码和测试定义时保持 `runtime-not-run`；声称真实 Undo、序列化往返、Bake、Player 或发布通过前必须取得相应运行证据。
- 选择 Managed Automation/AIBrain 通道执行 Graph 时转读 Automation 条目，并要求 AICommand、TaskContract
  与 RunRecord；直接领域运行时消费者不受该 AI 传输协议约束，本条目自身也不授权执行。

## Verified facts

### 唯一作者权威与稳定身份

- 对已经接入 Stable Graph V2 的领域，`ESGraphAssetBase` 及其具体 Graph 资产类型是该 Graph 的作者数据权威；
  GraphView 是 Editor 投影，不拥有业务数据。Story 是迁移期例外：`ESStoryGraphAsset` 尚未接入现行链，当前仍由
  `ESStoryDefinitionDataInfo` 拥有正式 Story 作者数据，Graph 不得成为同一内容的第二权威。
- Graph 保存 `graphId`、`originGraphId`、`schemaVersion`、Node、Port 和 Edge。NodeId、PortId 与 EdgeId
  是序列化身份；端点语义还由 `NodeId + StableKey` 表达。独立复制会生成新的 GraphId，并保留合法的
  OriginGraphId；新增、复制或粘贴节点/连线时会为新记录生成新的稳定身份。
- Domain 由具体 Graph 资产类型提供。连接必须通过模型层校验方向、类型、容量、重复关系、循环和领域规则；
  画布位置、显示名和数组偶然顺序都不是身份或连接语义。

### 拓扑术语与消费者产物命名

- `TopologyAnalyzer`: `ESGraphTopologyAnalyzer`
- `MultiEndpointRule`: `TwoOrMoreValidIndependentEndpointsInSameDirection`
- `SingleEndpointMultiConnectionIsMultiEndpoint`: `false`
- `BehaviorTreeProgramState`: `ReservedNotImplemented`
- `StoryAuthorAuthority`: `ESStoryDefinitionDataInfo`
- `StoryGraphIntegrationState`: `TypeExistsNotConnectedToDataInfoSnapshotChain`
- `ManagedProtocolRequiredWhen`: `ManagedAutomation/AIBrain`
- `CommercialState`: `Graph=Verifying; Story=Verifying; BehaviorTreeProgram=NotImplemented`

- 多端点节点表示同一方向声明了至少两个有效且独立的稳定端点；单个端点即使 `capacity == Multi` 且实际承载
  多条 Edge，仍只是单端点多连接。端点数量、允许多连接的端点数量和实际连接数是三个独立维度，不得互相推导。
- 参与拓扑计数的端点必须具有有效的 PortId、StableKey、Direction 与 Meaning，PortId 在 Graph 内唯一且
  StableKey 在节点内唯一；身份无效或重复的非空记录不参与多端点计数，并通过
  `InvalidEndpointRecordCount` 报告。空 Port 记录由 Graph validator 拒绝，Analyzer 自身跳过该记录。
- 项目统一通过 `ESGraphTopologyAnalyzer` 计算这些拓扑事实；编辑器显示、验证器和消费者不得用 Edge 数量、
  端口容量或节点类型名自行猜测 `IsMultiEndpointNode`。
- `Program` 后缀当前且唯一保留给不可变的 `ESBehaviorTreeProgram`，不得建立通用 `ESGraphProgram`；
  该 Program、Compiler 与 Runner 当前尚未实现，这里只定义后续实现必须遵守的命名合同。Story 消费者继续
  使用 `ESStoryDefinitionSnapshot`；在完成并验收显式迁移前，Story 的可变作者权威仍是
  `ESStoryDefinitionDataInfo`。通用 Graph 烘焙产物仍是 `ESBakedGraphSnapshot`，不能替代领域专属产物。
- `ESStoryGraphAsset` 与 `ESBehaviorTreeGraphAsset` / Profile 作者类型已经存在；前者尚未接入现行
  DataInfo -> Snapshot 链，后者尚未接入 Program / Compiler / Runner。类型存在不能冒充消费者闭环已实现。
- Stable Graph V2 与当前 Story 骨架成熟度均保持 `Verifying`；BehaviorTree 执行链仍是未实现合同。
  源码、静态分析器或测试定义存在不等于商业验收完成，三者均不得声称 `Stable`、商业验收或已发布。

### `edge.order` 是作者顺序

- `ESGraphEdgeRecord.order` 是关系的持久化作者顺序。新边取得新的 EdgeId 和下一个 order；重连保留
  EdgeId 与原 order；`MoveEdge` 只交换同一语义顺序组内的 order，并由编辑服务形成一个 Undo 事务。
- 有序输入按目标端点分组；其他多路关系按来源端点分组。读取时先按 order、再按 EdgeId 做确定性兜底。
  EdgeId 兜底只处理非法重复顺序、迁移和诊断，不能替代业务顺序。
- Bake 的内容签名显式写入每条边的 EdgeId、输出/输入 PortId 和 order，因此顺序变化会改变签名并使
  消费者快照或缓存失效。

### Undo 与原子修改

- `ESGraphEditService` 是受控作者修改入口。单对象修改使用 `Undo.RecordObject`；创建并连接、插入、重连、
  调序和 Schema 升级使用完整对象 Undo group。成功路径 collapse 后调用配置的 mark-dirty、autosave 和
  model-change 回调，失败路径 `Undo.RevertAllDownToGroup`，不得遗留半个节点、半条边或部分迁移。
- 现有测试源码覆盖重连后 Undo/Redo 保持 EdgeId、插入失败回滚、Undo 恢复资产修改，以及布局变化不使
  Bake cache 失效、内容变化使 cache 失效。这些是测试用例存在的静态事实，本轮未运行测试。
- Unity 2022.3 官方文档区分 `Undo.RecordObject` 与 `Undo.RegisterCompleteObjectUndo`；项目服务按修改粒度
  使用两者。`EditorUtility.SetDirty` 只用于标记对象已修改，不替代 Undo 记录。

### 显式迁移

- 当前 `CurrentSchemaVersion` 为 `4`，最小支持版本为 `1`。`TryUpgradeSchema` 先检查版本范围、节点/端口
  完整性、可迁移端点用途、EdgeId 唯一性和端点存在性，再一次性写入端点 meaning、迁移 order 与新版本号。
- Schema 升级不改变 Graph、Node、Port 或 Edge 身份。旧 Schema 迁移 order 时按 EdgeId 排序只是为缺失
  顺序建立确定性初值；正常作者顺序仍以持久化 `edge.order` 为唯一权威。
- 跨 Domain 粘贴、未知端口、无显式迁移的旧 Schema 和类型不兼容的内部边在修改前被拒绝。迁移失败必须
  保持原资产不变；不能在运行或 Bake 时静默猜测并修补。

### Bake Snapshot 与缓存

- `ESGraphSnapshotBaker` 先运行 Graph 校验；存在 Error 时不产出 Snapshot。节点和边按稳定 ID 排序后写入
  `ESBakedGraphSnapshot`，Snapshot 复制节点、端口、边和路由，并暴露只读集合与内容签名。
- Snapshot 是验证快照/消费者编译输入，不是通用 Runner。消费者必须生成自己的不可变产物并遵守领域运行时
  所有权边界；只有选择 Managed Automation/AIBrain 通道时才额外遵守 TaskContract、AICommand 与 AIBrain。
- `ESGraphBakeCache` 在读取和存储时逐项比对 Schema、Domain、GraphId、OriginGraphId、循环策略、节点、
  端口、Edge 及 order。布局变更不影响 Bake；结构、内容或 Schema 变更会使缓存失效。

### Legacy 禁用边界

- 现行 Graph 规则声明旧 `Assets/Plugins/ES/Editor/ESGraphView` 和
  `Assets/Plugins/ES/1_Design/Define/0Define-NodeRunner` 已删除，禁止恢复 `ESGraphViewWindow`、
  `NodeRunnerSO`、`NodeContainerSO`，也禁止换名复制可变 ScriptableObject Runner。

## Derived authoring rules

1. 修改 Graph 必须保持单一作者权威：资产负责可变作者数据，Snapshot/消费者产物负责冻结读取，窗口不持有
   第二份业务状态。
2. 区分多端点节点与单端点多连接，并复用 `ESGraphTopologyAnalyzer`；不得从容量或 Edge 数量反推端点拓扑。
3. 所有顺序语义必须落在 Edge order 并进入迁移、Undo、签名、Snapshot 和消费者产物；不得增加节点私有
   顺序表或从坐标、数组位置、EdgeId 推导正常业务顺序。
4. Schema 变化必须提供显式、可重复、失败不变更的迁移。无法证明身份与端点语义时应阻断，而不是补默认值
   后继续 Bake。
5. Undo 必须覆盖一个完整用户意图；多步修改失败时回滚整个 group，只有成功结果才 Dirty、通知和失效缓存。
6. Legacy 兼容需求必须重新设计为 V2 migration 或消费者适配，不能恢复旧 GraphView/NodeRunner 权威链。

## Common AI failure modes

| 错误行为 | 典型症状 | 预防检查与恢复 |
|---|---|---|
| 用坐标、数组下标或 EdgeId 推导正常顺序 | 重排后行为漂移、签名不稳定 | 检查 `edge.order` 是否进入迁移、Undo、Snapshot 和签名；失败时回滚完整事务 |
| 把单端点多连接误报为多端点节点 | 分支语义、验证和 UI 统计不一致 | 使用 `ESGraphTopologyAnalyzer` 分别检查端点数量、容量和实际连接数 |
| 为 Story 或通用 Graph 创建 `Program` | 与 BehaviorTree 的唯一命名合同冲突 | 保留 `ESBehaviorTreeProgram`；Story 使用 `ESStoryDefinitionSnapshot`，通用 Graph 使用 Snapshot/领域产物 |
| GraphView 保存第二份业务数据 | ReloadDomain 后窗口与资产冲突 | 确认资产是唯一作者权威；丢弃窗口缓存并从资产重建投影 |
| 多步编辑只记录部分 Undo | 失败后遗留半节点/半边 | 一个用户意图对应一个 Undo group；失败执行 `RevertAllDownToGroup` |
| 运行或 Bake 时猜测迁移 | 未知端口被补默认值后继续 | 预检 Schema/端点/身份；无显式迁移则 Blocked，保持原资产不变 |
| 把 Snapshot 或测试源码存在当成运行成功 | 没有 Unity 回执却称已验证 | 记录 `runtime-not-run`，补 Test Runner、ReloadDomain 和序列化证据 |
| 恢复 Legacy NodeRunner | 出现第二套可变 SO Runner | 拒绝恢复，改为 V2 migration 或消费者不可变产物适配 |

## Execution checklist

1. 开始前读取 Graph P0、稳定身份 P0、当前条目和 SourceRefs；确认最多三个路由条目。
2. 实施中检查 Owner、稳定身份、端点语义、`edge.order`、Undo group、Dirty/Save、缓存失效与回滚。
3. 完成后验证迁移幂等、失败不变更、重复执行、Undo/Redo、Snapshot 签名和 Legacy 拒绝路径。
4. 涉及 Prefab、AssetDatabase、序列化或 Domain Reload 时补读对应 canonical 条目，不在本条目复制通用规则。
5. 未取得 Unity 运行证据时禁止声称可用、Stable、商业级或发布完成。

## Evidence boundary and non-claims

- 本条目假定当前读取到的源文件与下列 SHA-256 对应；任一哈希变化后条目立即 stale。
- Legacy 禁用结论来自现行 Graph 规则；本条目不据此断言序列化资产、Library 缓存、外部包、Player 或
  历史分支中绝对不存在旧引用。
- 测试源码存在不等于测试已运行。本轮没有启动 Unity Editor、没有执行 EditMode/PlayMode Test Runner、
  没有验证 ReloadDomain、真实 Undo UI、资产序列化往返、迁移 replay、失败恢复、Profiler、Player/IL2CPP
  或发布链。
- 因此证据状态为 `runtime-not-run`；不声明 Stable Graph V2 已从 `Verifying` 晋升为 `Stable`，也不声明
  Agent、BehaviorTree、Story 或其他消费者已经完成商业接入。

## Official documentation

- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.RecordObject.html
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.RegisterCompleteObjectUndo.html
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorUtility.SetDirty.html

以上 Unity 2022.3 官方页面于 `2026-08-23` 取得 HTTP 200；它们解释 Unity API 语义，不替代项目源码与
Graph 领域规则的具体约束。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/GraphView与NodeRunner_数据权威稳定身份与重构门禁_AI协作警告.md` (`4a1bde6f96bad3461178fc0385d3e4b26eb7184ea7efc92de3879abb9f042d44`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` (`ffd47f75089d13023597277357ce63bcd6c05b6e97d2683a7a5e64e33e234649`)
- `Assets/Plugins/ES/1_Design/Graph/ESGraphAssetBase.cs` (`e38cd48eb34968a92149476cb14cbc655a245fa7f7eb1879c164acad95722482`)
- `Assets/Plugins/ES/1_Design/Graph/ESGraphSnapshot.cs` (`4ab11772427a93f78324bee992c8f14f9c92deb8c3e861f5c651c3fc697f4d00`)
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESGraphEditService.cs` (`9583044a3dff31f12a1a5a4c8be2c9a4d932a67e01a45c1cc0da96cd0f0f887e`)
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESGraphBakeCache.cs` (`f2078b506bd3794ed3e2516fdff3401abc56195798111b6879baeffaf46fec79`)
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESGraphAuthoringProfiles.cs` (`c4c079c55ae0cb1dd3ba004cd77f84aa45ee4117ba149ef62f527a35a041b010`)
- `Assets/Plugins/ES/1_Design/Tests/ESGraphAssetTests.cs` (`0a056cd8793e61acc1202ea2545c1be3d107ab667093f247314d774e6f6b95a6`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESStoryDefinitionDataInfo.cs` (`d5ee735d61295abc126c44af74792c0e133c7d77f56683e328b49d0fe718e0bf`)
- `Assets/Scripts/ESLogic/Runtime/Story/Definitions/ESStoryDefinitionCatalog.cs` (`df7be43d2e524d1c50a2bc3f6ab1c62831e64d6624b1c2d3ab0cf4f84db83231`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESStoryModule.cs` (`900f1edc2c816b2e47e8a28fca08eb47e48c9f28ccbd645df548d190f788c78c`)
- `.agents/skills/es-stable-graph-authoring/references/graph-contract.md` (`7605511a47992d94797b1f5eda98ce107eda1272784454e82ef0944f8d5368ac`)

`EvidenceLevel`: `S1`  
`StaleWhen`: Unity 版本、Graph P0、Schema/身份/端点/order、编辑事务、迁移、Snapshot 签名、Bake cache、Legacy 禁用边界或任一 SourceRef 哈希变化。
