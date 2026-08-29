# 项目最高警告：P0 - Info 必须对应 Group，Pack 不是默认聚合

Status: current
StableId: es.aiwarning.p0.info-group-pack-boundary
Authority: AIWarnings；SoDataInfo/Group/Pack 与 GameCore 当前源码为事实权威。
RouteKeys: aiwarnings, p0, gamecore, info, group, pack, configkey, content
Applicability: SoDataInfo、SoDataGroup<TInfo>、SoDataPack<TInfo>、GameCore Consumer、SO 表格与内容库。
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-p0-info-group-pack-boundary.md#evidence`
Owner: ES content/GameCore owners。
StaleWhen: SoDataInfo/Group/Pack、ConfigKey、Consumer 注入或内容迁移合同变化。

## 长期约束

- 每个具体 `SoDataInfo` 必须有对应的具体 `SoDataGroup<TInfo>`；正式 Info 资产恰有一个主 Group，运行时身份仍由显式 ConfigKey/RuntimeKey 提供。
- Group 只负责编辑器组织、内容库和启动聚合，不播放、不持有资源 Scope、不承担 ResourcePlan/Manifest/下载/生命周期；不得复制 Info 或制造第二套 Key。
- `SoDataPack<TInfo>` 不是 Group 同义词、资源包或默认 GameCore 容器；新增 Pack 业务依赖必须冻结，先完成独立职责、成员权威、版本/快照、Consumer 归属和验证合同。
- Builder 未接入 Group 的内容只能标记为孤立候选，不得宣称进入 RuntimeTable；多个 Group 只能表示多个内容库，不改变 Info 身份。
- `KeyName` 只是 Group/表格编辑器键，不是 ConfigKey、存档、网络或资源身份；禁止用字符串筛选、全盘扫描或混合领域万能 Group。
- 详细类型闭包、资产归属、Pack 风险/解冻条件、音频示例和迁移矩阵见专用 Knowledge；Knowledge 不授予内容迁移或发布权限。
