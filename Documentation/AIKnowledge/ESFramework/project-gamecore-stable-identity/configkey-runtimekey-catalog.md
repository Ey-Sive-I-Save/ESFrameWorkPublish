# ConfigKey、Catalog 与 RuntimeKey 身份边界

`KnowledgeId`: `esframework.project.configkey-runtimekey-catalog.v1`
`Authority`: `Source + AIWarnings`
`RouteKeys`: `gamecore`, `config-key`, `stable-key`, `enum-key`, `string-key`, `catalog`, `runtime-key`, `schema-hash`
`ContentHash`: `898ed00ae6d8734e1c2fd70c54e617baf1933caa0ad4429b257fe83fc2cec70b`
`EvidenceLevel`: `S1 / runtime-not-run`

## Summary

跨资产、版本、存档、网络或外部协议的身份必须使用带 Scope 的 EnumKey/StringKey。两种别名都存在时，它们必须在同一 Catalog 定义中解析到同一项；类型、Scope、Schema 或别名冲突必须 fail-closed。

`ESKeyCatalog.TryBuild` 对稳定身份排序后再分配稠密 runtimeKey，因此声明或加载顺序不是运行时 ABI。Catalog 还生成 SchemaHash，并通过 CatalogName + SchemaHash 做兼容握手。这里的“稠密 runtimeKey + 完整声明 SchemaHash”是 `ESKeyCatalog` 的合同，不能泛化到所有旧的 `ESConfigKeyTable` 路径：后者可能按 EnumKey 直接映射，或为 StringKey 生成表内临时键，且其 SchemaHash 字段覆盖面不同；跨表/跨进程仍必须按稳定身份重新解析，不能复用这些整数或把两种哈希当成同一协议。

RuntimeKey 只属于当前进程、当前 Catalog/强类型表及其当前生命周期。它可在初始化边界解析后用于热路径，但不得写入 ScriptableObject、JSON、存档、网络、Manifest、发布 Catalog 或跨进程缓存，也不得从注册顺序、数组位置、InstanceID、GUID、路径、显示名或 `KeyName` 恢复。

兼容性边界：`ESConfigKeyTable` 的裸 String 注入/临时 RuntimeKey 只允许作为当前表的内部兼容路径；未注册 StringKey 不得因此获得可持久化或可跨表解释的身份。需要持久化、网络或迁移时，必须先有正式 Scope + EnumKey/StringKey 声明及明确 Schema/迁移规则，否则拒绝并回退安全默认。

`ESGameCoreConfigKey<TEnumKey>` 的 EnumKey/StringKey 是业务稳定身份；其中的 definition GUID/LocalFileId/type metadata 用于编辑器精确选择和烘焙核对，不会把资产身份变成运行时业务键。

## AI 身份选择决策

按以下顺序判断，禁止从“已有字段最方便”反推身份：

| 问题 | 是 | 否 |
|---|---|---|
| 键只在一个 Owner 实例内创建、销毁和解释？ | 使用局部类型键；不要全局 Catalog 化 | 继续判断 |
| 身份会跨资产、版本、存档、网络、DLC、Mod 或外部数据？ | 声明 Scope + EnumKey/StringKey + Schema + owner + 迁移 | 继续判断 |
| 目标是精确 Unity 主资产/子资产？ | 使用 GUID + LocalFileId + type；主资产 LocalFileId 归一为 0 | 继续判断 |
| 目标只是当前表内高频访问？ | 在表构建/注入后解析 runtimeKey，并绑定当前表生命周期 | 不得为了性能预建万能 Key |

稳定业务 Key 与资产身份可以同时存在，但职责不同：业务 Key 回答“这是什么定义”，GUID/LocalFileId 回答“当前作者资产是哪一个”。两者必须在编辑器选择、预检或 Bake 边界交叉校验，不能互相 fallback。

## Catalog 构建与消费纪律

1. 声明完整的 Scope、Enum/String 别名、值类型、Schema、默认/范围/公式、迁移信息和 owner。
2. Catalog 按稳定身份确定性排序后分配 runtimeKey；禁止依赖声明、加载或注册顺序。
3. 两个别名存在时必须落到同一定义和 runtimeKey；冲突直接阻断构建。
4. 配置、存档、网络和外部协议只保存稳定 Key 与需要的 SchemaHash；新进程或新表重新解析 runtimeKey。
5. 跨端/跨版本先比较 CatalogName/Scope 与 SchemaHash；不兼容则执行明确迁移或拒绝。
6. 热路径可缓存 runtimeKey，但必须同时绑定表/Catalog 生命周期；Clear/Rebuild 后旧值立即失效。

StringKey 按原值保存和比较。AI 不得 Trim、改大小写、替换字符、从显示名生成，或在冲突时悄悄建议另一个字符串来绕过身份错误。

## 常见错误输入与唯一动作

| 输入或现象 | 正确动作 |
|---|---|
| 只有 `KeyName`，显式 ConfigKey 为空 | 判配置无效；不得生成 StringKey 或 runtimeKey |
| 两个 Info 的 `KeyName` 相同、ConfigKey 不同 | 编辑器组织冲突单独处理；不得判运行时身份冲突 |
| 两个 Info 的 ConfigKey 相同、`KeyName` 不同 | 明确重复业务身份；拒绝注入 |
| EnumKey 与 StringKey 分别绑定不同定义/实例 | 阻断构建或提交；不得静默合并 |
| 拿到旧 runtimeKey、跨表 runtimeKey 或存档中的整数 | 丢弃并用稳定 Key 在当前表重新解析 |
| GUID/path/name 与业务 Key 不一致 | 以精确资产身份定位，再按声明表核对业务 Key；不得任选其一覆盖 |
| CatalogName/SchemaHash 不匹配 | 迁移或拒绝；不得继续解释对端 runtimeKey |
| 未注册 StringKey 在运行时出现 | fail-closed；不得隐式创建定义 |

## 修改前不可跳过检查

- [ ] 已区分局部键、业务稳定 Key、精确资产身份和 runtimeKey。
- [ ] 稳定 Key 有 Scope、强类型域、owner、Schema 与迁移边界。
- [ ] StringKey 保持原值；没有 Trim、大小写归一化或自动生成。
- [ ] 双别名解析到同一定义；类型、Scope、Schema 与别名冲突均 fail-closed。
- [ ] runtimeKey 只在注入/构建成功后可用，没有进入 SO、JSON、存档、网络、Manifest 或发布数据。
- [ ] Clear/Rebuild/进程切换后会重新解析 runtimeKey。
- [ ] GUID/LocalFileId/type 只承担资产身份，没有替代业务 Key。
- [ ] 未取得本次 Unity/Test 证据时，不把源码测试定义写成“测试已通过”。

## RequiredReads

- `Documentation/AIKnowledge/ESFramework/project-gamecore-stable-identity/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md`
- `Assets/Plugins/ES/1_Design/ConfigKey/ESKeyCatalog.cs`
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs`

## RelatedSkills

- `es-gamecore-config-authoring`
- `es-tag-config`
- `es-gamecore-integration`
- `es-ai-knowledge-curation`

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md` (`c6960fac99de98e02d304bca863a312314f065268f54f961f35cf61f68a847c7`)
- `Assets/Plugins/ES/1_Design/ConfigKey/ESKeyCatalog.cs` (`75a90c0ead7d2a9c22d495131eb9b9383809866106f0b46791953abd71638f03`)
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs` (`08c4fda0e5ec09db552834ff2137314aec6244709ea7d40c9c0e276a9987c33e`)
- `Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs` (`94204e17e8fb557fa80e28d400a654cd2f711d3d42ca5e372d881a2033503bff`)

## EvidenceRefs

- `Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs` defines order-independence, alias binding, inconsistent-reference rejection, process-table runtimeKey and retained-table cases; no Unity test result was produced.

## StaleWhen

Any SourceRef hash changes; Unity version changes; stable key Scope/alias rules, Catalog sort/build, SchemaHash handshake, ConfigKey metadata, runtimeKey lifecycle or persistence boundaries change; or current test/runtime evidence contradicts this S1 summary.
