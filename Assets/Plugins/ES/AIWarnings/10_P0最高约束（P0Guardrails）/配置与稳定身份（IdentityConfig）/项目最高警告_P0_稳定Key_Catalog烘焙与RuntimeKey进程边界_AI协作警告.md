# P0：稳定 Key 必须经 Catalog 烘焙，RuntimeKey 仅本进程权威

`Status`: `current`
`StableId`: `es.aiwarning.p0.stable-key-catalog-runtimekey.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `stable-key`, `catalog`, `runtime-key`, `schema-hash`
`Applicability`: GameCore ConfigKey、GameTag、属性、输入、任务、装备、能力、状态及跨资产/版本/存档/网络/DLC/Mod 身份。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-stable-key-catalog-runtimekey.md`
`StaleWhen`: Catalog/Schema、RuntimeKey 生命周期、Key 迁移、AI 内容协议或 SourceRefs 变化。

## 长期 P0 约束

- 跨对象、配置、版本、存档、网络、DLC 或 Mod 的业务身份必须是带 Scope 的 EnumKey/StringKey，经确定性 Catalog 由定义 Schema 生成 RuntimeKey + SchemaHash；局部单 Owner 容器键不升级，进入外部契约则立即升级。
- RuntimeKey 只对当前进程、Catalog 和生命周期有效，不是配置/存档/网络身份，也不可手工指定；HotSlot/Sparse 只改变存储策略。别名必须在同 Scope 同定义解析同 RuntimeKey，冲突为构建错误。
- 禁止持久化/跨进程传 RuntimeKey，禁止按注册/数组顺序、InstanceID、GUID、路径、Address、Bundle Hash、显示名恢复；禁止热路径裸 StringKey 查表、隐式创建、旧 RuntimeKey 双写/fallback 或跨 Catalog 解释。
- 稳定 Key 必须声明 Scope、值类型、Schema、存储策略、迁移信息和 owner；Catalog 按稳定身份排序构建。配置/存档/网络/Mod 只保存稳定身份和必要 SchemaHash，启动/切换时重新解析，跨版本不匹配必须迁移或安全拒绝。
- 编辑器审计重复/歧义别名、类型、未使用项、所有者、稳定 ID 冲突；失败 fail-closed。不得把所有 Dictionary<string> 全局 Catalog 化，也不得为未有正式 Table/Consumer 的领域预建万能空 Key。
- AI 内容协议只能使用强类型稳定 Key 和结构化参数；RuntimeKey、Handle、InstanceID、委托、自由字符串、裸 SO/Prefab/GameObject/Transform 不得成为 AI 或 Player 权威。正式内容须形成 Info/Group→GameCore→强类型 Table→Consumer 链。
- 输入档案只保存 configId、SchemaHash、bindingId、schemeId，不保存 RuntimeKey；不匹配时拒绝迁移并使用当前配置默认键位。资源和 GameCore 继续遵守各自 P0 反向边界。

详细模型、AI 内容准入、当前入口和原文快照见 Knowledge：`es.aiwarning.p0.stable-key-catalog-runtimekey.v1`。
