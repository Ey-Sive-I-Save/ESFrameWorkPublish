# P0 Info、Group 与 Pack 边界

`KnowledgeId`: `es.aiwarning.p0.info-group-pack-boundary.v1`  
`Authority`: `AIWarnings + SoDataInfo/Group/Pack source`  
`RouteKeys`: `aiwarnings`, `p0`, `gamecore`, `info`, `group`, `pack`, `configkey`, `content`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `ecd331f3f13765368a5112251ebf755534034f4b8201b7910e56a4788e0f9172`  
`SourceSetHash`: `ecd331f3f13765368a5112251ebf755534034f4b8201b7910e56a4788e0f9172`  
`EntryBodyHash`: `611d8e558d5c7b1f2e40938b3daac257291cea08055b2fbc5ce7da1e766ae502`  
`StaleWhen`: SoDataInfo/Group/Pack、ConfigKey、Consumer 注入或内容迁移合同变化。

## 迁移范围

原 Warning 174 行、11,517 UTF-8 字节；现 Warning 保留 Info/Group 类型闭包、主 Group、ConfigKey 身份、Pack 非默认容器和资源职责隔离。本条目承接类型闭包、资产归属、Group 注入链、Pack 风险/解冻条件、音频示例和迁移矩阵。

## 当前事实与规则

- `SoDataInfo` 是单条内容定义，具体 `TInfo` 必须配套 `SoDataGroup<TInfo>`；正式 Info 恰有一个主 Group，Group 负责编辑器组织和启动聚合，Info 的显式 ConfigKey 负责运行时身份。
- Group 不播放、不持有 Voice/AssetScope、不替代 ResourcePlan/Manifest/下载包、不复制 Info 或第二套 RuntimeData；多个 Group 表示多个内容库，不改变 Info 身份。
- `SoDataPack<TInfo>` 当前只是若干 Group 的编辑器聚合视图，平铺字典容易陈旧/双权威；新增 Pack 依赖必须先明确单一职责、成员快照/刷新、Consumer 归属、版本和自动化验证。
- `KeyName` 只用于 Group 字典、SO 表格和编辑器定位，不是 ConfigKey、存档、网络或资源身份；Builder 未接入 Group 的资产只能标记孤立候选。
- GameCore/内容 Consumer 必须显式收集对应 Group，RuntimeTable 按 Info 的强类型 Key 建表；不得全盘扫描、字符串筛选、混合领域万能 Group 或让 Pack 进入资源生命周期。

## EvidenceRefs

### evidence

- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/1-SoDataGroup.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_P0_Info必须对应Group_Pack非默认聚合_AI协作警告.md` (`18c53d9c66bf892b1dcad0a8c7d24268fe6461224ab0ac6218322559ebdf2ba4`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/0-SoDataInfo.cs` (`85bd3b3512aae56da1ebd0ef0bacbc98df8dbc2a742377c531fdb197ab7fe3ae`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/1-SoDataGroup.cs` (`7e39c3e21dc67354fa174886398ee3f4fc3ec66e70903a7049f4c5a3f70f5cb9`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/2-SoDataPack.cs` (`b07d4dfb9f53dfd0ea3b36e6c9d0e9a00acca34954d30e1315d2f189d846205c`)
