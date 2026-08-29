# SO 表格工具边界与安全约束

`KnowledgeId`: `es.aiwarning.so-table-tool-boundary.v1`  
`Authority`: `AIWarnings + current ESSoTableDataRule source`  
`RouteKeys`: `aiwarnings`, `editor`, `so-table`, `import`, `export`, `plan`, `batch`, `serialization`, `utf8`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `17ed8c1ebc558ccfb52bec1d3f77ff9c6ecb40ff479a098fbfb04c129b8edddf`  
`SourceSetHash`: `17ed8c1ebc558ccfb52bec1d3f77ff9c6ecb40ff479a098fbfb04c129b8edddf`  
`EntryBodyHash`: `f6d2902add482588acaa11f8919b695e77c269c10cf335a4f2e752618e672304`  
`StaleWhen`: `ESSoTableDataRule、映射/批处理协议、Group/Info 同步、编辑器命名空间或 SourceRef 哈希变化。`

## 迁移说明

原 Warning 281 行、11,768 UTF-8 字节；现 Warning 保留编译门禁、导入导出安全、计划只读、高风险确认、Group/Info 不猜测和 UTF-8 边界。本条目承接命名空间政策、表格式、子行规则、映射稳定性、结构化错误和性能上下文。

## 核心安全规则

- 修改 `ESSoTableDataRule` 或其 partial 前按相干步骤编译；Unity/Odin/反射/编辑器资产行为不能由 C# 编译单独证明。
- Export/Import 必须先做计划/断言；Batch 提供取消、执行、生成计划。计划生成只读，不写 SO、表或 SaveAssets。删除 SO/子行、清空、覆盖非空字段、重建表均需明确确认。
- CSV/XLSX 写入使用临时文件替换并在 `_backups` 创建备份；删除后清理执行缓存，避免后续批次复用已删除对象。
- 高频字段使用 checkbox/dropdown 从当前映射、列和反射候选选择，不让用户手输逗号字符串作为主流程；错误必须包含 stage、batch、表路径、行列、字段、目标类型/资产、原因和建议。
- Super Batch 尚未达到商业完成，不得把可用方向写成完整验收。

## 表与同步语义

- 标准表行顺序为 `##var/##type/##group/##comment/##assert/##rowDirective`，数据从第 7 行开始；只声明已实现的 required/unique/json/asset/range/regex 断言。
- `soFieldPath` 是稳定字段身份；导入兼容 active table name、columnName、displayName 和 soFieldPath 别名，但重复别名、改变路径或移除匹配注释均不安全。
- 子表默认稀疏：首行写 owner，后续继承非空父键；`owner` 只写 owner 字段不创建子元素。Keyed 模式空子键应警告/跳过，不得静默制造歧义。
- Group key 是容器定位；空 Group key 只能使用配置目标或唯一解析结果，多 Group 且无明确目标时不得猜测；创建 Group/Info 必须有非空有效键。
- 执行缓存仅覆盖单次计划生命周期，不得变成全局持久缓存；删除操作后必须失效。

## 命名与编码边界

稳定公共/配置类型不要盲目迁移命名空间；`ES.Internal`、`ES.Editor`、`ES.EditorInternal` 只按真实契约分层，涉及序列化、UXML、反射和菜单状态必须专项迁移。中文源文件、UI 文案和报告保持严格 UTF-8，禁止复制乱码或默认代码页重写。

## EvidenceRefs

- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/EditorOnly/InfoType`
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/SoTable/EditorOnly/InfoType/ESSoTableDataRule.ExecutionCache.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/SO表格（SOTable）/SO表格工具_AI协作说明.md` (`65430c28c5a7b968abe4b5bf16aa538f2e13991231831bd86f0ac5aaef2a8129`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/SoTable/EditorOnly/InfoType/ESSoTableDataRule.ExecutionCache.cs` (`fe800a52c9d195836dc12411a7afcccd471ada0c2be0b38c2a7b02d70c54ba90`)
